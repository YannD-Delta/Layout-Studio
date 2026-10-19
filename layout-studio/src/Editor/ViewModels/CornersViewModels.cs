using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    /// <summary>One rounded element: a slider for its radius and a live preview shape.</summary>
    public class CornerItemViewModel : ViewModelBase
    {
        public const double MaxRadius = 60;
        private readonly CornerDef def;
        private readonly ConstantStore store;
        private readonly History history;

        public CornerItemViewModel(CornerDef def, ConstantStore store, History history)
        {
            this.def = def;
            this.store = store;
            this.history = history;
            ResetCommand = new RelayCommand(Reset, () => IsModified);
            store.ValueChanged += key =>
            {
                if (key == def.Key) { RaiseAll(); ResetCommand.RaiseCanExecuteChanged(); }
            };
        }

        public string Key { get { return def.Key; } }
        public string Title { get { return def.Title; } }
        public double Round { get { return def.Round; } }
        public RelayCommand ResetCommand { get; private set; }

        /// <summary>The radius in pixels; for four different corners, the first one.</summary>
        public double Radius
        {
            get { return Numbers(store.Get(def.Key))[0]; }
            set
            {
                double r = Math.Min(MaxRadius, Math.Max(0, Math.Round(value)));
                string error;
                history.BeginBatch();
                try
                {
                    store.TrySet(def.Key, ConstantValue.ToInvariant(r), out error);
                    if (def.Key == "GridViewCoverCornerRadius") SetGridCoverCorners(r);
                }
                finally { history.EndBatch(); }
                RaiseAll();
            }
        }

        public bool IsCustom { get { var n = Numbers(store.Get(def.Key)); return n.Any(x => Math.Abs(x - n[0]) > 1e-9); } }
        public bool IsModified { get { return store.Get(def.Key) != store.GetDefault(def.Key); } }
        public System.Windows.CornerRadius Shape { get { return new System.Windows.CornerRadius(Math.Min(Radius, 40)); } }

        /// <summary>The grid cover has four corner constants that must agree: the plate behind the name, the name plate, the selection ring (radius + its 3 px) and the on/off flag.</summary>
        private void SetGridCoverCorners(double r)
        {
            string error, n = ConstantValue.ToInvariant(r);
            store.TrySet("GridViewCoverGridBackgroundDisabledCornerRadius", n + "," + n + ",0,0", out error);
            store.TrySet("GridViewShowGameNameCornerRadius", "0,0," + n + "," + n, out error);
            store.TrySet("GridViewSelectionBorderCornerRadius", ConstantValue.ToInvariant(r > 0 ? r + 3 : 0), out error);
            store.TrySet("GridViewCoverUseRoundedCorners", r > 0 ? "True" : "False", out error);
        }

        private void Reset()
        {
            history.BeginBatch();
            try { store.Reset(def.Key); }
            finally { history.EndBatch(); }
        }

        private static double[] Numbers(string value)
        {
            return value.Split(',').Select(p => double.Parse(p, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
        }
    }

    /// <summary>The Corners page: one master slider scaling every element (0 = square, 100 = the Round look) and a slider per element.</summary>
    public class CornersEditorViewModel : ViewModelBase, IGestureTarget
    {
        private readonly History history;
        private bool gesture;

        public CornersEditorViewModel(ConstantStore store, Schema schema, History history)
        {
            this.history = history;
            Items = schema.Corners.Select(c => new CornerItemViewModel(c, store, history)).ToList();
            store.ValueChanged += key => { if (Items.Any(i => i.Key == key)) Raise("Master"); };
        }

        public IList<CornerItemViewModel> Items { get; private set; }

        /// <summary>Percent of the Round look: the average of every element's radius relative to its Round value.</summary>
        public double Master
        {
            get
            {
                var ratios = Items.Where(i => i.Round > 0).Select(i => Math.Min(i.Radius, CornerItemViewModel.MaxRadius) / i.Round).ToList();
                return ratios.Count == 0 ? 0 : Math.Round(ratios.Average() * 100);
            }
            set
            {
                double pct = Math.Min(150, Math.Max(0, value));
                history.BeginBatch();
                try { foreach (var item in Items) item.Radius = item.Round * pct / 100.0; }
                finally { history.EndBatch(); }
                Raise("Master");
            }
        }

        public void BeginGesture() { if (!gesture) { history.BeginBatch(); gesture = true; } }
        public void EndGesture() { if (gesture) { gesture = false; history.EndBatch(); } }
    }
}
