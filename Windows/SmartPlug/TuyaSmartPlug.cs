using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using TuyaLan.Devices;
using TuyaLan.Protocol;

namespace BatteryGuardian.SmartPlug
{
    /// <summary>
    /// Talks to a Tuya smart plug over the local network.
    /// </summary>
    /// <remarks>
    /// The protocol itself is <c>TuyaLan.TuyaLocalDevice</c> from the Tuya LAN
    /// SDK. This class is only the adapter: it keeps the app's
    /// <see cref="ISmartPlug"/> shape, holds one connection open between polls,
    /// and translates the SDK's exceptions into the ones
    /// <see cref="SmartPlugController"/> classifies.
    ///
    /// It used to carry its own copy of the protocol. That copy was built from a
    /// description that does not match real 3.5 firmware - wrong frame layout,
    /// the 3.4 session-key derivation, control on 0x07 with a bare dps body - so
    /// it could never have switched a physical plug.
    /// </remarks>
    public sealed class TuyaSmartPlug : ISmartPlug
    {
        private readonly TuyaDeviceOptions _options;
        private readonly TuyaProtocolVersion _preferredVersion;

        /// <summary>The version that last connected; tried first from then on.</summary>
        private TuyaProtocolVersion _workingVersion;
        private readonly string _relayDp;
        private readonly object _gate = new();

        private TuyaLocalDevice? _device;
        private bool _disposed;

        public TuyaSmartPlug(
            string deviceId,
            string host,
            int port,
            byte[] localKey,
            int relayDp,
            int timeoutMs = 5000,
            string protocolVersion = "3.5")
        {
            if (deviceId == null) throw new ArgumentNullException(nameof(deviceId));
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (localKey == null) throw new ArgumentNullException(nameof(localKey));

            _relayDp = relayDp.ToString(CultureInfo.InvariantCulture);

            // The version decides the frame layout - 3.4 and 3.5 do not share one -
            // so guessing wrong means the plug never answers. The setting was
            // ignored here for months and 3.5 was hardcoded; a 3.4 device failed
            // every single time with "unreachable" and nothing said why.
            _preferredVersion = TuyaProtocolVersionExtensions.ParseLabel(protocolVersion);
            if (_preferredVersion == TuyaProtocolVersion.V33)
                _preferredVersion = TuyaProtocolVersion.V35;   // ParseLabel falls back to 3.3 for junk

            _workingVersion = _preferredVersion;

            _options = new TuyaDeviceOptions
            {
                Host = host,
                Port = port,
                DeviceId = deviceId,
                LocalKeyBytes = localKey,
                Version = _preferredVersion,
                ConnectTimeout = TimeSpan.FromMilliseconds(timeoutMs),
                CommandTimeout = TimeSpan.FromMilliseconds(timeoutMs),
                // Keep the socket warm. Without the heartbeat an idle plug ends
                // up with a half-open connection, so the next command pays for a
                // fresh handshake - slow enough to miss the few seconds a suspend
                // allows, and unreliable enough to be the reason the charger did
                // not come back on at the low threshold.
                EnableHeartbeat = true,
            };
        }

        public async Task<RelayState> GetStatusAsync(CancellationToken ct = default)
        {
            TuyaStatus status = await RunAsync((device, token) => device.GetStatusAsync(token), ct)
                .ConfigureAwait(false);
            return MapRelay(status.GetBool(_relayDp));
        }

        public Task TurnOnAsync(CancellationToken ct = default) => SetRelayAsync(true, ct);

        public Task TurnOffAsync(CancellationToken ct = default) => SetRelayAsync(false, ct);

        private Task SetRelayAsync(bool on, CancellationToken ct) =>
            RunAsync(async (device, token) =>
            {
                await device.SetDpAsync(_relayDp, on, token).ConfigureAwait(false);
                return true;
            }, ct);

