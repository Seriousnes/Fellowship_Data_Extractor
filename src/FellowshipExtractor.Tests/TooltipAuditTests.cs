namespace FellowshipExtractor.Tests;

/// <summary>
/// Every description number contradicted by a modifier or missing from its documents is in the checked-in baseline,
/// and every baseline entry still is one. A new gap fails the first test; a fixed gap fails the second until its
/// entry leaves the baseline. A failing run writes the current findings beside the test assembly.
/// </summary>
public sealed class TooltipAuditTests
{
    private const string BaselineFile = "TooltipAuditBaseline.tsv";
    private const string CurrentFile = "TooltipAudit.current.tsv";

    private static readonly Lazy<IReadOnlyList<TooltipNumber>> Audited = new(() => TooltipAudit.Run(Export.Loaded.Documents));

    private static IEnumerable<TooltipNumber> Findings() =>
        Audited.Value.Where(number => number.Status is TooltipNumberStatus.Contradicted or TooltipNumberStatus.Missing);

    private static string Line(TooltipNumber number) =>
        $"{number.Key}\t{number.Status}\t{number.Name}\t{number.Context}\t{number.Evidence}";

    private static Dictionary<string, string> Baseline() =>
        File.ReadLines(Path.Combine(AppContext.BaseDirectory, BaselineFile))
            .Skip(1)
            .Where(line => line.Length > 0)
            .Select(line => line.Split('\t'))
            .ToDictionary(fields => string.Join('\t', fields[..4]), fields => fields[4], StringComparer.Ordinal);

    private static string Written()
    {
        var path = Path.Combine(AppContext.BaseDirectory, CurrentFile);
        File.WriteAllLines(path, ["type\tid\tordinal\tnumber\tstatus\tname\tcontext\tevidence", .. Findings().Select(Line)]);
        return path;
    }

    [Fact]
    public void NoFindingOutsideTheBaseline()
    {
        var baseline = Baseline();
        var added = Findings()
            .Where(number => baseline.GetValueOrDefault(number.Key) != number.Status.ToString())
            .Select(Line)
            .ToArray();

        Assert.True(added.Length == 0,
            $"{added.Length} description numbers are new findings:\n{string.Join('\n', added.Take(40))}\n" +
            $"Current findings: {Written()}");
    }

    [Fact]
    public void EveryBaselineEntryIsStillAFinding()
    {
        var current = Findings().ToDictionary(number => number.Key, number => number.Status.ToString(), StringComparer.Ordinal);
        var fixedEntries = Baseline()
            .Where(entry => current.GetValueOrDefault(entry.Key) != entry.Value)
            .Select(entry => $"{entry.Key}\t{entry.Value}")
            .ToArray();

        Assert.True(fixedEntries.Length == 0,
            $"{fixedEntries.Length} baseline entries are no longer findings. Remove them from {BaselineFile}:\n" +
            $"{string.Join('\n', fixedEntries.Take(40))}\nCurrent findings: {Written()}");
    }

    [Fact]
    public void InspiredAllegianceRatesAreNotItsHasteRating()
    {
        var rate = Assert.Single(Audited.Value, number => number is { Type: "sigil", Id: 2, Number: "1.2" });

        Assert.NotEqual(TooltipNumberStatus.Contradicted, rate.Status);
        Assert.All(
            Audited.Value.Where(number => number is { Type: "sigil", Id: 2 } && number.Context.Contains("Haste rating")),
            number => Assert.NotEqual(TooltipNumberStatus.Contradicted, number.Status));
    }

    [Fact]
    public void KillEmAllIsContradictedByItsCooldownAcceleration()
    {
        var number = Assert.Single(Audited.Value, number => number is { Type: "effect", Id: 3160, Number: "+60%" });

        Assert.Equal(TooltipNumberStatus.Contradicted, number.Status);
        Assert.Contains("cooldownAcceleration Additive 1.6", number.Evidence);
    }
}
