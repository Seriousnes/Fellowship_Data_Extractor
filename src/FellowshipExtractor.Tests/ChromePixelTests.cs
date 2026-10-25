using Fellowship.SDK.Documents;
using Fellowship.Codex.Services;

using Microsoft.Extensions.Logging.Abstractions;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FellowshipExtractor.Tests;

public sealed class ChromePixelTests
{
    private sealed record Reading(EntityStoreService Store, IChromeAssetService Chrome);

    private static readonly Lazy<Reading> Instance = new(Build, isThreadSafe: true);

    private static Reading Read => Instance.Value;

    private static string Ui => Export.Loaded.Ui;

    private static Reading Build()
    {
        var snapshot = new FolderExportSource(Export.Loaded.Folder)
            .LoadAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            .Snapshot;

        var store = new EntityStoreService(snapshot, NullLogger<EntityStoreService>.Instance);

        return new Reading(
            store,
            new ChromeAssetService(
                new StorageTextureSource(new MemoryStorage()),
                store,
                new DesignTokenService(store.Settings)));
    }

    private static string Texture(string url) => Path.Combine(Ui, url[(url.LastIndexOf('/') + 1)..]);

    private static Image<Rgba32> Carried(string url) => Image.Load<Rgba32>(Texture(url));

    private static (int Width, int Height) Shape(string file)
    {
        using var image = Image.Load(file);
        return (image.Width, image.Height);
    }

    private static bool Painted(Image<Rgba32> image, int row)
    {
        for (var x = 0; x < image.Width; x++)
        {
            if (image[x, row].A > 0) return true;
        }

        return false;
    }

    private static (Rectangle Box, int Chamfer) Hollow(Image<Rgba32> frame)
    {
        var reached = new bool[frame.Width, frame.Height];
        var walking = new Stack<Point>();
        walking.Push(new Point(frame.Width / 2, frame.Height / 2));
        reached[frame.Width / 2, frame.Height / 2] = true;

        int left = frame.Width, right = -1, top = frame.Height, bottom = -1;

        while (walking.Count > 0)
        {
            var at = walking.Pop();

            left = Math.Min(left, at.X);
            right = Math.Max(right, at.X);
            top = Math.Min(top, at.Y);
            bottom = Math.Max(bottom, at.Y);

            foreach (var step in new[] { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) })
            {
                var next = new Point(at.X + step.X, at.Y + step.Y);

                if (next.X < 0 || next.Y < 0 || next.X >= frame.Width || next.Y >= frame.Height) continue;
                if (reached[next.X, next.Y] || frame[next.X, next.Y].A != 0) continue;

                reached[next.X, next.Y] = true;
                walking.Push(next);
            }
        }

        var cut = 0;
        while (cut < frame.Width && !reached[cut, top]) cut++;