        public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
        {
            try
            {
                await GetStatusAsync(ct).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        // ------------------------------------------------------------------

        private async Task<T> RunAsync<T>(Func<TuyaLocalDevice, CancellationToken, Task<T>> action,
                                          CancellationToken ct)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            try
            {
                TuyaLocalDevice device = await EnsureConnectedAsync(ct).ConfigureAwait(false);
                return await action(device, ct).ConfigureAwait(false);
            }
            catch (TuyaLan.TuyaTimeoutException ex)
            {
                // A timeout means the socket is probably dead while the device
                // still believes a session is open. Drop it, so the next attempt
                // reconnects instead of writing into a socket nobody reads.
                await DisposeDeviceAsync().ConfigureAwait(false);
                throw new TimeoutException("The smart plug did not answer in time.", ex);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not TuyaLan.TuyaException)
            {
                // Socket, IO: same reasoning.
                await DisposeDeviceAsync().ConfigureAwait(false);
                throw;
            }
            catch (TuyaLan.TuyaException ex)
            {
                // Handshake, crypto and protocol failures all mean the same thing
                // to the user: the plug answered, but not usefully.
                throw new TuyaProtocolException(ex.Message, ex);
            }
        }

        private async Task<TuyaLocalDevice> EnsureConnectedAsync(CancellationToken ct)
        {
            TuyaLocalDevice? current = _device;

            // 2.1.0's IsConnected is authoritative, and EnsureConnectedAsync
            // reconnects over a dead socket. Before that, this method returned a
            // cached device whose IsConnected stayed true after the socket died,
            // so nothing ever reconnected.
            if (current is not null && current.IsConnected) return current;

            await DisposeDeviceAsync().ConfigureAwait(false);

            // Try the version that last worked, then ladder down. Laddering is
            // only correct when the device *answered* and did not understand us -
            // a rejected handshake is the signature of a version mismatch. A
            // timeout or a refused connection is a network problem, and stepping
            // down would just triple the time it takes to report that.
            Exception? lastError = null;
            TuyaLocalDevice? device = null;

            foreach (TuyaProtocolVersion version in VersionsToTry())
            {
                var candidate = new TuyaLocalDevice(WithVersion(version));
                try
                {
                    await candidate.ConnectAsync(ct).ConfigureAwait(false);
                    device = candidate;
                    _workingVersion = version;
                    DiagnosticLog.WriteAlways(
                        $"PLUG CONNECT: {version} to {_options.Host}:{_options.Port} succeeded.");
                    break;
                }
                catch (OperationCanceledException)
                {
                    await candidate.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    bool answeredButRejected = ex is TuyaLan.TuyaHandshakeException or TuyaLan.TuyaCryptoException;

                    DiagnosticLog.WriteAlways(
                        $"PLUG CONNECT: {version} to {_options.Host}:{_options.Port} failed - " +
                        $"{ex.GetType().Name}: {ex.Message}" +
                        (answeredButRejected ? " (device answered and rejected the handshake)" : " (network)"));

                    await candidate.DisposeAsync().ConfigureAwait(false);

                    if (!answeredButRejected) throw;   // not a version problem
                }
            }

            if (device == null)
                throw lastError ?? new InvalidOperationException("The plug could not be reached.");

            lock (_gate)
            {
                if (_disposed)
                {
                    device.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    throw new ObjectDisposedException(nameof(TuyaSmartPlug));
                }
                _device = device;
            }

            // The SDK raises this when the receive loop or the heartbeat notices
            // the socket is gone. Nothing subscribed to it before, so a connection
            // that died overnight was reused for ever: every read and every command
            // timed out, nothing reconnected, and no switch ever happened again
            // until the app was restarted. That is the shape of the failure the
            // owner kept reporting.
            device.Disconnected += OnDeviceDisconnected;

            return device;
        }

        /// <summary>
        /// The last working version first, then downwards: 3.5, 3.4, 3.3.
        /// </summary>
        private TuyaProtocolVersion[] VersionsToTry() => _workingVersion switch
        {
            TuyaProtocolVersion.V33 => new[] { TuyaProtocolVersion.V33 },
            TuyaProtocolVersion.V34 => new[] { TuyaProtocolVersion.V34, TuyaProtocolVersion.V33 },
            _ => new[] { TuyaProtocolVersion.V35, TuyaProtocolVersion.V34, TuyaProtocolVersion.V33 },
        };

        /// <summary>A copy of the options for one attempt, with its own version.</summary>
        private TuyaDeviceOptions WithVersion(TuyaProtocolVersion version) => new()
        {
            Host = _options.Host,
            Port = _options.Port,
            DeviceId = _options.DeviceId,
            LocalKeyBytes = _options.LocalKeyBytes,
            Version = version,
            DeviceType = _options.DeviceType,
            ConnectTimeout = _options.ConnectTimeout,
            CommandTimeout = _options.CommandTimeout,
            EnableHeartbeat = _options.EnableHeartbeat,
        };

        private void OnDeviceDisconnected(object? sender, Exception ex)
        {
            DiagnosticLog.WriteAlways(
                $"PLUG: connection lost - {ex.GetType().Name}: {ex.Message}. It will be re-opened on the next attempt.");

            lock (_gate)
            {
                if (ReferenceEquals(_device, sender)) _device = null;
            }
        }

        private async Task DisposeDeviceAsync()
        {
            TuyaLocalDevice? device;
            lock (_gate)
            {
                device = _device;
                _device = null;
            }

            if (device == null) return;

            try
            {
                await device.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // A dead socket is not worth reporting on a teardown path.
            }
        }

        /// <summary>Maps the SDK's nullable boolean onto the controller's tri-state.</summary>
        public static RelayState MapRelay(bool? value) => value switch
        {
            true => RelayState.On,
            false => RelayState.Off,
            _ => RelayState.Unknown,
        };

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            TuyaLocalDevice? device;
            lock (_gate)
            {
                device = _device;
                _device = null;
            }

            if (device == null) return;

            try
            {
                device.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch
            {
                // Nothing useful to do while shutting down.
            }
        }
    }
}
