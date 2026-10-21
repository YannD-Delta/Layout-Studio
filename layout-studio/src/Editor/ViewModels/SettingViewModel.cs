using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    /// <summary>One setting a preset choice sets, as the user reads it: the setting's title and the value it gets.</summary>
    public class VariableViewModel
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Key { get; set; }
    }

    public class ChoiceViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string Preview { get; set; }
        public string PreviewPath { get { return PreviewImages.PathOf(Preview); } }
        public bool HasPreview { get { return PreviewPath != null; } }

        /// <summary>The constants this choice sets (presets only).</summary>
        public IDictionary<string, string> Constants { get; set; }

        /// <summary>The other choices of the same group: their pictures are compared to find the part that changes.</summary>
        public IList<ChoiceViewModel> Siblings { get; set; }

        /// <summary>Gives a constant name its title (set by the shell from the schema); the name itself when unknown.</summary>
        public Func<string, string> KeyTitle { get; set; }

        public int VariableCount { get { return Constants == null ? 0 : Constants.Count; } }
        public bool HasVariables { get { return VariableCount > 0; } }
        /// <summary>Words a count ("{0} settings"); set by the setting from the language file.</summary>
        public Func<int, string> CountText { get; set; }
        public string VariablesHeader { get { return CountText != null ? CountText(VariableCount) : VariableCount == 1 ? "1 setting" : VariableCount + " settings"; } }

        /// <summary>"Corner radius of the card: 20", one line per constant the choice sets, in the order of the schema.</summary>
        public IList<VariableViewModel> Variables
        {
            get
            {
                var list = new List<VariableViewModel>();
                if (Constants == null) return list;
                foreach (var kv in Constants)
                {
                    string title = KeyTitle == null ? null : KeyTitle(kv.Key);
                    list.Add(new VariableViewModel { Key = kv.Key, Name = string.IsNullOrEmpty(title) ? kv.Key : title, Value = Pretty(kv.Value) });
                }
                return list;
            }
        }

        private static string Pretty(string raw)
        {
            if (raw == "True") return "on";
            if (raw == "False") return "off";
            return raw;
        }
    }

    public class OrderedItemViewModel
    {
        public string Key { get; set; }
        public string Title { get; set; }
    }

    /// <summary>One header tile slot: a combo of tile types bound to a slot constant.</summary>
    public class SlotViewModel : ViewModelBase
    {
        private readonly string key;
        private readonly ConstantStore store;
        private readonly History history;

        public SlotViewModel(int index, string key, IList<ChoiceViewModel> choices, ConstantStore store, History history)
        {
            Index = index;
            this.key = key;
            Choices = choices;
            this.store = store;
            this.history = history;
        }

        public int Index { get; private set; }
        public IList<ChoiceViewModel> Choices { get; private set; }

        public ChoiceViewModel SelectedChoice
        {
            get { return Choices.FirstOrDefault(c => c.Value == store.Get(key)); }
            set
            {
                if (value == null) return;
                string error;
                history.BeginBatch();
                try { store.TrySet(key, value.Value, out error); }
                finally { history.EndBatch(); }
                Raise("SelectedChoice");
            }
        }

        public void Refresh() { Raise("SelectedChoice"); }
    }

    /// <summary>One control of the schema, bound to its constant(s). Edits go through the store inside a history batch.</summary>
    public class SettingViewModel : ViewModelBase, IGestureTarget
    {
        private readonly Control def;
        private readonly ConstantStore store;
        private readonly History history;
        private readonly ProfileApplier applier;
        private readonly Profile profile;
        private readonly CultureInfo culture;
        private readonly Func<string, string> localize;
        private bool visible = true;
        private string error;

        public SettingViewModel(Control def, ConstantStore store, History history, ProfileApplier applier, Profile profile,
            Func<string, string> localize, CultureInfo culture)
        {
            this.def = def;
            this.store = store;
            this.history = history;
            this.applier = applier;
            this.profile = profile;
            this.culture = culture;
            this.localize = localize;
            Title = (def.TitleKey != null ? localize(def.TitleKey) : null) ?? def.Title ?? def.Id;

            Choices = def.Choices.Select(c => new ChoiceViewModel { Id = c.Id, Name = (c.LocKey != null ? localize(c.LocKey) : null) ?? c.Name ?? c.Id, Value = c.Value, Preview = c.Preview, Constants = c.Constants, CountText = CountWords }).ToList();
            foreach (var choice in Choices) choice.Siblings = Choices;
            Items = new ObservableCollection<OrderedItemViewModel>();
            Slots = new List<SlotViewModel>();
            if (def.Kind == "tileSlots")
                for (int i = 0; i < def.Keys.Count; i++) Slots.Add(new SlotViewModel(i + 1, def.Keys[i], Choices, store, history));
            RebuildItems();
            StepUpCommand = new RelayCommand(() => Nudge(+1));
            StepDownCommand = new RelayCommand(() => Nudge(-1));
            MoveUpCommand = new RelayCommand(item => MoveUp(item as OrderedItemViewModel));
            MoveDownCommand = new RelayCommand(item => MoveDown(item as OrderedItemViewModel));
            ResetCommand = new RelayCommand(ResetOne, () => IsModified);

            store.ValueChanged += key =>
            {
                if (def.Keys.Contains(key) || def.ExtraKeys.Contains(key)) Refresh();
            };
            if (def.Kind == "preset") history.Changed += () => { Raise("SelectedChoice"); Raise("IsModified"); ResetCommand.RaiseCanExecuteChanged(); };   // an undo or redo can change the recorded choice
        }

        /// <summary>The editor template used for a schema kind. Kinds without an editor yet map to "unsupported".</summary>
        public static string TemplateKind(string schemaKind)
        {
            switch (schemaKind)
            {
                case "toggle": return "toggle";
                case "slider": return "slider";
                case "number": return "number";
                case "color": return "color";
                case "thickness": return "sides";
                case "choice": return "choice";
                case "preset": return "choice";
                case "orderedList": return "orderedList";
                case "tileSlots": return "tileSlots";
                default: return "unsupported";
            }
        }

        /// <summary>The template key of this setting: presets are picture tiles, a short list of choices is a segmented bar, the rest follow their kind.</summary>
        public string TemplateName
        {
            get
            {
                if (def.Kind == "preset") return "presetTiles";
                if (def.Kind == "choice" && def.Choices.Count <= 4) return "segmented";
                if (def.Kind == "thickness" && Def != null && Def.Type == ConstantType.CornerRadius) return "corners";
                return TemplateKind(def.Kind);
            }
        }

        public string Id { get { return def.Id; } }
        public string Kind { get { return def.Kind; } }
        public IList<string> Keys { get { return def.Keys; } }
        public string Title { get; set; }
        public string Description { get { return def.Description; } }
        public bool NeedsRestart { get { return def.NeedsRestart; } }
        public Control Definition { get { return def; } }
        public IList<ChoiceViewModel> Choices { get; private set; }

        /// <summary>Gives a constant name its title, so a preset tile can list what it sets in words (set by the shell).</summary>
        public Func<string, string> KeyTitle { set { foreach (var c in Choices) c.KeyTitle = value; } }
        public ObservableCollection<OrderedItemViewModel> Items { get; private set; }
        public IList<SlotViewModel> Slots { get; private set; }
        /// <summary>Arrow buttons of a number field: one step up or down, clamped to the range, one undo step each.</summary>
        public RelayCommand StepUpCommand { get; private set; }
        public RelayCommand StepDownCommand { get; private set; }

        /// <summary>The schema step; without one, whole numbers move by 1 and small ranges (0..1 opacities) by 0.01.</summary>
        private double NudgeStep()
        {
            var d = Def;
            if (d == null) return 1;
            if (d.Step.HasValue && d.Step.Value > 0) return d.Step.Value;
            if (d.Type == ConstantType.Int) return 1;
            return d.Min.HasValue && d.Max.HasValue && d.Max.Value - d.Min.Value <= 2 ? 0.01 : 1;
        }

        private void Nudge(int direction)
        {
            double current = double.Parse(store.Get(Key), NumberStyles.Float, CultureInfo.InvariantCulture);
            double next = Math.Round(current + direction * NudgeStep(), 6);
            history.BeginBatch();
            try { SetRaw(ConstantValue.ToInvariant(Snap(next))); }
            finally { history.EndBatch(); }
        }

        /// <summary>Back to the default value of this one setting (one undo step); only offered while it differs.</summary>
        public RelayCommand ResetCommand { get; private set; }

        /// <summary>True when the setting differs from the theme's default: the orange dot of the row.</summary>
        public bool IsModified
        {
            get
            {
                if (def.Kind == "preset")
                {
                    var selected = SelectedChoice;
                    return selected != null && selected.Id != (def.DefaultChoice ?? "Default");
                }
                return def.Keys.Concat(def.ExtraKeys).Any(k => store.Get(k) != store.GetDefault(k));
            }
        }

        public string ResetTooltip { get { return "Back to the default (" + DefaultText + ")"; } }

        /// <summary>The default value as the user sees it ("450", "On", "Rounded").</summary>
        public string DefaultText
        {
            get
            {
                if (def.Kind == "preset")
                {
                    var c = Choices.FirstOrDefault(x => x.Id == (def.DefaultChoice ?? "Default"));
                    return c == null ? "-" : c.Name;
                }
                if (Key == null) return "-";
                string raw = store.GetDefault(Key);
                if (def.Kind == "toggle") return raw == "True" ? "On" : "Off";
                if (def.Kind == "choice") { var c = Choices.FirstOrDefault(x => x.Id == raw); return c == null ? raw : c.Name; }
                var d = Def;
                double number;
                if (d != null && (d.Type == ConstantType.Double || d.Type == ConstantType.Int) && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                    return ConstantValue.FormatUi(number, culture);
                return raw;
            }
        }

        /// <summary>Default, range and constant name, for the tooltip of a row.</summary>
        public string Details
        {
            get
            {
                var parts = new List<string> { "Default: " + DefaultText };
                var d = Def;
                if (d != null && d.Min.HasValue && d.Max.HasValue && (def.Kind == "slider" || def.Kind == "number"))
                    parts.Add("Range: " + ConstantValue.FormatUi(d.Min.Value, culture) + " to " + ConstantValue.FormatUi(d.Max.Value, culture));
                if (def.Keys.Count > 0) parts.Add(def.Keys.Count == 1 ? def.Keys[0] : def.Keys[0] + " and " + (def.Keys.Count - 1) + " more");
                return string.Join("  \u00b7  ", parts);
            }
        }

        private void ResetOne()
        {
            history.BeginBatch();
            try { Reset(); }
            finally { history.EndBatch(); }
        }

        public RelayCommand MoveUpCommand { get; private set; }
        public RelayCommand MoveDownCommand { get; private set; }

        public bool Visible
        {
            get { return visible; }
            set { if (visible != value) { visible = value; Raise("Visible"); } }
        }

        private bool highlighted;
        /// <summary>True while a search is typed and this setting matches it: the row wears an accent bar so the eye finds it.</summary>
        public bool Highlighted
        {
            get { return highlighted; }
            set { if (highlighted != value) { highlighted = value; Raise("Highlighted"); } }
        }

        /// <summary>True when the block keeps this setting behind its "More" link.</summary>
        public bool IsMore { get { return def.More; } }

        /// <summary>The last validation message for this setting, null when the value is fine.</summary>
        public string Error
        {
            get { return error; }
            private set { if (error != value) { error = value; Raise("Error"); } }
        }

        private string Key { get { return def.Keys.Count > 0 ? def.Keys[0] : null; } }
        private ConstantDef Def { get { return Key == null ? null : store.Find(Key); } }

        public double Min { get { return Def != null && Def.Min.HasValue ? Def.Min.Value : 0; } }
        /// <summary>The end of the slider track: the schema maximum, or the current value when a typed one went beyond it (so the slider does not pull it back).</summary>
        public double SliderMax { get { return Key == null ? Max : Math.Max(Max, double.Parse(store.Get(Key), NumberStyles.Float, CultureInfo.InvariantCulture)); } }
        public double Max { get { return Def != null && Def.Max.HasValue ? Def.Max.Value : 100; } }
        public double Step { get { return Def != null && Def.Step.HasValue ? Def.Step.Value : 1; } }

        /// <summary>The value as text, using the user's culture for numbers (a French user types 0,55).</summary>
        public string Text
        {
            get
            {
                if (Key == null) return null;
                string raw = store.Get(Key);
                var d = Def;
                if (d != null && (d.Type == ConstantType.Double || d.Type == ConstantType.Int))
                    return ConstantValue.FormatUi(double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture), culture);
                return raw;
            }
            set { SetFromText(value); }
        }

        private double? sliderRaw;   // what the slider last wrote, before it was snapped to the step

        /// <summary>
        /// The slider position. A slider drags relative to the value it is given back, so while the stored (snapped) value is the snap of what the
        /// slider wrote, the slider gets what it wrote: handing it the snapped value would make a slow drag of less than half a step go nowhere.
        /// </summary>
        public double SliderValue
        {
            get
            {
                double stored = double.Parse(store.Get(Key), NumberStyles.Float, CultureInfo.InvariantCulture);
                if (sliderRaw.HasValue && Math.Abs(Snap(sliderRaw.Value) - stored) < 1e-9) return Math.Max(Min, Math.Min(stored > Max ? stored : Max, sliderRaw.Value));
                sliderRaw = null;
                return stored;
            }
            set { sliderRaw = value; SetRaw(ConstantValue.ToInvariant(Snap(value)), true); }
        }

        /// <summary>A slider drag gives any double: stay in the range, land on the schema's step (counted from the minimum) and drop floating-point noise.</summary>
        private double Snap(double value)
        {
            // the slider and the arrows stop at the end of the range; a typed value is free (see ConstantValue)
            if (Def != null && Def.Max.HasValue && value > Def.Max.Value) value = Def.Max.Value;
            if (Def == null || !Def.Step.HasValue || Def.Step.Value <= 0) return value;
            double origin = Def.Min ?? 0, step = Def.Step.Value;
            return Math.Round(origin + Math.Round((value - origin) / step) * step, 6);
        }

        public bool BoolValue
        {
            get { return store.Get(Key) == "True"; }
            set { SetRaw(value ? "True" : "False"); }
        }

        /// <summary>Choice and preset controls: the selected entry.</summary>
        public ChoiceViewModel SelectedChoice
        {
            get
            {
                if (def.Kind == "preset")
                {
                    string id;
                    if (!profile.Presets.TryGetValue(def.Id, out id)) id = def.DefaultChoice ?? "Default";
                    return Choices.FirstOrDefault(c => c.Id == id);
                }
                return Choices.FirstOrDefault(c => c.Id == store.Get(Key));
            }
            set
            {
                if (value == null) return;
                if (def.Kind == "preset") PresetHistory.Apply(history, applier, profile, def.Id, value.Id);
                else
                {
                    history.BeginBatch();
                    try { SetRaw(value.Id); }
                    finally { history.EndBatch(); }
                }
                Refresh();
            }
        }

        /// <summary>Groups everything changed until EndGesture into one undo step (a slider drag, for example).</summary>
        public void BeginGesture() { history.BeginBatch(); }

        public void EndGesture() { history.EndBatch(); }

        public void MoveUp(OrderedItemViewModel item) { Move(item, -1); }

        public void MoveDown(OrderedItemViewModel item) { Move(item, +1); }

        private void Move(OrderedItemViewModel item, int delta)
        {
            if (item == null) return;
            int from = Items.IndexOf(item);
            int to = from + delta;
            if (from < 0 || to < 0 || to >= Items.Count) return;
            string a = Items[from].Key, b = Items[to].Key;
            string valueA = store.Get(a), valueB = store.Get(b), e;
            history.BeginBatch();
            try
            {
                store.TrySet(a, valueB, out e);
                store.TrySet(b, valueA, out e);
            }
            finally { history.EndBatch(); }
            RebuildItems();
        }

        public bool Matches(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            query = query.Trim();
            return Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || (def.Description != null && def.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                || def.Keys.Any(k => k.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                || def.Choices.Any(c => (c.Name ?? c.Id).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public virtual void Reset()
        {
            sliderRaw = null;
            foreach (var key in def.Keys.Concat(def.ExtraKeys)) store.Reset(key);
            if (def.Kind == "preset") PresetHistory.Apply(history, applier, profile, def.Id, def.DefaultChoice ?? "Default");   // one undo step that also restores the recorded choice
            Refresh();
        }

        public void Refresh()
        {
            RebuildItems();
            foreach (var slot in Slots) slot.Refresh();
            RaiseAll();
            ResetCommand.RaiseCanExecuteChanged();
        }

        private void RebuildItems()
        {
            if (def.Kind != "orderedList") return;
            var ordered = def.Keys
                .Select((k, i) => new { Key = k, Title = (def.ItemTitles.Count > i ? def.ItemTitles[i] : null) ?? k, Position = int.Parse(store.Get(k), CultureInfo.InvariantCulture) })
                .OrderBy(x => x.Position).ToList();
            Items.Clear();
            foreach (var x in ordered) Items.Add(new OrderedItemViewModel { Key = x.Key, Title = x.Title });
        }

        // ---- wireframe: the plan of the Playnite window, with the part this setting touches lit (shown on hover) -------

        /// <summary>The plan drawn in the hover tooltip (see SettingRegions), or null when the setting has none.</summary>
        public WireframePlan Plan { get { return def.Kind == "preset" ? null : WireframePlan.For(Key); } }
        public bool HasPlan { get { return Plan != null; } }

        // ---- color picker ----------------------------------------------------------------------------------------

        private bool pickerOpen;

        /// <summary>The picker popup of a color constant is open.</summary>
        public bool PickerOpen { get { return pickerOpen; } set { if (pickerOpen != value) { pickerOpen = value; Raise("PickerOpen"); } } }

        /// <summary>The ARGB bytes of the stored color; opaque black when the value cannot be read.</summary>
        private byte[] Argb()
        {
            try
            {
                string text = Key == null ? null : store.Get(Key);
                if (text != null && text.Length == 9 && text[0] == '#')
                    return new[] { Convert.ToByte(text.Substring(1, 2), 16), Convert.ToByte(text.Substring(3, 2), 16), Convert.ToByte(text.Substring(5, 2), 16), Convert.ToByte(text.Substring(7, 2), 16) };
            }
            catch (FormatException) { }
            return new byte[] { 255, 0, 0, 0 };
        }

        private void SetChannel(int index, double value)
        {
            var c = Argb();
            c[index] = (byte)Math.Max(0, Math.Min(255, Math.Round(value)));
            SetRaw("#" + c[0].ToString("X2") + c[1].ToString("X2") + c[2].ToString("X2") + c[3].ToString("X2"));
        }

        /// <summary>Alpha, red, green and blue of the color, 0 to 255, for the picker's sliders.</summary>
        public double ColorA { get { return Argb()[0]; } set { SetChannel(0, value); } }
        public double ColorR { get { return Argb()[1]; } set { SetChannel(1, value); } }
        public double ColorG { get { return Argb()[2]; } set { SetChannel(2, value); } }
        public double ColorB { get { return Argb()[3]; } set { SetChannel(3, value); } }

        /// <summary>A quick swatch: the parameter is "#RRGGBB" (the alpha stays) or "#AARRGGBB".</summary>
        public RelayCommand SetColorCommand
        {
            get
            {
                return new RelayCommand(p =>
                {
                    string text = p as string;
                    if (string.IsNullOrEmpty(text) || text[0] != '#') return;
                    if (text.Length == 7) text = "#" + Argb()[0].ToString("X2") + text.Substring(1);
                    SetRaw(text.ToUpperInvariant());
                });
            }
        }

        // ---- four sides (margins, paddings, corner radii) --------------------------------------------------------

        private bool IsCornerRadius { get { return Def != null && Def.Type == ConstantType.CornerRadius; } }

        /// <summary>True for the controls edited as four sides: thicknesses (left, top, right, bottom) and corner radii (four corners).</summary>
        public bool HasSides { get { return def.Kind == "thickness" && Key != null; } }

        private string CountWords(int n)
        {
            return string.Format(CultureInfo.InvariantCulture, Word(n == 1 ? "LOCLS_SettingOne" : "LOCLS_SettingMany", n == 1 ? "{0} setting" : "{0} settings"), n);
        }

        private string Word(string key, string english)
        {
            string text = null;
            try { text = localize(key); } catch (Exception) { }
            return string.IsNullOrEmpty(text) ? english : text;
        }

        public string Side1Label { get { return IsCornerRadius ? Word("LOCLS_Side_TopLeft", "Top left") : Word("LOCLS_Side_Left", "Left"); } }
        public string Side2Label { get { return IsCornerRadius ? Word("LOCLS_Side_TopRight", "Top right") : Word("LOCLS_Side_Top", "Top"); } }
        public string Side3Label { get { return IsCornerRadius ? Word("LOCLS_Side_BottomRight", "Bottom right") : Word("LOCLS_Side_Right", "Right"); } }
        public string Side4Label { get { return IsCornerRadius ? Word("LOCLS_Side_BottomLeft", "Bottom left") : Word("LOCLS_Side_Bottom", "Bottom"); } }
        public string AllLabel { get { return Word("LOCLS_Side_All", "All"); } }

        /// <summary>The four numbers of the stored value (one number: all four; two numbers: left/right then top/bottom).</summary>
        private double[] Sides()
        {
            var p = store.Get(Key).Split(',').Select(x => double.Parse(x.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
            if (p.Length == 1) return new[] { p[0], p[0], p[0], p[0] };
            if (p.Length == 2) return new[] { p[0], p[1], p[0], p[1] };
            return new[] { p[0], p[1], p[2], p[3] };
        }

        private string SideText(int index) { return HasSides ? ConstantValue.FormatUi(Sides()[index], culture) : null; }

        private void SetSideText(int index, string text)
        {
            double number;
            try { number = ConstantValue.ParseUi(text ?? "", culture); }
            catch (FormatException) { Error = "'" + text + "' is not a number"; Refresh(); return; }
            var sides = Sides();
            sides[index] = number;
            WriteSides(sides);
        }

        private void WriteSides(double[] sides)
        {
            bool same = sides.All(s => Math.Abs(s - sides[0]) < 1e-9);
            SetRaw(same ? ConstantValue.ToInvariant(sides[0]) : string.Join(",", sides.Select(ConstantValue.ToInvariant)));
        }

        public string Side1Text { get { return SideText(0); } set { SetSideText(0, value); } }
        public string Side2Text { get { return SideText(1); } set { SetSideText(1, value); } }
        public string Side3Text { get { return SideText(2); } set { SetSideText(2, value); } }
        public string Side4Text { get { return SideText(3); } set { SetSideText(3, value); } }

        /// <summary>One number for all four sides: empty while they differ.</summary>
        public string AllText
        {
            get
            {
                if (!HasSides) return null;
                var s = Sides();
                return s.All(x => Math.Abs(x - s[0]) < 1e-9) ? ConstantValue.FormatUi(s[0], culture) : "";
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value)) { Refresh(); return; }
                double number;
                try { number = ConstantValue.ParseUi(value, culture); }
                catch (FormatException) { Error = "'" + value + "' is not a number"; Refresh(); return; }
                WriteSides(new[] { number, number, number, number });
            }
        }

        private static double PictureSize(double value, double scale, double min, double max) { return Math.Max(min, Math.Min(max, value * scale)); }

        /// <summary>The margin of the small box inside its frame in the picture: the real numbers scaled down and kept inside the frame (a negative side pulls the box out of it).</summary>
        public System.Windows.Thickness SidesPreview
        {
            get
            {
                if (!HasSides) return new System.Windows.Thickness();
                var s = Sides();
                return new System.Windows.Thickness(PictureSize(s[0], 0.6, -10, 30), PictureSize(s[1], 0.6, -10, 18), PictureSize(s[2], 0.6, -10, 30), PictureSize(s[3], 0.6, -10, 18));
            }
        }

        /// <summary>The corners of the small box of the picture (scaled down, at most 24).</summary>
        public System.Windows.CornerRadius CornersPreview
        {
            get
            {
                if (!HasSides) return new System.Windows.CornerRadius();
                var s = Sides().Select(x => Math.Max(0, Math.Min(24, x))).ToArray();
                return new System.Windows.CornerRadius(s[0], s[1], s[2], s[3]);
            }
        }

        private void SetFromText(string text)
        {
            var d = Def;
            string raw = text;
            if (d != null && (d.Type == ConstantType.Double || d.Type == ConstantType.Int))
            {
                double number;
                try { number = ConstantValue.ParseUi(text ?? "", culture); }
                catch (FormatException) { Error = "'" + text + "' is not a number"; Refresh(); return; }
                raw = ConstantValue.ToInvariant(number);
            }
            SetRaw(raw);
        }

        private void SetRaw(string raw, bool fromSlider = false)
        {
            if (!fromSlider) sliderRaw = null;   // a value set any way but by the slider: the slider shows it exactly
            string message;
            history.BeginBatch();
            try { Error = store.TrySet(Key, raw, out message) ? null : message; }
            finally { history.EndBatch(); }
            Refresh();
        }
    }
}