        return (new Rectangle(left, top, right - left + 1, bottom - top + 1), cut);
    }

    private static bool Covers(Image<Rgba32> backing, double inset)
    {
        var left = (int)Math.Ceiling(backing.Width * inset);
        var top = (int)Math.Ceiling(backing.Height * inset);

        for (var y = top; y < backing.Height - top; y++)
        {
            for (var x = left; x < backing.Width - left; x++)
            {
                if (backing[x, y].A < 254) return false;
            }
        }

        return true;
    }

    private static Rectangle Well(string file)
    {
        using var plate = Image.Load<Rgba32>(file);

        var fill = plate[plate.Width / 2, plate.Height / 2];

        var reached = new bool[plate.Width, plate.Height];
        var walking = new Stack<Point>();
        walking.Push(new Point(plate.Width / 2, plate.Height / 2));
        reached[plate.Width / 2, plate.Height / 2] = true;

        int left = plate.Width, right = -1, top = plate.Height, bottom = -1;

        while (walking.Count > 0)
        {
            var at = walking.Pop();

            left = Math.Min(left, at.X);
            right = Math.Max(right, at.X);
            top = Math.Min(top, at.Y);
            bottom = Math.Max(bottom, at.Y);

            foreach (var step in new[] { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) })
            {
                var next = new Point(at.X + step.X, at.Y + step.Y);

                if (next.X < 0 || next.Y < 0 || next.X >= plate.Width || next.Y >= plate.Height) continue;
                if (reached[next.X, next.Y] || !plate[next.X, next.Y].Equals(fill)) continue;

                reached[next.X, next.Y] = true;
                walking.Push(next);
            }
        }

        return new Rectangle(left, top, right - left + 1, bottom - top + 1);
    }

    [Fact]
    public void TheWellTheStylesheetCutsTheArtToIsTheHoleInEveryPlate()
    {
        var wells = Enum.GetValues<Fellowship.Codex.Models.AttunementNodeState>()
            .Select(state => Well(Texture(Read.Chrome.AttunementNode(state))))
            .ToArray();

        Assert.All(wells, well =>
        {
            Assert.Equal(new Size(154, 180), well.Size);
            Assert.Equal(208 - well.Right, well.Left);
            Assert.Equal(240 - well.Bottom, well.Top);
        });

        Assert.Single(wells.Distinct());
    }

    [Fact]
    public void EveryStateFrameIsTheShapeTheStylesheetDerivesItsHeightFrom()
    {
        Assert.All(
            Enum.GetValues<Fellowship.Codex.Models.AttunementNodeState>(),
            state => Assert.Equal((208, 240), Shape(Texture(Read.Chrome.AttunementNode(state)))));

        Assert.All(
            Enum.GetValues<Fellowship.Codex.Models.GlyphState>(),
            state => Assert.Equal((148, 188), Shape(Texture(Read.Chrome.GlyphPlate(state)))));
    }

    [Fact]
    public void TheWellTheStylesheetCutsARaritySlotToIsTheHoleInTheRarityFrame()
    {
        using var frame = Carried("T_Borders_RarityFrame_full.webp");

        Assert.Equal(60, frame.Width);
        Assert.Equal(60, frame.Height);

        var (well, chamfer) = Hollow(frame);

        Assert.Equal(new Rectangle(4, 4, 52, 52), well);
        Assert.Equal(9, chamfer);
    }

    [Fact]
    public void EveryTintedRarityFrameKeepsTheWellTheUntintedFrameCuts()
    {
        var named = Read.Store.Settings.Rarities
            .Select(rung => Texture(Read.Chrome.SlotFrame(rung.Name)))
            .ToArray();

        var written = named.Where(File.Exists).ToArray();

        if (written.Length == 0) return;

        var absent = named.Except(written, StringComparer.OrdinalIgnoreCase).ToArray();

        Assert.True(
            absent.Length == 0,
            $"The export tints the rarity frame for some rungs and not others. Missing: {string.Join(", ", absent)}");

        Assert.All(written, file =>
        {
            using var frame = Image.Load<Rgba32>(file);

            var (well, chamfer) = Hollow(frame);

            Assert.Equal(new Rectangle(4, 4, 52, 52), well);
            Assert.Equal(9, chamfer);
        });
    }

    [Fact]
    public void TheWellTheStylesheetCutsAPlainSlotToIsTheHoleInTheAbilityFrame()
    {
        using var frame = Carried(Read.Chrome.SlotFrame(null));

        Assert.Equal(128, frame.Width);
        Assert.Equal(128, frame.Height);

        var (well, chamfer) = Hollow(frame);

        Assert.Equal(new Rectangle(10, 10, 108, 108), well);
        Assert.Equal(16, chamfer);
    }

    [Fact]
    public void TheRarityBackingFillsItsWellSoTheArtIsCompositedOverIt()
    {
        using var backing = Carried(Read.Chrome.SlotBacking(Read.Store.Settings.Rarities[0].Name));

        Assert.True(Covers(backing, 4d / 60), "T_Borders_EmptyWithoutFrame no longer hides texture drawn under it.");
    }

    [Fact]
    public void TheAbilityBandIsOpaqueSoTheArtIsCutToTheWellRatherThanDrawnUnderIt()
    {
        using var frame = Carried(Read.Chrome.SlotFrame(null));

        var middle = frame.Height / 2;

        Assert.All(
            Enumerable.Range(0, 10),
            column => Assert.Equal(255, frame[column, middle].A));

        Assert.All(
            Enumerable.Range(frame.Width - 10, 10),
            column => Assert.Equal(255, frame[column, middle].A));
    }

    [Fact]
    public void TheGoldRuleIsOneBandOfFourPaintedRows()
    {
        using var rule = Carried(Read.Chrome.GoldDivider);

        var painted = Enumerable.Range(0, rule.Height).Where(row => Painted(rule, row)).ToArray();

        Assert.Equal(4, painted.Length);
        Assert.Equal(painted.Length, painted[^1] - painted[0] + 1);
    }

    [Fact]
    public void EveryTextureTheWindowNamesIsInTheFolderTheChromeReads()
    {
        string[] windowed =
        [
            Read.Chrome.RoundedWindow, Read.Chrome.ThinWindow, Read.Chrome.WindowFill,
            Read.Chrome.Window, Read.Chrome.Panel,
        ];

        Assert.All(windowed, url =>
            Assert.True(
                File.Exists(Texture(url)),
                $"{Texture(url)} is not there, so the window would draw nothing."));
    }

    [Fact]
    public void EveryTextureAModifierRowNamesIsInTheFolderTheChromeReads()
    {
        string[] named =
        [
            Read.Chrome.ModifierPlate,
            Read.Chrome.ModifierDecor,
            .. ChromeAssetService.ModifierSlots(Read.Store.Settings).Select(slot => Read.Chrome.ModifierGlyph(slot)!),
        ];

        Assert.All(named, url =>
            Assert.True(
                File.Exists(Texture(url)),
                $"{Texture(url)} is not there, so a modifier row would draw nothing."));
    }

    [Fact]
    public void EveryGlyphsAllocatedArtIsInTheFolderTheChromeReads()
    {
        var glyphs = Read.Store.Entities.OfType<GlyphDocument>().ToArray();

        Assert.NotEmpty(glyphs);

        Assert.All(glyphs, glyph =>
        {
            var allocated = Read.Chrome.GlyphRune(glyph, ChromeAssetService.GlyphStateFor(1));

            Assert.NotNull(allocated);
            Assert.True(File.Exists(Texture(allocated)), $"{Texture(allocated)} is not there.");
        });
    }
}
