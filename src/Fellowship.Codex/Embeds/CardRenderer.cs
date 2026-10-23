using SkiaSharp;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class CardRenderer(IDesignTokenService tokens, ITextureReader textures)
{
    public const int Width = 1200;

    public const int Height = 300;

    private const int Margin = 40;

    private const int IconEdge = Height - 2 * Margin;

    private const int Frame = 4;

    private const int Gap = 40;

    private const float NameSize = 76;

    private const float SmallestName = 44;

    private const float KindSize = 32;

    private const float LineSize = 28;

    private const float Leading = 40;

    private static readonly string Fonts = Path.Combine(AppContext.BaseDirectory, "Fonts");

    private static readonly SKTypeface Display = Face("EBGaramond-Regular.ttf");

    private static readonly SKTypeface Sans = Face("NotoSans-Regular.ttf");

    private static readonly SKTypeface SansSemiBold = Face("NotoSans-SemiBold.ttf");

    private static SKTypeface Face(string file) =>
        SKTypeface.FromFile(Path.Combine(Fonts, file))
        ?? throw new InvalidOperationException($"{Path.Combine(Fonts, file)} is not a readable font.");

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

    private byte[] Draw(CardModel card, IReadOnlyList<(SKImage Image, CardLayer Layer)> layers)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;

        canvas.Clear(Colour("core.secondary-1000"));

        using (var line = new SKPaint { Color = Colour("core.secondary-700"), Style = SKPaintStyle.Stroke, StrokeWidth = Frame })
        {
            canvas.DrawRect(Frame / 2f, Frame / 2f, Width - Frame, Height - Frame, line);
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
        using var lineFont = new SKFont(Sans, LineSize);
        using var leadFont = new SKFont(SansSemiBold, LineSize);

        using var namePaint = new SKPaint { Color = accent, IsAntialias = true };
        using var kindPaint = new SKPaint { Color = Colour("core.text-secondary"), IsAntialias = true };
        using var linePaint = new SKPaint { Color = Colour("core.text-primary"), IsAntialias = true };
        using var leadPaint = new SKPaint { Color = Colour("core.primary-200"), IsAntialias = true };

        var baseline = Margin + nameFont.Metrics.CapHeight;
        canvas.DrawText(card.Name, left, baseline, SKTextAlign.Left, nameFont, namePaint);

        baseline += nameFont.Metrics.Descent - kindFont.Metrics.Ascent;
        canvas.DrawText(Clipped(card.KindLine, kindFont, width), left, baseline, SKTextAlign.Left, kindFont, kindPaint);

        baseline += kindFont.Metrics.Descent + 8;

        foreach (var (lead, text) in Wrapped(card.Lines, lineFont, leadFont, width, CardModel.Rows))
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
            var lead = lines[index].Lead is { } named ? named + CardModel.LeadMark : null;
            var words = lines[index].Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var last = index == lines.Count - 1;
            var mine = 0;
            var row = string.Empty;

            float Room() => width - (mine == 0 && lead is not null ? leadFont.MeasureText(lead) : 0);

            void Close(string text, bool more)
            {
                rows.Add((mine == 0 ? lead : null,
                    more ? Clipped(text + CardText.Ellipsis, font, Room()) : Clipped(text, font, Room())));
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

        var bare = text.EndsWith(CardText.Ellipsis, StringComparison.Ordinal) ? text[..^CardText.Ellipsis.Length] : text;
        var end = bare.Length;

        while (end > 0 && font.MeasureText(bare[..end].TrimEnd() + CardText.Ellipsis) > width) end--;

        return bare[..end].TrimEnd() + CardText.Ellipsis;
    }

    private SKColor Colour(string token) =>
        SKColor.Parse(tokens.Color(token)
            ?? throw new InvalidOperationException($"The design tokens have no {token}."));
}
