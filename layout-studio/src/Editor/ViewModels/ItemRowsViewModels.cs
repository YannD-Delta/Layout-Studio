using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    /// <summary>A header card type that is not on the row yet: one button adds it.</summary>
    public class CardChoiceViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ItemTitle { get; set; }
        public RelayCommand AddCommand { get; set; }
    }

    /// <summary>
    /// One row of the Items page: an item in one place of the game page (a chip of the artwork header, a header card, a row of the information
    /// panel, a section...). The row holds what you decide about it there: shown or not, a second switch (new line, open by default), the order
    /// when the place has one, and a short list of rarer options behind "more".
    /// </summary>
    public class ItemRowViewModel : ViewModelBase, IDragSource, IDragEndAware
    {
        private bool visible = true, moreOpen, dropLineAbove, startsBoxShown;
        private int boxNumber;
        private int index;

        public string Zone { get; set; }
        /// <summary>Who the row is inside its place: the item id for chips, the position for cards.</summary>
        public string RowKey { get; set; }
        public string ItemId { get; set; }
        public string Title { get; set; }
        /// <summary>Words under the name: what this place does for the item.</summary>
        public string Description { get; set; }
        public string[] Regions { get; set; }
        /// <summary>More words a search matches besides the title (the item's id and English name, the card type's id).</summary>
        public string SearchText { get; set; }
        /// <summary>A short word at the end of the row (the column of a section).</summary>
        public string Badge { get { return Zone == ItemZones.Sections ? Description : null; } }

        /// <summary>"Also shown in: header card, section": the other places that show the item now; set by the page.</summary>
        public Func<string> Also { get; set; }
        public string Details { get { return Also == null ? null : Also(); } }
        public bool NeedsRestart { get { return false; } }
        public string Error { get { return null; } }

        public WireframePlan Plan { get { return Regions == null ? null : WireframePlan.ForRegions(Regions); } }
        public bool HasPlan { get { return Plan != null; } }

        // ---- what the row controls -------------------------------------------------------------------------------------------------

        /// <summary>The main switch of the item in this place (null: the place has none, or the item is always shown).</summary>
        public SettingViewModel Show { get; set; }
        public bool HasShow { get { return Show != null; } }
        /// <summary>A word next to the switch when the item's name does not say enough ("HowLongToBeat time on the cover").</summary>
        public string ShowLabel { get; set; }
        public bool HasShowLabel { get { return !string.IsNullOrEmpty(ShowLabel); } }
        /// <summary>The second switch of the row ("start a new line", "open by default"); its title is the tooltip.</summary>
        public SettingViewModel Flag { get; set; }
        public bool HasFlag { get { return Flag != null; } }
        /// <summary>The rarer options of the item in this place, behind the "more" button.</summary>
        public IList<SettingViewModel> More { get; set; }
        public bool HasMore { get { return More != null && More.Count > 0; } }

        // The three controls at the end of a row keep their column from one row to the next: a row without the control leaves a hole when
        // another row of the same place has it ("Hidden"), and nothing at all when no row has it ("Collapsed").
        private bool listHasShow, listHasFlag, listHasMore;
        public string ShowSlot { get { return HasShow ? "Visible" : listHasShow ? "Hidden" : "Collapsed"; } }
        public string FlagSlot { get { return HasFlag ? "Visible" : listHasFlag ? "Hidden" : "Collapsed"; } }
        public string MoreSlot { get { return HasMore ? "Visible" : listHasMore ? "Hidden" : "Collapsed"; } }

        public void SetSlots(bool anyShow, bool anyFlag, bool anyMore)
        {
            if (listHasShow == anyShow && listHasFlag == anyFlag && listHasMore == anyMore) return;
            listHasShow = anyShow; listHasFlag = anyFlag; listHasMore = anyMore;
            Raise("ShowSlot"); Raise("FlagSlot"); Raise("MoreSlot");
        }
        public RelayCommand ToggleMoreCommand { get; private set; }
        public RelayCommand FoldCommand { get; set; }
        public string FoldText { get; set; }
        /// <summary>Set by the page: the item is shown twice and the section can be folded away.</summary>
        public Func<bool> ShowsTwice { get; set; }
        public bool IsShownTwice { get { return ShowsTwice != null && ShowsTwice(); } }
        public string TwiceText { get; set; }

        /// <summary>True when something in the row differs from the theme's default (the orange dot).</summary>
        public Func<bool> ExtraModified { get; set; }
        public bool IsModified
        {
            get
            {
                if (ExtraModified != null && ExtraModified()) return true;
                if (Show != null && Show.IsModified) return true;
                if (Flag != null && Flag.IsModified) return true;
                return More != null && More.Any(s => s.IsModified);
            }
        }

        /// <summary>The keys of every setting of the row, to find the rows a changed constant touches.</summary>
        public IEnumerable<string> Keys
        {
            get
            {
                var all = new List<SettingViewModel>();
                if (Show != null) all.Add(Show);
                if (Flag != null) all.Add(Flag);
                if (More != null) all.AddRange(More);
                return all.SelectMany(s => s.Keys);
            }
        }

        public ItemRowViewModel()
        {
            ToggleMoreCommand = new RelayCommand(() => IsMoreOpen = !IsMoreOpen, () => HasMore);
            MoveUpCommand = new RelayCommand(() => { if (Move != null) Move(this, -1); }, () => CanReorder && Index > 0);
            MoveDownCommand = new RelayCommand(() => { if (Move != null) Move(this, +1); }, () => CanReorder && Index < LastIndex);
            RemoveCommand = new RelayCommand(() => { if (Remove != null) Remove(this); });
        }

        public bool IsMoreOpen
        {
            get { return moreOpen; }
            set { if (moreOpen != value) { moreOpen = value; Raise("IsMoreOpen"); Raise("MoreShown"); } }
        }

        /// <summary>The settings under the row: built only while the row is open, so a page of 80 rows stays light.</summary>
        public IList<SettingViewModel> MoreShown
        {
            get { return moreOpen && More != null ? More : (IList<SettingViewModel>)new SettingViewModel[0]; }
        }

        /// <summary>Whether a search or the "changed only" filter keeps the row.</summary>
        public bool Visible { get { return visible; } set { if (visible != value) { visible = value; Raise("Visible"); } } }

        // ---- order -----------------------------------------------------------------------------------------------------------------

        public bool CanReorder { get; set; }
        public int LastIndex { get; set; }
        public int Index { get { return index; } set { if (index != value) { index = value; Raise("Index"); MoveUpCommand.RaiseCanExecuteChanged(); MoveDownCommand.RaiseCanExecuteChanged(); } } }
        public RelayCommand MoveUpCommand { get; private set; }
        public RelayCommand MoveDownCommand { get; private set; }
        /// <summary>Moves the row by a number of places (set by the page).</summary>
        public Action<ItemRowViewModel, int> Move { get; set; }

        public bool DropLineAbove { get { return dropLineAbove; } set { if (dropLineAbove != value) { dropLineAbove = value; Raise("DropLineAbove"); } } }

        public string DragFormat { get { return DragFormats.ItemRow; } }
        public string DragPayload { get { return Zone + "|" + RowKey; } }
        public Action OnDragEnded { get; set; }
        public void DragEnded() { if (OnDragEnded != null) OnDragEnded(); }

        // ---- header cards ----------------------------------------------------------------------------------------------------------

        public bool IsCard { get; set; }
        public RelayCommand RemoveCommand { get; private set; }
        /// <summary>Takes the card off the row (set by the page).</summary>
        public Action<ItemRowViewModel> Remove { get; set; }
        /// <summary>Sets whether the card starts a new glass box (set by the page).</summary>
        public Action<ItemRowViewModel, bool> SetStartsBox { get; set; }

        /// <summary>True when the card starts a new glass box; false when it sits in the box of the card before it.</summary>
        public bool StartsBox
        {
            get { return startsBoxShown; }
            set { if (startsBoxShown != value && SetStartsBox != null) SetStartsBox(this, value); }
        }

        public void SetBox(bool starts, int number)
        {
            if (startsBoxShown != starts) { startsBoxShown = starts; Raise("StartsBox"); Raise("JoinsBox"); }
            if (boxNumber != number) { boxNumber = number; Raise("BoxText"); }
        }

        public bool JoinsBox { get { return !startsBoxShown; } }
        public string BoxFormat { get; set; }
        public string BoxText { get { return string.Format(CultureInfo.InvariantCulture, BoxFormat ?? "Box {0}", boxNumber); } }

        public void Refresh()
        {
            Raise("IsModified");
            Raise("Details");
            Raise("IsShownTwice");
            Raise("HasShow");
        }
    }

    /// <summary>One place of the game page and the flat list of the items it shows, in the order they have on the page.</summary>
    public class ItemZoneViewModel : ViewModelBase
    {
        private bool lookOpen, dropLineAtEnd;

        public string Id { get; set; }
        public string Title { get; set; }
        public string Hint { get; set; }
        public string[] Regions { get; set; }
        public ObservableCollection<ItemRowViewModel> Rows { get; private set; }

        public ItemZoneViewModel()
        {
            Rows = new ObservableCollection<ItemRowViewModel>();
            Available = new ObservableCollection<CardChoiceViewModel>();
            ToggleLookCommand = new RelayCommand(() => IsLookOpen = !IsLookOpen, () => HasLook);
            Rows.CollectionChanged += (s, e) => { Raise("HasVisibleRows"); Raise("VisibleCount"); };
        }

        /// <summary>The switch of the whole place (show the chips, show the header bar).</summary>
        public SettingViewModel Master { get; set; }
        public bool HasMaster { get { return Master != null; } }

        /// <summary>How the place looks: sizes, spacing, glow... behind the "look" button.</summary>
        public IList<SettingViewModel> Look { get; set; }
        public bool HasLook { get { return Look != null && Look.Count > 0; } }
        public RelayCommand ToggleLookCommand { get; private set; }
        public bool IsLookOpen { get { return lookOpen; } set { if (lookOpen != value) { lookOpen = value; Raise("IsLookOpen"); Raise("LookShown"); } } }
        public IList<SettingViewModel> LookShown { get { return lookOpen && Look != null ? Look : (IList<SettingViewModel>)new SettingViewModel[0]; } }

        /// <summary>Header cards that are not on the row (cards place only).</summary>
        public ObservableCollection<CardChoiceViewModel> Available { get; private set; }
        public bool HasAvailable { get { return Available.Count > 0; } }
        public string AddLabel { get; set; }
        public string FullText { get; set; }
        public bool IsFull { get; set; }

        /// <summary>Opens another page for what this place cannot do itself (the sections page for the order of the sections).</summary>
        public RelayCommand LinkCommand { get; set; }
        public string LinkText { get; set; }
        public bool HasLink { get { return LinkCommand != null; } }

        public bool CanReorder { get; set; }
        public int VisibleCount { get { return Rows.Count(r => r.Visible); } }
        public bool HasVisibleRows { get { return Rows.Any(r => r.Visible); } }
        public bool IsModified { get; set; }

        /// <summary>False while a search, "changed only" or a single item is on: the tools of the place (look, add a card) step aside.</summary>
        public bool ShowTools { get { return showTools; } set { if (showTools != value) { showTools = value; Raise("ShowTools"); Raise("IsShown"); } } }
        /// <summary>A setting of the place's look matched the search.</summary>
        public bool LookMatched { get { return lookMatched; } set { if (lookMatched != value) { lookMatched = value; Raise("LookMatched"); Raise("IsShown"); } } }
        private bool showTools = true, lookMatched;

        /// <summary>Whether the place shows at all: it has a row to show, or tools and nothing narrows the page down.</summary>
        public bool IsShown { get { return HasVisibleRows || (showTools && (HasMaster || HasLook || HasAvailable || HasLink)) || lookMatched; } }

        public bool DropLineAtEnd { get { return dropLineAtEnd; } private set { if (dropLineAtEnd != value) { dropLineAtEnd = value; Raise("DropLineAtEnd"); } } }

        /// <summary>Moves a row of this place to a place in the list (set by the page).</summary>
        public Func<string, int, bool> Reorder { get; set; }

        public void Refresh()
        {
            Raise("HasAvailable");
            Raise("IsFull");
            Raise("IsModified");
            Raise("HasVisibleRows");
            Raise("VisibleCount");
            Raise("IsShown");
        }

        /// <summary>Numbers the rows and tells each how far the list goes, so the up and down buttons know their limits.</summary>
        public void Renumber()
        {
            bool anyShow = Rows.Any(r => r.HasShow), anyFlag = Rows.Any(r => r.HasFlag), anyMore = Rows.Any(r => r.HasMore);
            foreach (var r in Rows) r.SetSlots(anyShow, anyFlag, anyMore);
            for (int i = 0; i < Rows.Count; i++)
            {
                Rows[i].LastIndex = Rows.Count - 1;
                Rows[i].Index = i;
                Rows[i].MoveUpCommand.RaiseCanExecuteChanged();
                Rows[i].MoveDownCommand.RaiseCanExecuteChanged();
            }
        }

        // ---- drag and drop ---------------------------------------------------------------------------------------------------------

        private string KeyOf(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return null;
            int bar = payload.IndexOf('|');
            return bar > 0 && payload.Substring(0, bar) == Id ? payload.Substring(bar + 1) : null;
        }

        /// <summary>Whether a dragged row belongs to this place and the place can be reordered.</summary>
        public bool CanDrop(string payload) { return CanReorder && KeyOf(payload) != null && Rows.Any(r => r.RowKey == KeyOf(payload)); }

        /// <summary>Shows the insertion line for a drag over the list at a place; no line where the drop is refused.</summary>
        public void SetDropTarget(string payload, int index)
        {
            ClearDropTarget();
            if (!CanDrop(payload)) return;
            if (index >= 0 && index < Rows.Count) Rows[index].DropLineAbove = true;
            else DropLineAtEnd = true;
        }

        public void ClearDropTarget()
        {
            foreach (var r in Rows) r.DropLineAbove = false;
            DropLineAtEnd = false;
        }

        /// <summary>The row goes to a place among the rows as shown, the dragged one included: 3 means "before the row shown 4th".</summary>
        public void Drop(string payload, int index)
        {
            ClearDropTarget();
            string key = KeyOf(payload);
            var row = key == null ? null : Rows.FirstOrDefault(r => r.RowKey == key);
            if (row == null || Reorder == null) return;
            int current = Rows.IndexOf(row);
            if (index > current) index--;   // the row is taken out first: a place further down shifts up by one
            Reorder(key, index);
        }
    }
}
