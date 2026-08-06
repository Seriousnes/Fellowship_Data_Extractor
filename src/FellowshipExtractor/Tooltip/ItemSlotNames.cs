using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Localization;
using CUE4Parse.UE4.Readers;

namespace FellowshipExtractor.Tooltip;

public sealed record SlotName(string Type, string Shipped);

public sealed class ItemSlotNames
{
    public const string SlotWeightRowStructure = "/Script/fellowship.CRSlotWeightRow";

    private const string LocresPath = "fellowship/Content/Localization/Game/en/Game.locres";
    private const string TextNamespace = "Fellowship";
    private const string KeyPrefix = "ItemType_";
    private const string UnknownType = "Unknown";
    private const string OrdinalField = "ItemType";
    private const string DynamicTagPrefix = "ItemID.HeroItem.Dynamic.";

    private static readonly char[] TagSeparators = ['.', '_'];

    private static readonly Dictionary<string, string> SlotLabels =
        new(StringComparer.Ordinal) { ["Necklace"] = "Neck" };

    private readonly List<SlotName> _vocabulary = [];
    private readonly Dictionary<int, SlotName> _byOrdinal = [];
    private readonly Dictionary<string, SlotName?> _byToken = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _vocabulary.Count;

    public ItemSlotNames(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        ReadVocabulary(provider);
        ReadOrdinals(provider, assets);
    }

    public SlotName? ByOrdinal(int ordinal) => _byOrdinal.GetValueOrDefault(ordinal);

    public SlotName? Named(string token)
    {
        if (_byToken.TryGetValue(token, out var cached)) return cached;
        return _byToken[token] = Match(token);
    }

    public SlotName? FromTag(string tagName)
    {
        if (!tagName.StartsWith(DynamicTagPrefix, StringComparison.OrdinalIgnoreCase)) return null;

        foreach (var token in tagName[DynamicTagPrefix.Length..].Split(TagSeparators))
        {
            if (Named(token) is { } slot) return slot;
        }

        return null;
    }

    private SlotName? Match(string token)
    {
        if (token.Length == 0) return null;

        foreach (var slot in _vocabulary)
        {
            if (slot.Type.Equals(token, StringComparison.OrdinalIgnoreCase) ||
                slot.Shipped.Equals(token, StringComparison.OrdinalIgnoreCase))
            {
                return slot;
            }
        }

        SlotName? prefixed = null;

        foreach (var slot in _vocabulary)
        {
            if (!slot.Type.StartsWith(token, StringComparison.OrdinalIgnoreCase) &&
                !slot.Shipped.StartsWith(token, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (prefixed is not null && prefixed != slot)
            {
                throw new InvalidOperationException(
                    $"'{token}' is the start of both {prefixed.Shipped} and {slot.Shipped}, so it names no one " +
                    "slot. The shipped slot names have changed and the ids that abbreviate them are no longer " +
                    "readable.");
            }

            prefixed = slot;
        }

        return prefixed;
    }

    private void ReadVocabulary(AbstractFileProvider provider)
    {
        if (!provider.Files.TryGetValue(LocresPath, out var file))
        {
            throw new InvalidOperationException(
                $"{LocresPath} is missing. It is the only shipped source of what a slot is called, so every " +
                "item would export without one.");
        }

        using var archive = new FByteArchive(file.Path, file.Read(), provider.Versions);
        var locres = new FTextLocalizationResource(archive);

        foreach (var (textNamespace, entries) in locres.Entries)
        {
            if (!textNamespace.Str.Equals(TextNamespace, StringComparison.Ordinal)) continue;

            foreach (var (key, entry) in entries)
            {
                if (!key.Str.StartsWith(KeyPrefix, StringComparison.Ordinal)) continue;

                var type = key.Str[KeyPrefix.Length..];
                if (type.Equals(UnknownType, StringComparison.Ordinal)) continue;

                _vocabulary.Add(new SlotName(type, SlotLabels.GetValueOrDefault(type) ?? entry.LocalizedString));
            }
        }

        _vocabulary.Sort((left, right) => string.CompareOrdinal(left.Type, right.Type));

        if (_vocabulary.Count == 0)
        {
            throw new InvalidOperationException(
                $"The {TextNamespace} locres namespace holds no {KeyPrefix} entries, so no slot has a name.");
        }
    }

    private void ReadOrdinals(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        var labels = new Dictionary<int, string>();

        foreach (var objectPath in TableFinder.ByRowStructure(assets, SlotWeightRowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                if (PropertyWalk.AsOrdinal(PropertyWalk.Get(row, OrdinalField)?.GenericValue) is { } ordinal)
                {
                    labels.TryAdd(ordinal, rowName.Text);
                }
            }
        }

        if (labels.Count == 0)
        {
            throw new InvalidOperationException(
                "No table declares CRSlotWeightRow, so nothing says which slot an ECRHeroItemType ordinal is " +
                "and no fixed hero item would export one.");
        }

        var claimed = new HashSet<string>(StringComparer.Ordinal);
        var unlabelled = new List<int>();

        foreach (var (ordinal, label) in labels)
        {
            if (Named(label) is { } slot)
            {
                _byOrdinal[ordinal] = slot;
                claimed.Add(slot.Type);
            }
            else
            {
                unlabelled.Add(ordinal);
            }
        }

        if (unlabelled.Count == 0) return;

        var unclaimed = _vocabulary.Where(slot => !claimed.Contains(slot.Type)).ToArray();

        if (unlabelled.Count != 1 || unclaimed.Length != 1)
        {
            throw new InvalidOperationException(
                $"DT_SlotWeights labels {unlabelled.Count} ordinals with something the shipped slot names do not " +
                $"cover, against {unclaimed.Length} slot names no other ordinal took. The two only identify each " +
                "other while there is exactly one of each.");
        }

        _byOrdinal[unlabelled[0]] = unclaimed[0];
    }
}
