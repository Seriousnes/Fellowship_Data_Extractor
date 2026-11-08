using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Fellowship.Codex.Usage;

/// <summary>
/// Counts the circuits the server holds, connected and disconnected. A circuit is closed when the server
/// disposes it, so one in disconnected retention is still open.
/// </summary>
public sealed class CircuitCount(ServerWatch server) : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        server.CircuitOpened();

        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        server.CircuitClosed();

        return Task.CompletedTask;
    }
}
