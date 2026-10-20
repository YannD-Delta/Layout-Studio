using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    /// <summary>A drag source that wants to know when its drag is over (dropped or cancelled), to tidy up the drop cues.</summary>
    public interface IDragEndAware
    {
        void DragEnded();
    }

    public class SectionBlockViewModel : ViewModelBase, IDragSource, IDragEndAware
    {
        public string DragFormat { get { return DragFormats.Section; } }
        public string DragPayload { get { return Id; } }

        /// <summary>Set by the editor: clears the drop line once the drag is over.</summary>
        public System.Action OnDragEnded { get; set; }
        public void DragEnded() { if (OnDragEnded != null) OnDragEnded(); }

        private bool visible, openByDefault, canMove, dropLineAbove;
        private int column;

        public string Id { get; set; }

        /// <summary>0 = left column, 1 = right column.</summary>
        public int Column
        {
            get { return column; }
            set { if (column != value) { column = value; Raise("Column"); } }
        }

        /// <summary>The insertion line shown above this block while a section is dragged over its column.</summary>
        public bool DropLineAbove
        {
            get { return dropLineAbove; }
            set { if (dropLineAbove != value) { dropLineAbove = value; Raise("DropLineAbove"); } }
        }
        public string Title { get; set; }
        public bool CanHide { get; set; }
        public bool NeedsRestart { get; set; }

        public bool Visible
        {
            get { return visible; }
            set { if (visible != value) { visible = value; Raise("Visible"); } }
        }

        public bool OpenByDefault
        {
            get { return openByDefault; }
            set { if (openByDefault != value) { openByDefault = value; Raise("OpenByDefault"); } }
        }

        public bool CanMove
        {
            get { return canMove; }
            set { if (canMove != value) { canMove = value; Raise("CanMove"); } }
        }
        public RelayCommand ToggleVisibleCommand { get; set; }
        public RelayCommand ToggleOpenCommand { get; set; }

        /// <summary>The left/right button: sends the section to the end of the other column (only for sections that can change column).</summary>
        public RelayCommand MoveColumnCommand { get; set; }

        /// <summary>Opens the Items page on the card of this section, with every option the theme has for it. Null when the section has no item card.</summary>
        public RelayCommand OptionsCommand { get; set; }
        public bool HasOptions { get { return OptionsCommand != null; } }
    }

    /// <summary>The two-column wireframe of the Details view: blocks to drag between and inside the columns.</summary>
    public class SectionsEditorViewModel : ViewModelBase
    {
        private readonly ConstantStore store;
        private readonly Schema schema;
        private readonly History history;

        public SectionsEditorViewModel(ConstantStore store, Schema schema, History history)
        {
            this.store = store;
            this.schema = schema;
            this.history = history;
            Left = new ObservableCollection<SectionBlockViewModel>();
            Right = new ObservableCollection<SectionBlockViewModel>();
            Rebuild();
            store.ValueChanged += key => Rebuild();
        }

        /// <summary>Set by the shell: opens the Items page on the card with this id.</summary>
        public System.Action<string> OpenItemOptions { get; set; }

        public ObservableCollection<SectionBlockViewModel> Left { get; private set; }
        public ObservableCollection<SectionBlockViewModel> Right { get; private set; }

        private bool leftDropLineAtEnd, rightDropLineAtEnd;

        /// <summary>The insertion line after the last block of the left column.</summary>
        public bool LeftDropLineAtEnd
        {
            get { return leftDropLineAtEnd; }
            private set { if (leftDropLineAtEnd != value) { leftDropLineAtEnd = value; Raise("LeftDropLineAtEnd"); } }
        }

        /// <summary>The insertion line after the last block of the right column.</summary>
        public bool RightDropLineAtEnd
        {
            get { return rightDropLineAtEnd; }
            private set { if (rightDropLineAtEnd != value) { rightDropLineAtEnd = value; Raise("RightDropLineAtEnd"); } }
        }

        /// <summary>
        /// Moves a block to a column and a place (one undo step). The index is a place among the blocks as they are shown, the dragged block
        /// included: 3 means "before the block that is shown 4th". Fixed sections only reorder inside their column.
        /// </summary>
        public void Drop(string sectionId, int column, int index)
        {
            ClearDropTarget();
            if (string.IsNullOrEmpty(sectionId)) return;
            SectionBlockViewModel dragged;
            if (blocks.TryGetValue(sectionId, out dragged) && dragged.Column == column)
            {
                // the model takes the block out first: a place further down shifts up by one
                int current = (column == 0 ? Left : Right).IndexOf(dragged);
                if (index > current) index--;
            }
            history.BeginBatch();
            try { WireframeModel.Move(store, schema, sectionId, column, index); }
            finally { history.EndBatch(); }
            Rebuild();
        }

        /// <summary>The left/right button: the section goes to the end of the other column.</summary>
        public void MoveToOtherColumn(SectionBlockViewModel block)
        {
            if (block == null || !block.CanMove) return;
            int target = 1 - block.Column;
            Drop(block.Id, target, (target == 0 ? Left : Right).Count);
        }

        /// <summary>Whether a section may be dropped on a column: any section reorders inside its own column, only movable ones change column.</summary>
        public bool CanDrop(string sectionId, int column)
        {
            SectionBlockViewModel block;
            if (string.IsNullOrEmpty(sectionId) || (column != 0 && column != 1) || !blocks.TryGetValue(sectionId, out block)) return false;
            return block.Column == column || block.CanMove;
        }

        /// <summary>Shows the insertion line for a drag over a column at a place (see <see cref="Drop"/>); no line where the drop is refused.</summary>
        public void SetDropTarget(string sectionId, int column, int index)
        {
            ClearDropTarget();
            if (!CanDrop(sectionId, column)) return;
            var list = column == 0 ? Left : Right;
            if (index >= 0 && index < list.Count) list[index].DropLineAbove = true;
            else if (column == 0) LeftDropLineAtEnd = true;
            else RightDropLineAtEnd = true;
        }

        public void ClearDropTarget()
        {
            foreach (var b in Left) b.DropLineAbove = false;
            foreach (var b in Right) b.DropLineAbove = false;
            LeftDropLineAtEnd = false;
            RightDropLineAtEnd = false;
        }

        public void ToggleVisible(SectionBlockViewModel block)
        {
            if (block == null || !block.CanHide) return;
            history.BeginBatch();
            try { WireframeModel.SetVisible(store, schema, block.Id, !block.Visible); }
            finally { history.EndBatch(); }
            Rebuild();
        }

        public void ToggleOpen(SectionBlockViewModel block)
        {
            if (block == null) return;
            history.BeginBatch();
            try { WireframeModel.SetOpen(store, schema, block.Id, !block.OpenByDefault); }
            finally { history.EndBatch(); }
            Rebuild();
        }

        /// <summary>Where a block dropped at pointer height <paramref name="y"/> goes, given the heights of the column's blocks.</summary>
        public static int InsertionIndex(IList<double> heights, double y)
        {
            double top = 0;
            for (int i = 0; i < heights.Count; i++)
            {
                if (y < top + heights[i] / 2) return i;
                top += heights[i];
            }
            return heights.Count;
        }

        private void Rebuild()
        {
            var slots = WireframeModel.FromStore(store, schema);
            Fill(Left, slots.Where(s => s.Column == 0), 0);
            Fill(Right, slots.Where(s => s.Column == 1), 1);
        }

        private readonly Dictionary<string, SectionBlockViewModel> blocks = new Dictionary<string, SectionBlockViewModel>();

        /// <summary>Updates the existing block objects in place, so bindings and drag sources keep their identity.</summary>
        private void Fill(ObservableCollection<SectionBlockViewModel> target, IEnumerable<SectionSlot> slots, int column)
        {
            var wanted = new List<SectionBlockViewModel>();
            foreach (var s in slots.OrderBy(x => x.Index))
            {
                SectionBlockViewModel block;
                if (!blocks.TryGetValue(s.Id, out block))
                {
                    block = new SectionBlockViewModel { Id = s.Id, Title = s.Title, CanHide = s.CanHide, NeedsRestart = s.NeedsRestart };
                    var captured = block;
                    block.ToggleVisibleCommand = new RelayCommand(() => ToggleVisible(captured), () => captured.CanHide);
                    block.ToggleOpenCommand = new RelayCommand(() => ToggleOpen(captured));
                    block.MoveColumnCommand = new RelayCommand(() => MoveToOtherColumn(captured), () => captured.CanMove);
                    block.OnDragEnded = ClearDropTarget;
                    var def = schema.Sections.FirstOrDefault(d => d.Id == s.Id);
                    var family = def == null ? null : ItemFamilies.FamilyOfSection(def);
                    if (family != null) { string familyId = family.Id; block.OptionsCommand = new RelayCommand(() => { if (OpenItemOptions != null) OpenItemOptions(familyId); }); }
                    blocks[s.Id] = block;
                }
                block.Column = column;
                block.Visible = s.Visible;
                block.OpenByDefault = s.OpenByDefault;
                block.CanMove = s.CanMove;
                wanted.Add(block);
            }
            for (int i = target.Count - 1; i >= 0; i--)
                if (!wanted.Contains(target[i])) target.RemoveAt(i);
            for (int i = 0; i < wanted.Count; i++)
            {
                int at = target.IndexOf(wanted[i]);
                if (at < 0) target.Insert(i, wanted[i]);
                else if (at != i) target.Move(at, i);
            }
        }
    }
}
