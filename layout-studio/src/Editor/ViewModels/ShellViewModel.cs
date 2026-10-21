using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    /// <summary>A visual editor that can count what differs from the defaults and put everything back.</summary>
    public interface IResettable
    {
        int ChangedCount { get; }
        void ResetAll();
    }

    /// <summary>State of the editor window: pages built from the schema, search, undo/redo and page reset.</summary>
    public partial class ShellViewModel : ViewModelBase
    {
        private readonly ConstantStore store;
        private readonly History history;
        private readonly ProfileApplier applier;
        private readonly Profile profile;
        private readonly Func<string, string> localizeText;
        private SizesEditorViewModel sizes;
        private Func<LiveContext> liveContext;
        private readonly CultureInfo culture;
        private Dictionary<string, string> keyTitles = new Dictionary<string, string>();
        private readonly Func<string, string, string> Page;   // a page title in the user's language, the English name as the fallback
        private string search = "";
        private bool showChangedOnly;
        private PageViewModel selectedPage;
        private readonly Dictionary<PageViewModel, IList<string>> ownedKeys = new Dictionary<PageViewModel, IList<string>>();

        public ShellViewModel(Schema schema, ConstantStore store, History history, ProfileApplier applier, Profile profile,
            Func<string, string> localize, CultureInfo culture, Func<string> previewImage = null,
            IProfileSharing sharing = null, Action<string> copyToClipboard = null, PlayniteSettingsViewModel playniteSettings = null, IList<string> sensorProblems = null, FactsViewModel facts = null,
            Func<IEnumerable<KeyValuePair<string, string>>> environment = null, Func<LiveContext> liveContext = null,
            Func<IEnumerable<InstalledAddon>> installedAddons = null)
        {
            this.store = store;
            this.history = history;
            this.applier = applier;
            this.profile = profile;
            BigResetThreshold = 6;
            localizeText = localize;
            this.culture = culture;
            PlayniteSettings = playniteSettings;
            Page = (name, english) => { string text = null; try { text = localize("LOCLS_Page_" + name); } catch (Exception) { } return string.IsNullOrEmpty(text) ? english : text; };
            SensorProblems = sensorProblems ?? new List<string>();
            if (schema.HasLayout)
            {
                BuildComposite(schema, sharing, copyToClipboard, facts, environment, liveContext, previewImage, installedAddons);
            }
            else
            {
            var covered = SchemaCoverage.VisualKeys(schema);
            var allControls = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).ToList();
            bool hasColorsPage = allControls.Any(c => c.Id == "ColorProfile") && allControls.Any(c => c.Id == "Accent") && schema.Find("GlyphColor") != null;
            bool hasCornersPage = schema.Corners.Count > 0;
            bool hasItemsPage = allControls.Any(ItemFamilies.IsItemControl);
            Pages = schema.Pages.Select(p => new PageViewModel(p.Id, p.Title, p.Groups.Select(g =>
                new GroupViewModel(SchemaCoverage.ShortTitle(g.Title), g.Controls.Where(c => !SchemaCoverage.IsCovered(c, covered) && !SchemaCoverage.IsPresetOfAVisualPage(c, hasColorsPage, hasCornersPage) && !(hasItemsPage && ItemFamilies.IsOwnedByItemsPage(c))).Select(c =>
                    new SettingViewModel(c, store, history, applier, profile, localize, culture)).ToList())).Where(g => g.Settings.Count > 0).ToList())).ToList();
            var controls = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).ToList();
            keyTitles = KeyTitles(schema, controls, localize);
            foreach (var setting in Pages.SelectMany(p => p.Settings)) setting.KeyTitle = key => { string t; return keyTitles.TryGetValue(key, out t) ? t : null; };
            var visual = new List<PageViewModel>();
            var sectionsVm = schema.Sections.Count > 0 ? new SectionsEditorViewModel(store, schema, history) : null;
            if (sectionsVm != null) visual.Add(new PageViewModel("visual-sections", Page("Sections", "Sections"), "sections", sectionsVm));
            if (hasItemsPage)
            {
                var itemsVm = new ItemsEditorViewModel(schema, store, history, applier, profile, localize, culture);
                visual.Add(new PageViewModel("visual-items", Page("Items", "Items"), "items", itemsVm));
                itemsVm.OpenSections = () => OpenPage("visual-sections");
                if (sectionsVm != null) sectionsVm.OpenItemOptions = id => { Search = ""; ShowChangedOnly = false; OpenPage("visual-items"); itemsVm.ShowOnly(id); };   // a search or "changed only" would leave the card empty
            }
            if (schema.Sizes.Count > 0)
            {
                sizes = new SizesEditorViewModel(store, schema, history, culture, profile.Editor) { Localize = localize };
                this.liveContext = liveContext;
                RefreshLive();
                visual.Add(new PageViewModel("visual-sizes", Page("Sizes", "Sizes"), "sizes", sizes));
            }
            if (schema.Tuning.Count > 0) visual.Add(new PageViewModel("visual-background", Page("Background", "Background"), "background", new BackgroundEditorViewModel(store, schema, history, applier, profile, localize, culture, previewImage)));
            if (controls.Any(c => c.Kind == "tileSlots")) {
                var tilesVm = new TilesEditorViewModel(store, schema, history);
                visual.Add(new PageViewModel("visual-tiles", Page("HeaderCards", "Header cards"), "tiles", tilesVm));
            }
            if (controls.Any(c => c.Id == "ColorProfile") && controls.Any(c => c.Id == "Accent") && schema.Find("GlyphColor") != null)
                visual.Add(new PageViewModel("visual-colors", Page("Colors", "Colors"), "colors", new ColorsEditorViewModel(store, schema, history, applier, profile)));
            if (schema.Corners.Count > 0) visual.Add(new PageViewModel("visual-corners", Page("Corners", "Corners"), "corners", new CornersEditorViewModel(store, schema, history)));
            if (facts != null) visual.Add(new PageViewModel("visual-rules", Page("Thresholds", "Thresholds"), "rules", new RulesEditorViewModel(facts, profile.Editor, history)));
            for (int i = 0; i < visual.Count; i++) Pages.Insert(i, visual[i]);
            if (playniteSettings != null) Pages.Insert(0, new PageViewModel("advice", Page("Advice", "Graphics and performance"), "advice", new AdviceViewModel(playniteSettings, store, history, profile, OpenPage, playniteSettings.Ui)));
            foreach (var page in Pages) page.FilterChanged = ApplyFilter;
            OwnKeys(schema, controls);
            foreach (var page in Pages) page.Keywords = KeywordsOf(page.Kind);
            Pages.Add(new PageViewModel("diagnostics", Page("Diagnostics", "Diagnostics"), "diagnostics",
                new DiagnosticsViewModel(() => CollectDiagnostics(schema, environment), copyToClipboard)));
            if (sharing != null) Pages.Add(new PageViewModel("profile", Page("Profile", "Profile"), "share", new SharingViewModel(sharing, copyToClipboard)));
            foreach (var page in Pages.Where(p => p.Keywords == null)) { page.Keywords = KeywordsOf(page.Kind); page.FilterChanged = ApplyFilter; }   // the Diagnostics and Profile pages are found by their words too
            selectedPage = Pages.FirstOrDefault();
            PagesView = System.Windows.Data.CollectionViewSource.GetDefaultView(Pages);
            PagesView.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription("Group"));
            }

            UndoCommand = new RelayCommand(() => history.Undo(), () => history.CanUndo);
            RedoCommand = new RelayCommand(() => history.Redo(), () => history.CanRedo);
            ResetPageCommand = new RelayCommand(AskOrResetPage, () => selectedPage != null && selectedPage.ChangedCount > 0);
            applier.PresetChanged += () => { Raise("RestartRequired"); RefreshChanged(); };
            store.ValueChanged += key => RefreshChanged();
            history.Changed += () =>
            {
                Raise("UndoLabel");
                Raise("RedoLabel");
                ResetPending = false;
                Raise("RestartRequired");
                UndoCommand.RaiseCanExecuteChanged();
                RedoCommand.RaiseCanExecuteChanged();
                RefreshChanged();
            };
            RefreshChanged();
        }

        /// <summary>The facts the Diagnostics page lists: environment (from the plugin), theme, profile, Playnite settings, sensors.</summary>
        private IList<DiagnosticLine> CollectDiagnostics(Schema schema, Func<IEnumerable<KeyValuePair<string, string>>> environment)
        {
            var lines = new List<DiagnosticLine>();
            if (environment != null)
                foreach (var kv in environment()) lines.Add(new DiagnosticLine(kv.Key, kv.Value));
            lines.Add(new DiagnosticLine("Theme settings", store.Definitions.Count() + " constants, " + schema.Pages.Count + " pages in the schema"));
            var problems = schema.Validate();
            lines.Add(problems.Count == 0 ? new DiagnosticLine("Schema", "consistent", DiagnosticLevel.Ok)
                : new DiagnosticLine("Schema", problems.Count + " problem" + (problems.Count == 1 ? "" : "s"), DiagnosticLevel.Warning, string.Join("; ", problems.Take(4))));
            lines.Add(new DiagnosticLine("Profile in use", profile.Name + ": " + profile.Overrides.Count + " saved values, " + profile.Presets.Count + " preset choices"));
            if (profile.ReadOnly || !string.IsNullOrEmpty(profile.Warning))
                lines.Add(new DiagnosticLine("Profile file", profile.ReadOnly ? "read-only" : "fell back", DiagnosticLevel.Warning, profile.Warning));
            int unknown = profile.Overrides.Keys.Count(k => store.Find(k) == null);
            if (unknown > 0) lines.Add(new DiagnosticLine("Saved values this theme version does not know", unknown.ToString(), DiagnosticLevel.Info, "kept in the profile, not applied"));
            lines.Add(new DiagnosticLine("Changed from the defaults", ChangedTotal + " settings"));
            lines.Add(applier.RestartRequired ? new DiagnosticLine("Restart", "needed for a preset choice", DiagnosticLevel.Warning, "Playnite must be restarted to apply it")
                : new DiagnosticLine("Restart", "not needed", DiagnosticLevel.Ok));
            if (PlayniteSettings != null)
            {
                lines.Add(PlayniteSettings.HasFindings ? new DiagnosticLine("Playnite settings", PlayniteSettings.Findings.Count + " conflict with the theme", DiagnosticLevel.Warning, "see the Graphics and performance page")
                    : new DiagnosticLine("Playnite settings", "no conflict", DiagnosticLevel.Ok));
                if (PlayniteSettings.ChangedByUsCount > 0)
                    lines.Add(new DiagnosticLine("Playnite settings changed by Layout Studio", PlayniteSettings.ChangedByUsCount.ToString(), DiagnosticLevel.Info, "Put back as before restores them"));
            }
            lines.Add(SensorProblems.Count == 0 ? new DiagnosticLine("HWiNFO and GameActivity", "no mismatch", DiagnosticLevel.Ok)
                : new DiagnosticLine("HWiNFO and GameActivity", SensorProblems.Count + " mismatch" + (SensorProblems.Count == 1 ? "" : "es"), DiagnosticLevel.Warning, string.Join("; ", SensorProblems.Take(3))));
            return lines;
        }

        /// <summary>The title a user knows each constant by: the title of its control, or of its corner or size entry.</summary>
        private static Dictionary<string, string> KeyTitles(Schema schema, IList<Control> controls, Func<string, string> localize)
        {
            var map = new Dictionary<string, string>();
            foreach (var c in controls)
            {
                string title = (c.TitleKey != null ? localize(c.TitleKey) : null) ?? c.Title;
                if (string.IsNullOrEmpty(title) || c.Kind == "preset") continue;
                foreach (var k in c.Keys) if (!map.ContainsKey(k)) map[k] = c.Keys.Count == 1 ? title : title + " (" + k + ")";
            }
            foreach (var c in schema.Corners) if (!map.ContainsKey(c.Key) && !string.IsNullOrEmpty(c.Title)) map[c.Key] = c.Title + ": corner radius";
            foreach (var s in schema.Sizes)
            {
                if (!string.IsNullOrEmpty(s.Title) && s.WidthKey != null && !map.ContainsKey(s.WidthKey)) map[s.WidthKey] = s.Title + ": width";
                if (!string.IsNullOrEmpty(s.Title) && s.HeightKey != null && !map.ContainsKey(s.HeightKey)) map[s.HeightKey] = s.Title + ": height";
            }
            return map;
        }

        /// <summary>The constants a visual page edits, so its changes can be counted and reset (pages with their own reset are left out).</summary>
        private void OwnKeys(Schema schema, IList<Control> controls)
        {
            foreach (var page in Pages)
            {
                IEnumerable<string> keys = null;
                if (page.Kind == "sizes") keys = schema.Sizes.SelectMany(s => new[] { s.WidthKey, s.HeightKey });
                else if (page.Kind == "corners") keys = schema.Corners.Select(c => c.Key);
                else if (page.Kind == "background") keys = schema.Tuning.Select(t => t.Key).Concat(new[] { "PageGlassOpacity" });
                else if (page.Kind == "tiles") { var t = controls.First(c => c.Kind == "tileSlots"); keys = t.Keys.Concat(t.ExtraKeys); }
                if (keys != null) ownedKeys[page] = keys.Where(k => k != null && store.Find(k) != null).Distinct().ToList();
            }
        }

        private static string KeywordsOf(string kind)
        {
            switch (kind)
            {
                case "sections": return "sections order column genres tags notes categories features move hide";
                case "items": return "items information chips cards genres tags categories features release date platforms developers source score scores rating achievements hltb howlongtobeat play time played install size last session notes video news section sections tab column right new line wrap open default show hide grid view order move buttons widgets music players online panel";
                case "sizes": return "sizes size width height artwork cover button logo ratio card header";
                case "background": return "background blur dark darkness glass window panel opacity artwork zone top bar sidebar list filter";
                case "tiles": return "header tiles cards cell fps cpu gpu ram score playtime install size";
                case "colors": return "colors colours accent profile palette theme";
                case "corners": return "corners corner radius round rounded square";
                case "rules": return "thresholds seuils figure colors fps cpu gpu ram limit score glow pulse";
                case "advice": return "advice graphics performance gpu cpu slow lag fps blur animation darken background playnite settings problem";
                case "diagnostics": return "diagnostics problem bug report version log schema profile restart sensors hwinfo check";
                case "share": return "profile share export import reset backup code";
                case "globalpresets": return "global preset look theme style direction delta whole all reset default";
                default: return "";
            }
        }

        private int ChangedOf(PageViewModel page)
        {
            if (page.IsComposite) return page.Groups.Sum(g => g.ChangedCount);
            if (page.Kind == "settings") return page.Settings.Count(s => s.IsModified);
            var resettable = page.Editor as IResettable;
            if (resettable != null) return resettable.ChangedCount;
            IList<string> keys;
            return ownedKeys.TryGetValue(page, out keys) ? keys.Count(k => store.Get(k) != store.GetDefault(k)) : 0;
        }

        /// <summary>Recounts what differs from the defaults on every page: the sidebar counts, the Changed button and the reset button follow.</summary>
        public void RefreshChanged()
        {
            foreach (var page in Pages)
            {
                if (page.IsComposite)
                    foreach (var block in page.Groups) { block.ChangedCount = ChangedOf(block); if (block.ChangedCount == 0) block.ResetPending = false; block.RaiseResetCanExecute(); }
                page.ChangedCount = ChangedOf(page);
            }
            Raise("ChangedTotal");
            Raise("ResetPageLabel");
            ResetPageCommand.RaiseCanExecuteChanged();
        }

        public int ChangedTotal { get { return Pages.Sum(p => p.ChangedCount); } }

        /// <summary>"Reset this page (12)": the number of settings the button would put back.</summary>
        public string ResetPageLabel
        {
            get
            {
                int n = selectedPage == null ? 0 : selectedPage.ChangedCount;
                if (resetPending && n > 0) return string.Format(CultureInfo.InvariantCulture, Word("LOCLS_ResetPage_Confirm", "Click again to reset {0} settings"), n);
                string word = Word("LOCLS_ResetPage", "Reset this page");
                return n > 0 ? word + " (" + n + ")" : word;
            }
        }

        private bool resetPending;

        /// <summary>A reset of this many settings or more asks for a second click first (it stays one undo step either way).</summary>
        public int BigResetThreshold { get; set; }

        /// <summary>True after the first click of a big reset: the button asks for a second one. Any change, or another page, cancels it.</summary>
        public bool ResetPending
        {
            get { return resetPending; }
            private set { if (resetPending != value) { resetPending = value; Raise("ResetPending"); Raise("ResetPageLabel"); } }
        }

        private void AskOrResetPage()
        {
            if (!resetPending && selectedPage != null && selectedPage.ChangedCount >= BigResetThreshold) { ResetPending = true; return; }
            ResetPending = false;
            ResetPage();
        }

        /// <summary>The sidebar list, grouped under LAYOUT, LOOK, SETTINGS and PROFILE.</summary>
        public System.ComponentModel.ICollectionView PagesView { get; private set; }

        /// <summary>Shows only the settings that differ from the defaults, on every page.</summary>
        public bool ShowChangedOnly
        {
            get { return showChangedOnly; }
            set { if (showChangedOnly != value) { showChangedOnly = value; ApplyFilter(); Raise("ShowChangedOnly"); } }
        }

        /// <summary>HWiNFO gadget positions that hold another kind of sensor than GameActivity expects (empty when all is well).</summary>
        public IList<string> SensorProblems { get; private set; }
        public bool HasSensorProblems { get { return SensorProblems.Count > 0; } }

        /// <summary>Playnite's own settings that conflict with the theme, with buttons to fix them; null when unavailable.</summary>
        public PlayniteSettingsViewModel PlayniteSettings { get; private set; }

        /// <summary>Why the profile fell back to defaults or is read-only; null when all is well. Shown as a banner.</summary>
        public string ProfileWarning { get { return profile.Warning; } }

        /// <summary>True once a choice was made that only takes effect after Playnite restarts.</summary>
        public bool RestartRequired { get { return applier.RestartRequired; } }

        public IList<PageViewModel> Pages { get; private set; }

        public PageViewModel SelectedPage
        {
            get { return selectedPage; }
            set
            {
                if (ReferenceEquals(selectedPage, value)) return;
                if (selectedPage != null) selectedPage.IsCurrent = false;
                selectedPage = value;
                if (value != null) value.IsCurrent = true;
                ResetPending = false;
                var itemsPage = value == null ? null : value.Editor as ItemsEditorViewModel;
                if (itemsPage != null) itemsPage.ShowOnly(null);   // reached from the sidebar: every card (a section button focuses one right after)
                var diagnostics = value == null ? null : value.Editor as DiagnosticsViewModel;
                if (diagnostics != null) diagnostics.Refresh();   // the facts were collected when the window opened: read them again when the page is shown
                if (value != null && value.IsComposite)
                {
                    // a page of blocks: the Items page shows every card again, the Diagnostics block reads the facts again
                    if (itemsEditor != null && value.Id == "items") itemsEditor.ShowOnly(null);
                    if (diagnosticsEditor != null && value.Groups.Any(g => ReferenceEquals(g.Editor, diagnosticsEditor))) diagnosticsEditor.Refresh();
                }
                Raise("SelectedPage");
                Raise("ResetPageLabel");
                ResetPageCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>Selects the page with this id (the theme's gear buttons open the editor on the page that holds their settings); an unknown or empty id changes nothing.</summary>
        public void OpenPage(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var page = Pages.FirstOrDefault(p => p.Id == id);
            if (page != null) { SelectedPage = page; return; }
            OpenPlace(id);   // an older page id, or "page/block": the page opens and the block comes into view
        }

        public string Search
        {
            get { return search; }
            set
            {
                search = value ?? "";
                ApplyFilter();
                Raise("Search");
                if (search.Length > 0 && selectedPage != null && selectedPage.Dimmed)
                {
                    var next = Pages.FirstOrDefault(p => !p.Dimmed && p.MatchCount > 0);
                    if (next != null) SelectedPage = next;
                }
                if (search.Length > 0 && selectedPage != null && selectedPage.IsComposite)
                {
                    var first = FirstShownBlock(selectedPage);
                    if (first != null) first.RequestScroll();   // the page opens on the first block that matches
                }
            }
        }

        public RelayCommand ClearSearchCommand { get { return new RelayCommand(() => { Search = ""; }, () => search.Length > 0); } }

        /// <summary>Asks Playnite what it shows now (the plugin calls this when the editor is activated and every few seconds): the Sizes page marks the sizes that are on screen.</summary>
        public void RefreshLive()
        {
            if (sizes == null || liveContext == null) return;
            try { sizes.Context = liveContext(); } catch (Exception) { sizes.Context = null; }
        }

        private string Word(string key, string english)
        {
            string text = null;
            try { text = localizeText(key); } catch (Exception) { }
            return string.IsNullOrEmpty(text) ? english : text;
        }

        private string Label(string word, bool can, IList<string> keys, bool hasAction)
        {
            if (!can) return word;
            string what = null;
            if (hasAction) what = Word("LOCLS_Undo_Preset", "a preset choice");
            else if (keys.Count == 1) { string title; what = keyTitles.TryGetValue(keys[0], out title) ? title : keys[0]; }
            else if (keys.Count > 1) what = string.Format(CultureInfo.InvariantCulture, Word("LOCLS_Undo_Settings", "{0} settings"), keys.Count);
            if (what == null) return word;
            return word + (string.Equals(culture.TwoLetterISOLanguageName, "fr", StringComparison.OrdinalIgnoreCase) ? " : " : ": ") + what;
        }

        /// <summary>"Undo: Window background blur": the button says what Ctrl+Z would put back.</summary>
        public string UndoLabel { get { return Label(Word("LOCLS_Undo", "Undo"), history.CanUndo, history.NextUndoKeys, history.NextUndoHasAction); } }

        public string RedoLabel { get { return Label(Word("LOCLS_Redo", "Redo"), history.CanRedo, history.NextRedoKeys, history.NextRedoHasAction); } }

        // ---- mode: the desktop editor can also edit the Fullscreen profile -------------------------------------

        /// <summary>"Desktop" or "Fullscreen": which theme this shell edits.</summary>
        public string Mode { get; set; }

        /// <summary>Switches the editor to the other theme (set by the plugin; null when the Fullscreen theme cannot be edited from here).</summary>
        public Action<string> ModeSwitch { get; set; }

        public bool HasModes { get { return ModeSwitch != null; } }
        public bool IsDesktopMode { get { return Mode != "Fullscreen"; } }
        public bool IsFullscreenMode { get { return Mode == "Fullscreen"; } }
        public RelayCommand ShowDesktopCommand { get { return new RelayCommand(() => { if (ModeSwitch != null && IsFullscreenMode) ModeSwitch("Desktop"); }); } }
        public RelayCommand ShowFullscreenCommand { get { return new RelayCommand(() => { if (ModeSwitch != null && IsDesktopMode) ModeSwitch("Fullscreen"); }); } }

        public RelayCommand UndoCommand { get; private set; }
        public RelayCommand RedoCommand { get; private set; }
        public RelayCommand ResetPageCommand { get; private set; }

        private void ApplyFilter()
        {
            foreach (var page in Pages)
            {
                if (page.IsComposite) { ApplyCompositeFilter(page); continue; }
                foreach (var group in page.Groups)
                {
                    foreach (var setting in group.Settings) setting.Visible = setting.Matches(search) && (!showChangedOnly || setting.IsModified);
                    group.Visible = group.Settings.Any(s => s.Visible)
                        && (search.Length > 0 || page.ActiveGroup == null || ReferenceEquals(group, page.ActiveGroup));
                }
                if (page.Kind == "settings")
                {
                    page.MatchCount = page.Settings.Count(s => s.Visible);
                    page.Dimmed = search.Length > 0 && page.MatchCount == 0;
                }
                else if (page.Editor is ItemsEditorViewModel)
                {
                    // an items page searches the options inside its cards, like a generated page
                    page.MatchCount = ((ItemsEditorViewModel)page.Editor).Filter(search, showChangedOnly);
                    page.Dimmed = search.Length > 0 && page.MatchCount == 0;
                }
                else
                {
                    // a visual page matches by its title or keywords
                    bool hit = search.Length > 0 && ((page.Title ?? "").IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0 || (page.Keywords ?? "").IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
                    page.Dimmed = search.Length > 0 && !hit;
                    if (search.Length > 0) page.MatchCount = hit ? 1 : 0;
                }
            }
        }

        private void ResetPage()
        {
            history.BeginBatch();
            try
            {
                if (selectedPage.IsComposite) { foreach (var block in selectedPage.Groups) ResetBlockCore(block); }
                else if (selectedPage.Kind == "settings") { foreach (var setting in selectedPage.Settings.Where(s => s.IsModified).ToList()) setting.Reset(); }
                else if (selectedPage.Editor is IResettable) ((IResettable)selectedPage.Editor).ResetAll();
                else
                {
                    IList<string> keys;
                    if (ownedKeys.TryGetValue(selectedPage, out keys)) foreach (var key in keys) store.Reset(key);
                }
            }
            finally
            {
                history.EndBatch();
            }
            UndoCommand.RaiseCanExecuteChanged();
            RefreshChanged();
        }
    }
}
