using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Fellowship.SDK.Generators;

/// <summary>
/// Declares the enumerated fields from the vocabulary the SDK commits, so the words the build states
/// and the members C# offers cannot part company. The vocabulary is committed because a clone that
/// cannot run the extractor still has to compile; the enums are not, because this reads them at
/// every build.
/// </summary>
[Generator]
public sealed class VocabularyEnumGenerator : IIncrementalGenerator
{
    private const string VocabularyName = "vocabulary.json";
    private const string HintName = "VocabularyEnums.g.cs";
    private const string Category = "Fellowship.SDK";

    private static readonly DiagnosticDescriptor NoVocabulary = new(
        "FSDK001",
        "The vocabulary is not among the additional files",
        "No {0} was passed to the generator. Add it as an AdditionalFiles item so the enumerated fields can be declared.",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnreadableVocabulary = new(
        "FSDK002",
        "The vocabulary cannot be read",
        "{0} cannot be read: {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor IllegalMember = new(
        "FSDK003",
        "The vocabulary states a word that is not a legal C# identifier",
        "{0} states \"{1}\" for {2}, which is not a legal C# identifier and cannot be an enum member",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EmptyField = new(
        "FSDK004",
        "The vocabulary states no members for an enumerated field",
        "{0} states no members for {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var vocabulary = context.AdditionalTextsProvider
            .Where(static text => IsVocabulary(text.Path))
            .Select(static (text, token) => text.GetText(token)?.ToString())
            .Collect();

        context.RegisterSourceOutput(vocabulary, static (production, texts) => Emit(production, texts));
    }

    private static bool IsVocabulary(string path)
    {
        var separator = path.LastIndexOfAny(['/', '\\']);
        var name = separator < 0 ? path : path.Substring(separator + 1);
        return string.Equals(name, VocabularyName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Always declares all five fields, so a vocabulary this cannot read fails the build on the
    /// diagnostic that says why rather than on every use of a type that then does not exist.
    /// </summary>
    private static void Emit(SourceProductionContext context, ImmutableArray<string?> vocabularies)
    {
        var json = vocabularies.FirstOrDefault(static text => text is not null);
        if (json is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(NoVocabulary, Location.None, VocabularyName));
            context.AddSource(HintName, SourceText.From(Declare(Fields(null, null)), Encoding.UTF8));
            return;
        }

        JsonValue root;
        try
        {
            root = JsonReader.Parse(json);
        }
        catch (FormatException failure)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(UnreadableVocabulary, Location.None, VocabularyName, failure.Message));
            context.AddSource(HintName, SourceText.From(Declare(Fields(null, null)), Encoding.UTF8));
            return;
        }

        context.AddSource(HintName, SourceText.From(Declare(Checked(Fields(root, context), context)), Encoding.UTF8));
    }

    /// <summary>
    /// The five fields and the members the vocabulary states for each, in the order the declaration
    /// carries them. A null root yields the fields with no members, which is what a vocabulary this
    /// could not read leaves behind.
    /// </summary>
    private static List<(string Name, List<(string Member, int? Value)> Members)> Fields(
        JsonValue? root,
        SourceProductionContext? context)
    {
        var heroes = Items(root?["heroes"]);

        return
        [
            ("ArmorType", Bare(Names(root?["statCalculation"]?["armorWeights"]))),
            ("PrimaryStat", Bare(heroes.Select(hero => Text(hero["primaryStat"])))),
            ("Hero", Bare(heroes.Select(hero => Text(hero["name"])))),
            ("GemTypeName", Bare(Items(root?["gemTypes"]).Select(Text))),
            ("Rarity", Ladder(Items(root?["rarities"]), context)),
        ];
    }

    /// <summary>
    /// Sorted ordinal, because the vocabulary lists heroes in the build's own order and a member's
    /// position is a value a reader could come to depend on.
    /// </summary>
    private static List<(string Member, int? Value)> Bare(IEnumerable<string> members) =>
        [.. members
            .Where(member => member.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(member => member, StringComparer.Ordinal)
            .Select(member => (member, (int?)null))];

    /// <summary>
    /// The rarity ladder, the one field whose members carry a value. A rung is filed under one word,
    /// prints another on an item and a third on a gem, so the ordinal is the only thing that names a
    /// rung once. The member is the filed word and the value is the shipped ordinal.
    /// </summary>
    private static List<(string Member, int? Value)> Ladder(
        IReadOnlyList<JsonValue> rarities,
        SourceProductionContext? context)
    {
        var ladder = new List<(string Member, int? Value)>();
        foreach (var rarity in rarities)
        {
            var name = Text(rarity["name"]);
            if (rarity["tier"] is { Kind: JsonKind.Number } tier
                && int.TryParse(tier.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                ladder.Add((name, value));
                continue;
            }

            context?.ReportDiagnostic(Diagnostic.Create(
                UnreadableVocabulary,
                Location.None,
                VocabularyName,
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
                context.ReportDiagnostic(Diagnostic.Create(EmptyField, Location.None, VocabularyName, name));
                continue;
            }

            for (var at = members.Count - 1; at >= 0; at--)
            {
                if (IsIdentifier(members[at].Member)) continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    IllegalMember, Location.None, VocabularyName, members[at].Member, name));
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
        source.Append("// Declared from vocabulary.json by Fellowship.SDK.Generators at build time.\n");
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

    private static IReadOnlyList<JsonValue> Items(JsonValue? value) =>
        value is { Kind: JsonKind.Array } array ? array.Items : [];

    private static IEnumerable<string> Names(JsonValue? value) =>
        value is { Kind: JsonKind.Object } keyed ? keyed.Members.Select(member => member.Key) : [];

    private static string Text(JsonValue? value) =>
        value is { Kind: JsonKind.String } text ? text.Text : string.Empty;
}
