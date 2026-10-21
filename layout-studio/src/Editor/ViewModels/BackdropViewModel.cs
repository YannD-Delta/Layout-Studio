using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    /// <summary>One button of a row of choices (a mode, a harmony, a style).</summary>
    public class BackdropChoiceViewModel : ViewModelBase
    {
        private bool selected;
        public string Id { get; set; }
        public string Name { get; set; }
        public RelayCommand SelectCommand { get; set; }
        public bool IsSelected { get { return selected; } set { if (selected != value) { selected = value; Raise("IsSelected"); } } }
    }

    /// <summary>
    /// The top of the "Generated backdrop" block: the mode (game artwork, one colour, gradient, radial gradient, circles), the harmony and style
    /// the Randomize button works with, and the palette shortcuts. Everything it does is a set of constants written in one undo step; the other
    /// controls of the block (colours, sliders, the table of circles) are the generated settings.
    /// </summary>
    public class BackdropEditorViewModel : ViewModelBase
    {
        public const string HarmonyKey = "BackdropHarmony", StyleKey = "BackdropStyle";
        private readonly ConstantStore store;
        private readonly History history;
        private readonly Profile profile;
        private readonly Func<string, string> localize;
        private readonly Func<string> coverPath;
        private readonly Random seeds = new Random();
        private string seedError;

        public BackdropEditorViewModel(ConstantStore store, History history, Profile profile, Func<string, string> localize, Func<string> coverPath)
        {
            this.store = store;
            this.history = history;
            this.profile = profile;
            this.localize = localize ?? (k => null);
            this.coverPath = coverPath;
            Modes = new[]
            {
                Choice("0", "LOCLS_Backdrop_Mode0", "Game artwork", SetMode), Choice("1", "LOCLS_Backdrop_Mode1", "One colour", SetMode),
                Choice("2", "LOCLS_Backdrop_Mode2", "Gradient", SetMode), Choice("3", "LOCLS_Backdrop_Mode3", "Radial", SetMode),
                Choice("4", "LOCLS_Backdrop_Mode4", "Circles", SetMode)
            };
            Harmonies = Enum.GetValues(typeof(BackdropHarmony)).Cast<BackdropHarmony>()
                .Select(h => Choice(h.ToString(), "LOCLS_Backdrop_Harmony" + h, SplitWords(h.ToString()), SetHarmony)).ToList();
            Styles = Enum.GetValues(typeof(BackdropStyle)).Cast<BackdropStyle>()
                .Select(s => Choice(s.ToString(), "LOCLS_Backdrop_Style" + s, s.ToString(), SetStyle)).ToList();
            RandomizeCommand = new RelayCommand(Randomize);
            FromAccentCommand = new RelayCommand(FromAccent);
            FromCoverCommand = new RelayCommand(FromCover, () => HasCover);
            Refresh();
            var watched = new HashSet<string>(new[] { "BackdropMode", "BackdropSeed", "BackdropColor1", "BackdropColor2", "BackdropColor3" });
            store.ValueChanged += key => { if (watched.Contains(key)) Refresh(); };
            history.Changed += Refresh;
        }

        public IList<BackdropChoiceViewModel> Modes { get; private set; }
        public IList<BackdropChoiceViewModel> Harmonies { get; private set; }
        public IList<BackdropChoiceViewModel> Styles { get; private set; }
        public RelayCommand RandomizeCommand { get; private set; }
        public RelayCommand FromAccentCommand { get; private set; }
        public RelayCommand FromCoverCommand { get; private set; }

        public string ModeTitle { get { return T("LOCLS_Backdrop_ModeTitle", "WHAT SITS BEHIND THE WINDOW"); } }
        public string HarmonyTitle { get { return T("LOCLS_Backdrop_HarmonyTitle", "RANDOMIZE: HARMONY OF THE COLOURS"); } }
        public string StyleTitle { get { return T("LOCLS_Backdrop_StyleTitle", "RANDOMIZE: KIND OF CIRCLES"); } }
        public string RandomizeText { get { return T("LOCLS_Backdrop_Randomize", "Randomize"); } }
        public string FromAccentText { get { return T("LOCLS_Backdrop_FromAccent", "Colours of the accent"); } }
        public string FromCoverText { get { return T("LOCLS_Backdrop_FromCover", "Colours of the game artwork"); } }
        public string SeedText { get { return T("LOCLS_Backdrop_Seed", "Seed"); } }

        public int ModeNumber
        {
            get { int n; return int.TryParse(store.Get("BackdropMode"), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : 0; }
        }

        /// <summary>True when a generated backdrop (not the game artwork) is on: the palette and the shortcuts are useful.</summary>
        public bool IsGenerated { get { return ModeNumber > 0; } }

        /// <summary>The three colours, as shown by the swatches of the editor.</summary>
        public IList<string> Palette { get { return new[] { store.Get("BackdropColor1"), store.Get("BackdropColor2"), store.Get("BackdropColor3") }; } }

        public bool HasCover
        {
            get { string path = coverPath == null ? null : coverPath(); return !string.IsNullOrEmpty(path) && File.Exists(path); }
        }

        public BackdropHarmony Harmony { get { return Read(HarmonyKey, BackdropHarmony.Analogous); } }
        public BackdropStyle Style { get { return Read(StyleKey, BackdropStyle.Aurora); } }

        /// <summary>The seed of the last Randomize; typing another one makes that backdrop (same seed, same harmony, same style = same backdrop).</summary>
        public string Seed
        {
            get { return store.Get("BackdropSeed"); }
            set
            {
                int n;
                if (!int.TryParse((value ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n < 0) { SeedError = T("LOCLS_Backdrop_SeedError", "A whole number, 0 or more"); return; }
                SeedError = null;
                if (store.Get("BackdropSeed") != n.ToString(CultureInfo.InvariantCulture)) Generate(n, null);
            }
        }

        public string SeedError
        {
            get { return seedError; }
            private set { if (seedError != value) { seedError = value; Raise("SeedError"); Raise("HasSeedError"); } }
        }

        public bool HasSeedError { get { return seedError != null; } }

        public void SetMode(string id)
        {
            history.BeginBatch();
            try
            {
                string error;
                store.TrySet("BackdropMode", id, out error);
            }
            finally { history.EndBatch(); }
            Refresh();
        }

        /// <summary>A new seed and the palette and circles that come from it with the chosen harmony and style. Switches the artwork off when it was on.</summary>
        public void Randomize()
        {
            Generate(seeds.Next(1, 1000000), null);
        }

        /// <summary>The same, with a seed that is given (tests, the seed box).</summary>
        public void Randomize(int seed)
        {
            Generate(seed, null);
        }

        /// <summary>A palette built on the accent colour of the theme.</summary>
        public void FromAccent()
        {
            string accent = store.Get("GlyphColor");
            if (string.IsNullOrEmpty(accent)) return;
            Generate(CurrentSeed(), accent);
        }

        /// <summary>A palette built on the dominant colour of the artwork of the selected game.</summary>
        public void FromCover()
        {
            string path = coverPath == null ? null : coverPath();
            string color = DominantColor.Of(path);
            if (color == null) return;
            Generate(CurrentSeed(), color);
        }

        private void SetHarmony(string id)
        {
            profile.Editor[HarmonyKey] = id;
            Generate(CurrentSeed(), null);
            Raise("Harmony");
        }

        private void SetStyle(string id)
        {
            profile.Editor[StyleKey] = id;
            Generate(CurrentSeed(), null);
            Raise("Style");
        }

        private int CurrentSeed()
        {
            int n;
            return int.TryParse(store.Get("BackdropSeed"), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n > 0 ? n : 1;
        }

        private void Generate(int seed, string baseColor)
        {
            var look = baseColor == null ? BackdropGenerator.Generate(seed, Harmony, Style) : BackdropGenerator.GenerateFrom(baseColor, seed, Harmony, Style);
            history.BeginBatch();
            try
            {
                string error;
                foreach (var kv in look.ToConstants()) store.TrySet(kv.Key, kv.Value, out error);
                if (ModeNumber == 0) store.TrySet("BackdropMode", "4", out error);   // Randomize on the artwork: the circles are the richest look
                double count;
                if (!double.TryParse(store.Get("BackdropBlobCount"), NumberStyles.Float, CultureInfo.InvariantCulture, out count) || count < 1)
                    store.TrySet("BackdropBlobCount", look.SuggestedCount.ToString(CultureInfo.InvariantCulture), out error);
            }
            finally { history.EndBatch(); }
            Refresh();
        }

        private void Refresh()
        {
            string mode = ModeNumber.ToString(CultureInfo.InvariantCulture);
            foreach (var m in Modes) m.IsSelected = m.Id == mode;
            string harmony = Harmony.ToString(), style = Style.ToString();
            foreach (var h in Harmonies) h.IsSelected = h.Id == harmony;
            foreach (var s in Styles) s.IsSelected = s.Id == style;
            Raise("ModeNumber"); Raise("IsGenerated"); Raise("Palette"); Raise("Seed"); Raise("HasCover");
        }

        private T Read<T>(string key, T fallback) where T : struct
        {
            string text;
            T value;
            return profile.Editor.TryGetValue(key, out text) && Enum.TryParse(text, out value) ? value : fallback;
        }

        private BackdropChoiceViewModel Choice(string id, string key, string english, Action<string> select)
        {
            return new BackdropChoiceViewModel { Id = id, Name = T(key, english), SelectCommand = new RelayCommand(() => select(id)) };
        }

        private string T(string key, string english)
        {
            return localize(key) ?? english;
        }

        private static string SplitWords(string pascal)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in pascal)
            {
                if (char.IsUpper(c) && sb.Length > 0) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }
    }

    /// <summary>The colour that stands out most in a picture: the average of the strongest hue (grey pixels count for little).</summary>
    public static class DominantColor
    {
        /// <summary>"#FFRRGGBB", or null when the file is missing or is not a picture.</summary>
        public static string Of(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                var decoder = BitmapDecoder.Create(new Uri(path), BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
                BitmapSource frame = decoder.Frames[0];
                double scale = 48.0 / Math.Max(frame.PixelWidth, frame.PixelHeight);
                if (scale < 1) frame = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
                var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                int w = converted.PixelWidth, h = converted.PixelHeight, stride = w * 4;
                var pixels = new byte[stride * h];
                converted.CopyPixels(pixels, stride, 0);
                return Of(pixels, w * h);
            }
            catch (Exception ex)
            {
                if (ex is IOException || ex is NotSupportedException || ex is InvalidOperationException || ex is ArgumentException || ex is FileFormatException) return null;
                throw;
            }
        }

        /// <summary>The same on raw Bgra32 pixels.</summary>
        public static string Of(byte[] bgra, int count)
        {
            const int bins = 12;
            var weight = new double[bins];
            var sum = new double[bins, 3];
            double[] all = new double[3];
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                double b = bgra[i * 4] / 255.0, g = bgra[i * 4 + 1] / 255.0, r = bgra[i * 4 + 2] / 255.0;
                if (bgra[i * 4 + 3] < 128) continue;
                double hue, sat, light;
                Rgb.ToHsl(new[] { r, g, b }, out hue, out sat, out light);
                all[0] += r; all[1] += g; all[2] += b; n++;
                double w = sat * (1 - Math.Abs(2 * light - 1));   // vivid and neither black nor white
                int bin = (int)(hue / 360.0 * bins) % bins;
                weight[bin] += w;
                sum[bin, 0] += r * w; sum[bin, 1] += g * w; sum[bin, 2] += b * w;
            }
            if (n == 0) return null;
            int best = 0;
            for (int k = 1; k < bins; k++) if (weight[k] > weight[best]) best = k;
            double[] rgb = weight[best] > 1e-6
                ? new[] { sum[best, 0] / weight[best], sum[best, 1] / weight[best], sum[best, 2] / weight[best] }
                : new[] { all[0] / n, all[1] / n, all[2] / n };
            return Rgb.ToHex(rgb);
        }
    }
}
