using System.Buffers;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// The whole export as one page anybody can open: every named entity, its icon and its tooltip
/// text, in a single self-contained HTML file with nothing to install and nothing to fetch.
///
/// It reads what <see cref="ExportAllCommand"/> already wrote rather than resolving anything
/// again, so the page can never disagree with entities.jsonl. Rows without a display name are
/// left out because a nameless entity has no tooltip to show; that is the only thing dropped,
/// and every remaining row goes in as it was written.
/// </summary>
public static class WebDbCommand
{
    private const string FolderName = "export";
    private const string SourceName = "entities.jsonl";
    private const string FileName = "tooltips.html";

    /// <summary>The one line of the shell the payload replaces. Verified to occur exactly once.</summary>
    private const string PayloadMarker = "const DATA = \"__PAYLOAD__\";";

    private const string ShellResource = "FellowshipExtractor.Tooltip.WebDb.html";

    public static async Task RunAsync(ExtractorOptions options)
    {
        var folder = Path.Combine(options.ResolvedOutputDir, FolderName);
        var source = Path.Combine(folder, SourceName);

        if (!File.Exists(source))
        {
            Console.Error.WriteLine($"No export at {source}.");
            Console.Error.WriteLine("Run 'export-all' first: this command only reformats what that wrote.");
            return;
        }

        var entities = new List<JsonDocument>();
        var iconNames = new SortedSet<string>(StringComparer.Ordinal);
        var lines = 0;

        foreach (var line in File.ReadLines(source))
        {
            if (line.Length == 0) continue;
            lines++;

            var document = JsonDocument.Parse(line);
            if (!HasDisplayName(document.RootElement))
            {
                document.Dispose();
                continue;
            }

            entities.Add(document);
            CollectIcons(document.RootElement, iconNames);
        }

        Console.WriteLine($"read {lines} rows from {source}");
        Console.WriteLine($"  {entities.Count} carry a display name and become cards");
        Console.WriteLine($"  {iconNames.Count} distinct icons referenced");

        var icons = Thumbnails(folder, iconNames);
        var payload = Payload(entities, icons, out var resolvedTokens);

        foreach (var document in entities) document.Dispose();

        var target = Path.Combine(folder, FileName);
        await WritePageAsync(target, payload);

        var bytes = new FileInfo(target).Length;
        Console.WriteLine($"\nwrote {target}");
        Console.WriteLine($"  payload {payload.Length:N0} bytes, page {bytes:N0} bytes");
        Console.WriteLine($"  {resolvedTokens} description tokens resolved to a share of an attribute or a number");
    }

    private static bool HasDisplayName(JsonElement entity) =>
        entity.TryGetProperty("displayName", out var name) && name.ValueKind == JsonValueKind.String;

