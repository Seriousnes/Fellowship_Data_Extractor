using Fellowship.SDK.Client;

namespace Fellowship.Codex.Tests;

public sealed class CodexClientTests
{
    [Fact]
    public void The_client_addresses_the_deployed_codex_when_the_caller_names_no_other()
    {
        using var bare = new HttpClient();

        _ = new CodexClient(bare);

        Assert.Equal(CodexClient.Origin, bare.BaseAddress);
    }
}
