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
                if (!fromSlider) sliderRaw = null;   // set any way but by the slider: the slider shows the stored value exactly
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

        private double? sliderRaw;   // what the slider last wrote, before rounding
        private bool fromSlider;

        /// <summary>
        /// The slider position of this element: while the stored radius is the rounded value of what the slider wrote, the slider gets what it wrote
        /// (a slider drags relative to the value it is given back: the rounded one would make a slow drag of under half a pixel go nowhere).
        /// </summary>
        public double SliderRadius
        {
            get
            {
                double stored = Radius;
                if (sliderRaw.HasValue && Math.Abs(Math.Min(MaxRadius, Math.Max(0, Math.Round(sliderRaw.Value))) - stored) < 1e-9) return Math.Min(MaxRadius, Math.Max(0, sliderRaw.Value));
                sliderRaw = null;
                return stored;
            }
            set { sliderRaw = value; fromSlider = true; try { Radius = value; } finally { fromSlider = false; } }
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
            SharpCommand = new RelayCommand(() => ApplyLook(0));
            SoftCommand = new RelayCommand(() => ApplyLook(0.6));
            AppleCommand = new RelayCommand(() => ApplyLook(1));
            PillCommand = new RelayCommand(() => ApplyLook(1.5));
            store.ValueChanged += key =>
            {
                if (applying || !Items.Any(i => i.Key == key)) return;
                held = null;   // an element changed on its own (undo, reset, its own slider): the master is the average again
                Raise("Master");
            };
        }

        /// <summary>
        /// The designed set of radii in pixels (100 % of the Designed look): nested corners step down from the card to the chips.
        /// An element missing here uses its Round value.
        /// </summary>
        public static readonly Dictionary<string, double> Designed = new Dictionary<string, double>
        {
            { "HeroCardCornerRadius", 28 }, { "SectionCornerRadius", 20 }, { "HeaderTileCornerRadius", 18 }, { "HeroCoverCornerRadius", 18 },
            { "HeroButtonCornerRadius", 16 }, { "HeroChipCornerRadius", 14 }, { "DetailsViewCoverCornerRadius", 14 },
            { "DetailsViewExtraButtonsCornerRadius", 10 }, { "ControlCornerRadius", 10 }, { "PopupCornerRadius", 18 },
            { "ListItemCornerRadius", 12 }, { "SidebarItemCornerRadius", 14 }, { "GridViewCoverCornerRadius", 12 }, { "GridViewSelectionBorderCornerRadius", 15 }
        };

        public RelayCommand SharpCommand { get; private set; }
        public RelayCommand SoftCommand { get; private set; }
        public RelayCommand AppleCommand { get; private set; }
        public RelayCommand PillCommand { get; private set; }

        /// <summary>Sets every element to a share of its designed radius (one undo step).</summary>
        private void ApplyLook(double share)
        {
            applying = true;
            history.BeginBatch();
            try
            {
                foreach (var item in Items)
                {
                    double designed;
                    if (!Designed.TryGetValue(item.Key, out designed)) designed = item.Round;
                    item.Radius = designed * share;
                }
            }
            finally { history.EndBatch(); applying = false; }
            held = null;   // the master reads the elements again
            Raise("Master");
        }

        private bool applying;
        private double? held;  // the value the master slider wrote last; radii are whole pixels, so the average cannot give it back exactly

        public IList<CornerItemViewModel> Items { get; private set; }

        /// <summary>
        /// Percent of the Round look. While the elements still match what the slider wrote it is exactly that value (a slider drags
        /// relative to its current value, so a reading that differs from what it wrote makes the thumb drift or stick); once an
        /// element changes on its own it is the average of every element's radius relative to its Round value.
        /// </summary>
        public double Master
        {
            get
            {
                if (held.HasValue) return held.Value;
                var ratios = Items.Where(i => i.Round > 0).Select(i => Math.Min(i.Radius, CornerItemViewModel.MaxRadius) / i.Round).ToList();
                return ratios.Count == 0 ? 0 : Math.Round(ratios.Average() * 100);
            }
            set
            {
                double pct = Math.Min(150, Math.Max(0, value));
                applying = true;
                history.BeginBatch();
                try { foreach (var item in Items) item.Radius = item.Round * pct / 100.0; }
                finally { history.EndBatch(); applying = false; }
                held = pct;
                Raise("Master");
            }
        }

        public void BeginGesture() { if (!gesture) { history.BeginBatch(); gesture = true; } }
        public void EndGesture() { if (gesture) { gesture = false; history.EndBatch(); } }
    }
}
