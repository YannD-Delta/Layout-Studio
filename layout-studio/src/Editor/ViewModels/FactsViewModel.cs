using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Media;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    /// <summary>Limits and colours of the colour rules, kept in the profile's editor choices (not in theme constants).</summary>
    public class FactsSettings
    {
        public static readonly Color DefaultRed = Color.FromRgb(0xFF, 0x5A, 0x52);
        public static readonly Color DefaultOrange = Color.FromRgb(0xFF, 0xA6, 0x30);
        public static readonly Color DefaultGreen = Color.FromRgb(0x4A, 0xDE, 0x80);
        public static readonly Color DefaultBlue = Color.FromRgb(0x4D, 0xA3, 0xFF);
        public static readonly Color DefaultNeutral = Color.FromRgb(0xE8, 0xEA, 0xED);

        public double FpsRedBelow = 40, FpsOrangeUpTo = 120, FpsBlueFrom = 150;
        // Same limits as the Stream Deck. GPU load stays neutral until 95: a busy GPU in a game is good news.
        public double CpuWarn = 75, CpuCrit = 95, RamWarn = 85, RamCrit = 95, GpuWarn = 95, GpuCrit = 98;
        /// <summary>Scores (0-100) from which a score glows, and from which it also pulses.</summary>
        public double ScoreGlowFrom = 80, ScoreTopFrom = 90;

        /// <summary>Samples logged before this instant are ignored (the sensors were mapped wrongly then); null = use all.</summary>
        public DateTime? Since;

        public Color Red = DefaultRed, Orange = DefaultOrange, Green = DefaultGreen, Blue = DefaultBlue, Neutral = DefaultNeutral;
        private static readonly string[] ColorKeys = { "facts.color.red", "facts.color.orange", "facts.color.green", "facts.color.blue", "facts.color.neutral" };

        /// <summary>Every limit and colour as editor strings (what is saved in the profile); the cut-off date is left out.</summary>
        public Dictionary<string, string> ToEditor()
        {
            var d = new Dictionary<string, string>();
            var saved = new Dictionary<string, string>();
            DateTime? keep = Since; Since = null;
            SaveTo(saved);
            Since = keep;
            foreach (var kv in saved) d[kv.Key] = kv.Value;
            return d;
        }

        /// <summary>Takes the limits and colours of <paramref name="editor"/>; keys that are missing fall back to the defaults.</summary>
        public void Load(IDictionary<string, string> editor)
        {
            var fresh = From(editor);
            FpsRedBelow = fresh.FpsRedBelow; FpsOrangeUpTo = fresh.FpsOrangeUpTo; FpsBlueFrom = fresh.FpsBlueFrom;
            ScoreGlowFrom = fresh.ScoreGlowFrom; ScoreTopFrom = fresh.ScoreTopFrom; CpuWarn = fresh.CpuWarn; CpuCrit = fresh.CpuCrit; RamWarn = fresh.RamWarn; RamCrit = fresh.RamCrit; GpuWarn = fresh.GpuWarn; GpuCrit = fresh.GpuCrit;
            Red = fresh.Red; Orange = fresh.Orange; Green = fresh.Green; Blue = fresh.Blue; Neutral = fresh.Neutral;
        }

        public static string Hex(Color c) { return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"); }

        public static bool TryParseColor(string text, out Color color)
        {
            color = default(Color);
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            if (!text.StartsWith("#")) text = "#" + text;
            if (text.Length != 7 && text.Length != 9) return false;
            try { color = (Color)ColorConverter.ConvertFromString(text); return true; }
            catch (FormatException) { return false; }
        }

        private static readonly string[] Keys = { "facts.fps.redBelow", "facts.fps.orangeUpTo", "facts.fps.blueFrom", "facts.cpu.warn", "facts.cpu.crit", "facts.ram.warn", "facts.ram.crit", "facts.gpu.warn", "facts.gpu.crit", "facts.score.glowFrom", "facts.score.topFrom" };

        public static FactsSettings From(IDictionary<string, string> editor)
        {
            var s = new FactsSettings();
            if (editor == null) return s;
            s.FpsRedBelow = Read(editor, Keys[0], s.FpsRedBelow);
            s.FpsOrangeUpTo = Read(editor, Keys[1], s.FpsOrangeUpTo);
            s.FpsBlueFrom = Read(editor, Keys[2], s.FpsBlueFrom);
            s.CpuWarn = Read(editor, Keys[3], s.CpuWarn);
            s.CpuCrit = Read(editor, Keys[4], s.CpuCrit);
            s.RamWarn = Read(editor, Keys[5], s.RamWarn);
            s.RamCrit = Read(editor, Keys[6], s.RamCrit);
            s.GpuWarn = Read(editor, Keys[7], s.GpuWarn);
            s.GpuCrit = Read(editor, Keys[8], s.GpuCrit);
            s.ScoreGlowFrom = Read(editor, Keys[9], s.ScoreGlowFrom);
            s.ScoreTopFrom = Read(editor, Keys[10], s.ScoreTopFrom);
            var colors = new[] { s.Red, s.Orange, s.Green, s.Blue, s.Neutral };
            for (int i = 0; i < ColorKeys.Length; i++)
            {
                string hex; Color parsedColor;
                if (editor.TryGetValue(ColorKeys[i], out hex) && TryParseColor(hex, out parsedColor)) colors[i] = parsedColor;
            }
            s.Red = colors[0]; s.Orange = colors[1]; s.Green = colors[2]; s.Blue = colors[3]; s.Neutral = colors[4];
            string since;
            DateTime parsed;
            if (editor.TryGetValue("facts.since", out since) && DateTime.TryParse(since, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out parsed)) s.Since = parsed;
            return s;
        }

        public void SaveTo(IDictionary<string, string> editor)
        {
            var values = new[] { FpsRedBelow, FpsOrangeUpTo, FpsBlueFrom, CpuWarn, CpuCrit, RamWarn, RamCrit, GpuWarn, GpuCrit, ScoreGlowFrom, ScoreTopFrom };
            for (int i = 0; i < Keys.Length; i++) editor[Keys[i]] = values[i].ToString("R", CultureInfo.InvariantCulture);
            var colors = new[] { Red, Orange, Green, Blue, Neutral };
            for (int i = 0; i < ColorKeys.Length; i++) editor[ColorKeys[i]] = Hex(colors[i]);
            if (Since.HasValue) editor["facts.since"] = Since.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        }

        private static double Read(IDictionary<string, string> editor, string key, double fallback)
        {
            string text;
            double value;
            return editor.TryGetValue(key, out text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value) ? value : fallback;
        }
    }

    /// <summary>
    /// What the theme shows for the selected game, from GameActivity's logged samples: text and colour for the median FPS and the median
    /// CPU, GPU and RAM load. The theme binds to these properties; Update is called when the selection changes.
    /// </summary>
    public class FactsViewModel : INotifyPropertyChanged
    {
        private readonly FactsSettings settings;
        private GameFacts facts = new GameFacts();

        public FactsViewModel(FactsSettings settings) { this.settings = settings; }

        /// <summary>The cut-off the activity data is read with.</summary>
        public DateTime? Since { get { return settings.Since; } }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Update(GameFacts newFacts)
        {
            facts = newFacts ?? new GameFacts();
            Refresh();
        }

        /// <summary>Takes the limits and colours of another profile (after an import or a reset) and recolours the theme.</summary>
        public void Reload(IDictionary<string, string> editor)
        {
            DateTime? keep = settings.Since;
            settings.Load(editor);
            DateTime parsed;
            string since;
            if (editor != null && editor.TryGetValue("facts.since", out since) && DateTime.TryParse(since, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out parsed)) settings.Since = parsed;
            else settings.Since = keep;
            Refresh();
        }

        /// <summary>Recomputes the colours (the limits changed) and announces every value.</summary>
        public void Refresh()
        {
            foreach (var name in new[]
            {
                "MedianFpsText", "MinFpsText", "MaxFpsText", "FpsBrush", "FpsLevel", "HasFps",
                "CpuText", "CpuBrush", "CpuLevel", "HasCpu", "GpuText", "GpuBrush", "GpuLevel", "HasGpu", "RamText", "RamBrush", "RamLevel", "HasRam", "HasAny", "CriticScoreLevel", "CommunityScoreLevel", "UserScoreLevel", "CriticScoreBrush", "CommunityScoreBrush", "UserScoreBrush"
            })
            {
                var handler = PropertyChanged;
                if (handler != null) handler(this, new PropertyChangedEventArgs(name));
            }
        }

        private int? criticScore, communityScore, userScore;

        /// <summary>The scores of the selected game (null = none); the theme makes high ones glow.</summary>
        public void SetScores(int? critic, int? community, int? user)
        {
            criticScore = critic; communityScore = community; userScore = user;
            Refresh();
        }

        /// <summary>"None", "Glow" (from the glow limit) or "Top" (from the top limit): what the theme binds its score triggers to.</summary>
        public string CriticScoreLevel { get { return ScoreLevel(criticScore); } }
        public string CommunityScoreLevel { get { return ScoreLevel(communityScore); } }
        public string UserScoreLevel { get { return ScoreLevel(userScore); } }

        /// <summary>The colour of each score: the same levels as the desktop glow (top = blue, glow = green, else neutral), so every theme agrees.</summary>
        public Brush CriticScoreBrush { get { return ScoreBrush(criticScore); } }
        public Brush CommunityScoreBrush { get { return ScoreBrush(communityScore); } }
        public Brush UserScoreBrush { get { return ScoreBrush(userScore); } }

        private Brush ScoreBrush(int? v)
        {
            var level = ScoreLevel(v);
            return BrushOf(level == "Top" ? ColorLevel.Blue : level == "Glow" ? ColorLevel.Green : ColorLevel.None);
        }

        private string ScoreLevel(int? v)
        {
            if (!v.HasValue) return "None";
            if (v.Value >= settings.ScoreTopFrom) return "Top";
            return v.Value >= settings.ScoreGlowFrom ? "Glow" : "None";
        }

        public bool HasAny { get { return facts.HasAnyData; } }
        public bool HasFps { get { return facts.Fps.HasData; } }
        public bool HasCpu { get { return facts.Cpu.HasData; } }
        public bool HasGpu { get { return facts.Gpu.HasData; } }
        public bool HasRam { get { return facts.Ram.HasData; } }

        public string MedianFpsText { get { return Whole(facts.Fps, ""); } }
        public string MinFpsText { get { return Whole(facts.Fps.HasData ? new MetricSummary { Count = 1, Median = facts.Fps.Min } : facts.Fps, ""); } }
        public string MaxFpsText { get { return Whole(facts.Fps.HasData ? new MetricSummary { Count = 1, Median = facts.Fps.Max } : facts.Fps, ""); } }
        public string CpuText { get { return Whole(facts.Cpu, " %"); } }
        public string GpuText { get { return Whole(facts.Gpu, " %"); } }
        public string RamText { get { return Whole(facts.Ram, " %"); } }

        public ColorLevel FpsLevel { get { return FpsScale().Level(facts.Fps.HasData ? (double?)facts.Fps.Median : null); } }
        public ColorLevel CpuLevel { get { return ColorScale.Load(settings.CpuWarn, settings.CpuCrit).Level(facts.Cpu.HasData ? (double?)facts.Cpu.Median : null); } }
        public ColorLevel GpuLevel { get { if (!facts.Gpu.HasData) return ColorLevel.None; var v = facts.Gpu.Median; return v > settings.GpuCrit ? ColorLevel.Red : v > settings.GpuWarn ? ColorLevel.Orange : ColorLevel.None; } }
        public ColorLevel RamLevel { get { return ColorScale.Load(settings.RamWarn, settings.RamCrit).Level(facts.Ram.HasData ? (double?)facts.Ram.Median : null); } }

        public Brush FpsBrush { get { return BrushOf(FpsLevel); } }
        public Brush CpuBrush { get { return BrushOf(CpuLevel); } }
        public Brush GpuBrush { get { return BrushOf(GpuLevel); } }
        public Brush RamBrush { get { return BrushOf(RamLevel); } }

        private ColorScale FpsScale() { return ColorScale.Fps(settings.FpsRedBelow, settings.FpsOrangeUpTo, settings.FpsBlueFrom); }

        private static string Whole(MetricSummary m, string suffix)
        {
            return m.HasData ? Math.Round(m.Median).ToString("0", CultureInfo.InvariantCulture) + suffix : "—";
        }

        /// <summary>The limits and colours in use; the Rules page edits them and calls <see cref="Refresh"/>.</summary>
        public FactsSettings Settings { get { return settings; } }

        private Brush BrushOf(ColorLevel level)
        {
            Color c;
            switch (level)
            {
                case ColorLevel.Red: c = settings.Red; break;
                case ColorLevel.Orange: c = settings.Orange; break;
                case ColorLevel.Green: c = settings.Green; break;
                case ColorLevel.Blue: c = settings.Blue; break;
                default: c = settings.Neutral; break;
            }
            var brush = new SolidColorBrush(c);
            brush.Freeze();
            return brush;
        }
    }
}
