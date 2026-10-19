using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    /// <summary>One number of the colour rules (a limit in FPS or percent) with its slider.</summary>
    public class RuleRowViewModel : ViewModelBase, IGestureTarget
    {
        private readonly RulesEditorViewModel owner;
        private readonly Func<FactsSettings, double> get;
        private readonly Action<FactsSettings, double> set;

        public RuleRowViewModel(RulesEditorViewModel owner, string group, string title, string hint, double min, double max, Func<FactsSettings, double> get, Action<FactsSettings, double> set)
        {
            this.owner = owner;
            Group = group; Title = title; Hint = hint; Min = min; Max = max;
            this.get = get; this.set = set;
        }

        public string Group { get; private set; }
        public string Title { get; private set; }
        public string Hint { get; private set; }
        public double Min { get; private set; }
        public double Max { get; private set; }

        public double Value
        {
            get { return get(owner.Settings); }
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value)) return;
                double v = Math.Min(Max, Math.Max(Min, Math.Round(value)));
                owner.Change(s => set(s, v));
            }
        }

        public void Refresh() { RaiseAll(); }
        public void BeginGesture() { owner.BeginGesture(); }
        public void EndGesture() { owner.EndGesture(); }
    }

    /// <summary>One colour of the rules (red, orange, green, blue, neutral) edited as #RRGGBB.</summary>
    public class RuleColorViewModel : ViewModelBase
    {
        private readonly RulesEditorViewModel owner;
        private readonly Func<FactsSettings, Color> get;
        private readonly Action<FactsSettings, Color> set;
        private string error;

        public RuleColorViewModel(RulesEditorViewModel owner, string title, Func<FactsSettings, Color> get, Action<FactsSettings, Color> set)
        {
            this.owner = owner; Title = title; this.get = get; this.set = set;
        }

        public string Title { get; private set; }
        public Brush Swatch { get { return new SolidColorBrush(get(owner.Settings)); } }
        public string Error { get { return error; } private set { error = value; Raise("Error"); } }

        public string Hex
        {
            get { return FactsSettings.Hex(get(owner.Settings)); }
            set
            {
                Color c;
                if (!FactsSettings.TryParseColor(value, out c)) { Error = "Use a colour like #4ADE80"; Raise("Hex"); return; }
                Error = null;
                owner.Change(s => set(s, c));
            }
        }

        public void Refresh() { RaiseAll(); }
    }

    /// <summary>The Rules page: when the median FPS and the CPU, GPU and RAM figures turn red, orange, green or blue, and which colours those are.</summary>
    public class RulesEditorViewModel : ViewModelBase, IResettable
    {
        private readonly FactsViewModel facts;
        private readonly IDictionary<string, string> editor;
        private readonly History history;
        private bool gesture;

        public RulesEditorViewModel(FactsViewModel facts, IDictionary<string, string> editor, History history)
        {
            this.facts = facts; this.editor = editor; this.history = history;
            Rows = new List<RuleRowViewModel>
            {
                new RuleRowViewModel(this, "Median FPS", "Red below", "Under this many FPS the figure is red.", 5, 240, s => s.FpsRedBelow, (s, v) => s.FpsRedBelow = v),
                new RuleRowViewModel(this, "Median FPS", "Orange up to", "From the red limit up to here: orange. Above it: green.", 10, 360, s => s.FpsOrangeUpTo, (s, v) => s.FpsOrangeUpTo = v),
                new RuleRowViewModel(this, "Median FPS", "Blue from", "At this many FPS or more: blue.", 30, 480, s => s.FpsBlueFrom, (s, v) => s.FpsBlueFrom = v),
                new RuleRowViewModel(this, "CPU load", "Orange above", "Up to here: green.", 1, 100, s => s.CpuWarn, (s, v) => s.CpuWarn = v),
                new RuleRowViewModel(this, "CPU load", "Red above", "", 1, 100, s => s.CpuCrit, (s, v) => s.CpuCrit = v),
                new RuleRowViewModel(this, "RAM load", "Orange above", "Up to here: green.", 1, 100, s => s.RamWarn, (s, v) => s.RamWarn = v),
                new RuleRowViewModel(this, "RAM load", "Red above", "", 1, 100, s => s.RamCrit, (s, v) => s.RamCrit = v),
                new RuleRowViewModel(this, "GPU load", "Orange above", "A busy GPU in a game is good: it stays neutral up to here.", 1, 100, s => s.GpuWarn, (s, v) => s.GpuWarn = v),
                new RuleRowViewModel(this, "GPU load", "Red above", "", 1, 100, s => s.GpuCrit, (s, v) => s.GpuCrit = v),
                new RuleRowViewModel(this, "Scores", "Glow from", "Critic, community and user scores glow in green from this score.", 1, 100, s => s.ScoreGlowFrom, (s, v) => s.ScoreGlowFrom = v),
                new RuleRowViewModel(this, "Scores", "Pulse from", "From this score they glow in blue and pulse (if the animation option is on).", 1, 100, s => s.ScoreTopFrom, (s, v) => s.ScoreTopFrom = v)
            };
            Colors = new List<RuleColorViewModel>
            {
                new RuleColorViewModel(this, "Red", s => s.Red, (s, c) => s.Red = c),
                new RuleColorViewModel(this, "Orange", s => s.Orange, (s, c) => s.Orange = c),
                new RuleColorViewModel(this, "Green", s => s.Green, (s, c) => s.Green = c),
                new RuleColorViewModel(this, "Blue", s => s.Blue, (s, c) => s.Blue = c),
                new RuleColorViewModel(this, "Neutral (white)", s => s.Neutral, (s, c) => s.Neutral = c)
            };
            ResetCommand = new RelayCommand(ResetAll);
        }

        public FactsSettings Settings { get { return facts.Settings; } }
        public IList<RuleRowViewModel> Rows { get; private set; }
        public IList<RuleColorViewModel> Colors { get; private set; }
        public RelayCommand ResetCommand { get; private set; }

        /// <summary>Edits the settings as one undo step, saves them in the profile and recolours the theme at once.</summary>
        public void Change(Action<FactsSettings> edit)
        {
            var before = Settings.ToEditor();
            edit(Settings);
            var after = Settings.ToEditor();
            if (SameValues(before, after)) return;
            history.BeginBatch();
            try
            {
                Commit(after);
                history.RecordAction(() => Commit(before), () => Commit(after));
            }
            finally { history.EndBatch(); }
        }

        public void BeginGesture() { if (!gesture) { history.BeginBatch(); gesture = true; } }
        public void EndGesture() { if (gesture) { gesture = false; history.EndBatch(); } }

        public void ResetAll()
        {
            Change(s => s.Load(new Dictionary<string, string>()));
        }

        /// <summary>How many limits and colours differ from the defaults.</summary>
        public int ChangedCount
        {
            get
            {
                var now = Settings.ToEditor();
                var defaults = new FactsSettings().ToEditor();
                return now.Count(kv => { string d; return defaults.TryGetValue(kv.Key, out d) && d != kv.Value; });
            }
        }

        private void Commit(IDictionary<string, string> values)
        {
            Settings.Load(values);
            Settings.SaveTo(editor);
            facts.Refresh();
            foreach (var r in Rows) r.Refresh();
            foreach (var c in Colors) c.Refresh();
        }

        private static bool SameValues(Dictionary<string, string> a, Dictionary<string, string> b)
        {
            return a.Count == b.Count && a.All(kv => { string other; return b.TryGetValue(kv.Key, out other) && other == kv.Value; });
        }
    }
}
