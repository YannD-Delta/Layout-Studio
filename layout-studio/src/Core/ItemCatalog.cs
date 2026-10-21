using System;
using System.Collections.Generic;
using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>One item (a piece of game information, a section or a widget) and every place of the game page where the theme can show it.</summary>
    public class ItemPlaces
    {
        public ItemFamily Family;
        public string ChipShowKey;                                   // the chip in the artwork header (HeroShow...)
        public string WidgetShowKey;                                 // the switch of a widget of the header (HeroShowMusicPlayer...): it has no place in the chip line
        public string ChipOrderKey;                                  // its place in the chip line (HeroChipOrder...)
        public readonly List<string> ChipBreakKeys = new List<string>();   // "new line before" switches of its chip(s)
        public readonly List<string> TileIds = new List<string>();   // header card types that show it (ids of the tile choices)
        public readonly List<string> PanelKeys = new List<string>(); // rows of the information panel (InfoPanelShow...)
        public SectionDef Section;                                   // its own section of the game page
        public readonly List<string> Keys = new List<string>();      // every constant the item owns

        public string Id { get { return Family.Id; } }
        public string Title { get { return Family.Title; } }
        public bool InChips { get { return ChipShowKey != null && ChipOrderKey != null; } }
        public bool InCards { get { return TileIds.Count > 0; } }
        public bool InPanel { get { return PanelKeys.Count > 0; } }
        public bool InSection { get { return Section != null; } }
    }

    /// <summary>
    /// The list of items, generated from the schema: the chips come from the HeroShow switches, the cards from the tile types of the header cells,
    /// the panel rows from InfoPanelShow, the sections from the schema's sections. Nothing is listed by hand except the few tile types that
    /// belong to the same item (the three HowLongToBeat tiles...).
    /// </summary>
    public static class ItemCatalog
    {
        /// <summary>The item a header card type belongs to ("HltbMainExtra" is HowLongToBeat, "Scores" is the community score...).</summary>
        public static string TileItemId(string tileId)
        {
            if (string.IsNullOrEmpty(tileId)) return null;
            if (tileId.StartsWith("Hltb", StringComparison.Ordinal)) return "Hltb";
            if (tileId == "AchievementsPercent") return "Achievements";
            if (tileId == "Scores") return "CommunityScore";
            return tileId;
        }

        public static IList<ItemPlaces> Build(Schema schema)
        {
            var controls = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).ToList();
            var tileControl = controls.FirstOrDefault(c => c.Kind == "tileSlots");
            var orderKeys = controls.Where(c => c.Kind == "orderedList" && c.Id == ChipLayout.ListId).SelectMany(c => c.Keys).ToList();
            var result = new List<ItemPlaces>();
            // the family of each constant is worked out once (it matches the constant's name against every family word)
            var owned = controls.Where(c => ItemFamilies.IsItemControl(c) && schema.ItemsMayEdit(c.Keys[0])).Select(c => new KeyValuePair<string, ItemFamily>(c.Keys[0], ItemFamilies.FamilyOf(c.Keys[0]))).ToList();
            var orderFamilies = orderKeys.Select(k => new KeyValuePair<string, ItemFamily>(k, ItemFamilies.FamilyOf(k))).ToList();
            foreach (var family in ItemFamilies.All)
            {
                var item = new ItemPlaces { Family = family };
                foreach (var pair in owned.Where(o => ReferenceEquals(o.Value, family)))
                {
                    string key = pair.Key;
                    item.Keys.Add(key);
                    if (key == "HeroShow" + family.Id) item.ChipShowKey = key;
                    else if (key.StartsWith("HeroChipBreak", StringComparison.Ordinal)) item.ChipBreakKeys.Add(key);
                    else if (key.StartsWith("InfoPanelShow", StringComparison.Ordinal)) item.PanelKeys.Add(key);
                }
                var order = orderFamilies.FirstOrDefault(o => ReferenceEquals(o.Value, family));
                if (order.Key != null) { item.ChipOrderKey = order.Key; item.Keys.Add(order.Key); }
                else if (item.ChipShowKey != null) { item.WidgetShowKey = item.ChipShowKey; item.ChipShowKey = null; }
                if (tileControl != null)
                    item.TileIds.AddRange(tileControl.Choices.Where(c => c.Id != "Empty" && TileItemId(c.Id) == family.Id).Select(c => c.Id));
                item.Section = schema.Sections.FirstOrDefault(s => ReferenceEquals(ItemFamilies.FamilyOfSection(s), family));
                result.Add(item);
            }
            return result;
        }

        /// <summary>
        /// Who owns a constant of the header: "item:&lt;id&gt;" for an item, "zone:&lt;place&gt;" for a constant about a whole place (the chips line, the cards row).
        /// A constant must have exactly one owner, so no option of a card or a chip is left without a home on the Items page.
        /// </summary>
        public static IList<string> OwnersOf(IList<ItemPlaces> catalog, string key)
        {
            var owners = new List<string>();
            foreach (var item in catalog)
                if (item.Keys.Contains(key)) owners.Add("item:" + item.Id);
            var zone = ItemZones.ZoneOwning(key);
            if (zone != null) owners.Add("zone:" + zone);
            return owners;
        }
    }
}
