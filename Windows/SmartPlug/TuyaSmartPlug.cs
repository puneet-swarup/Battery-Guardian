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
        private readonly string _relayDp;
        private readonly object _gate = new();

        private TuyaLocalDevice? _device;
        private bool _disposed;

        public TuyaSmartPlug(string deviceId, string host, int port, byte[] localKey, int relayDp, int timeoutMs = 5000)
        {
            if (deviceId == null) throw new ArgumentNullException(nameof(deviceId));
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (localKey == null) throw new ArgumentNullException(nameof(localKey));

            _relayDp = relayDp.ToString(CultureInfo.InvariantCulture);
            _options = new TuyaDeviceOptions
            {
                Host = host,
                Port = port,
                DeviceId = deviceId,
                LocalKeyBytes = localKey,
                Version = TuyaProtocolVersion.V35,
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
                throw new TimeoutException("The smart plug did not answer in time.", ex);
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
            if (current is { IsConnected: true }) return current;

            await DisposeDeviceAsync().ConfigureAwait(false);

            var device = new TuyaLocalDevice(_options);
            await device.ConnectAsync(ct).ConfigureAwait(false);

            lock (_gate)
            {
                if (_disposed)
                {
                    device.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    throw new ObjectDisposedException(nameof(TuyaSmartPlug));
                }
                _device = device;
            }

            return device;
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
