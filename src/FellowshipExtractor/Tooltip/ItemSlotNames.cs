using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Localization;
using CUE4Parse.UE4.Readers;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// One equipment slot under both its names: the one ECRHeroItemType gives it, which asset fields
/// are spelled with, and the one the game prints.
/// </summary>
public sealed record SlotName(string Type, string Shipped);

/// <summary>
/// What a slot is called on screen. Two shipped sources answer between them and neither alone does.
///
/// The locres carries the enum's own display map, one Fellowship/ItemType_&lt;EnumName&gt; entry per
/// ECRHeroItemType value, and its text is what a player reads. Two of them are not the name the
/// assets use: Waist prints as "Legs" and Trinket prints as "Relic". So the enum name is not the
/// answer and the text is.
///
/// Nothing cooked carries the enum itself, so the ordinals come from DT_SlotWeights, which has one
/// row per slot and repeats the ordinal inside it as ItemType. Its row labels are a designer's
/// rather than the enum's - it calls ordinal 6 Gloves, which the locres does not key at all - so
/// only the ordinals are taken from it and the names stay the locres keys.
/// </summary>
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

    /// <summary>
    /// The one slot whose locres entry is the item-type noun rather than the slot label.
    /// Fellowship/ItemType_Necklace reads "Necklace", and that is the word the build uses for the
    /// thing: "Requires Necklace or Weapon of Epic rarity", "Expedition Necklace". The slot itself
    /// ships as Neck, spelled that way by 17 dynamic item ids and by the slot-requirement string
    /// "Neck, Finger". No table states the distinction, so it is set here rather than derived.
    ///
    /// Waist and Trinket are not in here: "Legs" and "Relic" are the locres text and are already
    /// what the game prints.
    /// </summary>
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

    /// <summary>The slot an ECRHeroItemType ordinal names, or null when it names none.</summary>
    public SlotName? ByOrdinal(int ordinal) => _byOrdinal.GetValueOrDefault(ordinal);

    /// <summary>
    /// The slot a token names, or null when it names no slot. A token is matched against the enum
    /// name and against the shipped text, exactly first and then as a prefix of exactly one of
    /// them. The prefix rule is what reconciles the two spellings the build uses for the same slot:
    /// the dynamic item ids say Wrist where the enum, the locres, the ability pool tags and all 480
    /// fixed item rows say Wrists.
    /// </summary>
    public SlotName? Named(string token)
    {
        if (_byToken.TryGetValue(token, out var cached)) return cached;
        return _byToken[token] = Match(token);
    }

    /// <summary>
    /// The slot a dynamic item's own id names. A dynamic item carries no HeroItemType and its
    /// RuleSet names a loot ruleset - Default_NoGemSlot, SetBonus_GemSlot - rather than a slot, so
    /// the id is the only place the slot is: ItemID.HeroItem.Dynamic.Head.SetB_B_Crit. The eleven
    /// legendaries file under their rarity instead and name the slot one segment further down, as
    /// Legendary.Back_A_CriticalStrike, so the id is read left to right and the first token naming
    /// a slot wins. That answers all 237 shipped definitions and leaves only the bare
    /// ItemID.HeroItem.Dynamic.Legendary category tag, which names a rarity and no slot.
    /// </summary>
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

                // The enum's own no-slot value. Nothing wears it and it would swallow any token
                // beginning with U.
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

        // DT_SlotWeights labels ordinal 6 Gloves, which is neither an enum name nor anything the
        // locres prints. One ordinal left over and one slot name left over identify each other.
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
