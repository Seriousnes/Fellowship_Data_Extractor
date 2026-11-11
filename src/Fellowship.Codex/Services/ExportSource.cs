

using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Services;

public sealed record ExportSnapshot(SettingsFile Settings, IReadOnlyList<EntityDocument> Entities);

public sealed record ExportVersion(
    string Build,
    IReadOnlyDictionary<DocumentKey, IReadOnlyList<ReadOnlyMemory<byte>>> Documents,
    string Settings);

public sealed record ExportFiles(ReadOnlyMemory<byte> Entities, ReadOnlyMemory<byte> Settings);

public sealed record ExportHistory(
    string Latest,
    ExportSnapshot Snapshot,
    ExportFiles Files,
    IReadOnlyDictionary<string, ExportVersion> Older,
    IReadOnlyDictionary<string, IReadOnlyList<PatchNote>> Notes);

public interface IExportSource
{
    Task<ExportHistory> LoadAsync(CancellationToken cancellationToken);
}

public sealed class ExportHistoryHolder
{
    private ExportHistory? loaded;

    public ExportHistory History
    {
        get => loaded ?? throw new InvalidOperationException(
            "The export history is not loaded yet. Program.cs awaits IExportSource.LoadAsync after " +
            "builder.Build() and before app.Run().");
        set => loaded = value;
    }
}

