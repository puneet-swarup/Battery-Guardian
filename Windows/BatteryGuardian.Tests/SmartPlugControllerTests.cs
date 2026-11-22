using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using BatteryGuardian.SmartPlug;
using Xunit;

namespace BatteryGuardian.Tests
{
 /// <summary>
 /// The controller owns state and timing, not just the decision. These tests
 /// exist because the decision tests passed while the app did nothing: an
 /// unreachable plug held the controller's gate through two minutes of retries
 /// and every 30-second reading queued behind it.
 /// </summary>
 public class SmartPlugControllerTests
 {
  private static readonly SmartPlugConfig Cfg = new(true, 95, 15, "dev", "1.2.3.4", 6668, 1, 3);

  [Fact]
  public async Task ASlowEvaluationIsSkippedNotQueued()
  {
   var plug = new BlockingPlug();
   using var controller = new SmartPlugController(plug, Cfg, new[] { 0 });

   Task<SmartPlugStatus> first = controller.EvaluateAsync(50);
   Assert.True(plug.Entered.Wait(TimeSpan.FromSeconds(5)), "the first evaluation never reached the plug");

   var clock = Stopwatch.StartNew();
   SmartPlugStatus second = await controller.EvaluateAsync(50);
   clock.Stop();

   Assert.NotNull(second.Message);
   Assert.Contains("skipped", second.Message, StringComparison.OrdinalIgnoreCase);
   Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2),
     "the second reading waited behind the first instead of being skipped");