    /// <summary>
    /// Gathers icon names from the whole row, not just its top level: an item's art is often
    /// carried by the ability it grants rather than by the item itself.
    /// </summary>
    private static void CollectIcons(JsonElement value, SortedSet<string> into)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject())
                {
                    if (property.NameEquals("icon") && property.Value.ValueKind == JsonValueKind.String)
                    {
                        into.Add(property.Value.GetString()!);
                    }
                    else
                    {
                        CollectIcons(property.Value, into);
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray()) CollectIcons(item, into);
                break;
        }
    }

    private static async Task WritePageAsync(string target, byte[] payload)
    {
        var shell = Shell();
        var marker = shell.IndexOf(PayloadMarker, StringComparison.Ordinal);
        if (marker < 0 || shell.IndexOf(PayloadMarker, marker + 1, StringComparison.Ordinal) >= 0)
        {
            throw new InvalidOperationException(
                $"{ShellResource} must contain exactly one '{PayloadMarker}' line for the payload.");
        }

        var head = Encoding.UTF8.GetBytes(shell[..marker] + "const DATA = ");
        var tail = Encoding.UTF8.GetBytes(";" + shell[(marker + PayloadMarker.Length)..]);

        await using var output = File.Create(target);
        await output.WriteAsync(head);
        await output.WriteAsync(payload);
        await output.WriteAsync(tail);
    }

    private static string Shell()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ShellResource)
            ?? throw new InvalidOperationException($"Embedded resource {ShellResource} is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// The single JSON object the page reads. Serialised with the default encoder, which escapes
    /// every angle bracket and ampersand, so no description can close the script element it sits
    /// in however the game's own markup is written.
    /// </summary>
    private static byte[] Payload(
        List<JsonDocument> entities, IReadOnlyList<(string Name, string Uri)> icons, out int resolvedTokens)
    {
        var buffer = new ArrayBufferWriter<byte>(4 << 20);
        using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false });
        var resolved = 0;

        writer.WriteStartObject();

        writer.WriteStartObject("meta");
        writer.WriteString("generated", DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture));
        writer.WriteString("source", SourceName);
        writer.WriteEndObject();

        // Keyed by the icon file name the entity carries, extension and all, so the page needs no
        // string surgery to look one up. The key therefore ends in .png while the value is WebP.
        writer.WriteStartObject("icons");
        foreach (var (name, uri) in icons) writer.WriteString(name, uri);
        writer.WriteEndObject();

        writer.WriteStartArray("entities");
        foreach (var entity in entities) resolved += WriteEntity(entity.RootElement, writer);
        writer.WriteEndArray();

        writer.WriteEndObject();
        writer.Flush();

        resolvedTokens = resolved;
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Copies one row through verbatim and adds a placeholderText map beside every placeholders
    /// map it contains, at every depth: the ability an item grants and a legendary's rolls carry
    /// tokens of their own, so a top-level-only pass would leave {Token} on screen for all of
    /// them. Answers with the number of tokens the row resolved.
    /// </summary>
    private static int WriteEntity(JsonElement value, Utf8JsonWriter writer)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                var resolved = 0;

                foreach (var property in value.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);

                    if (property.NameEquals("placeholders"))
                    {
                        property.Value.WriteTo(writer);
                    }
                    else
                    {
                        resolved += WriteEntity(property.Value, writer);
                    }
                }

                if (value.TryGetProperty("placeholders", out var placeholders))
                {
                    resolved += WritePlaceholderText(placeholders, writer);
                }

                writer.WriteEndObject();
                return resolved;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                var fromItems = 0;
                foreach (var item in value.EnumerateArray()) fromItems += WriteEntity(item, writer);
                writer.WriteEndArray();
                return fromItems;

            default:
                value.WriteTo(writer);
                return 0;
        }
    }

    /// <summary>
    /// What each {Token} in the description prints, worked out here rather than in the page.
    /// A token that scales off a stat prints as a share of it; a period, a duration or a count
    /// prints as itself.
    ///
    /// Every number is read as a decimal straight from the JSON text because binary floating point
    /// does not survive the multiplication: 8.767 x 100 is 876.6999999999999 as a double, and 48 of
    /// the shipped coefficients land like that.
    /// </summary>
    private static int WritePlaceholderText(JsonElement placeholders, Utf8JsonWriter writer)
    {
        if (placeholders.ValueKind != JsonValueKind.Object) return 0;

        var written = 0;

        foreach (var placeholder in placeholders.EnumerateObject())
        {
            if (placeholder.Value.ValueKind != JsonValueKind.Object) continue;
            if (Substitution(placeholder.Value) is not { } text) continue;

            if (written == 0) writer.WriteStartObject("placeholderText");
            written++;

            writer.WriteString(placeholder.Name, text);
        }

        if (written > 0) writer.WriteEndObject();
        return written;
    }

    /// <summary>
    /// One token's text. A token that carries a word rather than a number prints that word: the
    /// gem overflow sentences substitute the gem type's name this way.
    ///
    /// A coefficient prints as a share of the attribute the row named, or as a
    /// bare share where the row named none: Heart Splitter's "{BonusOfRend} of the Total Rend
    /// Damage" supplies its own object and wants 30%, not 30% of a thing called Dot.
    ///
    /// A coefficient with a spread prints as the band rather than the mean, because the band is
    /// what the game rolls in and the mean is a number it never shows. The band is written in
    /// shares of the attribute rather than in hit points so the page needs no assumed stat value:
    /// Fire Ball reads 526.338% to 643.302% of Intellect, which at 100 Intellect is 526 to 643.
    ///
    /// A scalar prints as itself unless the token declares otherwise. Two of the game's own token
    /// flags decide that: ShowPercent, which turns 0.5 into 50%, and ShowUnit, which supplies the
    /// word a sentence like "healing you every {BloodRushPeriod}" leaves out.
    /// </summary>
    private static string? Substitution(JsonElement placeholder)
    {
        // A token standing in for a name is already the words it prints and has no arithmetic to do.
        if (Text(placeholder, "text") is { } word) return word;

        if (Decimal(placeholder, "coefficient") is { } coefficient)
        {
            var attribute = Text(placeholder, "attribute");
            var of = attribute is null ? "" : $" of {attribute}";

            // Spread is the whole band, so half of it lies either side of the mean. Zero is a band
            // of nothing and prints as the single exact number.
            return Decimal(placeholder, "spread") is { } spread && spread > 0m
                ? $"{Share(coefficient * (1m - spread / 2m))} to {Share(coefficient * (1m + spread / 2m))}{of}"
                : Share(coefficient) + of;
        }

        if (Decimal(placeholder, "value") is not { } value) return null;

        // A gem or set bonus argument says outright how it is printed, and the arithmetic differs
        // per format: Ruby node 1 stores 0.8 and 1.02 in the same sentence and prints "+2% ... above
        // 80% health".
        if (Text(placeholder, "format") is { } format)
        {
            return format switch
            {
                "percent" => Share(value),
                "delta" => Signed(value - 1m),
                _ => Number(value),
            };
        }

        if (Flag(placeholder, "percent")) return Share(value);

        var number = Number(value);
        return Text(placeholder, "unit") is { } unit ? $"{number} {Singular(unit, value)}" : number;
    }

    private static string Share(decimal value) => Number(value * 100m) + "%";

    private static string Signed(decimal value) => (value < 0 ? "" : "+") + Share(value);

    private static string Number(decimal value) =>
        value.ToString("0.############", CultureInfo.InvariantCulture);

    /// <summary>Only "1 second" needs it, and the shipped units are all plural nouns ending in s.</summary>
    private static string Singular(string unit, decimal value) =>
        value == 1m && unit.EndsWith('s') ? unit[..^1] : unit;

    private static string? Text(JsonElement holder, string name) =>
        holder.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool Flag(JsonElement holder, string name) =>
        holder.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.True;

    private static decimal? Decimal(JsonElement holder, string name) =>
        holder.TryGetProperty(name, out var property) &&
        property.ValueKind == JsonValueKind.Number &&
        property.TryGetDecimal(out var value)
            ? value
            : null;

    /// <summary>
    /// Every referenced icon as a WebP data URI, keyed by its png file name.
    ///
    /// The exported png set is 137 MB, which no single file can carry, and lossless or JPEG
    /// re-encoding both fail: JPEG paints the transparent quarter of the set solid black, and
    /// lossless WebP is four times the size of lossy at the same edge. Alpha compression has to
    /// stay on; without it the same set costs an extra 1.6 MB.
    ///
    /// 64px lossy at quality 70 is 1.0 MB for all of them. 96px would be exactly 2x the 52px the
    /// cards draw at and looks better on a dense display, but it costs 1.7 MB, and the whole page
    /// has to stay small enough to publish and open quickly.
    /// </summary>
    private static IReadOnlyList<(string Name, string Uri)> Thumbnails(string folder, SortedSet<string> names)
    {
        const int edge = 64;
        const int quality = 70;

        var wanted = names.ToArray();
        var uris = new string?[wanted.Length];
        var missing = 0;

        Parallel.For(0, wanted.Length, i =>
        {
            var file = Path.Combine(folder, wanted[i]);
            if (!File.Exists(file))
            {
                Interlocked.Increment(ref missing);
                return;
            }

            using var image = Image.Load<Rgba32>(file);

            // Downscale only. A handful of sources are already smaller than the target and a few
            // are portraits rather than squares, so the aspect is kept and never padded.
            using var thumbnail = Math.Max(image.Width, image.Height) <= edge
                ? image.Clone()
                : image.Clone(c => c.Resize(new ResizeOptions
                {
                    Size = new Size(edge, edge),
                    Mode = ResizeMode.Max,
                    Sampler = KnownResamplers.Box,
                }));

            using var encoded = new MemoryStream();
            thumbnail.Save(encoded, new WebpEncoder
            {
                FileFormat = WebpFileFormatType.Lossy,
                Quality = quality,
                Method = WebpEncodingMethod.BestQuality,
                UseAlphaCompression = true,
                SkipMetadata = true,
            });

            uris[i] = "data:image/webp;base64," + Convert.ToBase64String(encoded.ToArray());
        });

        var icons = new List<(string Name, string Uri)>(wanted.Length);
        for (var i = 0; i < wanted.Length; i++)
        {
            if (uris[i] is { } uri) icons.Add((wanted[i], uri));
        }

        var payload = icons.Sum(icon => (long)icon.Uri.Length);
        Console.WriteLine($"  {icons.Count} icons encoded to {edge}px webp, {payload:N0} bytes of data uri");
        if (missing > 0) Console.WriteLine($"  {missing} referenced icons are not on disk and render as a marker");

        return icons;
    }
}
