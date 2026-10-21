using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    /// <summary>A link from a block to another place that shows or touches the same thing: "Sizes, corners and density > Sizes".</summary>
    public class SeeAlsoViewModel : ViewModelBase
    {
        public string Text { get; set; }
        public string Target { get; set; }
        public RelayCommand OpenCommand { get; set; }
    }

    /// <summary>One Items card an add-on feeds: a row of the Add-ons page with a link to the card.</summary>
    public class ProvidedItemViewModel
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Where { get; set; }
        public RelayCommand OpenCommand { get; set; }
    }

    /// <summary>The Add-ons page block of one add-on: is it installed, what does it give the theme, where to set it.</summary>
    public class AddonViewModel : ViewModelBase
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public AddonState State { get; set; }
        public string InstalledId { get; set; }
        public string Provides { get; set; }
        public IList<ProvidedItemViewModel> Items { get; set; }

        public bool HasItems { get { return Items != null && Items.Count > 0; } }
        public bool IsInstalled { get { return State == AddonState.Installed; } }
        public bool IsDisabled { get { return State == AddonState.Disabled; } }
        public bool IsMissing { get { return State == AddonState.Missing; } }
        public bool IsUnknown { get { return State == AddonState.Unknown; } }

        /// <summary>"Installed", "Installed, switched off", "Not installed" or "Not detected here": the words come from the language file when given.</summary>
        public string StatusText { get; set; }
        public string ItemsHeading { get; set; }
    }

    /// <summary>A plain headed block of a page: presets and settings made from the schema, the rarely changed ones behind "More", and often a specialised editor.</summary>
    public class GroupViewModel : ViewModelBase
    {
        private bool visible = true;
        private bool moreOpen, forceMore, resetPending;
        private int changedCount;

        public GroupViewModel(string title, IList<SettingViewModel> settings)
            : this(null, title, settings, new List<SettingViewModel>())
        {
        }

        public GroupViewModel(string id, string title, IList<SettingViewModel> main, IList<SettingViewModel> extra)
        {
            Id = id;
            Title = title;
            Main = main;
            Extra = extra;
            Settings = main.Concat(extra).ToList();
            SeeAlso = new List<SeeAlsoViewModel>();
            Regions = new string[0];
            ToggleMoreCommand = new RelayCommand(() => MoreOpen = !MoreOpen);
        }

        public string Id { get; private set; }
        public string Title { get; private set; }
        public string PageId { get; set; }

        /// <summary>True when the block is the only one of its page and has the page's own name: the page title already says it.</summary>
        public bool TitleHidden { get; set; }

        /// <summary>Every generated setting of the block, the ones behind "More" included.</summary>
        public IList<SettingViewModel> Settings { get; private set; }

        /// <summary>The presets and settings always shown.</summary>
        public IList<SettingViewModel> Main { get; private set; }

        /// <summary>The rarely changed settings, behind the block's one "More" link.</summary>
        public IList<SettingViewModel> Extra { get; private set; }

        public bool HasMain { get { return Main.Count > 0; } }
        public bool HasExtra { get { return Extra.Count > 0; } }

        /// <summary>The specialised editor shown in the block (view model), its kind (background, sections...), or null.</summary>
        public object Editor { get; set; }
        public string EditorKind { get; set; }
        public bool HasEditor { get { return Editor != null; } }

        public AddonViewModel Addon { get; set; }
        public bool HasAddon { get { return Addon != null; } }

        public IList<SeeAlsoViewModel> SeeAlso { get; private set; }
        public bool HasSeeAlso { get { return SeeAlso.Count > 0; } }

        /// <summary>The rectangles of the window plan this block lights.</summary>
        public string[] Regions { get; set; }

        /// <summary>Words a search can find in a block that has an editor or an add-on card (its settings are not generated).</summary>
        public string Keywords { get; set; }

        /// <summary>Constants this block's editor (or its presets) sets, with no generated setting: counted and reset with the block.</summary>
        public IList<IList<string>> OwnedControls { get; set; }

        /// <summary>Constants the editor edits one by one (a corner, a size, a slider of the background): each counts on its own when it is not at its default.</summary>
        public IList<string> CountedKeys { get; set; }
        public IList<string> OwnedPresets { get; set; }

        /// <summary>True when the block homes options that have no generated setting (an editor's, or set only by a preset): the block is found by their names.</summary>
        public bool HasOwned { get; set; }

        /// <summary>Called when the user wants to put the block back: set by the shell.</summary>
        public Action ResetHandler { get; set; }
        public Action<GroupViewModel> NavigateHandler { get; set; }

        public bool Visible
        {
            get { return visible; }
            set { if (visible != value) { visible = value; Raise("Visible"); } }
        }

        /// <summary>The "More" part is open (the user's choice).</summary>
        public bool MoreOpen
        {
            get { return moreOpen; }
            set { if (moreOpen != value) { moreOpen = value; Raise("MoreOpen"); Raise("ExtraShown"); Raise("MoreLabel"); } }
        }

        /// <summary>True while a search or "changed only" finds something behind "More": it opens by itself.</summary>
        public bool ForceMore
        {
            get { return forceMore; }
            set { if (forceMore != value) { forceMore = value; Raise("ExtraShown"); } }
        }

        public bool ExtraShown { get { return HasExtra && (moreOpen || forceMore); } }
        public string MoreText { get; set; }
        public string LessText { get; set; }
        public string MoreLabel
        {
            get
            {
                string word = moreOpen ? (LessText ?? "Less") : (MoreText ?? "More");
                return moreOpen ? word : word + " (" + Extra.Count + ")";
            }
        }
        public RelayCommand ToggleMoreCommand { get; private set; }

        /// <summary>Settings of this block (editor included) that differ from the defaults.</summary>
        public int ChangedCount
        {
            get { return changedCount; }
            set { if (changedCount != value) { changedCount = value; Raise("ChangedCount"); Raise("HasChanges"); Raise("ResetLabel"); } }
        }
        public bool HasChanges { get { return changedCount > 0; } }

        public string ResetText { get; set; }
        public string ResetConfirmText { get; set; }

        /// <summary>"Reset (3)", or the question of the second click while a big reset waits.</summary>
        public string ResetLabel
        {
            get
            {
                string word = ResetText ?? "Reset";
                if (resetPending) return string.Format(CultureInfo.InvariantCulture, ResetConfirmText ?? "Click again to reset {0}", changedCount);
                return changedCount > 0 ? word + " (" + changedCount + ")" : word;
            }
        }

        public bool ResetPending
        {
            get { return resetPending; }
            set { if (resetPending != value) { resetPending = value; Raise("ResetPending"); Raise("ResetLabel"); } }
        }

        private RelayCommand resetCommand;
        public RelayCommand ResetCommand
        {
            get { return resetCommand ?? (resetCommand = new RelayCommand(() => { if (ResetHandler != null) ResetHandler(); }, () => changedCount > 0)); }
        }
        public void RaiseResetCanExecute() { if (resetCommand != null) resetCommand.RaiseCanExecuteChanged(); }

        /// <summary>The sidebar entry of this block: opens its page and scrolls to it.</summary>
        public RelayCommand NavigateCommand
        {
            get { return new RelayCommand(() => { if (NavigateHandler != null) NavigateHandler(this); }); }
        }

        /// <summary>Raised when the page should bring this block into view (a sidebar click, a see-also link, a search hit).</summary>
        public event Action ScrollRequested;

        /// <summary>True from the request until the view has scrolled (the block may not be drawn yet when the request is made).</summary>
        public bool ScrollPending { get; set; }

        public void RequestScroll()
        {
            ScrollPending = true;
            var h = ScrollRequested;
            if (h != null) h();
        }
    }

    /// <summary>A sub-category button at the top of a long settings page (older layout): shows one group, or all of them.</summary>
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

        /// <summary>A page of the new navigation: a title, a summary, a small plan of the window and plain headed blocks (no sub-category buttons).</summary>
        public PageViewModel(string id, string title, string summary, string[] regions, IList<GroupViewModel> blocks)
        {
            Id = id;
            Title = title;
            Summary = summary;
            Groups = blocks;
            Kind = "settings";
            Group = "";
            IsComposite = true;
            ShowCount = true;
            Chips = new List<GroupChipViewModel>();
            Plan = WireframePlan.ForRegions(regions) ?? WireframePlan.None;
            foreach (var b in blocks) { b.PageId = id; b.TitleHidden = blocks.Count == 1 && string.Equals(b.Title, title, StringComparison.CurrentCultureIgnoreCase); }
            matchCount = blocks.SelectMany(g => g.Settings).Count();
        }

        /// <summary>True for a page of blocks (the ten pages of the editor): its content is the block list under a small plan of the window.</summary>
        public bool IsComposite { get; private set; }
        public string Summary { get; private set; }

        /// <summary>The plan of the window with the rectangles this page controls lit; null for a page that has none.</summary>
        public WireframePlan Plan { get; private set; }
        public bool HasPlan { get { return Plan != null; } }

        /// <summary>False for a page with the one block: the sidebar does not list a block that only repeats the page name.</summary>
        public bool HasSeveralBlocks { get { return Groups.Count > 1; } }

        /// <summary>The blocks of the page (same list as Groups).</summary>
        public IList<GroupViewModel> Blocks { get { return Groups; } }

        /// <summary>How many options live on this page (shown in the sidebar).</summary>
        public int OptionCount { get; set; }
        public string CountText { get { return !IsComposite ? MatchCount.ToString(CultureInfo.InvariantCulture) : Searching ? MatchCount.ToString(CultureInfo.InvariantCulture) : OptionCount == 0 ? "" : OptionCount.ToString(CultureInfo.InvariantCulture); } }
        private bool searching;
        public bool Searching { get { return searching; } set { if (searching != value) { searching = value; Raise("Searching"); Raise("CountText"); } } }

        private bool current;
        /// <summary>The page shown now: the sidebar lists its blocks under it.</summary>
        public bool IsCurrent { get { return current; } set { if (current != value) { current = value; Raise("IsCurrent"); } } }

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
            Group = kind == "advice" ? "ADVICE" : kind == "sections" || kind == "items" || kind == "sizes" || kind == "tiles" || kind == "corners" ? "LAYOUT"
                : kind == "background" || kind == "colors" || kind == "rules" ? "LOOK" : kind == "share" || kind == "diagnostics" ? "PROFILE" : "SETTINGS";
        }

        /// <summary>The sidebar heading this page is listed under in the older layout: LAYOUT, LOOK, SETTINGS or PROFILE.</summary>
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

        /// <summary>"settings" for generated pages (and the pages of blocks), otherwise the visual editor's kind (sections, sizes, ...).</summary>
        public string Kind { get; private set; }
        public object Editor { get; private set; }
        public bool ShowCount { get; private set; }

        public IEnumerable<SettingViewModel> Settings { get { return Groups.SelectMany(g => g.Settings); } }

        /// <summary>Settings of this page that match the current search.</summary>
        public int MatchCount
        {
            get { return matchCount; }
            set { if (matchCount != value) { matchCount = value; Raise("MatchCount"); Raise("CountText"); } }
        }
    }
}
