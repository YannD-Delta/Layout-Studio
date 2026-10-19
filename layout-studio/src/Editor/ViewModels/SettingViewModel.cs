using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    public class ChoiceViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string Preview { get; set; }
        public string PreviewPath { get { return PreviewImages.PathOf(Preview); } }
        public bool HasPreview { get { return PreviewPath != null; } }
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

            Choices = def.Choices.Select(c => new ChoiceViewModel { Id = c.Id, Name = (c.LocKey != null ? localize(c.LocKey) : null) ?? c.Name ?? c.Id, Value = c.Value, Preview = c.Preview }).ToList();
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
                case "thickness": return "text";
                case "choice": return "choice";
                case "preset": return "choice";
                case "orderedList": return "orderedList";
                case "tileSlots": return "tileSlots";
                default: return "unsupported";
            }
        }

        public string Id { get { return def.Id; } }
        public string Kind { get { return def.Kind; } }
        public string Title { get; private set; }
        public string Description { get { return def.Description; } }
        public bool NeedsRestart { get { return def.NeedsRestart; } }
        public Control Definition { get { return def; } }
        public IList<ChoiceViewModel> Choices { get; private set; }
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

        /// <summary>The last validation message for this setting, null when the value is fine.</summary>
        public string Error
        {
            get { return error; }
            private set { if (error != value) { error = value; Raise("Error"); } }
        }

        private string Key { get { return def.Keys.Count > 0 ? def.Keys[0] : null; } }
        private ConstantDef Def { get { return Key == null ? null : store.Find(Key); } }

        public double Min { get { return Def != null && Def.Min.HasValue ? Def.Min.Value : 0; } }
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

        public double SliderValue
        {
            get { return double.Parse(store.Get(Key), NumberStyles.Float, CultureInfo.InvariantCulture); }
            set { SetRaw(ConstantValue.ToInvariant(Snap(value))); }
        }

        /// <summary>A slider drag gives any double: land on the schema's step (counted from the minimum) and drop floating-point noise.</summary>
        private double Snap(double value)
        {
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

        private void SetRaw(string raw)
        {
            string message;
            history.BeginBatch();
            try { Error = store.TrySet(Key, raw, out message) ? null : message; }
            finally { history.EndBatch(); }
            Refresh();
        }
    }
}
