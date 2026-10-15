using System.Net;

namespace Fellowship.SDK.Client;

public sealed record CodexFault
{
    public string? Title { get; init; }

    public string? Detail { get; init; }

    public int? Status { get; init; }
}

public sealed class CodexException(HttpStatusCode status, CodexFault fault)
    : Exception(Read(status, fault))
{
    public HttpStatusCode Status { get; } = status;

    public CodexFault Fault { get; } = fault;

    private static string Read(HttpStatusCode status, CodexFault fault) =>
        (fault.Title, fault.Detail) switch
        {
            ({ Length: > 0 } title, { Length: > 0 } detail) => $"{title} {detail}",
            ({ Length: > 0 } title, _) => title,
            (_, { Length: > 0 } detail) => detail,
            _ => $"The codex answered {(int)status} {status}.",
        };
}