   plug.Release.Set();
   await first;
  }

  [Fact]
  public async Task AFailingPlugGivesUpWithinOneReading()
  {
   using var controller = new SmartPlugController(new ThrowingPlug(), Cfg, new[] { 0, 0 });

   var clock = Stopwatch.StartNew();
   SmartPlugStatus status = await controller.EvaluateAsync(96);
   clock.Stop();

   Assert.Equal(SmartPlugError.Unreachable, status.LastError);
   Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), "an unreachable plug must not hold the controller open");
  }

  [Fact]
  public async Task RecoversOnTheNextReadingAfterAFailure()
  {
   using var controller = new SmartPlugController(new FlakyPlug(failuresBeforeSuccess: 2), Cfg, new[] { 0 });

   SmartPlugStatus failed = await controller.EvaluateAsync(96);
   Assert.Equal(SmartPlugError.Unreachable, failed.LastError);

   SmartPlugStatus recovered = await controller.EvaluateAsync(96);
   Assert.Equal(SmartPlugError.None, recovered.LastError);
   Assert.Equal(RelayState.Off, recovered.Relay);
  }

  [Fact]
  public async Task TurnsThePlugOnEvenWhenTheStateReadFails()
  {
   // The plug is idle for hours before the low threshold arrives, so the state
   // read fails exactly when the charger needs to come back on. This is the bug
   // the user kept hitting: the read threw and the command was never sent.
   var plug = new UnreadablePlug();
   using var controller = new SmartPlugController(plug, Cfg, new[] { 0 });

   SmartPlugStatus status = await controller.EvaluateAsync(10);

   Assert.True(plug.TurnedOn, "the plug was never told to switch on");
   Assert.Equal(RelayState.On, status.Relay);
  }

  [Fact]
  public async Task TurnsThePlugOffEvenWhenTheStateReadFails()
  {
   var plug = new UnreadablePlug();
   using var controller = new SmartPlugController(plug, Cfg, new[] { 0 });

   SmartPlugStatus status = await controller.EvaluateAsync(96);

   Assert.True(plug.TurnedOff, "the plug was never told to switch off");
   Assert.Equal(RelayState.Off, status.Relay);
  }

  [Fact]
  public async Task AnUnreadablePlugBetweenTheThresholdsIsReportedAsAFailure()
  {
   var plug = new UnreadablePlug();
   using var controller = new SmartPlugController(plug, Cfg, new[] { 0 });

   SmartPlugStatus status = await controller.EvaluateAsync(50);

   Assert.Equal(SmartPlugError.Unreachable, status.LastError);
   Assert.False(plug.TurnedOn || plug.TurnedOff);
  }

  private sealed class UnreadablePlug : ISmartPlug
  {
   public bool TurnedOn { get; private set; }
   public bool TurnedOff { get; private set; }

   public Task<RelayState> GetStatusAsync(CancellationToken ct = default) =>
    throw new InvalidOperationException("the plug did not answer");

   public Task TurnOnAsync(CancellationToken ct = default) { TurnedOn = true; return Task.CompletedTask; }
   public Task TurnOffAsync(CancellationToken ct = default) { TurnedOff = true; return Task.CompletedTask; }
   public Task<bool> TestConnectionAsync(CancellationToken ct = default) => Task.FromResult(false);
   public void Dispose() { }
  }

  [Fact]
  public async Task ACommandIsNeverRecordedAsAnObservation()
  {
   // The plug cannot be read but accepts commands. The status must keep saying
   // "unknown" - an unconfirmed command is intent, not observation - while
   // IntendedRelay records what we asked for.
   var plug = new UnreadablePlug();
   using var controller = new SmartPlugController(plug, Cfg, new[] { 0 });

   SmartPlugStatus status = await controller.SetRelayAsync(false, TimeSpan.FromSeconds(5));

   Assert.True(plug.TurnedOff);
   Assert.Equal(RelayState.Unknown, status.Relay);
   Assert.Equal(RelayState.Off, controller.IntendedRelay);
  }

  [Fact]
  public async Task AVerifiedCommandRecordsWhatThePlugReports()
  {
   var plug = new ScriptedPlug(RelayState.Off);
   using var controller = new SmartPlugController(plug, Cfg, new[] { 0 });

   SmartPlugStatus status = await controller.SetRelayAsync(false, TimeSpan.FromSeconds(5), verify: true);

   Assert.Equal(RelayState.Off, status.Relay);
   Assert.Contains("reports", status.Message);
  }

  [Fact]
  public async Task ACommandThatMustNotWaitIsDroppedWhenThePlugIsBusy()
  {
   // The sleep path must never wait: send, or give up.
   var plug = new BlockingPlug();
   using var controller = new SmartPlugController(plug, Cfg, new[] { 0 });

   Task<SmartPlugStatus> busy = controller.EvaluateAsync(50);
   Assert.True(plug.Entered.Wait(TimeSpan.FromSeconds(5)));

   var clock = Stopwatch.StartNew();
   SmartPlugStatus status = await controller.SetRelayAsync(
    false, TimeSpan.FromSeconds(2), verify: false, waitForGate: false);
   clock.Stop();

   Assert.Contains("busy", status.Message, StringComparison.OrdinalIgnoreCase);
   Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1), "the command waited for the gate");

   plug.Release.Set();
   await busy;
  }

  private sealed class ScriptedPlug : ISmartPlug
  {
   private RelayState _relay;

   public ScriptedPlug(RelayState initial) => _relay = initial;

   public Task<RelayState> GetStatusAsync(CancellationToken ct = default) => Task.FromResult(_relay);
   public Task TurnOnAsync(CancellationToken ct = default) { _relay = RelayState.On; return Task.CompletedTask; }
   public Task TurnOffAsync(CancellationToken ct = default) { _relay = RelayState.Off; return Task.CompletedTask; }
   public Task<bool> TestConnectionAsync(CancellationToken ct = default) => Task.FromResult(true);
   public void Dispose() { }
  }

  private sealed class BlockingPlug : ISmartPlug
  {
   public readonly ManualResetEventSlim Entered = new(false);
   public readonly ManualResetEventSlim Release = new(false);

   public async Task<RelayState> GetStatusAsync(CancellationToken ct = default)
   {
    Entered.Set();
    await Task.Run(() => Release.Wait(TimeSpan.FromSeconds(20)), ct);
    return RelayState.On;
   }

   public Task TurnOnAsync(CancellationToken ct = default) => Task.CompletedTask;
   public Task TurnOffAsync(CancellationToken ct = default) => Task.CompletedTask;
   public Task<bool> TestConnectionAsync(CancellationToken ct = default) => Task.FromResult(true);
   public void Dispose() { }
  }

  private sealed class ThrowingPlug : ISmartPlug
  {
   public Task<RelayState> GetStatusAsync(CancellationToken ct = default) =>
    throw new InvalidOperationException("plug is not answering");
   public Task TurnOnAsync(CancellationToken ct = default) => throw new InvalidOperationException("no");
   public Task TurnOffAsync(CancellationToken ct = default) => throw new InvalidOperationException("no");
   public Task<bool> TestConnectionAsync(CancellationToken ct = default) => Task.FromResult(false);
   public void Dispose() { }
  }

  private sealed class FlakyPlug : ISmartPlug
  {
   private int _remainingFailures;

   public FlakyPlug(int failuresBeforeSuccess) => _remainingFailures = failuresBeforeSuccess;

   public Task<RelayState> GetStatusAsync(CancellationToken ct = default)
   {
    if (_remainingFailures-- > 0) throw new InvalidOperationException("plug is not answering");
    return Task.FromResult(RelayState.On);
   }

   public Task TurnOnAsync(CancellationToken ct = default) => Task.CompletedTask;
   public Task TurnOffAsync(CancellationToken ct = default) => Task.CompletedTask;
   public Task<bool> TestConnectionAsync(CancellationToken ct = default) => Task.FromResult(true);
   public void Dispose() { }
  }
 }
}
