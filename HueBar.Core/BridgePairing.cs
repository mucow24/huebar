namespace HueBar.Core;

/// <summary>How a pairing attempt ended. Maps 1:1 to the message the connect pane shows.</summary>
public enum PairingStatus
{
    /// <summary>The bridge issued an application key.</summary>
    Connected,
    /// <summary>The bridge rejected pairing for a reason other than "link button not pressed".</summary>
    BridgeError,
    /// <summary>The link button was never pressed before the deadline (the token cancelled).</summary>
    TimedOut,
}

/// <summary>The result of running the pairing loop to completion.</summary>
public sealed class PairingOutcome
{
    public PairingStatus Status { get; private init; }
    public string? Username { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static PairingOutcome Connected(string username) =>
        new() { Status = PairingStatus.Connected, Username = username };
    public static PairingOutcome BridgeError(string message) =>
        new() { Status = PairingStatus.BridgeError, ErrorMessage = message };
    public static PairingOutcome TimedOut() =>
        new() { Status = PairingStatus.TimedOut };
}

/// <summary>
/// The pairing retry loop, lifted out of the WPF connect pane so it can be tested headlessly.
/// Polls the bridge until it hands back a key, reports a non-recoverable error, or the caller's
/// token cancels (the pairing deadline). "Link button not pressed" is the one error that means
/// "keep waiting"; everything else stops the loop.
/// </summary>
public static class BridgePairing
{
    /// <param name="attemptPair">Performs one pairing attempt (typically <c>HueClient.PairAsync</c>).</param>
    /// <param name="waitBetweenAttempts">
    /// Awaited between attempts (typically <c>Task.Delay(...)</c>); injectable so tests run instantly.
    /// </param>
    /// <param name="ct">Cancels the whole loop — its deadline is the pairing timeout.</param>
    public static async Task<PairingOutcome> RunAsync(
        Func<CancellationToken, Task<PairResult>> attemptPair,
        Func<CancellationToken, Task> waitBetweenAttempts,
        CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var result = await attemptPair(ct);

                if (result.Success)
                    return PairingOutcome.Connected(result.Username ?? "");

                if (!result.LinkButtonNotPressed)
                    return PairingOutcome.BridgeError(result.ErrorMessage ?? "Unknown bridge error.");

                await waitBetweenAttempts(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The deadline elapsed mid-attempt or mid-wait: treat as a timeout, not a crash.
            // A cancellation NOT triggered by ct (e.g. HttpClient.Timeout on an unreachable IP)
            // falls through this filter and propagates as the connectivity failure it is.
        }

        return PairingOutcome.TimedOut();
    }
}
