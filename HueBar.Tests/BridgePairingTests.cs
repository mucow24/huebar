using HueBar.Core;

namespace HueBar.Tests;

public class BridgePairingTests
{
    // An injected "delay" that returns instantly but still honors cancellation, so the loop
    // runs at full speed in tests yet a cancelled token ends it exactly as a real delay would.
    private static readonly Func<CancellationToken, Task> InstantWait =
        ct => { ct.ThrowIfCancellationRequested(); return Task.CompletedTask; };

    [Fact]
    public async Task Succeeds_on_the_first_attempt_when_the_button_is_already_pressed()
    {
        int calls = 0;
        var outcome = await BridgePairing.RunAsync(
            _ => { calls++; return Task.FromResult(PairResult.Ok("theKey")); },
            InstantWait,
            CancellationToken.None);

        Assert.Equal(PairingStatus.Connected, outcome.Status);
        Assert.Equal("theKey", outcome.Username);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Retries_while_the_link_button_is_not_pressed_then_connects()
    {
        int calls = 0;
        var outcome = await BridgePairing.RunAsync(
            _ =>
            {
                calls++;
                return Task.FromResult(calls < 3
                    ? PairResult.Fail(101, "link button not pressed")
                    : PairResult.Ok("theKey"));
            },
            InstantWait,
            CancellationToken.None);

        Assert.Equal(PairingStatus.Connected, outcome.Status);
        Assert.Equal("theKey", outcome.Username);
        Assert.Equal(3, calls); // two "not pressed" polls, then success
    }

    [Fact]
    public async Task Stops_immediately_on_a_non_link_button_bridge_error()
    {
        int calls = 0;
        var outcome = await BridgePairing.RunAsync(
            _ => { calls++; return Task.FromResult(PairResult.Fail(1, "unauthorized user")); },
            InstantWait,
            CancellationToken.None);

        Assert.Equal(PairingStatus.BridgeError, outcome.Status);
        Assert.Equal("unauthorized user", outcome.ErrorMessage);
        Assert.Equal(1, calls); // did NOT keep polling
    }

    [Fact]
    public async Task Times_out_when_the_button_is_never_pressed_before_the_deadline()
    {
        using var cts = new CancellationTokenSource();
        int calls = 0;
        var outcome = await BridgePairing.RunAsync(
            _ =>
            {
                calls++;
                if (calls >= 3) cts.Cancel(); // simulate the deadline elapsing after a few polls
                return Task.FromResult(PairResult.Fail(101, "link button not pressed"));
            },
            InstantWait,
            cts.Token);

        Assert.Equal(PairingStatus.TimedOut, outcome.Status);
    }

    [Fact]
    public async Task An_already_cancelled_token_times_out_without_attempting()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        int calls = 0;

        var outcome = await BridgePairing.RunAsync(
            _ => { calls++; return Task.FromResult(PairResult.Ok("k")); },
            InstantWait,
            cts.Token);

        Assert.Equal(PairingStatus.TimedOut, outcome.Status);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Cancellation_thrown_from_the_attempt_is_reported_as_a_timeout()
    {
        using var cts = new CancellationTokenSource();
        var outcome = await BridgePairing.RunAsync(
            _ => { cts.Cancel(); throw new OperationCanceledException(); },
            InstantWait,
            cts.Token);

        Assert.Equal(PairingStatus.TimedOut, outcome.Status);
    }

    [Fact]
    public async Task A_cancellation_not_caused_by_the_deadline_propagates_as_a_failure()
    {
        // HttpClient.Timeout also surfaces as an OperationCanceledException — but with the
        // pairing token NOT cancelled. That's "nothing answered at that address", not "the user
        // never pressed the button", so it must escape the loop as the connectivity failure it
        // is rather than being reported as a pairing timeout.
        await Assert.ThrowsAsync<TaskCanceledException>(() => BridgePairing.RunAsync(
            _ => throw new TaskCanceledException("simulated HttpClient timeout"),
            InstantWait,
            CancellationToken.None));
    }
}
