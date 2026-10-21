using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    /// <summary>The options of one item that show in one place (the chips, the game page, the grid...).</summary>
    public class ItemSurfaceViewModel
    {
        public string Title { get; set; }
        public IList<SettingViewModel> Settings { get; set; }
    }

    /// <summary>One item (Genres, Release date, HowLongToBeat...) with every option the theme has for it, instead of a lone checkbox in a long list.</summary>
    public class ItemCardViewModel : ViewModelBase
    {
        private bool expanded, visible = true;

        public string Id { get; set; }
        public string Title { get; set; }
        public IList<ItemSurfaceViewModel> Surfaces { get; set; }

        public IEnumerable<SettingViewModel> Settings { get { return Surfaces.SelectMany(s => s.Settings); } }
        public int OptionCount { get { return Settings.Count(); } }
        /// <summary>Words a count ("{0} options"); set by the editor from the language file.</summary>
        public System.Func<int, string> CountText { get; set; }
        public string OptionsText { get { return CountText != null ? CountText(OptionCount) : OptionCount == 1 ? "1 option" : OptionCount + " options"; } }
        public int ChangedCount { get { return Settings.Count(s => s.IsModified); } }
        public bool HasChanges { get { return ChangedCount > 0; } }

        public bool IsExpanded { get { return expanded; } set { if (expanded != value) { expanded = value; Raise("IsExpanded"); } } }
        public bool Visible { get { return visible; } set { if (visible != value) { visible = value; Raise("Visible"); } } }

        public void RefreshCounts() { Raise("ChangedCount"); Raise("HasChanges"); Raise("ShowsTwice"); }

        /// <summary>Reads the current value of a constant; set by the editor (null: the card never says "shown twice").</summary>
        public System.Func<string, string> Reader { get; set; }

        /// <summary>Folds the section at the bottom (one undo step); set by the editor.</summary>
        public System.Action FoldSection { get; set; }

        /// <summary>The words of the notice ("Shown in the chips and in its own section"); set by the editor from the language file.</summary>
        public string TwiceText { get; set; }
        public string FoldText { get; set; }

        /// <summary>True when the chip and the section both show this item and the section is not folded away at the bottom.</summary>
        public bool ShowsTwice { get { return Reader != null && ItemDuplicates.ShowsTwice(ItemDuplicates.For(Id), Reader); } }

        public RelayCommand FoldSectionCommand { get { return new RelayCommand(() => { if (FoldSection != null) FoldSection(); }); } }
    }

    /// <summary>
    /// The Items page: everything the game page can show, as flat lists by place (information chips, header cards, buttons and widgets, information
    /// panel, sections, grid and covers). A row is an item in one place: a switch, the order where the place has one (drag, or the up and down buttons),
    /// and the rarer options behind "more". Every option of an item is still held by its <see cref="Cards"/> entry, which counts, searches and resets them.
    /// </summary>
    public class ItemsEditorViewModel : ViewModelBase, IResettable
    {
        private class TileInfo
        {
            public string Id, Name, ItemId;
            public int Value;
        }

        private readonly History history;
        private readonly ConstantStore store;
        private readonly Schema schema;
        private readonly Func<string, string> localize;
        private readonly IList<ItemPlaces> catalog;
        private readonly Dictionary<string, SettingViewModel> byKey = new Dictionary<string, SettingViewModel>();
        private readonly Dictionary<string, ItemCardViewModel> cardById = new Dictionary<string, ItemCardViewModel>();
        private readonly Dictionary<string, List<ItemRowViewModel>> rowsByKey = new Dictionary<string, List<ItemRowViewModel>>();
        private readonly List<ItemRowViewModel> allRows = new List<ItemRowViewModel>();
        private readonly List<TileInfo> tiles = new List<TileInfo>();
        private readonly IList<string> slotKeys = new List<string>();
        private readonly IList<string> extraKeys = new List<string>();
        private readonly HashSet<string> cardKeys;
        private readonly ISet<string> chipOrderKeys;
        private readonly ISet<string> sectionKeys;
        private readonly Dictionary<int, List<ItemRowViewModel>> cardPool = new Dictionary<int, List<ItemRowViewModel>>();
        private readonly ItemZoneViewModel chipsZone, cardsZone, sectionsZone;
        private readonly List<SettingViewModel> zoneSettings = new List<SettingViewModel>();
        private string focusedId, lastQuery = "";
        private bool lastChangedOnly;

        public ItemsEditorViewModel(Schema schema, ConstantStore store, History history, ProfileApplier applier, Profile profile,
            Func<string, string> localize, CultureInfo culture)
        {
            this.history = history;
            this.store = store;
            this.schema = schema;
            this.localize = localize;
            // the card words come from Core in English; the language file may translate each (key: ItemFamilies.LocKey)
            Func<string, string> Loc = english =>
            {
                string text = null;
                try { text = localize(ItemFamilies.LocKey(english)); } catch (Exception) { }
                return string.IsNullOrEmpty(text) ? english : text;
            };
            var cards = new List<ItemCardViewModel>();
            var controls = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).ToList();
            var itemControls = controls.Where(c => ItemFamilies.IsItemControl(c) && schema.ItemsMayEdit(c.Keys[0])).ToList();
            var familyOf = itemControls.ToDictionary(c => c.Keys[0], c => ItemFamilies.FamilyOf(c.Keys[0]));
            foreach (var family in ItemFamilies.All)
            {
                var mine = itemControls.Where(c => ReferenceEquals(familyOf[c.Keys[0]], family)).ToList();
                if (mine.Count == 0) continue;
                var surfaces = mine
                    .Select(c =>
                    {
                        var setting = new SettingViewModel(c, store, history, applier, profile, localize, culture);
                        string title = ItemFamilies.OptionTitle(c.Keys[0], family);
                        if (title != null) setting.Title = Loc(title);
                        byKey[c.Keys[0]] = setting;
                        return new { Surface = ItemFamilies.SurfaceOf(c.Keys[0]), Setting = setting };
                    })
                    .GroupBy(x => x.Surface)
                    .OrderBy(g => ItemFamilies.SurfaceRank(g.Key))
                    .Select(g => new ItemSurfaceViewModel { Title = Loc(g.Key), Settings = g.Select(x => x.Setting).ToList() })
                    .ToList();
                var rule = ItemDuplicates.For(family.Id);
                var card = new ItemCardViewModel
                {
                    Id = family.Id, Title = Loc(family.Title), Surfaces = surfaces,
                    CountText = n => string.Format(CultureInfo.InvariantCulture, Phrase(localize, n == 1 ? "LOCLS_OptionOne" : "LOCLS_OptionMany", n == 1 ? "{0} option" : "{0} options"), n)
                };
                if (rule != null && ItemDuplicates.AllKeys().All(k => store.Find(k) != null))
                {
                    card.Reader = store.Get;
                    card.TwiceText = Phrase(localize, "LOCLS_Twice", "Shown in the artwork chips and in its own section.");
                    card.FoldText = Phrase(localize, "LOCLS_FoldSection", "Fold the section at the bottom");
                    card.FoldSection = () =>
                    {
                        history.BeginBatch();
                        try { foreach (var key in ItemDuplicates.FoldKeys(rule)) { string error; store.TrySet(key, "True", out error); } }
                        finally { history.EndBatch(); }
                    };
                }
                cards.Add(card);
                cardById[family.Id] = card;
            }
            Cards = cards;
            Once = new InfoOnceViewModel(schema, store, history, profile.Editor, localize);
            catalog = ItemCatalog.Build(schema);

            // the header cards: 12 boxes of up to 4 cards
            var tileControl = controls.FirstOrDefault(c => c.Kind == "tileSlots");
            if (tileControl != null)
            {
                slotKeys = tileControl.Keys.ToList();
                extraKeys = tileControl.ExtraKeys.ToList();
                foreach (var c in tileControl.Choices.Where(c => c.Id != "Empty"))
                {
                    string name = null;
                    try { if (c.LocKey != null) name = localize(c.LocKey); } catch (Exception) { }
                    int value;
                    if (!int.TryParse(c.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) continue;
                    tiles.Add(new TileInfo { Id = c.Id, Name = string.IsNullOrEmpty(name) ? (c.Name ?? c.Id) : name, ItemId = ItemCatalog.TileItemId(c.Id), Value = value });
                }
            }
            cardKeys = new HashSet<string>(slotKeys.Concat(extraKeys));
            chipOrderKeys = ChipLayout.WatchedKeys(schema);
            sectionKeys = SectionLayout.WatchedKeys(schema);

            Func<Control, SettingViewModel> make = c => { var s = new SettingViewModel(c, store, history, applier, profile, localize, culture); zoneSettings.Add(s); return s; };
            Func<string, IList<SettingViewModel>> lookOf = zone => controls
                .Where(c => c.Keys.Count == 1 && c.Kind != "tileSlots" && c.Kind != "orderedList" && schema.ItemsMayEdit(c.Keys[0]) && ItemZones.ZoneOwning(c.Keys[0]) == zone && c.Keys[0] != ItemZones.MasterKey(zone))
                .Select(make).ToList();
            Func<string, SettingViewModel> masterOf = zone =>
            {
                string key = ItemZones.MasterKey(zone);
                var control = key == null ? null : controls.FirstOrDefault(c => c.Keys.Count == 1 && c.Keys[0] == key && c.Kind == "toggle");
                return control == null ? null : make(control);
            };

            chipsZone = new ItemZoneViewModel { Id = ItemZones.Chips, Title = Phrase(localize, "LOCLS_Zone_Chips", "Information chips"), Regions = ItemZones.RegionsOf(ItemZones.Chips),
                Hint = Phrase(localize, "LOCLS_Zone_ChipsHint", "The small labels over the artwork, in the order they have there. Drag a row or use the arrows."),
                Master = masterOf(ItemZones.Chips), Look = lookOf(ItemZones.Chips), CanReorder = true };
            cardsZone = new ItemZoneViewModel { Id = ItemZones.Cards, Title = Phrase(localize, "LOCLS_Zone_Cards", "Header cards"), Regions = ItemZones.RegionsOf(ItemZones.Cards),
                Hint = Phrase(localize, "LOCLS_Zone_CardsHint", "The glass cards under the artwork (time played, scores, achievements, install size...), left to right. A card that does not start a new box sits next to the card before it."),
                Master = masterOf(ItemZones.Cards), Look = lookOf(ItemZones.Cards), CanReorder = true,
                AddLabel = Phrase(localize, "LOCLS_Items_AddCard", "Add a card"),
                FullText = Phrase(localize, "LOCLS_Items_Full", "The header row has no room for another card (12 boxes of 4 cards at most).") };
            var widgetsZone = new ItemZoneViewModel { Id = ItemZones.Widgets, Title = Phrase(localize, "LOCLS_Zone_Widgets", "Buttons and widgets of the header"), Regions = ItemZones.RegionsOf(ItemZones.Widgets),
                Hint = Phrase(localize, "LOCLS_Zone_WidgetsHint", "Players online, music player, shortcuts, buttons and your rating stars. Their place is fixed by the theme.") };
            var panelZone = new ItemZoneViewModel { Id = ItemZones.Panel, Title = Phrase(localize, "LOCLS_Zone_Panel", "Information panel"), Regions = ItemZones.RegionsOf(ItemZones.Panel),
                Hint = Phrase(localize, "LOCLS_Zone_PanelHint", "The rows of the panel that opens over the page. Their order is fixed by the theme."), Look = lookOf(ItemZones.Panel) };
            sectionsZone = new ItemZoneViewModel { Id = ItemZones.Sections, Title = Phrase(localize, "LOCLS_Zone_Sections", "Sections"), Regions = ItemZones.RegionsOf(ItemZones.Sections),
                Hint = Phrase(localize, "LOCLS_Zone_SectionsHint", "Shown, and open or folded by default. The order and the column are set on the Sections page."), Look = lookOf(ItemZones.Sections),
                LinkText = Phrase(localize, "LOCLS_Items_OrderSections", "Order the sections") };
            var elsewhereZone = new ItemZoneViewModel { Id = ItemZones.Elsewhere, Title = Phrase(localize, "LOCLS_Zone_Elsewhere", "Grid view, game list and covers"), Regions = ItemZones.RegionsOf(ItemZones.Elsewhere),
                Hint = Phrase(localize, "LOCLS_Zone_ElsewhereHint", "Where an item also shows outside the details page.") };
            Zones = new List<ItemZoneViewModel> { chipsZone, cardsZone, widgetsZone, panelZone, sectionsZone, elsewhereZone };
            foreach (var zone in Zones) zone.Reorder = (key, at) => ReorderRow(zone, key, at);

            BuildChipRows();
            BuildSimpleRows(widgetsZone);
            BuildSimpleRows(panelZone);
            BuildSimpleRows(elsewhereZone);
            BuildSectionRows();
            if (tiles.Count > 0) RebuildCardRows();

            foreach (var zone in Zones) { zone.Renumber(); zone.Refresh(); }
            store.ValueChanged += OnValueChanged;
            history.Changed += () => { RefreshAll(); };
        }

        private string Word(string key, string english) { return Phrase(localize, key, english); }

        private static string Phrase(Func<string, string> localize, string key, string english)
        {
            string text = null;
            try { text = localize(key); } catch (Exception) { }
            return string.IsNullOrEmpty(text) ? english : text;
        }

        public IList<ItemCardViewModel> Cards { get; private set; }

        /// <summary>The places of the game page, top to bottom, each a flat list of rows.</summary>
        public IList<ItemZoneViewModel> Zones { get; private set; }

        /// <summary>Opens the Sections page (set by the shell).</summary>
        public Action OpenSections { set { sectionsZone.LinkCommand = value == null ? null : new RelayCommand(value); } }

        /// <summary>The "Information shown once" part: items on several surfaces and the buttons that hide the extra copies.</summary>
        public InfoOnceViewModel Once { get; private set; }

        // ---- building the rows -----------------------------------------------------------------------------------------------------

        private ItemCardViewModel CardOf(string itemId) { ItemCardViewModel c; return cardById.TryGetValue(itemId, out c) ? c : null; }

        private SettingViewModel S(string key) { SettingViewModel s; return key != null && byKey.TryGetValue(key, out s) ? s : null; }

        private string TitleOf(ItemPlaces item)
        {
            var card = CardOf(item.Id);
            if (card != null) return card.Title;
            string text = null;
            try { text = localize(ItemFamilies.LocKey(item.Title)); } catch (Exception) { }
            return string.IsNullOrEmpty(text) ? item.Title : text;
        }

        /// <summary>The options of an item that belong to one place of the page, except the ones named.</summary>
        private IList<SettingViewModel> MoreOf(ItemPlaces item, string zone, params string[] except)
        {
            var card = CardOf(item.Id);
            if (card == null) return new List<SettingViewModel>();
            return card.Settings.Where(s => s.Keys.Count == 1 && ItemZones.ZoneOf(s.Keys[0]) == zone && !except.Contains(s.Keys[0])).ToList();
        }

        private ItemRowViewModel NewRow(string zone, ItemPlaces item, string rowKey)
        {
            var row = new ItemRowViewModel
            {
                Zone = zone, RowKey = rowKey, ItemId = item == null ? null : item.Id, Title = item == null ? null : TitleOf(item),
                Regions = ItemZones.RegionsOf(zone), More = new List<SettingViewModel>(),
                BoxFormat = Word("LOCLS_Items_Box", "Box {0}")
            };
            row.Move = (r, delta) => MoveRow(r, delta);
            row.OnDragEnded = () => { var z = Zones == null ? null : Zones.FirstOrDefault(x => x.Id == zone); if (z != null) z.ClearDropTarget(); };
            row.Remove = RemoveCardRow;
            row.SetStartsBox = SetCardBox;
            if (item != null) { row.Also = () => AlsoText(item, zone); row.SearchText = item.Id + " " + item.Title + " " + item.ChipOrderKey; }
            allRows.Add(row);
            return row;
        }

        private void Register(ItemRowViewModel row)
        {
            foreach (var key in row.Keys)
            {
                List<ItemRowViewModel> list;
                if (!rowsByKey.TryGetValue(key, out list)) rowsByKey[key] = list = new List<ItemRowViewModel>();
                list.Add(row);
            }
        }

        private void AttachDuplicates(ItemRowViewModel row, ItemPlaces item)
        {
            var card = CardOf(item.Id);
            if (card == null || card.Reader == null) return;
            row.ShowsTwice = () => card.ShowsTwice;
            row.TwiceText = card.TwiceText;
            row.FoldText = card.FoldText;
            row.FoldCommand = new RelayCommand(() => card.FoldSection());
        }

        private void BuildChipRows()
        {
            foreach (var item in catalog.Where(i => i.InChips))
            {
                var row = NewRow(ItemZones.Chips, item, item.Id);
                row.CanReorder = true;
                row.Show = S(item.ChipShowKey);
                row.Description = chipsZone.Title;
                string orderKey = item.ChipOrderKey;
                row.ExtraModified = () => store.Get(orderKey) != store.GetDefault(orderKey);
                // one "new line" switch sits on the row; HowLongToBeat has three (one per time) and keeps them behind "more"
                if (item.ChipBreakKeys.Count == 1)
                {
                    row.Flag = S(item.ChipBreakKeys[0]);
                    if (row.Flag != null) row.Flag.Title = Word("LOCLS_Item_Startanewlinebeforeit", "Start a new line before it");
                }
                string breakKey = item.ChipBreakKeys.Count == 1 ? item.ChipBreakKeys[0] : null;
                row.More = MoreOf(item, ItemZones.Chips, item.ChipShowKey, breakKey, item.ChipOrderKey);
                Register(row);
                chipsZone.Rows.Add(row);
            }
            SyncChipOrder();
        }

        /// <summary>Widgets, panel rows and the items shown outside the details page: one row per item that has a switch in that place.</summary>
        private void BuildSimpleRows(ItemZoneViewModel zone)
        {
            foreach (var item in catalog)
            {
                var card = CardOf(item.Id);
                if (card == null) continue;
                var mine = card.Settings.Where(s => s.Keys.Count == 1 && ItemZones.ZoneOf(s.Keys[0]) == zone.Id).ToList();
                if (mine.Count == 0) continue;
                var row = NewRow(zone.Id, item, item.Id);
                row.Description = zone.Title;
                var main = MainSwitch(zone.Id, mine);
                row.Show = main;
                if (zone.Id == ItemZones.Elsewhere && main != null) row.ShowLabel = Word("LOCLS_Item_" + System.Text.RegularExpressions.Regex.Replace(ItemFamilies.SurfaceOf(main.Keys[0]), "[^A-Za-z]", ""), ItemFamilies.SurfaceOf(main.Keys[0]));
                else if (zone.Id == ItemZones.Widgets && main != null && main.Title.IndexOf(row.Title, StringComparison.OrdinalIgnoreCase) < 0) row.ShowLabel = main.Title;
                row.More = mine.Where(s => !ReferenceEquals(s, main)).ToList();
                Register(row);
                zone.Rows.Add(row);
            }
        }

        /// <summary>The switch that stands for the item in a place: the first toggle, preferring the one that shows it.</summary>
        private static SettingViewModel MainSwitch(string zone, IList<SettingViewModel> settings)
        {
            var toggles = settings.Where(s => s.Kind == "toggle").ToList();
            if (toggles.Count == 0) return null;
            Func<SettingViewModel, int> rank = s =>
            {
                string k = s.Keys[0];
                if (k.EndsWith("ShowOnCover", StringComparison.Ordinal)) return 0;
                if (k.StartsWith("LibraryListShow", StringComparison.Ordinal)) return 1;
                if (k.StartsWith("GridCoverShow", StringComparison.Ordinal)) return 2;
                if (k.StartsWith("GridViewShow", StringComparison.Ordinal) && k.EndsWith("Panel", StringComparison.Ordinal)) return 3;
                if (k.StartsWith("InfoPanelShow", StringComparison.Ordinal) || k.StartsWith("HeroShow", StringComparison.Ordinal) || k.Contains("ShowEditButton")
                    || k.Contains("ShowFavoriteButton") || k.Contains("ShowHiddenButton") || k.Contains("ShowInfoButton") || k.Contains("ShowUserRating") || k.Contains("AllowUseOfLogos")) return 0;
                return 9;
            };
            return toggles.OrderBy(rank).First();
        }

        private void BuildSectionRows()
        {
            foreach (var item in catalog.Where(i => i.InSection))
            {
                var def = item.Section;
                var row = NewRow(ItemZones.Sections, item, item.Id);
                row.Description = sectionsZone.Title;
                row.Show = S(def.ShowKey);
                row.Flag = S(def.ExpandKey);
                if (row.Flag != null) row.Flag.Title = Word("LOCLS_Item_Sectionopenbydefault", "Section open by default");
                row.More = MoreOf(item, ItemZones.Sections, def.ShowKey, def.ExpandKey);
                AttachDuplicates(row, item);
                Register(row);
                sectionsZone.Rows.Add(row);
            }
            SyncSectionOrder();
        }

        // ---- the places that follow the profile ------------------------------------------------------------------------------------

        private void SyncChipOrder()
        {
            var order = ChipLayout.Order(store, schema);
            for (int i = 0; i < order.Count; i++)
            {
                var row = chipsZone.Rows.FirstOrDefault(r => r.RowKey == order[i]);
                if (row == null) continue;
                int at = chipsZone.Rows.IndexOf(row);
                if (at != i) chipsZone.Rows.Move(at, i);
            }
            chipsZone.Renumber();
        }

        private void SyncSectionOrder()
        {
            var slots = WireframeModel.FromStore(store, schema);
            var wanted = new List<ItemRowViewModel>();
            foreach (var slot in slots)
            {
                var row = sectionsZone.Rows.FirstOrDefault(r => SectionIdOf(r) == slot.Id);
                if (row == null) continue;
                row.Description = Word(slot.Column == 0 ? "LOCLS_LeftColumn" : "LOCLS_RightColumn", slot.Column == 0 ? "Left column" : "Right column");
                row.Refresh();
                wanted.Add(row);
            }
            for (int i = 0; i < wanted.Count; i++)
            {
                int at = sectionsZone.Rows.IndexOf(wanted[i]);
                if (at >= 0 && at != i) sectionsZone.Rows.Move(at, i);
            }
            sectionsZone.Renumber();
        }

        private string SectionIdOf(ItemRowViewModel row)
        {
            var item = catalog.FirstOrDefault(i => i.Id == row.ItemId);
            return item == null || item.Section == null ? null : item.Section.Id;
        }

        // ---- the header cards ------------------------------------------------------------------------------------------------------

        private List<CardEntry> ReadCards() { return CardLayout.Read(store.Get, slotKeys, extraKeys); }

        private TileInfo TileOf(int value) { return tiles.FirstOrDefault(t => t.Value == value); }

        private void RebuildCardRows()
        {
            var entries = ReadCards();
            var used = new Dictionary<int, int>();
            var wanted = new List<ItemRowViewModel>();
            int box = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                var tile = TileOf(entries[i].Tile);
                if (tile == null) continue;   // a value the schema does not know: the theme shows nothing for it, the row would be a blank
                int nth;
                used.TryGetValue(tile.Value, out nth);
                used[tile.Value] = nth + 1;
                List<ItemRowViewModel> pool;
                if (!cardPool.TryGetValue(tile.Value, out pool)) cardPool[tile.Value] = pool = new List<ItemRowViewModel>();
                ItemRowViewModel row;
                if (nth < pool.Count) row = pool[nth];
                else
                {
                    var item = catalog.FirstOrDefault(c => c.Id == tile.ItemId);
                    row = NewRow(ItemZones.Cards, item, "0");
                    row.Title = tile.Name;
                    row.SearchText = (row.SearchText ?? "") + " " + tile.Id;
                    row.IsCard = true;
                    row.CanReorder = true;
                    row.Description = cardsZone.Title;
                    pool.Add(row);
                }
                if (entries[i].NewBox) box++;
                row.RowKey = wanted.Count.ToString(CultureInfo.InvariantCulture);
                row.SetBox(entries[i].NewBox, box);
                wanted.Add(row);
            }
            // release the rows of cards that are gone
            foreach (var pair in cardPool.ToList())
            {
                int keep;
                used.TryGetValue(pair.Key, out keep);
                while (pair.Value.Count > keep) { allRows.Remove(pair.Value[pair.Value.Count - 1]); pair.Value.RemoveAt(pair.Value.Count - 1); }
            }
            for (int i = cardsZone.Rows.Count - 1; i >= 0; i--)
                if (!wanted.Contains(cardsZone.Rows[i])) cardsZone.Rows.RemoveAt(i);
            for (int i = 0; i < wanted.Count; i++)
            {
                int at = cardsZone.Rows.IndexOf(wanted[i]);
                if (at < 0) cardsZone.Rows.Insert(i, wanted[i]);
                else if (at != i) cardsZone.Rows.Move(at, i);
            }
            cardsZone.Renumber();
            ApplyFilterToRows(cardsZone);

            var placed = new HashSet<int>(entries.Select(e => e.Tile));
            cardsZone.Available.Clear();
            foreach (var tile in tiles.Where(t => !placed.Contains(t.Value)))
            {
                var captured = tile;
                var item = catalog.FirstOrDefault(c => c.Id == tile.ItemId);
                cardsZone.Available.Add(new CardChoiceViewModel { Id = tile.Id, Name = tile.Name, ItemTitle = item == null ? tile.Name : TitleOf(item), AddCommand = new RelayCommand(() => AddCard(captured.Id)) });
            }
            var probe = entries.Select(e => new CardEntry { Tile = e.Tile, NewBox = e.NewBox }).ToList();
            probe.Add(new CardEntry { Tile = 1, NewBox = true });
            bool full = CardLayout.Pack(probe, slotKeys, extraKeys) == null;
            probe[probe.Count - 1].NewBox = false;
            cardsZone.IsFull = full && CardLayout.Pack(probe, slotKeys, extraKeys) == null;
            cardsZone.IsModified = cardKeys.Any(k => store.Get(k) != store.GetDefault(k));
            cardsZone.Refresh();
        }

        /// <summary>Writes a list of cards into the constants (one undo step). False when the theme has no room for it.</summary>
        private bool ApplyCards(IList<CardEntry> list)
        {
            var packed = CardLayout.Pack(list, slotKeys, extraKeys);
            if (packed == null) return false;
            history.BeginBatch();
            try
            {
                foreach (var kv in packed)
                {
                    string error;
                    if (store.Get(kv.Key) != kv.Value) store.TrySet(kv.Key, kv.Value, out error);
                }
            }
            finally { history.EndBatch(); }
            return true;
        }

        private void AddCard(string tileId)
        {
            var tile = tiles.FirstOrDefault(t => t.Id == tileId);
            if (tile == null) return;
            var list = ReadCards();
            list.Add(new CardEntry { Tile = tile.Value, NewBox = true });
            if (!ApplyCards(list))
            {
                list[list.Count - 1].NewBox = false;   // no free box: join the last one if it has room
                ApplyCards(list);
            }
            RebuildCardRows();
        }

        private void RemoveCardRow(ItemRowViewModel row)
        {
            int index;
            if (!row.IsCard || !int.TryParse(row.RowKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out index)) return;
            ApplyCards(CardLayout.Remove(ReadCards(), index));
            RebuildCardRows();
        }

        private void SetCardBox(ItemRowViewModel row, bool starts)
        {
            int index;
            if (!row.IsCard || !int.TryParse(row.RowKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out index)) return;
            var list = ReadCards();
            if (index < 0 || index >= list.Count) return;
            list[index].NewBox = starts || index == 0;
            ApplyCards(list);
            RebuildCardRows();
        }

        // ---- order -----------------------------------------------------------------------------------------------------------------

        private void MoveRow(ItemRowViewModel row, int delta)
        {
            var zone = Zones.First(z => z.Id == row.Zone);
            int at = zone.Rows.IndexOf(row);
            ReorderRow(zone, row.RowKey, Math.Max(0, Math.Min(zone.Rows.Count - 1, at + delta)));
        }

        /// <summary>Puts a row of a place at a position of its list (one undo step). The chip line and the card row are the places that have an order.</summary>
        private bool ReorderRow(ItemZoneViewModel zone, string rowKey, int position)
        {
            bool changed = false;
            history.BeginBatch();
            try
            {
                if (zone.Id == ItemZones.Chips) changed = ChipLayout.Move(store, schema, rowKey, position);
                else if (zone.Id == ItemZones.Cards)
                {
                    int from;
                    if (int.TryParse(rowKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out from))
                    {
                        var before = ReadCards();
                        var after = CardLayout.Move(before, from, position);
                        changed = !before.Select(c => c.Tile).SequenceEqual(after.Select(c => c.Tile)) && ApplyCards(after);
                    }
                }
            }
            finally { history.EndBatch(); }
            if (zone.Id == ItemZones.Cards) RebuildCardRows();
            return changed;
        }

        // ---- the text under a row --------------------------------------------------------------------------------------------------

        /// <summary>"Also shown in: header card, section": the other places that show the item now.</summary>
        private string AlsoText(ItemPlaces item, string except)
        {
            var places = new List<string>();
            bool lineOn = store.Find("HeroShowInfoLine") == null || store.Get("HeroShowInfoLine") == "True";
            if (except != ItemZones.Chips && item.InChips && lineOn && store.Get(item.ChipShowKey) == "True") places.Add(Word("LOCLS_Surface_Chips", "Artwork chips"));
            if (except != ItemZones.Cards && item.InCards && tiles.Any(t => t.ItemId == item.Id && cardKeysHold(t.Value))) places.Add(Word("LOCLS_Surface_Tile", "Header tile"));
            if (except != ItemZones.Panel && item.InPanel && item.PanelKeys.Any(k => store.Get(k) == "True")) places.Add(Word("LOCLS_Surface_Panel", "Information panel"));
            if (except != ItemZones.Sections && item.InSection && (item.Section.ShowKey == null || store.Get(item.Section.ShowKey) == "True")) places.Add(Word("LOCLS_Surface_Section", "Its own section"));
            if (places.Count == 0) return null;
            return string.Format(CultureInfo.InvariantCulture, Word("LOCLS_Items_Also", "Also shown in: {0}"), string.Join(", ", places));
        }

        private bool cardKeysHold(int value)
        {
            foreach (var key in cardKeys) if (store.Get(key) == value.ToString(CultureInfo.InvariantCulture)) return true;
            return false;
        }

        // ---- following the profile -------------------------------------------------------------------------------------------------

        private bool syncing;

        private void OnValueChanged(string key)
        {
            foreach (var c in Cards) c.RefreshCounts();
            Raise("ChangedCount");
            List<ItemRowViewModel> rows;
            if (rowsByKey.TryGetValue(key, out rows)) foreach (var r in rows) r.Refresh();
            if (syncing) return;
            syncing = true;
            try
            {
                if (chipOrderKeys.Contains(key)) { SyncChipOrder(); foreach (var r in chipsZone.Rows) r.Refresh(); }
                if (sectionKeys.Contains(key)) SyncSectionOrder();
                if (cardKeys.Contains(key) && tiles.Count > 0) RebuildCardRows();
            }
            finally { syncing = false; }
        }

        private void RefreshAll()
        {
            foreach (var c in Cards) c.RefreshCounts();
            Raise("ChangedCount");
            foreach (var r in allRows) r.Refresh();
            foreach (var z in Zones) z.Refresh();
        }

        // ---- search, focus, reset --------------------------------------------------------------------------------------------------

        /// <summary>The item shown alone (opened from a section of the Sections page), or null for all of them.</summary>
        public string FocusedId { get { return focusedId; } }
        public bool IsFocused { get { return focusedId != null; } }
        public string FocusedTitle { get { var c = Cards.FirstOrDefault(x => x.Id == focusedId); return c == null ? "" : c.Title; } }
        public RelayCommand ShowAllCommand { get { return new RelayCommand(() => ShowOnly(null)); } }

        /// <summary>Shows one item alone, with its options open (null: all items again).</summary>
        public void ShowOnly(string id)
        {
            focusedId = string.IsNullOrEmpty(id) || !allRows.Any(r => r.ItemId == id) ? null : id;
            Filter(lastQuery, lastChangedOnly);
            if (focusedId != null) foreach (var r in allRows.Where(r => r.ItemId == focusedId && r.HasMore)) r.IsMoreOpen = true;
            Raise("FocusedId"); Raise("IsFocused"); Raise("FocusedTitle");
        }

        /// <summary>Every option of every item, for searching and counting.</summary>
        public IEnumerable<SettingViewModel> AllSettings { get { return Cards.SelectMany(c => c.Settings); } }

        /// <summary>The settings that belong to a whole place (the chips line, the header bar...).</summary>
        public IEnumerable<SettingViewModel> PlaceSettings { get { return zoneSettings; } }

        private bool OrderChanged { get { return chipOrderKeys.Any(k => store.Get(k) != store.GetDefault(k)); } }
        private bool CardsChanged { get { return cardKeys.Any(k => store.Get(k) != store.GetDefault(k)); } }

        public int ChangedCount
        {
            get
            {
                // each setting counts once; the order of the chips and the row of header cards count once each
                return AllSettings.Count(s => s.IsModified) + zoneSettings.Count(s => s.IsModified) + (OrderChanged ? 1 : 0) + (CardsChanged ? 1 : 0);
            }
        }

        public void ResetAll()
        {
            history.BeginBatch();
            try
            {
                foreach (var s in AllSettings.Where(s => s.IsModified).ToList()) s.Reset();
                foreach (var s in zoneSettings.Where(s => s.IsModified).ToList()) s.Reset();
                foreach (var key in chipOrderKeys.Concat(cardKeys)) store.Reset(key);
            }
            finally { history.EndBatch(); }
        }

        private bool Searching { get { return !string.IsNullOrWhiteSpace(lastQuery); } }

        /// <summary>
        /// Shows only the rows that match the search (and, with <paramref name="changedOnly"/>, the changed ones). A row matches by the name of the item or by
        /// one of its options; its "more" list opens when only an option matched. Returns how many rows match. An empty search shows everything.
        /// </summary>
        public int Filter(string query, bool changedOnly)
        {
            lastQuery = query ?? "";
            lastChangedOnly = changedOnly;
            int matches = 0;
            foreach (var zone in Zones)
            {
                ApplyFilterToRows(zone);
                matches += zone.Rows.Count(r => r.Visible);
                // the settings of a whole place count when they match the search
                int hits = 0;
                if (zone.Look != null)
                    foreach (var s in zone.Look)
                    {
                        s.Visible = (!Searching || s.Matches(query)) && (!changedOnly || s.IsModified);
                        if (s.Visible && (Searching || changedOnly)) hits++;
                    }
                if (Searching && zone.Master != null && zone.Master.Matches(query)) hits++;   // the switch of the whole place (the chips line, the header bar) is found too
                if (hits > 0 && Searching) zone.IsLookOpen = true;
                matches += Searching ? hits : 0;
                zone.LookMatched = hits > 0;
                zone.ShowTools = !Searching && !changedOnly && focusedId == null;
                zone.Refresh();
            }
            foreach (var c in Cards)
            {
                bool inFocus = focusedId == null || c.Id == focusedId;
                c.Visible = inFocus && allRows.Any(r => r.ItemId == c.Id && r.Visible);
            }
            return matches;
        }

        private void ApplyFilterToRows(ItemZoneViewModel zone)
        {
            string q = (lastQuery ?? "").Trim();
            bool searching = q.Length > 0;
            foreach (var row in zone.Rows)
            {
                bool inFocus = focusedId == null || row.ItemId == focusedId;
                bool titleHit = searching && ((row.Title ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || (row.SearchText ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || (row.Show != null && row.Show.Matches(q)) || (row.Flag != null && row.Flag.Matches(q)));
                int moreHits = 0;
                if (row.More != null)
                    foreach (var s in row.More)
                    {
                        s.Visible = (!searching || titleHit || s.Matches(q)) && (!lastChangedOnly || s.IsModified);
                        if (s.Visible && (!searching || s.Matches(q))) moreHits++;
                    }
                bool hit = !searching || titleHit || moreHits > 0;
                row.Visible = inFocus && hit && (!lastChangedOnly || row.IsModified);
                if (searching && row.Visible && !titleHit && moreHits > 0) row.IsMoreOpen = true;
            }
        }
    }
}
