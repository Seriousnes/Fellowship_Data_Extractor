using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;

namespace FellowshipExtractor.Api.Services;

public interface IExportPathService
{
    string Export { get; }

    string Entities { get; }

    string Vocabulary { get; }

    string Ui { get; }
}

public sealed class ExportPathService(IConfiguration configuration) : IExportPathService
{
    private const string Variable = "FELLOWSHIP_EXPORT";

    public string Export { get; } = configuration[Variable]
        ?? throw new InvalidOperationException(
            $"{Variable} is not set. It names the folder the extractor's export-all command wrote, " +
            "and the AppHost passes it. Set it to run the API outside the AppHost.");

    public string Entities => Path.Combine(Export, "entities.jsonl");

    public string Vocabulary => Path.Combine(Export, "vocabulary.json");

    public string Ui => Path.Combine(Export, "ui");
}
