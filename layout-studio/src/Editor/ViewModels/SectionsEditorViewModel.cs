using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    public class SectionBlockViewModel : ViewModelBase, IDragSource
    {
        public string DragFormat { get { return DragFormats.Section; } }
        public string DragPayload { get { return Id; } }

        private bool visible, openByDefault, canMove;

        public string Id { get; set; }
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

        public ObservableCollection<SectionBlockViewModel> Left { get; private set; }
        public ObservableCollection<SectionBlockViewModel> Right { get; private set; }

        /// <summary>Moves a block to a column and index (one undo step). Fixed sections only reorder inside their column.</summary>
        public void Drop(string sectionId, int column, int index)
        {
            if (string.IsNullOrEmpty(sectionId)) return;
            history.BeginBatch();
            try { WireframeModel.Move(store, schema, sectionId, column, index); }
            finally { history.EndBatch(); }
            Rebuild();
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
            Fill(Left, slots.Where(s => s.Column == 0));
            Fill(Right, slots.Where(s => s.Column == 1));
        }

        private readonly Dictionary<string, SectionBlockViewModel> blocks = new Dictionary<string, SectionBlockViewModel>();

        /// <summary>Updates the existing block objects in place, so bindings and drag sources keep their identity.</summary>
        private void Fill(ObservableCollection<SectionBlockViewModel> target, IEnumerable<SectionSlot> slots)
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
                    blocks[s.Id] = block;
                }
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
