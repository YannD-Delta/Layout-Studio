using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LayoutStudio.Core
{
    public enum AdviceStatus { Ok, Attention, Tip }

    [Flags]
    public enum AdviceImpact { Graphics = 1, Performance = 2 }

    /// <summary>One line of the "Graphics and performance" page: what a setting is, whether it is a problem, and what it really costs.</summary>
    public class AdviceItem
    {
        public string Id;
        public string Title;
        public string TitleSuffix = "";   // a value shown after the (translated) title, for example " (0.85)"
        public AdviceStatus Status;
        public AdviceImpact Impact;
        public string What;       // what the setting does
        public string MeasuredKey = "";   // suffix of the translation key when the measured text depends on the state ("Off")
        public string Measured;   // what it changes, with the numbers measured on the testbed (docs/reviews/benchmark.md, appearance-options.md)
        public List<KeyValuePair<string, object>> Changes = new List<KeyValuePair<string, object>>();   // Playnite settings to write to fix it
        public string FixLabel;
        public string PageId;     // editor page where a theme setting is changed
        public string PageLabel;
    }

    /// <summary>
    /// Checks Playnite's own window-background options and a few theme choices, and says for each whether it touches the graphics, the
    /// performance, or both. Every number comes from a measurement (tools\testbed-appearance-matrix.ps1, tools\testbed-benchmark.ps1) on a
    /// Radeon RX 7900 XTX: a weaker GPU feels the same options more. Options without a measured effect are never called a problem.
    /// </summary>
    public static class Advice
    {
        public const string MeasuredOn = "Measured on a Radeon RX 7900 XTX with fake games; a weaker graphics card feels the same options more.";

        /// <param name="playnite">Playnite's live settings, or null when they cannot be read.</param>
        /// <param name="theme">Value of a theme constant as text (null when unknown).</param>
        /// <param name="presets">The preset chosen for each preset group of the profile.</param>
        /// <param name="themeGlassOn">The theme's window glass (the artwork behind the window) is on.</param>
        public static IList<AdviceItem> Evaluate(IDictionary<string, object> playnite, Func<string, string> theme, IDictionary<string, string> presets, bool themeGlassOn)
        {
            var items = new List<AdviceItem>();
            if (playnite != null) PlayniteItems(items, playnite, themeGlassOn);
            ThemeItems(items, theme ?? (k => null), presets ?? new Dictionary<string, string>());
            return items;
        }

        public static int AttentionCount(IEnumerable<AdviceItem> items)
        {
            return items.Count(i => i.Status == AdviceStatus.Attention);
        }

        private static void PlayniteItems(List<AdviceItem> items, IDictionary<string, object> d, bool glassOn)
        {
            if (d.ContainsKey("ShowBackgroundImageOnWindow"))
            {
                bool off = !IsOn(d, "ShowBackgroundImageOnWindow") && glassOn;
                var item = new AdviceItem
                {
                    Id = "windowBackground", Title = "Show the background image on the window", Impact = AdviceImpact.Graphics,
                    Status = off ? AdviceStatus.Attention : AdviceStatus.Ok,
                    What = "Playnite's option that hands the selected game's artwork to the theme. The theme's glass blurs and dims it behind the whole window. It must stay ON: it is the only image the glass gets.",
                    Measured = off
                        ? "It is OFF: the window background stays on a static image instead of following the game. Turning it off would save about 17 % memory and 12 % GPU, which is not worth an empty background."
                        : "ON, as it should be. Turning it off saves about 17 % memory and 12 % GPU but leaves the window background static."
                };
                if (off) { item.MeasuredKey = "Off"; item.Changes.Add(new KeyValuePair<string, object>("ShowBackgroundImageOnWindow", true)); item.FixLabel = "Turn on"; }
                items.Add(item);
            }
            bool showOn = IsOn(d, "ShowBackgroundImageOnWindow") || !d.ContainsKey("ShowBackgroundImageOnWindow");
            items.Add(Layer(d, showOn, "playniteDarken", "Darken the window background", "DarkenWindowBackgroundImage", "BackgroundImageDarkAmount",
                "Playnite darkens the artwork before the theme draws it. The theme has its own darkness, so the two add up.",
                "Pulls the colours toward a dark grey: the window dims and loses its colour. No measurable performance cost."));
            items.Add(Layer(d, showOn, "playniteBlur", "Blur the window background", "BlurWindowBackgroundImage", "BackgroundImageBlurAmount",
                "Playnite blurs the artwork before the theme draws it, then the theme blurs it again.",
                "Hidden under the theme's own blur (36); visible when the theme blur is 0. No measurable performance cost."));
            var anim = new AdviceItem
            {
                Id = "playniteTransition", Title = "Animate background transitions", Impact = AdviceImpact.Graphics | AdviceImpact.Performance,
                What = "Playnite fades from one game's background to the next. The theme already fades its own layers.",
                Measured = "While changing games: about +18 % CPU, +60 % GPU and +43 MB memory with it ON. Keeping it off costs nothing visible."
            };
            if (IsOn(d, "BackgroundImageAnimation"))
            {
                anim.Status = AdviceStatus.Attention; anim.FixLabel = "Turn off";
                anim.Changes.Add(new KeyValuePair<string, object>("BackgroundImageAnimation", false));
            }
            items.Add(anim);
            var hq = new AdviceItem
            {
                Id = "playniteHighQuality", Title = "High quality background blur", Impact = AdviceImpact.Graphics,
                What = "A higher quality blur for Playnite's own background layer.",
                Measured = "No measurable difference in our test (alone or with a blur amount), so it is not treated as a problem."
            };
            if (IsOn(d, "HighQualityBackgroundBlur")) { hq.Status = AdviceStatus.Tip; hq.FixLabel = "Turn off"; hq.Changes.Add(new KeyValuePair<string, object>("HighQualityBackgroundBlur", false)); }
            items.Add(hq);
        }

        private static AdviceItem Layer(IDictionary<string, object> d, bool showOn, string id, string title, string flag, string amount, string what, string measured)
        {
            var item = new AdviceItem { Id = id, Title = title, Impact = AdviceImpact.Graphics, What = what, Measured = measured };
            double value = showOn ? Amount(d, flag, amount) : 0;   // with the background image off nothing is drawn, so nothing is darkened or blurred
            if (value > 0)
            {
                item.Status = AdviceStatus.Attention;
                item.TitleSuffix = " (" + value.ToString("0.##", CultureInfo.InvariantCulture) + ")";
                item.FixLabel = "Set to 0";
                item.Changes.Add(new KeyValuePair<string, object>(amount, 0.0));
            }
            return item;
        }

        private static void ThemeItems(List<AdviceItem> items, Func<string, string> theme, IDictionary<string, string> presets)
        {
            string animations;
            bool animOff = presets.TryGetValue("Animations", out animations) && animations == "Off";
            items.Add(new AdviceItem
            {
                Id = "themeAnimations", Title = "Theme animations", Impact = AdviceImpact.Performance | AdviceImpact.Graphics,
                Status = animOff ? AdviceStatus.Ok : AdviceStatus.Tip,
                What = "The fades and slides of the game page when you change game.",
                Measured = "With the Animations preset on Off: about -31 % CPU and -35 % GPU while changing games. The biggest saving of all the options we measured.",
                PageId = "appearance", PageLabel = "Appearance"
            });
            items.Add(new AdviceItem
            {
                Id = "themeScorePulse", Title = "Score tile pulse", Impact = AdviceImpact.Performance | AdviceImpact.Graphics,
                Status = theme("HeaderTileScoreAnimation") == "False" ? AdviceStatus.Ok : AdviceStatus.Tip,
                What = "The score tile glows and pulses when the selected game has a top score.",
                Measured = "It runs all the time the game is selected: turning it off cut the GPU use at rest by about 46 %.",
                PageId = "header", PageLabel = "Game header"
            });
            string material;
            bool lite = presets.TryGetValue("Material", out material) && material == "Light";
            items.Add(new AdviceItem
            {
                Id = "themeMaterial", Title = "Material: Lite", Impact = AdviceImpact.Performance | AdviceImpact.Graphics,
                Status = lite ? AdviceStatus.Ok : AdviceStatus.Tip,
                What = "The Lite material drops the glow and the shadows.",
                Measured = "About -47 % GPU at rest and -10 % while changing games; the look is flatter.",
                PageId = "appearance", PageLabel = "Appearance"
            });
            double radius;
            bool big = double.TryParse(theme("WindowGlassBlurRadius"), NumberStyles.Float, CultureInfo.InvariantCulture, out radius) && radius >= 80;
            items.Add(new AdviceItem
            {
                Id = "themeBlurRadius", Title = "Window background blur", TitleSuffix = big ? " (" + radius.ToString("0.##", CultureInfo.InvariantCulture) + ")" : "",
                Impact = AdviceImpact.Performance | AdviceImpact.Graphics,
                Status = big ? AdviceStatus.Tip : AdviceStatus.Ok,
                What = "How strongly the artwork behind the window is blurred (the default is 36).",
                Measured = "At 100 the GPU use while changing games is about 27 % higher than at 36; below that no difference was measurable.",
                PageId = "visual-background", PageLabel = "Background"
            });
        }

        private static bool IsOn(IDictionary<string, object> d, string flag)
        {
            object on;
            return d.TryGetValue(flag, out on) && on is bool && (bool)on;
        }

        private static double Amount(IDictionary<string, object> d, string flag, string amount)
        {
            object value;
            if (!IsOn(d, flag) || !d.TryGetValue(amount, out value) || value == null) return 0;
            try { return Convert.ToDouble(value, CultureInfo.InvariantCulture); }
            catch (Exception) { return 0; }
        }
    }
}
