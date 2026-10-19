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

    public class GroupViewModel : ViewModelBase
    {
        private bool visible = true;

        public string Title { get; private set; }
        public IList<SettingViewModel> Settings { get; private set; }

        public GroupViewModel(string title, IList<SettingViewModel> settings)
        {
            Title = title;
            Settings = settings;
        }

        public bool Visible
        {
            get { return visible; }
            set { if (visible != value) { visible = value; Raise("Visible"); } }
        }
    }

    /// <summary>A sub-category button at the top of a long settings page (the Details view has twelve groups): shows one group, or all of them.</summary>
    public class GroupChipViewModel : ViewModelBase
    {
        private bool selected;

        public GroupChipViewModel(string title, int count, GroupViewModel group, Action<GroupViewModel> select)
        {
            Title = title; Count = count; Group = group;
            SelectCommand = new RelayCommand(() => select(group));
        }

        public string Title { get; private set; }
        public int Count { get; private set; }
        public GroupViewModel Group { get; private set; }
        public RelayCommand SelectCommand { get; private set; }
        public bool IsSelected { get { return selected; } set { if (selected != value) { selected = value; Raise("IsSelected"); } } }
    }

    public class PageViewModel : ViewModelBase
    {
        private int matchCount;
        private int changedCount;
        private GroupViewModel activeGroup;

        public string Id { get; private set; }
        public string Title { get; private set; }
        public IList<GroupViewModel> Groups { get; private set; }

        public PageViewModel(string id, string title, IList<GroupViewModel> groups)
        {
            Id = id;
            Title = title;
            Groups = groups;
            Kind = "settings";
            Group = "SETTINGS";
            ShowCount = true;
            matchCount = groups.SelectMany(g => g.Settings).Count();
            Chips = new List<GroupChipViewModel>();
            if (groups.Count >= 3)
            {
                var chips = new List<GroupChipViewModel> { new GroupChipViewModel("All", matchCount, null, Select) };
                chips.AddRange(groups.Select(g => new GroupChipViewModel(ChipTitle(g.Title), g.Settings.Count, g, Select)));
                chips[0].IsSelected = true;
                Chips = chips;
            }
        }

        /// <summary>Sub-category buttons (empty when the page has fewer than three groups).</summary>
        public IList<GroupChipViewModel> Chips { get; private set; }
        public bool HasChips { get { return Chips.Count > 0; } }

        /// <summary>The group shown alone, or null for all of them. Search ignores it: matches are always listed.</summary>
        public GroupViewModel ActiveGroup { get { return activeGroup; } }

        /// <summary>Called when the chosen sub-category changes so the shell can re-apply the filter.</summary>
        public Action FilterChanged { get; set; }

        private void Select(GroupViewModel group)
        {
            activeGroup = group;
            foreach (var chip in Chips) chip.IsSelected = ReferenceEquals(chip.Group, group);
            if (FilterChanged != null) FilterChanged();
        }

        /// <summary>"Details view - information chips" is shown as "Information chips": the page name is already known.</summary>
        private static string ChipTitle(string title)
        {
            int dash = title.IndexOf(" - ", StringComparison.Ordinal);
            string t = dash >= 0 ? title.Substring(dash + 3) : title;
            return t.Length == 0 ? title : char.ToUpperInvariant(t[0]) + t.Substring(1);
        }

        /// <summary>A page that shows a visual editor instead of generated settings.</summary>
        public PageViewModel(string id, string title, string kind, object editor)
            : this(id, title, new List<GroupViewModel>())
        {
            Kind = kind;
            Editor = editor;
            ShowCount = false;
            Group = kind == "sections" || kind == "sizes" || kind == "tiles" || kind == "corners" ? "LAYOUT"
                : kind == "background" || kind == "colors" || kind == "rules" ? "LOOK" : kind == "share" ? "PROFILE" : "SETTINGS";
        }

        /// <summary>The sidebar heading this page is listed under: LAYOUT, LOOK, SETTINGS or PROFILE.</summary>
        public string Group { get; private set; }

        /// <summary>Settings of this page that differ from the defaults (shown as an orange count in the sidebar).</summary>
        public int ChangedCount
        {
            get { return changedCount; }
            set { if (changedCount != value) { changedCount = value; Raise("ChangedCount"); Raise("HasChanges"); } }
        }
        public bool HasChanges { get { return changedCount > 0; } }

        /// <summary>Words a search can find on a visual page (its settings are not generated controls).</summary>
        public string Keywords { get; set; }

        private bool dimmed;
        /// <summary>True while a search is typed and nothing on this page matches it: the sidebar dims the page and the page says so.</summary>
        public bool Dimmed { get { return dimmed; } set { if (dimmed != value) { dimmed = value; Raise("Dimmed"); } } }

        /// <summary>"settings" for generated pages, otherwise the visual editor's kind (sections, sizes, ...).</summary>
        public string Kind { get; private set; }
        public object Editor { get; private set; }
        public bool ShowCount { get; private set; }

        public IEnumerable<SettingViewModel> Settings { get { return Groups.SelectMany(g => g.Settings); } }

        /// <summary>Settings of this page that match the current search.</summary>
        public int MatchCount
        {
            get { return matchCount; }
            set { if (matchCount != value) { matchCount = value; Raise("MatchCount"); } }
        }
    }

    /// <summary>State of the editor window: pages built from the schema, search, undo/redo and page reset.</summary>
    public class ShellViewModel : ViewModelBase
    {
        private readonly ConstantStore store;
        private readonly History history;
        private readonly ProfileApplier applier;
        private readonly Profile profile;
        private string search = "";
        private bool showChangedOnly;
        private PageViewModel selectedPage;
        private readonly Dictionary<PageViewModel, IList<string>> ownedKeys = new Dictionary<PageViewModel, IList<string>>();

        public ShellViewModel(Schema schema, ConstantStore store, History history, ProfileApplier applier, Profile profile,
            Func<string, string> localize, CultureInfo culture, Func<string> previewImage = null,
            IProfileSharing sharing = null, Action<string> copyToClipboard = null, PlayniteSettingsViewModel playniteSettings = null, IList<string> sensorProblems = null, FactsViewModel facts = null)
        {
            this.store = store;
            this.history = history;
            this.applier = applier;
            this.profile = profile;
            PlayniteSettings = playniteSettings;
            SensorProblems = sensorProblems ?? new List<string>();
            Pages = schema.Pages.Select(p => new PageViewModel(p.Id, p.Title, p.Groups.Select(g =>
                new GroupViewModel(g.Title, g.Controls.Select(c =>
                    new SettingViewModel(c, store, history, applier, profile, localize, culture)).ToList())).ToList())).ToList();
            var controls = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).ToList();
            var visual = new List<PageViewModel>();
            if (schema.Sections.Count > 0) visual.Add(new PageViewModel("visual-sections", "Sections", "sections", new SectionsEditorViewModel(store, schema, history)));
            if (schema.Sizes.Count > 0) visual.Add(new PageViewModel("visual-sizes", "Sizes", "sizes", new SizesEditorViewModel(store, schema, history, culture, profile.Editor)));
            if (schema.Tuning.Count > 0) visual.Add(new PageViewModel("visual-background", "Background", "background", new BackgroundEditorViewModel(store, schema, history, applier, profile, localize, culture, previewImage)));
            if (controls.Any(c => c.Kind == "tileSlots")) visual.Add(new PageViewModel("visual-tiles", "Header cards", "tiles", new TilesEditorViewModel(store, schema, history)));
            if (controls.Any(c => c.Id == "ColorProfile") && controls.Any(c => c.Id == "Accent") && schema.Find("GlyphColor") != null)
                visual.Add(new PageViewModel("visual-colors", "Colors", "colors", new ColorsEditorViewModel(store, schema, history, applier, profile)));
            if (schema.Corners.Count > 0) visual.Add(new PageViewModel("visual-corners", "Corners", "corners", new CornersEditorViewModel(store, schema, history)));
            if (facts != null) visual.Add(new PageViewModel("visual-rules", "Figure colors", "rules", new RulesEditorViewModel(facts, profile.Editor, history)));
            for (int i = 0; i < visual.Count; i++) Pages.Insert(i, visual[i]);
            foreach (var page in Pages) page.FilterChanged = ApplyFilter;
            OwnKeys(schema, controls);
            foreach (var page in Pages) page.Keywords = KeywordsOf(page.Kind);
            if (sharing != null) Pages.Add(new PageViewModel("profile", "Profile", "share", new SharingViewModel(sharing, copyToClipboard)));
            selectedPage = Pages.FirstOrDefault();
            PagesView = System.Windows.Data.CollectionViewSource.GetDefaultView(Pages);
            PagesView.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription("Group"));

            UndoCommand = new RelayCommand(() => history.Undo(), () => history.CanUndo);
            RedoCommand = new RelayCommand(() => history.Redo(), () => history.CanRedo);
            ResetPageCommand = new RelayCommand(ResetPage, () => selectedPage != null && selectedPage.ChangedCount > 0);
            applier.PresetChanged += () => { Raise("RestartRequired"); RefreshChanged(); };
            store.ValueChanged += key => RefreshChanged();
            history.Changed += () =>
            {
                Raise("RestartRequired");
                UndoCommand.RaiseCanExecuteChanged();
                RedoCommand.RaiseCanExecuteChanged();
                RefreshChanged();
            };
            RefreshChanged();
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
                case "sizes": return "sizes size width height artwork cover button logo ratio card header";
                case "background": return "background blur dark darkness glass window panel opacity artwork zone top bar sidebar list filter";
                case "tiles": return "header tiles cards cell fps cpu gpu ram score playtime install size";
                case "colors": return "colors colours accent profile palette theme";
                case "corners": return "corners corner radius round rounded square";
                case "rules": return "figure colors fps cpu gpu ram limit threshold score glow pulse";
                case "share": return "profile share export import reset backup code";
                default: return "";
            }
        }

        private int ChangedOf(PageViewModel page)
        {
            if (page.Kind == "settings") return page.Settings.Count(s => s.IsModified);
            var resettable = page.Editor as IResettable;
            if (resettable != null) return resettable.ChangedCount;
            IList<string> keys;
            return ownedKeys.TryGetValue(page, out keys) ? keys.Count(k => store.Get(k) != store.GetDefault(k)) : 0;
        }

        /// <summary>Recounts what differs from the defaults on every page: the sidebar counts, the Changed button and the reset button follow.</summary>
        public void RefreshChanged()
        {
            foreach (var page in Pages) page.ChangedCount = ChangedOf(page);
            Raise("ChangedTotal");
            Raise("ResetPageLabel");
            ResetPageCommand.RaiseCanExecuteChanged();
        }

        public int ChangedTotal { get { return Pages.Sum(p => p.ChangedCount); } }

        /// <summary>"Reset this page (12)": the number of settings the button would put back.</summary>
        public string ResetPageLabel { get { return selectedPage != null && selectedPage.ChangedCount > 0 ? "Reset this page (" + selectedPage.ChangedCount + ")" : "Reset this page"; } }

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

        /// <summary>Playnite's own settings that conflict with the theme or cost performance, with buttons to turn them off; null when unavailable.</summary>
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
                selectedPage = value;
                Raise("SelectedPage");
                Raise("ResetPageLabel");
                ResetPageCommand.RaiseCanExecuteChanged();
            }
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
            }
        }

        public RelayCommand ClearSearchCommand { get { return new RelayCommand(() => { Search = ""; }, () => search.Length > 0); } }

        public RelayCommand UndoCommand { get; private set; }
        public RelayCommand RedoCommand { get; private set; }
        public RelayCommand ResetPageCommand { get; private set; }

        private void ApplyFilter()
        {
            foreach (var page in Pages)
            {
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
                if (selectedPage.Kind == "settings") { foreach (var setting in selectedPage.Settings.Where(s => s.IsModified).ToList()) setting.Reset(); }
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
