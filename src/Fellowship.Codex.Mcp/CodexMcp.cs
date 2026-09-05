using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Fellowship.Codex.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using ModelContextProtocol.Protocol;

namespace Fellowship.Codex.Mcp;

public static class CodexMcp
{
    public const string Name = "fellowship-codex";

    public const string Instructions =
        "This server reads a datamined export of Fellowship. Call list_types for the entity types the " +
        "build carries, find_entity to turn a name into a type and an id, then get_entity for that " +
        "entity's record. Ids are namespaced per type, so the same number names a different entity " +
        "under each slug. Names match by prefix, not by substring. A record's damage and healing " +
        "coefficients read as text such as '180% of Intellect' rather than a number, because a record " +
        "is not parameterised by hero, item level or rarity. A record's resource paths are the codex " +
        "API's own, rooted at /api, and its art fields name an image file rather than address one.";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        Converters = { new JsonStringEnumConverter() },
    };

    public static IMcpServerBuilder AddCodexMcp(this IHostApplicationBuilder builder, string export)
    {
        builder.Services.AddSingleton<IExportSource>(new FolderExportSource(export));
        builder.Services.AddSingleton<IArtSource, NamedArtSource>();
        builder.Services.AddCodexServices();

        return builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = Name,
                    Title = "Fellowship Codex",
                    Version = typeof(CodexMcp).Assembly.GetName().Version?.ToString() ?? "1.0.0",
                };
                options.ServerInstructions = Instructions;
            })
            .WithTools<CodexTools>(Json);
    }
}
