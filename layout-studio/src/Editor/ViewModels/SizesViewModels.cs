using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    [Flags]
    public enum DragModifiers { None = 0, Shift = 1, Ctrl = 2, Alt = 4 }

    /// <summary>One chip of the aspect-ratio row.</summary>
    public class RatioChoiceViewModel : ViewModelBase
    {
        private bool isSelected, isEnabled = true;

        public string Label { get; set; }
        public double? Value { get; set; }            // width / height; null = free
        public string DisabledReason { get; set; }
        public RelayCommand SelectCommand { get; set; }

        public bool IsSelected
        {
            get { return isSelected; }
            set { if (isSelected != value) { isSelected = value; Raise("IsSelected"); } }
        }

        public bool IsEnabled
        {
            get { return isEnabled; }
            set { if (isEnabled != value) { isEnabled = value; Raise("IsEnabled"); } }
        }
    }

    /// <summary>
    /// One box of the Sizes page: bound to a width and/or height constant, drawn at a fixed ladder scale, resized by drag handles
    /// per axis, optionally under an aspect-ratio lock. Movement is accumulated in real pixels so slow drags lose nothing.
    /// </summary>
    public class SizeItemViewModel : ViewModelBase, IGestureTarget
    {
        private const double LadderWidth = 340, LadderHeight = 160, BarWidth = 240, BarHeight = 28, DefaultRatioTolerance = 0.01;
        private static readonly double[] Ladder = { 4, 2, 1, 1 / 2.0, 1 / 3.0, 1 / 4.0, 1 / 6.0, 1 / 8.0 };
        private static readonly string[] ScaleLabels = { "×4", "×2", "×1", "×½", "×⅓", "×¼", "×⅙", "×⅛" };
        private static readonly KeyValuePair<string, double>[] RatioList =
        {
            new KeyValuePair<string, double>("1:1", 1), new KeyValuePair<string, double>("2:3", 2 / 3.0),
            new KeyValuePair<string, double>("3:4", 3 / 4.0), new KeyValuePair<string, double>("4:3", 4 / 3.0),
            new KeyValuePair<string, double>("16:9", 16 / 9.0), new KeyValuePair<string, double>("21:9", 21 / 9.0)
        };

        private readonly SizeDef def;
        private readonly ConstantStore store;
        private readonly History history;
        private readonly Schema schema;
        private readonly CultureInfo culture;
        private readonly IDictionary<string, string> editor;
        private readonly ConstantDef wDef, hDef;
        private string error;
        private bool gestureOpen, dragging, isActive, isVisible = true, widthClamped, heightClamped;
        private double startW, startH, accX, accY;
        private bool locked;
        private double ratio;     // width / height while locked or last used

        public SizeItemViewModel(SizeDef def, ConstantStore store, Schema schema, History history, CultureInfo culture, IDictionary<string, string> editor = null)
        {
            this.def = def;
            this.store = store;
            this.schema = schema;
            this.history = history;
            this.culture = culture;
            this.editor = editor ?? new Dictionary<string, string>();
            wDef = def.WidthKey == null ? null : store.Find(def.WidthKey);
            hDef = def.HeightKey == null ? null : store.Find(def.HeightKey);

            double maxW = HasWidth ? MaxW(true) : 0, maxH = HasHeight ? MaxH(true) : 0;
            int pick = Ladder.Length - 1;
            for (int i = 0; i < Ladder.Length; i++)
                if ((!HasWidth || maxW * Ladder[i] <= LadderWidth) && (!HasHeight || maxH * Ladder[i] <= LadderHeight)) { pick = i; break; }
            Scale = Ladder[pick];
            ScaleLabel = ScaleLabels[pick];

            ResetCommand = new RelayCommand(Reset, () => IsModified);
            ToggleLockCommand = new RelayCommand(ToggleLock, () => HasRatio);
            WidthUpCommand = new RelayCommand(() => Nudge(IsSquare ? "wh" : "w", +1, DragModifiers.None));
            WidthDownCommand = new RelayCommand(() => Nudge(IsSquare ? "wh" : "w", -1, DragModifiers.None));
            HeightUpCommand = new RelayCommand(() => Nudge("h", +1, DragModifiers.None));
            HeightDownCommand = new RelayCommand(() => Nudge("h", -1, DragModifiers.None));
            BuildRatios();
            LoadRatioState();
            store.ValueChanged += key =>
            {
                if (key == def.WidthKey || key == def.HeightKey) { RaiseAll(); ResetCommand.RaiseCanExecuteChanged(); }
            };
        }

        // ---- what it is -------------------------------------------------------------------------------------------

        public string Id { get { return def.Id; } }
        public string Title { get { return def.Title; } }
        public string Description { get { return def.Description; } }
        public string GroupId { get { return def.Group; } }
        public string WhereLabel { get { return def.WhereLabel; } }
        public string ViewChip
        {
            get
            {
                switch (def.View)
                {
                    case "details": return "Details view";
                    case "grid": return "Grid view";
                    case "details+grid": return "Details + Grid";
                    default: return "All views";
                }
            }
        }
        public string LiveText { get { return def.Live == "always" ? "Live" : "In the Details view"; } }
        public bool HasWidth { get { return def.WidthKey != null; } }
        public bool HasHeight { get { return def.HeightKey != null; } }
        public bool IsSquare { get { return def.WidthKey != null && def.WidthKey == def.HeightKey; } }
        public string Axes { get { return IsSquare ? "Square" : HasWidth && HasHeight ? "WH" : HasWidth ? "W" : "H"; } }
        public bool NeedsRestart { get { return (HasWidth && schema.NeedsRestart(def.WidthKey)) || (HasHeight && schema.NeedsRestart(def.HeightKey)); } }

        // ---- locator (a 200 x 100 miniature of the window) -----------------------------------------------------------

        public double LocatorX { get { return def.Rect[0] * 200; } }
        public double LocatorY { get { return def.Rect[1] * 100; } }
        public double LocatorWidth { get { return def.Rect[2] * 200; } }
        public double LocatorHeight { get { return def.Rect[3] * 100; } }
        public bool LocatorIsSmall { get { return def.Rect[2] * def.Rect[3] < 0.0015; } }
        public double LocatorBeaconX { get { return (def.Rect[0] + def.Rect[2] / 2) * 200 - 7; } }
        public double LocatorBeaconY { get { return (def.Rect[1] + def.Rect[3] / 2) * 100 - 7; } }

        // ---- drawing -----------------------------------------------------------------------------------------------

        public double Scale { get; private set; }
        public string ScaleLabel { get; private set; }
        public double Width { get { return HasWidth ? Number(def.WidthKey) : 0; } }
        public double Height { get { return HasHeight ? Number(def.HeightKey) : 0; } }
        public double DisplayWidth { get { return HasWidth ? Math.Max(8, Width * Scale) : BarWidth; } }
        public double DisplayHeight { get { return HasHeight ? Math.Max(8, Height * Scale) : BarHeight; } }
        public double MaxDisplayWidth { get { return HasWidth ? MaxW(true) * Scale : 0; } }
        public double MaxDisplayHeight { get { return HasHeight ? MaxH(true) * Scale : 0; } }

        public double MaxOutlineWidth { get { return HasWidth ? MaxDisplayWidth : DisplayWidth; } }
        public double MaxOutlineHeight { get { return HasHeight ? MaxDisplayHeight : DisplayHeight; } }
        public double StageHeight { get { return Math.Min(200, Math.Max(84, MaxOutlineHeight + 34)); } }

        // handles by axis: a bar on the right edge for the width, a bar on the bottom edge for the height, a corner bracket for both or a square
        public bool HasRightBar { get { return Axes == "W" || Axes == "WH"; } }
        public bool HasBottomBar { get { return Axes == "H" || Axes == "WH"; } }
        public bool HasCorner { get { return Axes == "WH" || Axes == "Square"; } }
        public bool ShowHeightField { get { return HasHeight && !IsSquare; } }
        public System.Windows.Thickness RightBarMargin { get { return new System.Windows.Thickness(DisplayWidth - 7, 0, 0, 0); } }
        public System.Windows.Thickness BottomBarMargin { get { return new System.Windows.Thickness(0, DisplayHeight - 7, 0, 0); } }
        public System.Windows.Thickness CornerMargin { get { return new System.Windows.Thickness(DisplayWidth - 9, DisplayHeight - 9, 0, 0); } }
        public System.Windows.Thickness BubbleMargin { get { return new System.Windows.Thickness(DisplayWidth + 14, Math.Max(0, DisplayHeight - 24), 0, 0); } }

        public RelayCommand WidthUpCommand { get; private set; }
        public RelayCommand WidthDownCommand { get; private set; }
        public RelayCommand HeightUpCommand { get; private set; }
        public RelayCommand HeightDownCommand { get; private set; }

        /// <summary>Called when the person clicks, focuses or drags in the card; the page then shows it as the active one.</summary>
        public Action<SizeItemViewModel> Activated { get; set; }
        public void Activate() { if (Activated != null) Activated(this); }

        public bool IsDragging { get { return dragging; } private set { if (dragging != value) { dragging = value; Raise("IsDragging"); } } }
        public bool IsWidthClamped { get { return widthClamped; } private set { if (widthClamped != value) { widthClamped = value; Raise("IsWidthClamped"); } } }
        public bool IsHeightClamped { get { return heightClamped; } private set { if (heightClamped != value) { heightClamped = value; Raise("IsHeightClamped"); } } }

        /// <summary>The value shown next to the handle while dragging, for example "500 px" or "230 × 345 · max".</summary>
        public string BubbleText
        {
            get
            {
                string text = HasWidth && HasHeight && !IsSquare ? ConstantValue.FormatUi(Width, culture) + " × " + ConstantValue.FormatUi(Height, culture)
                    : ConstantValue.FormatUi(HasWidth ? Width : Height, culture) + " px";
                return text + (IsWidthClamped || IsHeightClamped ? " · limit" : "");
            }
        }

        public bool IsActive { get { return isActive; } set { if (isActive != value) { isActive = value; Raise("IsActive"); } } }
        public bool IsVisible { get { return isVisible; } set { if (isVisible != value) { isVisible = value; Raise("IsVisible"); } } }

        public string Error
        {
            get { return error; }
            private set { if (error != value) { error = value; Raise("Error"); } }
        }

        // ---- values and fields ---------------------------------------------------------------------------------------

        public string WidthText
        {
            get { return HasWidth ? ConstantValue.FormatUi(Width, culture) : null; }
            set { SetText(true, value); }
        }

        public string HeightText
        {
            get { return HasHeight ? ConstantValue.FormatUi(Height, culture) : null; }
            set { SetText(false, value); }
        }

        public string RangeText
        {
            get
            {
                Func<ConstantDef, string> range = d => ConstantValue.FormatUi(d.Min ?? 0, culture) + " - " + ConstantValue.FormatUi(d.Max ?? 0, culture);
                if (HasWidth && HasHeight && !IsSquare)
                    return "W " + range(wDef) + ", H " + range(hDef) + " · default " + ConstantValue.FormatUi(DefaultOf(wDef), culture) + " × " + ConstantValue.FormatUi(DefaultOf(hDef), culture);
                var d1 = HasWidth ? wDef : hDef;
                return range(d1) + " · default " + ConstantValue.FormatUi(DefaultOf(d1), culture);
            }
        }

        public bool IsModified
        {
            get
            {
                return (HasWidth && store.Get(def.WidthKey) != store.GetDefault(def.WidthKey)) || (HasHeight && store.Get(def.HeightKey) != store.GetDefault(def.HeightKey));
            }
        }

        public RelayCommand ResetCommand { get; private set; }

        private void Reset()
        {
            history.BeginBatch();
            try
            {
                if (HasWidth) store.Reset(def.WidthKey);
                if (HasHeight) store.Reset(def.HeightKey);
            }
            finally { history.EndBatch(); }
            Error = null;
        }

        // ---- dragging --------------------------------------------------------------------------------------------------

        public void BeginGesture()
        {
            if (gestureOpen) return;
            history.BeginBatch();
            gestureOpen = true;
            startW = Width; startH = Height; accX = 0; accY = 0;
            IsDragging = true;
        }

        public void EndGesture()
        {
            if (!gestureOpen) return;
            gestureOpen = false;
            history.EndBatch();
            IsDragging = false;
        }

        /// <summary>A pointer movement in display pixels along the given handle ("w", "h" or "wh").</summary>
        public void Drag(string axis, double dx, double dy, DragModifiers modifiers)
        {
            bool single = !gestureOpen;
            if (single) BeginGesture();
            double fine = (modifiers & DragModifiers.Ctrl) != 0 ? 0.25 : 1;
            accX += dx / Scale * fine;
            accY += dy / Scale * fine;
            ApplyDrag(axis, modifiers, (modifiers & DragModifiers.Shift) != 0 ? 10 : (modifiers & DragModifiers.Ctrl) != 0 ? 1 : StepOf());
            if (single) EndGesture();
        }

        /// <summary>An arrow key on a focused handle: one step (ten with Shift) along the axis, one undo step.</summary>
        public void Nudge(string axis, int sign, DragModifiers modifiers)
        {
            double amount = sign * ((modifiers & DragModifiers.Shift) != 0 ? 10 : (modifiers & DragModifiers.Ctrl) != 0 ? 1 : StepOf());
            bool single = !gestureOpen;
            if (single) BeginGesture();
            accX = axis.Contains("w") ? amount : 0;
            accY = axis.Contains("h") ? amount : 0;
            ApplyDrag(axis, modifiers & ~DragModifiers.Shift, 1);
            if (single) EndGesture();
        }

        private double StepOf()
        {
            var d = HasWidth ? wDef : hDef;
            return d != null && d.Step.HasValue && d.Step.Value >= 1 ? d.Step.Value : 1;
        }

        private bool UnderLock(DragModifiers modifiers)
        {
            return HasRatio && !IsSquare && locked && (modifiers & DragModifiers.Alt) == 0 && Feasible(ratio);
        }

        private void ApplyDrag(string axis, DragModifiers modifiers, double step)
        {
            double tw = startW + accX, th = startH + accY;
            bool wClamp = false, hClamp = false;
            double newW = startW, newH = startH;
            bool setW = false, setH = false;

            if (IsSquare)
            {
                double side = startW + (Math.Abs(accX) >= Math.Abs(accY) ? accX : accY);
                double lo = Math.Max(MinW(), MinH()), hi = Math.Min(MaxW(false), MaxH(false));
                double clamped = Snap(Math.Min(hi, Math.Max(lo, side)), step);
                clamped = Math.Min(hi, Math.Max(lo, clamped));
                wClamp = hClamp = Math.Abs(clamped - side) > 0.5 * step + 1e-9 && (side < lo || side > hi);
                newW = newH = clamped; setW = setH = true;
            }
            else if (UnderLock(modifiers))
            {
                double r = ratio;
                if (axis == "w") th = tw / r;
                else if (axis == "h") tw = th * r;
                else if (Math.Abs(accX) >= Math.Abs(accY) * r) th = tw / r;
                else tw = th * r;
                double lo = FeasibleLow(r), hi = FeasibleHigh(r);
                double w = Math.Min(hi, Math.Max(lo, tw));
                wClamp = hClamp = tw < lo - 1e-9 || tw > hi + 1e-9;
                w = Math.Min(hi, Math.Max(lo, Snap(w, step)));
                newW = Math.Round(w);
                newH = Math.Min(MaxH(false), Math.Max(MinH(), Math.Round(w / r)));
                setW = setH = true;
            }
            else
            {
                if (HasWidth && axis.Contains("w"))
                {
                    double w = Math.Min(MaxW(false), Math.Max(MinW(), Snap(tw, step)));
                    wClamp = tw < MinW() - 1e-9 || tw > MaxW(false) + 1e-9;
                    newW = w; setW = true;
                }
                if (HasHeight && axis.Contains("h"))
                {
                    double h = Math.Min(MaxH(false), Math.Max(MinH(), Snap(th, step)));
                    hClamp = th < MinH() - 1e-9 || th > MaxH(false) + 1e-9;
                    newH = h; setH = true;
                }
            }
            IsWidthClamped = wClamp;
            IsHeightClamped = hClamp;
            string message;
            if (setW && HasWidth) store.TrySet(def.WidthKey, ConstantValue.ToInvariant(newW), out message);
            if (setH && HasHeight && def.HeightKey != def.WidthKey) store.TrySet(def.HeightKey, ConstantValue.ToInvariant(newH), out message);
            Raise("BubbleText");
        }

        private static double Snap(double value, double step)
        {
            return step <= 1 ? Math.Round(value) : Math.Round(value / step) * step;
        }

        // ---- typed values --------------------------------------------------------------------------------------------

        private void SetText(bool width, string text)
        {
            if (width ? !HasWidth : !HasHeight) return;
            double number;
            try { number = ConstantValue.ParseUi(text ?? "", culture); }
            catch (FormatException) { Error = "'" + text + "' is not a number"; RaiseAll(); return; }
            string message;
            history.BeginBatch();
            try
            {
                string key = width ? def.WidthKey : def.HeightKey;
                Error = store.TrySet(key, ConstantValue.ToInvariant(number), out message) ? null : message;
                if (IsSquare) store.TrySet(def.HeightKey, store.Get(key), out message);
                else if (HasWidth && HasHeight && locked && Feasible(ratio))
                {
                    double other = width ? Number(def.WidthKey) / ratio : Number(def.HeightKey) * ratio;
                    store.TrySet(width ? def.HeightKey : def.WidthKey, ConstantValue.ToInvariant(Math.Round(other)), out message);
                }
            }
            finally { history.EndBatch(); }
            RaiseAll();
        }

        // ---- aspect ratio --------------------------------------------------------------------------------------------

        public bool HasRatio { get { return def.HasRatio && HasWidth && HasHeight && !IsSquare; } }
        public IList<RatioChoiceViewModel> Ratios { get; private set; }
        public RelayCommand ToggleLockCommand { get; private set; }
        public bool IsRatioLocked { get { return HasRatio && locked; } }

        private double MinW() { return wDef == null ? 0 : wDef.Min ?? 0; }
        private double MinH() { return hDef == null ? 0 : hDef.Min ?? 0; }
        private double MaxW(bool forDrawing) { return wDef == null ? 0 : wDef.Max ?? (forDrawing ? Math.Max(1, Number(def.WidthKey) * 3) : double.MaxValue); }
        private double MaxH(bool forDrawing) { return hDef == null ? 0 : hDef.Max ?? (forDrawing ? Math.Max(1, Number(def.HeightKey) * 3) : double.MaxValue); }
        private double FeasibleLow(double r) { return Math.Max(MinW(), MinH() * r); }
        private double FeasibleHigh(double r) { return Math.Min(MaxW(false), MaxH(false) * r); }
        private bool Feasible(double r) { return FeasibleLow(r) <= FeasibleHigh(r) + 1e-9; }

        private void BuildRatios()
        {
            Ratios = new List<RatioChoiceViewModel>();
            if (!HasRatio) return;
            Ratios.Add(Chip("Free", null));
            foreach (var kv in RatioList) Ratios.Add(Chip(kv.Key, kv.Value));
            Ratios.Add(Chip("Current", null));
        }

        private RatioChoiceViewModel Chip(string label, double? value)
        {
            var chip = new RatioChoiceViewModel { Label = label, Value = value };
            chip.SelectCommand = new RelayCommand(() => SelectRatio(label));
            if (value.HasValue && !Feasible(value.Value))
            {
                chip.IsEnabled = false;
                chip.DisabledReason = "Not possible with the allowed sizes (width " + ConstantValue.FormatUi(MinW(), culture) + " to " + ConstantValue.FormatUi(MaxW(false), culture)
                    + ", height " + ConstantValue.FormatUi(MinH(), culture) + " to " + ConstantValue.FormatUi(MaxH(false), culture) + ").";
            }
            return chip;
        }

        private string EditorKey { get { return "ratio." + def.Id; } }

        private void LoadRatioState()
        {
            if (!HasRatio) return;
            string saved;
            if (!editor.TryGetValue(EditorKey, out saved))
            {
                saved = def.RatioDefault == "current" || def.RatioDefault == "free" || !def.RatioLocked ? "free" : def.RatioDefault;
            }
            ApplyState(saved, false);
        }

        private string State()
        {
            if (!locked) return "free";
            var chip = Ratios.FirstOrDefault(r => r.Value.HasValue && Math.Abs(r.Value.Value - ratio) / ratio < DefaultRatioTolerance);
            return chip != null ? chip.Label : "r=" + ratio.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>Sets the lock and the ratio from a stored state ("free", a chip label or "r=0.6667") without touching the sizes.</summary>
        private void ApplyState(string state, bool persist)
        {
            locked = false;
            if (state != null && state != "free")
            {
                var chip = Ratios.FirstOrDefault(r => r.Label == state && r.Value.HasValue);
                double parsed;
                if (chip != null) { ratio = chip.Value.Value; locked = true; }
                else if (state.StartsWith("r=") && double.TryParse(state.Substring(2), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) && parsed > 0) { ratio = parsed; locked = true; }
            }
            if (persist) editor[EditorKey] = State();
            RefreshRatioChips();
        }

        private void RefreshRatioChips()
        {
            string label = !locked ? "Free" : (Ratios.FirstOrDefault(r => r.Value.HasValue && Math.Abs(r.Value.Value - ratio) / ratio < DefaultRatioTolerance) ?? Ratios.Last()).Label;
            foreach (var chip in Ratios) chip.IsSelected = chip.Label == label;
            Raise("IsRatioLocked");
        }

        /// <summary>Picks a chip: keeps the width and recomputes the height; if that does not fit, the nearest possible size.</summary>
        public void SelectRatio(string label)
        {
            var chip = Ratios.FirstOrDefault(r => r.Label == label);
            if (chip == null || !chip.IsEnabled) return;
            string before = State();
            double r = label == "Free" ? 0 : label == "Current" ? Width / Height : chip.Value.Value;
            history.BeginBatch();
            try
            {
                if (r > 0 && Feasible(r)) FitTo(r);
                ApplyState(r > 0 ? (label == "Current" ? "r=" + r.ToString("R", CultureInfo.InvariantCulture) : label) : "free", true);
                string after = State();
                history.RecordAction(() => ApplyState(before, true), () => ApplyState(after, true));
            }
            finally { history.EndBatch(); }
            RaiseAll();
        }

        private void ToggleLock()
        {
            SelectRatio(locked ? "Free" : "Current");
        }

        private void FitTo(double r)
        {
            double w = Width, h = Height, nh = w / r, nw = h * r;
            string message;
            if (nh >= MinH() - 1e-9 && nh <= MaxH(false) + 1e-9) h = Math.Round(nh);
            else if (nw >= MinW() - 1e-9 && nw <= MaxW(false) + 1e-9) w = Math.Round(nw);
            else
            {
                w = Math.Round(Math.Min(FeasibleHigh(r), Math.Max(FeasibleLow(r) - 1e-9, w)) + (w < FeasibleLow(r) ? 0.5 : 0));
                w = Math.Ceiling(Math.Min(FeasibleHigh(r), Math.Max(FeasibleLow(r), w)) - 1e-9);
                h = Math.Round(w / r);
            }
            store.TrySet(def.WidthKey, ConstantValue.ToInvariant(w), out message);
            store.TrySet(def.HeightKey, ConstantValue.ToInvariant(h), out message);
        }

        // ---- helpers -----------------------------------------------------------------------------------------------

        private double Number(string key)
        {
            return double.Parse(store.Get(key), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private double DefaultOf(ConstantDef d)
        {
            return double.Parse(store.GetDefault(d.Key), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        /// <summary>True when the item matches a search text (title, description, where, constant keys).</summary>
        public bool Matches(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;
            string f = filter.Trim();
            foreach (var text in new[] { def.Title, def.Description, def.WhereLabel, def.WidthKey, def.HeightKey })
                if (text != null && text.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
    }

    /// <summary>A zone of the window and the size items that live in it.</summary>
    public class SizeGroupViewModel : ViewModelBase
    {
        private bool expanded = true;
        private int visibleCount;

        public string Id { get; set; }
        public string Title { get; set; }
        public IList<SizeItemViewModel> Items { get; set; }
        public int VisibleCount { get { return visibleCount; } set { if (visibleCount != value) { visibleCount = value; Raise("VisibleCount"); Raise("IsVisible"); } } }
        public bool IsVisible { get { return visibleCount > 0; } }
        public bool IsExpanded { get { return expanded; } set { if (expanded != value) { expanded = value; Raise("IsExpanded"); } } }
    }

    /// <summary>The Sizes page: cards grouped by zone, a filter, "changed only" and the active card.</summary>
    public class SizesEditorViewModel : ViewModelBase
    {
        private string filter = "";
        private bool changedOnly;
        private SizeItemViewModel active;

        public SizesEditorViewModel(ConstantStore store, Schema schema, History history, CultureInfo culture, IDictionary<string, string> editorState = null)
        {
            Items = schema.Sizes.Select(d => new SizeItemViewModel(d, store, schema, history, culture, editorState) { Activated = Activate }).ToList();
            var groups = schema.SizeGroups.Count > 0 ? schema.SizeGroups : new List<SizeGroup> { new SizeGroup { Id = null, Title = "Sizes" } };
            Groups = groups.Select(g => new SizeGroupViewModel
            {
                Id = g.Id, Title = g.Title,
                Items = Items.Where(i => g.Id == null || i.GroupId == g.Id).ToList()
            }).Where(g => g.Items.Count > 0).ToList();
            store.ValueChanged += key => { if (changedOnly) ApplyFilter(); };
            ApplyFilter();
        }

        public IList<SizeItemViewModel> Items { get; private set; }
        public IList<SizeGroupViewModel> Groups { get; private set; }

        public string Filter
        {
            get { return filter; }
            set { filter = value ?? ""; ApplyFilter(); Raise("Filter"); }
        }

        public bool ChangedOnly
        {
            get { return changedOnly; }
            set { changedOnly = value; ApplyFilter(); Raise("ChangedOnly"); }
        }

        public int VisibleCount { get { return Items.Count(i => i.IsVisible); } }
        public bool IsEmpty { get { return VisibleCount == 0; } }
        public SizeItemViewModel Active { get { return active; } }

        /// <summary>The card the person last clicked, focused or dragged (not merely hovered).</summary>
        public void Activate(SizeItemViewModel item)
        {
            if (ReferenceEquals(active, item)) return;
            if (active != null) active.IsActive = false;
            active = item;
            if (active != null) active.IsActive = true;
            Raise("Active");
        }

        private void ApplyFilter()
        {
            foreach (var item in Items) item.IsVisible = item.Matches(filter) && (!changedOnly || item.IsModified);
            foreach (var g in Groups) g.VisibleCount = g.Items.Count(i => i.IsVisible);
            Raise("VisibleCount");
            Raise("IsEmpty");
        }
    }
}
