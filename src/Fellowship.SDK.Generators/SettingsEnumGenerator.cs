using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Fellowship.SDK.Generators;

[Generator]
public sealed class SettingsEnumGenerator : IIncrementalGenerator
{
    private const string SettingsName = "settings.json";
    private const string HintName = "SettingsEnums.g.cs";
    private const string Category = "Fellowship.SDK";

    private static readonly DiagnosticDescriptor NoSettings = new(
        "FSDK001",
        "The settings are not among the additional files",
        "No {0} was passed to the generator. Add it as an AdditionalFiles item so the enumerated fields can be declared.",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnreadableSettings = new(
        "FSDK002",
        "The settings cannot be read",
        "{0} cannot be read: {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor IllegalMember = new(
        "FSDK003",
        "The settings state a word that is not a legal C# identifier",
        "{0} states \"{1}\" for {2}, which is not a legal C# identifier and cannot be an enum member",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EmptyField = new(
        "FSDK004",
        "The settings state no members for an enumerated field",
        "{0} states no members for {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var settings = context.AdditionalTextsProvider
            .Where(static text => IsSettings(text.Path))
            .Select(static (text, token) => text.GetText(token)?.ToString())
            .Collect();

        context.RegisterSourceOutput(settings, static (production, texts) => Emit(production, texts));
    }

    private static bool IsSettings(string path)
    {
        var separator = path.LastIndexOfAny(['/', '\\']);
        var name = separator < 0 ? path : path.Substring(separator + 1);
        return string.Equals(name, SettingsName, StringComparison.OrdinalIgnoreCase);
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<string?> settings)
    {
        var json = settings.FirstOrDefault(static text => text is not null);
        if (json is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(NoSettings, Location.None, SettingsName));
            context.AddSource(HintName, SourceText.From(Declare(Fields(null, null)), Encoding.UTF8));
            return;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException failure)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(UnreadableSettings, Location.None, SettingsName, failure.Message));
            context.AddSource(HintName, SourceText.From(Declare(Fields(null, null)), Encoding.UTF8));
            return;
        }

        List<(string Name, List<(string Member, int? Value)> Members)> fields;
        using (document)
        {
            fields = Checked(Fields(document.RootElement, context), context);
        }

        context.AddSource(HintName, SourceText.From(Declare(fields), Encoding.UTF8));
    }

    private static List<(string Name, List<(string Member, int? Value)> Members)> Fields(
        JsonElement? root,
        SourceProductionContext? context)
    {
        var heroes = Items(Member(root, "heroes"));

        return
        [
            ("ArmorType", Bare(Names(Member(Member(root, "statCalculation"), "armorWeights")))),
            ("PrimaryStat", Bare(heroes.Select(hero => Text(Member(hero, "primaryStat"))))),
            ("Hero", Bare(heroes.Select(hero => Text(Member(hero, "name"))))),
            ("GemType", Bare(Items(Member(root, "gemTypes")).Select(gemType => Text(gemType)))),
            ("Rarity", Ladder(Items(Member(root, "rarities")), context)),
        ];
    }

    private static List<(string Member, int? Value)> Bare(IEnumerable<string> members) =>
        [.. members
            .Where(member => member.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(member => member, StringComparer.Ordinal)
            .Select(member => (member, (int?)null))];

    private static List<(string Member, int? Value)> Ladder(
        IReadOnlyList<JsonElement> rarities,
        SourceProductionContext? context)
    {
        var ladder = new List<(string Member, int? Value)>();
        foreach (var rarity in rarities)
        {
            var name = Text(Member(rarity, "name"));
            if (Member(rarity, "tier") is { ValueKind: JsonValueKind.Number } tier && tier.TryGetInt32(out var value))
            {
                ladder.Add((name, value));
                continue;
            }

            context?.ReportDiagnostic(Diagnostic.Create(
                UnreadableSettings,
                Location.None,
                SettingsName,
                $"the rarity \"{name}\" states no whole-number tier"));
        }

        return [.. ladder.OrderBy(rung => rung.Value)];
    }

    private static List<(string Name, List<(string Member, int? Value)> Members)> Checked(
        List<(string Name, List<(string Member, int? Value)> Members)> fields,
        SourceProductionContext context)
    {
        foreach (var (name, members) in fields)
        {
            if (members.Count == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(EmptyField, Location.None, SettingsName, name));
                continue;
            }

            for (var at = members.Count - 1; at >= 0; at--)
            {
                if (IsIdentifier(members[at].Member)) continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    IllegalMember, Location.None, SettingsName, members[at].Member, name));
                members.RemoveAt(at);
            }
        }

        return fields;
    }

    private static bool IsIdentifier(string member) =>
        SyntaxFacts.IsValidIdentifier(member) && SyntaxFacts.GetKeywordKind(member) == SyntaxKind.None;

    private static string Declare(List<(string Name, List<(string Member, int? Value)> Members)> fields)
    {
        var source = new StringBuilder();
        source.Append("// <auto-generated/>\n");
        source.Append("// Declared from settings.json by Fellowship.SDK.Generators at build time.\n");
        source.Append("\nnamespace Fellowship.SDK;\n");

        foreach (var (name, members) in fields)
        {
            source.Append("\npublic enum ").Append(name).Append("\n{\n");
            foreach (var (member, value) in members)
            {
                source.Append("    ").Append(member);
                if (value.HasValue) source.Append(" = ").Append(value.Value.ToString(CultureInfo.InvariantCulture));
                source.Append(",\n");
            }

            source.Append("}\n");
        }

        return source.ToString();
    }

    private static JsonElement? Member(JsonElement? value, string name) =>
        value is { ValueKind: JsonValueKind.Object } keyed && keyed.TryGetProperty(name, out var member)
            ? member
            : null;

    private static IReadOnlyList<JsonElement> Items(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.Array } array ? [.. array.EnumerateArray()] : [];

    private static IEnumerable<string> Names(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.Object } keyed
            ? keyed.EnumerateObject().Select(member => member.Name)
            : [];

    private static string Text(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.String } text ? text.GetString() ?? string.Empty : string.Empty;
}
