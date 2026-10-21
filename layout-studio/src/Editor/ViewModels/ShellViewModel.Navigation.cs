using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    /// <summary>
    /// The ten pages of the editor (pages made of plain headed blocks), built from the schema when it carries a layout (ui-layout.yaml):
    /// every block gets the settings the layout homes there, its specialised editor if it has one, its see-also links, and counts and resets of its own.
    /// </summary>
    public partial class ShellViewModel
    {
        private Schema schemaRef;
        private ItemsEditorViewModel itemsEditor;
        private DiagnosticsViewModel diagnosticsEditor;
        private IList<InstalledAddon> installedList;
        private readonly Dictionary<GroupViewModel, IList<string>> blockOwnedKeys = new Dictionary<GroupViewModel, IList<string>>();

        /// <summary>The language-file key of a block title: LOCLS_Block_&lt;id without hyphens&gt;.</summary>
        public static string BlockKey(string blockId)
        {
            return "LOCLS_Block_" + (blockId ?? "").Replace("-", "");
        }

        private string Loc(string key, string english)
        {
            return Word(key, english);
        }

        private void BuildComposite(Schema schema, IProfileSharing sharing, Action<string> copyToClipboard, FactsViewModel facts,
            Func<IEnumerable<KeyValuePair<string, string>>> environment, Func<LiveContext> liveContext, Func<string> previewImage,
            Func<IEnumerable<InstalledAddon>> installedAddons)
        {
            schemaRef = schema;
            var localize = localizeText;
            var controls = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).ToList();
            keyTitles = KeyTitles(schema, controls, localize);

            // ---- the specialised editors, one each, reused as they are ----------------------------------------------------------------
            var editors = new Dictionary<string, object>();
            SectionsEditorViewModel sectionsVm = schema.Sections.Count > 0 ? new SectionsEditorViewModel(store, schema, history) : null;
            if (sectionsVm != null) editors["sections"] = sectionsVm;
            if (controls.Any(c => ItemFamilies.IsItemControl(c) && schema.ItemsMayEdit(c.Keys[0])))
            {
                itemsEditor = new ItemsEditorViewModel(schema, store, history, applier, profile, localize, culture);
                editors["items"] = itemsEditor;
                itemsEditor.OpenSections = () => OpenPage("sections");
                if (sectionsVm != null) sectionsVm.OpenItemOptions = id => OpenItem(id);
            }
            if (schema.Sizes.Count > 0)
            {
                sizes = new SizesEditorViewModel(store, schema, history, culture, profile.Editor) { Localize = localize };
                this.liveContext = liveContext;
                RefreshLive();
                editors["sizes"] = sizes;
            }
            if (schema.Tuning.Count > 0) editors["background"] = new BackgroundEditorViewModel(store, schema, history, applier, profile, localize, culture, previewImage);
            if (schema.Find("BackdropMode") != null) editors["backdrop"] = new BackdropEditorViewModel(store, history, profile, localize, previewImage);
            if (controls.Any(c => c.Kind == "tileSlots")) editors["tiles"] = new TilesEditorViewModel(store, schema, history);   // its other header options are the block's own settings now
            if (controls.Any(c => c.Id == "ColorProfile") && controls.Any(c => c.Id == "Accent") && schema.Find("GlyphColor") != null)
                editors["colors"] = new ColorsEditorViewModel(store, schema, history, applier, profile);
            if (schema.Corners.Count > 0) editors["corners"] = new CornersEditorViewModel(store, schema, history);
            if (facts != null) editors["rules"] = new RulesEditorViewModel(facts, profile.Editor, history);
            if (PlayniteSettings != null) editors["advice"] = new AdviceViewModel(PlayniteSettings, store, history, profile, OpenPage, PlayniteSettings.Ui);
            diagnosticsEditor = new DiagnosticsViewModel(() => CollectDiagnostics(schema, environment), copyToClipboard);
            editors["diagnostics"] = diagnosticsEditor;
            if (sharing != null) editors["share"] = new SharingViewModel(sharing, copyToClipboard);
            var globalPresets = sharing as IGlobalPresetSwitching;
            if (globalPresets != null && globalPresets.GlobalPresetList.Count > 0) editors["globalpresets"] = new GlobalPresetsViewModel(globalPresets, culture.TwoLetterISOLanguageName, store);
            installedList = installedAddons == null ? null : installedAddons().ToList();

            // ---- pages and blocks ---------------------------------------------------------------------------------------------------------
            var homesByBlock = schema.Homes.GroupBy(h => h.Value).ToDictionary(g => g.Key, g => g.Select(h => h.Key).ToList());
            var pages = new List<PageViewModel>();
            foreach (var page in schema.Pages)
            {
                var blocks = new List<GroupViewModel>();
                foreach (var g in page.Groups)
                {
                    string where = page.Id + "/" + g.Id;
                    object editor = null;
                    bool needsEditor = g.Editor != null;
                    if (needsEditor) editors.TryGetValue(g.Editor, out editor);
                    var generated = g.Controls.Where(c => !c.Owned).ToList();
                    if (needsEditor && editor == null && generated.Count == 0) continue;   // e.g. Thresholds without facts, Sharing without a profile store
                    Func<Control, SettingViewModel> make = c => new SettingViewModel(c, store, history, applier, profile, localize, culture);
                    var main = generated.Where(c => !c.More).Select(make).ToList();
                    var extra = generated.Where(c => c.More).Select(make).ToList();
                    var block = new GroupViewModel(g.Id, Loc(BlockKey(g.Id), g.Title), main, extra)
                    {
                        Editor = editor, EditorKind = editor == null ? null : g.Editor, Regions = g.Regions.ToArray(),
                        MoreText = Loc("LOCLS_More", "More"), LessText = Loc("LOCLS_Less", "Less"),
                        ResetText = Loc("LOCLS_ResetBlock", "Reset"), ResetConfirmText = Loc("LOCLS_ResetBlock_Confirm", "Click again to reset {0}")
                    };
                    foreach (var s in block.Settings) s.KeyTitle = key => { string t; return keyTitles.TryGetValue(key, out t) ? t : null; };
                    if (page.Id == "addons") SayWhereOfTwins(block);
                    // what the block's editor (or its presets) owns, to count and reset it with the block
                    var generatedKeys = new HashSet<string>(generated.SelectMany(c => c.Keys.Concat(c.ExtraKeys)));
                    var homeKeys = homesByBlock.ContainsKey(where) ? homesByBlock[where] : new List<string>();
                    var ownedKeys = homeKeys.Where(k => !generatedKeys.Contains(k)).ToList();
                    blockOwnedKeys[block] = ownedKeys;
                    var counted = EditorKeysOf(schema, g.Editor);
                    block.CountedKeys = ownedKeys.Where(counted.Contains).ToList();
                    block.OwnedControls = g.Controls.Where(c => c.Owned && c.Kind != "preset" && !c.Keys.Any(counted.Contains)).Select(c => (IList<string>)c.Keys.Concat(c.ExtraKeys).ToList()).ToList();
                    block.OwnedPresets = g.Controls.Where(c => c.Owned && c.Kind == "preset").Select(c => c.Id).ToList();
                    block.HasOwned = ownedKeys.Count > 0;
                    block.ResetHandler = () => AskOrResetBlock(block);
                    block.NavigateHandler = b => { OpenPage(where); };
                    foreach (var link in g.SeeAlso) block.SeeAlso.Add(MakeLink(schema, link));
                    if (g.Addon != null) block.Addon = MakeAddon(schema, g, itemsEditor != null);
                    block.Keywords = BlockKeywords(g, editor, ownedKeys, block);
                    blocks.Add(block);
                }
                if (blocks.Count == 0) continue;
                int optionCount = schema.Homes.Count(h => h.Value.StartsWith(page.Id + "/", StringComparison.Ordinal));
                var vm = new PageViewModel(page.Id, Loc("LOCLS_Nav_" + page.Id, page.Title), Loc("LOCLS_NavSummary_" + page.Id, page.Summary), page.Regions.ToArray(), blocks) { OptionCount = optionCount };
                vm.FilterChanged = ApplyFilter;
                pages.Add(vm);
            }
            Pages = pages;
            foreach (var page in Pages) page.Keywords = (page.Title + " " + page.Summary);
            selectedPage = Pages.FirstOrDefault(p => p.Id != "presets") ?? Pages.FirstOrDefault();   // the presets page is the first of the list but not the one to open on
            if (selectedPage != null) selectedPage.IsCurrent = true;
            PagesView = System.Windows.Data.CollectionViewSource.GetDefaultView(Pages);
        }

        /// <summary>Two options with the same words (the achievements progress bar of the game page and of the grid view) say which one is which.</summary>
        private void SayWhereOfTwins(GroupViewModel block)
        {
            foreach (var twins in block.Settings.GroupBy(s => s.Title).Where(g => g.Count() > 1))
                foreach (var s in twins)
                {
                    string key = s.Keys.Count > 0 ? s.Keys[0] : "";
                    string surface = key.StartsWith("GridView", StringComparison.Ordinal) || key.StartsWith("GridCover", StringComparison.Ordinal) ? "Grid view" : "Game page";
                    s.Title = Loc(ItemFamilies.LocKey(surface), surface) + ": " + s.Title;
                }
        }

        /// <summary>The constants an editor edits one by one, so a change to any of them counts on the block.</summary>
        private static HashSet<string> EditorKeysOf(Schema schema, string editor)
        {
            var keys = new HashSet<string>();
            switch (editor)
            {
                case "corners": foreach (var c in schema.Corners) keys.Add(c.Key); break;
                case "sizes": foreach (var z in schema.Sizes) { if (z.WidthKey != null) keys.Add(z.WidthKey); if (z.HeightKey != null) keys.Add(z.HeightKey); } break;
                case "background": foreach (var t in schema.Tuning) keys.Add(t.Key); break;
                case "backdrop": keys.Add("BackdropMode"); keys.Add("BackdropSeed"); break;
                case "colors": keys.Add("GlyphColor"); break;   // the custom accent: one unit for the three constants it writes
            }
            return keys;
        }

        private SeeAlsoViewModel MakeLink(Schema schema, SeeAlso link)
        {
            var page = schema.Pages.FirstOrDefault(p => p.Id == link.Page);
            var group = page == null ? null : page.Groups.FirstOrDefault(g => g.Id == link.Block);
            string target = page == null ? link.Page : Loc("LOCLS_Nav_" + page.Id, page.Title);
            if (group != null && page.Groups.Count > 1) target += " > " + Loc(BlockKey(group.Id), group.Title);
            string where = link.Page + "/" + link.Block;
            return new SeeAlsoViewModel { Text = Loc(SeeAlsoKey(link), link.Text), Target = target, OpenCommand = new RelayCommand(() => OpenPage(where)) };
        }

        /// <summary>The language-file key of a see-also text: LOCLS_See_&lt;page&gt;_&lt;block&gt;_&lt;letters of the English text&gt;, limited to 40 letters.</summary>
        public static string SeeAlsoKey(SeeAlso link)
        {
            var letters = new string((link.Text ?? "").Where(char.IsLetter).ToArray());
            return "LOCLS_See_" + link.Page + "_" + link.Block.Replace("-", "") + "_" + (letters.Length > 40 ? letters.Substring(0, 40) : letters);
        }

        private AddonViewModel MakeAddon(Schema schema, Group g, bool hasItems)
        {
            string matched;
            var state = AddonInventory.Detect(g.Addon, installedList, out matched);
            string status;
            switch (state)
            {
                case AddonState.Installed: status = Loc("LOCLS_Addon_Installed", "Installed"); break;
                case AddonState.Disabled: status = Loc("LOCLS_Addon_Disabled", "Installed, switched off in Playnite"); break;
                case AddonState.Missing: status = Loc("LOCLS_Addon_Missing", "Not installed"); break;
                default: status = Loc("LOCLS_Addon_Unknown", "Cannot tell from here"); break;
            }
            var items = new List<ProvidedItemViewModel>();
            var catalog = hasItems ? ItemCatalog.Build(schema) : new List<ItemPlaces>();
            foreach (var id in g.Addon.Items)
            {
                var family = ItemFamilies.All.FirstOrDefault(f => f.Id == id);
                if (family == null) continue;
                var place = catalog.FirstOrDefault(c => c.Id == id);
                var where = new List<string>();
                if (place != null)
                {
                    if (place.InChips) where.Add(Loc("LOCLS_Where_Chips", "chip"));
                    if (place.InCards) where.Add(Loc("LOCLS_Where_Cards", "header card"));
                    if (place.InPanel) where.Add(Loc("LOCLS_Where_Panel", "information panel"));
                    if (place.InSection) where.Add(Loc("LOCLS_Where_Section", "section"));
                    if (place.WidgetShowKey != null) where.Add(Loc("LOCLS_Where_Widget", "banner widget"));
                    if (place.Keys.Any(k => ItemZones.ZoneOf(k) == ItemZones.Elsewhere)) where.Add(Loc("LOCLS_Where_Elsewhere", "grid view or game list"));
                }
                string itemId = id;
                items.Add(new ProvidedItemViewModel
                {
                    Id = id, Title = Loc(ItemFamilies.LocKey(family.Title), family.Title), Where = string.Join(", ", where),
                    OpenCommand = hasItems ? new RelayCommand(() => OpenItem(itemId)) : null
                });
            }
            return new AddonViewModel
            {
                Id = g.Addon.Id, Title = Loc(BlockKey(g.Id), g.Title), State = state, InstalledId = matched, StatusText = status,
                Provides = Loc("LOCLS_AddonProvides_" + g.Addon.Id, g.Addon.Provides), Items = items,
                ItemsHeading = Loc("LOCLS_Addon_Provides", "Shows up in the theme as")
            };
        }

        /// <summary>Words a search can find in a block with an editor or an add-on card: its title, its editor's words, the titles of the options it owns.</summary>
        private string BlockKeywords(Group g, object editor, IList<string> ownedKeys, GroupViewModel block)
        {
            var words = new List<string> { g.Title, block.Title };
            if (g.Editor != null) words.Add(KeywordsOf(EditorPageKind(g.Editor)));
            // what the editor draws is not a list of settings: its boxes, corners, sections and sliders are found by the names the user knows them by
            if (schemaRef != null)
            {
                if (g.Editor == "sizes") foreach (var z in schemaRef.Sizes) { words.Add(z.Title); words.Add(z.Description); words.Add(z.WhereLabel); }
                if (g.Editor == "corners") foreach (var c in schemaRef.Corners) words.Add(c.Title);
                if (g.Editor == "background") foreach (var t in schemaRef.Tuning) words.Add(t.Title);
                if (g.Editor == "sections") foreach (var sec in schemaRef.Sections) words.Add(sec.Title);
            }
            foreach (var key in ownedKeys)
            {
                string title;
                if (keyTitles.TryGetValue(key, out title)) words.Add(title);
                words.Add(key);
            }
            if (g.Addon != null)
            {
                words.Add(g.Addon.Provides);
                words.AddRange(g.Addon.Items);
                words.AddRange(g.Addon.Match);
            }
            return string.Join(" ", words.Where(w => !string.IsNullOrEmpty(w)));
        }

        private static string EditorPageKind(string editor)
        {
            return editor;
        }

        /// <summary>Shows one item alone on the Items page (from a section of the Sections page, an add-on card or a see-also link).</summary>
        public void OpenItem(string id)
        {
            if (itemsEditor == null) return;
            Search = "";
            ShowChangedOnly = false;
            OpenPage("items");
            itemsEditor.ShowOnly(id);
        }

        // ---- counts and resets of a block ------------------------------------------------------------------------------------------------

        private int ChangedOf(GroupViewModel block)
        {
            int n = block.Settings.Count(s => s.IsModified);
            n += EditorChangedOf(block);
            return n;
        }

        /// <summary>What differs from the defaults in the block's editor and presets: one for each owned control or preset group that is not at its default.</summary>
        private int EditorChangedOf(GroupViewModel block)
        {
            int n = 0;
            if (block.OwnedControls != null)
                n += block.OwnedControls.Count(keys => keys.Any(k => store.Find(k) != null && store.Get(k) != store.GetDefault(k)));
            if (block.CountedKeys != null)
                n += block.CountedKeys.Count(k => store.Find(k) != null && store.Get(k) != store.GetDefault(k));
            if (block.OwnedPresets != null)
                foreach (var id in block.OwnedPresets)
                {
                    string chosen;
                    if (profile.Presets.TryGetValue(id, out chosen) && chosen != "Default") n++;
                }
            var resettable = block.Editor as IResettable;
            if (resettable != null && !blockOwnedKeys[block].Any(k => store.Find(k) != null)) n += resettable.ChangedCount;
            return n;
        }

        private void AskOrResetBlock(GroupViewModel block)
        {
            if (block.ChangedCount == 0) { block.ResetPending = false; return; }
            if (!block.ResetPending && block.ChangedCount >= BigResetThreshold) { block.ResetPending = true; return; }
            block.ResetPending = false;
            history.BeginBatch();
            try { ResetBlockCore(block); }
            finally { history.EndBatch(); }
            UndoCommand.RaiseCanExecuteChanged();
            RefreshChanged();
        }

        private void ResetBlockCore(GroupViewModel block)
        {
            foreach (var setting in block.Settings.Where(s => s.IsModified).ToList()) setting.Reset();
            var resettable = block.Editor as IResettable;
            bool hasKeys = false;
            foreach (var key in blockOwnedKeys[block])
                if (store.Find(key) != null) { hasKeys = true; store.Reset(key); }
            if (resettable != null && !hasKeys) resettable.ResetAll();
            if (block.OwnedPresets != null)
                foreach (var id in block.OwnedPresets)
                {
                    string chosen;
                    if (profile.Presets.TryGetValue(id, out chosen) && chosen != "Default") PresetHistory.Apply(history, applier, profile, id, "Default");
                }
        }

        // ---- filter and search over the blocks -------------------------------------------------------------------------------------------

        private void ApplyCompositeFilter(PageViewModel page)
        {
            string needle = search.Trim();
            bool filtering = needle.Length > 0 || showChangedOnly;
            int matches = 0;
            foreach (var group in page.Groups)
            {
                foreach (var setting in group.Settings)
                {
                    setting.Visible = setting.Matches(search) && (!showChangedOnly || setting.IsModified);
                    setting.Highlighted = needle.Length > 0 && setting.Visible;
                }
                int shown = group.Settings.Count(s => s.Visible);
                int editorHits = EditorHitsOf(group, needle);
                group.Visible = !filtering || shown + editorHits > 0;
                group.ForceMore = filtering && group.Extra.Any(s => s.Visible);
                matches += shown + editorHits;
            }
            page.MatchCount = matches;
            page.Searching = filtering;
            page.Dimmed = needle.Length > 0 && matches == 0;
        }

        private int EditorHitsOf(GroupViewModel group, string needle)
        {
            if (group.Editor is ItemsEditorViewModel) return ((ItemsEditorViewModel)group.Editor).Filter(search, showChangedOnly);
            if (group.Editor == null && group.Addon == null && !group.HasOwned) return 0;
            if (needle.Length == 0 && !showChangedOnly) return 0;
            bool keywordHit = needle.Length == 0 || (group.Keywords ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
            bool changedHit = !showChangedOnly || EditorChangedOf(group) > 0;
            return keywordHit && changedHit ? 1 : 0;
        }

        /// <summary>The first block of the page that shows after a search or a filter, so the page can scroll to it.</summary>
        private GroupViewModel FirstShownBlock(PageViewModel page)
        {
            return page.Groups.FirstOrDefault(g => g.Visible && (g.Settings.Any(s => s.Visible) || g.HasEditor || g.HasAddon));
        }

        /// <summary>Selects the page and brings the block into view.</summary>
        private bool OpenPlace(string place)
        {
            if (string.IsNullOrEmpty(place)) return false;
            string target = schemaRef == null ? null : schemaRef.ResolveAlias(place);
            string path = target ?? place;
            int slash = path.IndexOf('/');
            string pageId = slash < 0 ? path : path.Substring(0, slash);
            string blockId = slash < 0 ? null : path.Substring(slash + 1);
            var page = Pages.FirstOrDefault(p => p.Id == pageId);
            if (page == null) return false;
            SelectedPage = page;
            if (blockId != null)
            {
                var block = page.Groups.FirstOrDefault(g => g.Id == blockId);
                if (block != null) block.RequestScroll();
            }
            return true;
        }
    }
}
