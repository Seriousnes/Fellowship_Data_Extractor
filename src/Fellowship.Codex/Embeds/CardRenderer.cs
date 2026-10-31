using SkiaSharp;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class CardRenderer(IDesignTokenService tokens, ITextureReader textures)
{
    public const int Width = 1200;

    public const int MaxHeight = 900;

    private const int Margin = 56;

    private const int IconEdge = 120;

    private const int Frame = 4;

    private const int Gap = 32;

    private const float NameSize = 80;

    private const float SmallestName = 48;

    private const float KindSize = 36;

    private const float LineSize = 40;

    private const float Leading = 54;

    private const float ParagraphGap = 18;

    private const float TextWidth = Width - 2 * Margin;

    private static readonly string Fonts = Path.Combine(AppContext.BaseDirectory, "Fonts");

    private static readonly SKTypeface Display = Face("EBGaramond-Regular.ttf");

    private static readonly SKTypeface Sans = Face("NotoSans-Regular.ttf");

    private static readonly SKTypeface SansSemiBold = Face("NotoSans-SemiBold.ttf");

    private static readonly SKTypeface SansItalic = Face("NotoSans-Italic.ttf");

    private static readonly SKTypeface SansSemiBoldItalic = Face("NotoSans-SemiBoldItalic.ttf");

    private static SKTypeface Face(string file) =>
        SKTypeface.FromFile(Path.Combine(Fonts, file))
        ?? throw new InvalidOperationException($"{Path.Combine(Fonts, file)} is not a readable font.");

    private sealed class BodyFonts : IDisposable
    {
        private readonly SKFont regular = new(Sans, LineSize);

        private readonly SKFont bold = new(SansSemiBold, LineSize);

        private readonly SKFont italic = new(SansItalic, LineSize);

        private readonly SKFont boldItalic = new(SansSemiBoldItalic, LineSize);

        public SKFont Lead => bold;

        public SKFontMetrics Metrics => regular.Metrics;

        public SKFont For(CardRun run) => (run.Bold, run.Italic) switch
        {
            (true, true) => boldItalic,
            (true, false) => bold,
            (false, true) => italic,
            _ => regular,
        };

        public float Measure(IEnumerable<CardRun> runs) => runs.Sum(run => For(run).MeasureText(run.Text));

        public void Dispose()
        {
            regular.Dispose();
            bold.Dispose();
            italic.Dispose();
            boldItalic.Dispose();
        }
    }

    private sealed record Row(string? Lead, IReadOnlyList<CardRun> Runs, float Baseline);

    private sealed record Layout(float NameBaseline, float KindBaseline, IReadOnlyList<Row> Rows, int Height);

    public static int Height(CardModel card)
    {
        using var fonts = new BodyFonts();

        return Arrange(card, fonts).Height;
    }

    public async Task<byte[]> Render(CardModel card, CancellationToken cancellationToken)
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

    // The header is the icon beside the name and kind line; the tooltip text below it has the card's whole width.
    private static Layout Arrange(CardModel card, BodyFonts fonts)
    {
        using var nameFont = new SKFont(Display, NameSize);
        using var kindFont = new SKFont(SansSemiBold, KindSize);

        var nameBaseline = Margin + nameFont.Metrics.CapHeight;
        var kindBaseline = nameBaseline + nameFont.Metrics.Descent - kindFont.Metrics.Ascent;
        var header = Math.Max(card.Icon.Count > 0 ? Margin + IconEdge : 0, kindBaseline + kindFont.Metrics.Descent);

        var bottom = MaxHeight - Margin - fonts.Metrics.Descent;
        var rows = new List<Row>();

        // Each paragraph gap takes room from the rows, so the limit drops until the gapped rows fit.
        for (var limit = (int)((bottom - header - Gap + Leading) / Leading); limit > 0; limit--)
        {
            rows.Clear();
            var baseline = header + Gap - Leading - fonts.Metrics.Ascent;

            foreach (var (lead, runs, gap) in Wrapped(card.Lines, fonts, TextWidth, limit))
            {
                baseline += Leading + (gap && rows.Count > 0 ? ParagraphGap : 0);
                rows.Add(new Row(lead, runs, baseline));
            }

            if (baseline <= bottom) break;
        }

        var end = rows.Count > 0 ? rows[^1].Baseline + fonts.Metrics.Descent : header;

        return new Layout(nameBaseline, kindBaseline, rows, (int)Math.Ceiling(end + Margin));
    }

    private byte[] Draw(CardModel card, IReadOnlyList<(SKImage Image, CardLayer Layer)> layers)
    {
        using var fonts = new BodyFonts();
        var layout = Arrange(card, fonts);

        using var surface = SKSurface.Create(new SKImageInfo(Width, layout.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;

        canvas.Clear(Colour("core.secondary-1000"));

        using (var line = new SKPaint { Color = Colour("core.secondary-700"), Style = SKPaintStyle.Stroke, StrokeWidth = Frame })
        {
            canvas.DrawRect(Frame / 2f, Frame / 2f, Width - Frame, layout.Height - Frame, line);
        }

        var accent = SKColor.Parse(card.Colour);
        var left = (float)Margin;

        if (layers.Count > 0)
        {
            DrawIcon(canvas, layers, accent);
            left = Margin + IconEdge + Gap;
        }

        var width = Width - Margin - left;

        using var nameFont = Fitted(card.Name, width);
        using var kindFont = new SKFont(SansSemiBold, KindSize);

        using var namePaint = new SKPaint { Color = accent, IsAntialias = true };
        using var kindPaint = new SKPaint { Color = Colour("core.text-secondary"), IsAntialias = true };
        using var leadPaint = new SKPaint { Color = Colour("core.primary-200"), IsAntialias = true };
        using var runPaint = new SKPaint { IsAntialias = true };
        var text = Colour("core.text-primary");

        canvas.DrawText(card.Name, left, layout.NameBaseline, SKTextAlign.Left, nameFont, namePaint);
        canvas.DrawText(Clipped(card.KindLine, kindFont, width), left, layout.KindBaseline, SKTextAlign.Left, kindFont, kindPaint);

        foreach (var row in layout.Rows)
        {
            var x = (float)Margin;

            if (row.Lead is not null)
            {
                canvas.DrawText(row.Lead, x, row.Baseline, SKTextAlign.Left, fonts.Lead, leadPaint);
                x += fonts.Lead.MeasureText(row.Lead);
            }

            foreach (var run in row.Runs)
            {
                var font = fonts.For(run);
                runPaint.Color = run.Colour is null ? text : SKColor.Parse(run.Colour);

                canvas.DrawText(run.Text, x, row.Baseline, SKTextAlign.Left, font, runPaint);
                x += font.MeasureText(run.Text);
            }
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

    // A word is the runs between two spaces, so a style change inside a word keeps the word on one row.
    private static List<List<CardRun>> Words(IReadOnlyList<CardRun> runs)
    {
        var words = new List<List<CardRun>>();
        var word = new List<CardRun>();

        foreach (var run in runs)
        {
            var parts = run.Text.Split(' ');

            for (var index = 0; index < parts.Length; index++)
            {
                if (index > 0 && word.Count > 0)
                {
                    words.Add(word);
                    word = [];
                }

                if (parts[index].Length > 0) word.Add(run with { Text = parts[index] });
            }
        }

        if (word.Count > 0) words.Add(word);

        return words;
    }

    private static List<CardRun> Joined(List<CardRun> row, List<CardRun> word)
    {
        var joined = row.ToList();

        if (joined.Count > 0) joined[^1] = joined[^1] with { Text = joined[^1].Text + " " };

        foreach (var run in word)
        {
            if (joined.Count > 0 && joined[^1].Styled(run))
            {
                joined[^1] = joined[^1] with { Text = joined[^1].Text + run.Text };
                continue;
            }

            joined.Add(run);
        }

        return joined;
    }

    private static IReadOnlyList<(string? Lead, IReadOnlyList<CardRun> Runs, bool Gap)> Wrapped(
        IReadOnlyList<CardLine> lines, BodyFonts fonts, float width, int limit)
    {
        var rows = new List<(string? Lead, IReadOnlyList<CardRun> Runs, bool Gap)>();

        for (var index = 0; index < lines.Count && rows.Count < limit; index++)
        {
            var allowed = Math.Max(1, limit - rows.Count - (lines.Count - index - 1));
            var lead = lines[index].Lead is { } named ? named + CardModel.LeadMark : null;
            var gap = !lines[index].Continues;
            var words = Words(lines[index].Runs);
            var last = index == lines.Count - 1;
            var mine = 0;
            var row = new List<CardRun>();

            float Room() => width - (mine == 0 && lead is not null ? fonts.Lead.MeasureText(lead) : 0);

            void Close(List<CardRun> runs, bool more)
            {
                rows.Add((mine == 0 ? lead : null, Clipped(runs, fonts, Room(), more), mine == 0 && gap));
                mine++;
            }

            var at = 0;

            for (; at < words.Count; at++)
            {
                var next = Joined(row, words[at]);

                if (row.Count == 0 || fonts.Measure(next) <= Room())
                {
                    row = next;
                    continue;
                }

                if (mine == allowed - 1) break;

                Close(row, false);
                row = Joined([], words[at]);
            }

            if (at < words.Count)
            {
                Close(row, true);
                continue;
            }

            if (row.Count > 0 || lead is not null) Close(row, !last && rows.Count == limit - 1);
        }

        return rows;
    }

    private static List<CardRun> Ended(List<CardRun> runs, string ending)
    {
        if (ending.Length == 0) return runs;
        if (runs.Count == 0) return [new CardRun(ending)];

        var ended = runs.ToList();
        ended[^1] = ended[^1] with { Text = ended[^1].Text.TrimEnd() + ending };

        return ended;
    }

    private static IReadOnlyList<CardRun> Clipped(List<CardRun> runs, BodyFonts fonts, float width, bool more)
    {
        var whole = Ended(runs, more ? CardText.Ellipsis : string.Empty);

        if (fonts.Measure(whole) <= width) return whole;

        var body = runs.ToList();

        while (body.Count > 0 && fonts.Measure(Ended(body, CardText.Ellipsis)) > width)
        {
            var text = body[^1].Text[..^1];

            if (text.Length == 0) body.RemoveAt(body.Count - 1);
            else body[^1] = body[^1] with { Text = text };
        }

        return Ended(body, CardText.Ellipsis);
    }

    private static string Clipped(string text, SKFont font, float width)
    {
        if (font.MeasureText(text) <= width) return text;

        var bare = text.EndsWith(CardText.Ellipsis, StringComparison.Ordinal) ? text[..^CardText.Ellipsis.Length] : text;
        var end = bare.Length;

        while (end > 0 && font.MeasureText(bare[..end].TrimEnd() + CardText.Ellipsis) > width) end--;

        return bare[..end].TrimEnd() + CardText.Ellipsis;
    }

    private SKColor Colour(string token) =>
        SKColor.Parse(tokens.Color(token)
            ?? throw new InvalidOperationException($"The design tokens have no {token}."));
}
