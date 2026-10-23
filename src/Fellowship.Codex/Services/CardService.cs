using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

using Fellowship.SDK.Documents;

using SkiaSharp;

using Fellowship.Codex.Models;

namespace Fellowship.Codex.Services;

public sealed record CardLayer(string Address, float Share, bool Whole);

public sealed record CardLine(string? Lead, string Text);

public sealed record EntityCard(
    string Name, string Colour, string Kind, IReadOnlyList<CardLine> Lines, IReadOnlyList<CardLayer> Icon);

public interface ICardService
{
    int Width { get; }

    int Height { get; }

    Task<EntityCard> Card(EntityDocument document, TooltipQuery query);

    Task<byte[]> Render(EntityCard card, CancellationToken cancellationToken);
}

public sealed partial class CardService(
    ICodexViewService ui, ITooltipViewService tips, IFragmentService fragments, ITextureReader textures) : ICardService
{
    private const int CardWidth = 1200;

    private const int CardHeight = 300;

    private const int Margin = 40;

    private const int IconEdge = CardHeight - 2 * Margin;

    private const int Frame = 4;

    private const int Gap = 40;

    private const float NameSize = 76;

    private const float SmallestName = 44;

    private const float KindSize = 32;

    private const float LineSize = 28;

    private const float Leading = 40;

    private const int Rows = 2;

    private const float GlyphPlateShare = 0.8f;

    private const float GlyphRuneShare = 0.4f;

    private const int SummaryLength = 200;

    private const string Ellipsis = "…";

    private const string Separator = " · ";

    private const string LeadMark = ": ";

    private const char NoBreak = ' ';

    private const string ItemLevel = "Item Level";

    private const string Tempered = "Tempered";

    private const string KillScore = "Kill Score";

    private static readonly string Fonts = Path.Combine(AppContext.BaseDirectory, "Fonts");

    private static readonly SKTypeface Display = Face("EBGaramond-Regular.ttf");

    private static readonly SKTypeface Sans = Face("NotoSans-Regular.ttf");

    private static readonly SKTypeface SansSemiBold = Face("NotoSans-SemiBold.ttf");

    public int Width => CardWidth;

    public int Height => CardHeight;

    private static SKTypeface Face(string file) =>
        SKTypeface.FromFile(Path.Combine(Fonts, file))
        ?? throw new InvalidOperationException($"{Path.Combine(Fonts, file)} is not a readable font.");

    public async Task<EntityCard> Card(EntityDocument document, TooltipQuery query)
    {
        var type = EntityTypes.TypeOf(document);
        var rarity = query.Rarity is { } word ? ui.Store.Rarity(word, document is GemDocument) : null;
        var hero = ui.Store.Hero(query.Hero);

        var colour = rarity is null
            ? ui.NameColour(document)
            : CodexView.Readable(ui.Tokens.Rarity(rarity.Name)) ?? ui.NameColour(document);

        var tier = document is ItemDocument && tips.Rarity(document, query) is { } resolved
            ? tips.RarityName(document, resolved)
            : rarity is null
                ? ui.Rarity(document)
                : document is GemDocument ? rarity.GemDisplayName : rarity.DisplayName;

        var noun = PublicTypes.Noun(type);
        var named = (document as ItemDocument)?.Slot ?? Capitalised(noun);
        named = tier is null ? named : $"{tier} {named}";

        IReadOnlyList<CardLine> lines = document switch
        {
            ItemDocument item => ItemLines(item, query, hero),
            CreatureDocument creature => [.. Haunts(creature), .. await TooltipLines(document, query, hero)],
            _ => await TooltipLines(document, query, hero),
        };

        return new EntityCard(
            ui.Name(document),
            colour,
            string.Join(Separator, Kind(document, query, named)),
            lines,
            Icon(document, query));
    }

    public static string? Summary(EntityCard card)
    {
        if (card.Lines.Count == 0) return null;

        var texts = card.Lines.Select(line => line.Lead is null ? line.Text : line.Lead + LeadMark + line.Text).ToList();

        var text = string.Concat(texts.Select((line, index) =>
            index == 0 ? line : (char.IsPunctuation(texts[index - 1][^1]) ? " " : ". ") + line));

        text = text.Replace(NoBreak, ' ');

        return text.Length <= SummaryLength ? text : text[..SummaryLength].TrimEnd() + Ellipsis;
    }

    private IEnumerable<string> Kind(EntityDocument document, TooltipQuery query, string named)
    {
        yield return named;

        switch (document)
        {
            case ItemDocument item:
            {
                var level = tips.Level(item, query);

                if (level is { } power) yield return $"{ItemLevel} {Number(power)}";

                if (tips.Temper(item, query, tips.Rarity(item, query), level) is { } temper)
                {
                    yield return $"{Tempered} {temper}";
                }

                break;
            }
            case AugmentationDocument augmentation:
            {
                if (query.ItemLevel is { } level) yield return $"{ItemLevel} {Number(level)}";

                if (AugmentationRanks.Ranked(ui.Store, augmentation))
                {
                    yield return $"{AugmentationRanks.Text(ui.Store.Settings).EquippedRank} " +
                        Number(AugmentationRanks.Rank(ui.Store, query, augmentation));
                }

                break;
            }
            case CreatureDocument { KillScore: { } score }:
                yield return $"{KillScore} {Number(score)}";
                break;
        }
    }

    private IReadOnlyList<CardLine> ItemLines(ItemDocument item, TooltipQuery query, HeroEntry? hero)
    {
        var lines = new List<CardLine>();

        var rolls = tips.Rolls(item, query, tips.Rarity(item, query));

        if (rolls.Count > 0)
        {
            lines.Add(new CardLine(null, string.Join(" ", rolls.Select(line =>
                $"{TooltipView.Magnitude(line)} {StatNames.Short(line.Stat, line.DisplayName)}".Replace(' ', NoBreak)))));
        }

        if (tips.Granted(item) is { } granted && PublicTypes.DisplayNameOf(granted) is { } name)
        {
            lines.Add(new CardLine(name, Prose(granted, query, hero) ?? string.Empty));
        }

        if (Prose(item, query, hero) is { } description) lines.Add(new CardLine(null, description));

        return lines;
    }

    private string? Prose(EntityDocument document, TooltipQuery query, HeroEntry? hero) =>
        DocumentFields.Text(document, nameof(AbilityDocument.Description)) is { } text
            ? Plain(tips.Text(text, CodexView.PlaceholdersOf(document), query, hero, FellowshipCodex.Schools(document)).Value, string.Empty)
            : null;

    private IEnumerable<CardLine> Haunts(CreatureDocument creature)
    {
        var dungeons = (creature.FoundIn ?? [])
            .Where(found => found.Type == EntityType.Dungeon)
            .SelectMany(found => ui.Store.All(found.Type, found.Id).Take(1))
            .Select(ui.Name)
            .Distinct()
            .ToList();

        if (dungeons.Count == 0) yield break;

        yield return new CardLine(null, dungeons.Count == 1 ? dungeons[0] : string.Join(", ", dungeons.Select(Shortened)));
    }

    private static string Shortened(string name) =>
        name.Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: > 1 } words
            ? string.Concat(words.Select(word => word[0]))
            : name;

    private async Task<IReadOnlyList<CardLine>> TooltipLines(EntityDocument document, TooltipQuery query, HeroEntry? hero)
    {
        var tooltip = await fragments.Render<Components.Tooltip.Tooltip>(new()
        {
            ["Entity"] = document,
            ["Query"] = query,
            ["Hero"] = hero,
            ["Origin"] = string.Empty,
        });

        var lines = new List<CardLine>();
        var facts = new List<string>();

        foreach (Match match in Block().Matches(Styles().Replace(tooltip, string.Empty)))
        {
            if (match.Groups["key"].Success)
            {
                facts.Add($"{Plain(match.Groups["key"].Value, " ")} {Plain(match.Groups["value"].Value, " ")}".Replace(' ', NoBreak));
                continue;
            }

            if (Plain(match.Groups["prose"].Value, string.Empty) is { Length: > 0 } line) lines.Add(new CardLine(null, line));
        }

        if (lines.Count == 0 && facts.Count > 0) lines.Add(new CardLine(null, string.Join(", ", facts)));

        return lines;
    }

    private IReadOnlyList<CardLayer> Icon(EntityDocument document, TooltipQuery query)
    {
        if (document is GlyphDocument glyph)
        {
            var plate = new CardLayer(ui.Chrome.GlyphPlate(query.State), GlyphPlateShare, true);

            return ui.Chrome.GlyphRune(glyph, query.State) is { } rune
                ? [plate, new CardLayer(rune, GlyphRuneShare, true)]
                : [plate];
        }

        return ui.Icon(DocumentFields.Text(document, nameof(ItemDocument.Icon))) is { } icon
            ? [new CardLayer(icon, 1, false)]
            : [];
    }

    private static string Plain(string html, string between) =>
        string.Join(' ', WebUtility.HtmlDecode(Tags().Replace(html, between))
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public async Task<byte[]> Render(EntityCard card, CancellationToken cancellationToken)
    {
        var layers = new List<(SKImage Image, CardLayer Layer)>();

        try
        {
            foreach (var layer in card.Icon)
            {
                if (await textures.Read(layer.Address, cancellationToken) is { } bytes &&
                    SKImage.FromEncodedData(bytes) is { } image)
                {
                    layers.Add((image, layer));
                }
            }

            return Draw(card, layers);
        }
        finally
        {
            foreach (var (image, _) in layers) image.Dispose();
        }
    }

    private byte[] Draw(EntityCard card, IReadOnlyList<(SKImage Image, CardLayer Layer)> layers)
    {
        using var surface = SKSurface.Create(new SKImageInfo(CardWidth, CardHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;

        canvas.Clear(Colour("core.secondary-1000"));

        using (var line = new SKPaint { Color = Colour("core.secondary-700"), Style = SKPaintStyle.Stroke, StrokeWidth = Frame })
        {
            canvas.DrawRect(Frame / 2f, Frame / 2f, CardWidth - Frame, CardHeight - Frame, line);
        }

        var accent = SKColor.Parse(card.Colour);
        var left = (float)Margin;

        if (layers.Count > 0)
        {
            DrawIcon(canvas, layers, accent);
            left = Margin + IconEdge + Gap;
        }

        var width = CardWidth - Margin - left;

        using var nameFont = Fitted(card.Name, width);
        using var kindFont = new SKFont(SansSemiBold, KindSize);
        using var lineFont = new SKFont(Sans, LineSize);
        using var leadFont = new SKFont(SansSemiBold, LineSize);

        using var namePaint = new SKPaint { Color = accent, IsAntialias = true };
        using var kindPaint = new SKPaint { Color = Colour("core.text-secondary"), IsAntialias = true };
        using var linePaint = new SKPaint { Color = Colour("core.text-primary"), IsAntialias = true };
        using var leadPaint = new SKPaint { Color = Colour("core.primary-200"), IsAntialias = true };

        var baseline = Margin + nameFont.Metrics.CapHeight;
        canvas.DrawText(card.Name, left, baseline, SKTextAlign.Left, nameFont, namePaint);

        baseline += nameFont.Metrics.Descent - kindFont.Metrics.Ascent;
        canvas.DrawText(Clipped(card.Kind, kindFont, width), left, baseline, SKTextAlign.Left, kindFont, kindPaint);

        baseline += kindFont.Metrics.Descent + 8;

        foreach (var (lead, text) in Wrapped(card.Lines, lineFont, leadFont, width, Rows))
        {
            baseline += Leading;
            var x = left;

            if (lead is not null)
            {
                canvas.DrawText(lead, x, baseline, SKTextAlign.Left, leadFont, leadPaint);
                x += leadFont.MeasureText(lead);
            }

            canvas.DrawText(text, x, baseline, SKTextAlign.Left, lineFont, linePaint);
        }

        using var snapshot = surface.Snapshot();
        using var png = snapshot.Encode(SKEncodedImageFormat.Png, 100);

        return png.ToArray();
    }

    private void DrawIcon(SKCanvas canvas, IReadOnlyList<(SKImage Image, CardLayer Layer)> layers, SKColor accent)
    {
        var target = SKRect.Create(Margin, Margin, IconEdge, IconEdge);

        using var fill = new SKPaint { Color = Colour("core.secondary-900") };
        canvas.DrawRect(target, fill);

        using var paint = new SKPaint { IsAntialias = true };
        var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);

        foreach (var (image, layer) in layers)
        {
            var inset = IconEdge * (1 - layer.Share) / 2;
            var placed = SKRect.Inflate(target, -inset, -inset);

            if (layer.Whole)
            {
                var scale = Math.Min(placed.Width / image.Width, placed.Height / image.Height);
                var fitted = SKRect.Create(
                    placed.MidX - image.Width * scale / 2, placed.MidY - image.Height * scale / 2,
                    image.Width * scale, image.Height * scale);

                canvas.DrawImage(image, fitted, sampling, paint);
                continue;
            }

            var edge = Math.Min(image.Width, image.Height);
            var source = SKRect.Create((image.Width - edge) / 2f, (image.Height - edge) / 2f, edge, edge);

            canvas.DrawImage(image, source, placed, sampling, paint);
        }

        using var frame = new SKPaint { Color = accent, Style = SKPaintStyle.Stroke, StrokeWidth = Frame, IsAntialias = true };
        canvas.DrawRect(target, frame);
    }

    private static SKFont Fitted(string text, float width)
    {
        var size = NameSize;
        var font = new SKFont(Display, size);

        while (size > SmallestName && font.MeasureText(text) > width)
        {
            size -= 2;
            font.Size = size;
        }

        return font;
    }

    private static IReadOnlyList<(string? Lead, string Text)> Wrapped(
        IReadOnlyList<CardLine> lines, SKFont font, SKFont leadFont, float width, int limit)
    {
        var rows = new List<(string? Lead, string Text)>();

        for (var index = 0; index < lines.Count && rows.Count < limit; index++)
        {
            var allowed = Math.Max(1, limit - rows.Count - (lines.Count - index - 1));
            var lead = lines[index].Lead is { } named ? named + LeadMark : null;
            var words = lines[index].Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var last = index == lines.Count - 1;
            var mine = 0;
            var row = string.Empty;

            float Room() => width - (mine == 0 && lead is not null ? leadFont.MeasureText(lead) : 0);

            void Close(string text, bool more)
            {
                rows.Add((mine == 0 ? lead : null, more ? Clipped(text + Ellipsis, font, Room()) : Clipped(text, font, Room())));
                mine++;
            }

            var at = 0;

            for (; at < words.Length; at++)
            {
                var next = row.Length == 0 ? words[at] : $"{row} {words[at]}";

                if (row.Length == 0 || font.MeasureText(next) <= Room())
                {
                    row = next;
                    continue;
                }

                if (mine == allowed - 1) break;

                Close(row, false);
                row = words[at];
            }

            if (at < words.Length)
            {
                Close(row, true);
                continue;
            }

            if (row.Length > 0 || lead is not null) Close(row, !last && rows.Count == limit - 1);
        }

        return rows;
    }

    private static string Clipped(string text, SKFont font, float width)
    {
        if (font.MeasureText(text) <= width) return text;

        var bare = text.EndsWith(Ellipsis, StringComparison.Ordinal) ? text[..^Ellipsis.Length] : text;
        var end = bare.Length;

        while (end > 0 && font.MeasureText(bare[..end].TrimEnd() + Ellipsis) > width) end--;

        return bare[..end].TrimEnd() + Ellipsis;
    }

    private SKColor Colour(string token) =>
        SKColor.Parse(ui.Tokens.Color(token)
            ?? throw new InvalidOperationException($"The design tokens have no {token}."));

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Capitalised(string noun) =>
        noun.Length == 0 ? noun : char.ToUpper(noun[0], CultureInfo.InvariantCulture) + noun[1..];

    [GeneratedRegex(@"<style\b[^>]*>.*?</style>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Styles();

    [GeneratedRegex(
        @"<p class=""(?:desc|prose)\b[^""]*""[^>]*>(?<prose>.*?)</p>|<span class=""k""[^>]*>(?<key>.*?)</span>\s*<span class=""v""[^>]*>(?<value>.*?)</span>",
        RegexOptions.Singleline)]
    private static partial Regex Block();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();
}
