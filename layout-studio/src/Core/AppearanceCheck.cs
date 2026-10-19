using System;
using System.Collections.Generic;
using System.Globalization;

namespace LayoutStudio.Core
{
    /// <summary>A Playnite setting that fights the theme or costs performance, and what to change to fix it.</summary>
    public class SettingsFinding
    {
        public string Id;
        public string Message;
        public bool IsPerformance;
        public List<KeyValuePair<string, object>> Changes = new List<KeyValuePair<string, object>>();
    }

    /// <summary>
    /// Playnite's own appearance settings blur and darken the window background image before the theme draws it, so they add to the
    /// blur and darkness of Layout Studio; two more options are expensive. This reads the values of the live settings and words a hint.
    /// </summary>
    public static class AppearanceCheck
    {
        public static readonly string[] SettingNames =
        {
            "BlurWindowBackgroundImage", "BackgroundImageBlurAmount", "DarkenWindowBackgroundImage", "BackgroundImageDarkAmount",
            "HighQualityBackgroundBlur", "BackgroundImageAnimation", "ShowBackgroundImageOnWindow"
        };

        public static IList<SettingsFinding> Evaluate(IDictionary<string, object> settings)
        {
            var found = new List<SettingsFinding>();
            if (settings == null) return found;

            // Playnite draws its own copy of the artwork behind the window; its Blur and Darken options act on that copy. With an option
            // OFF, Playnite applies its default blur or darkening, which cannot be adjusted: the only way to get none is the option ON
            // with an amount of 0. So an option that is off is a problem too, and the fix is "on, amount 0".
            if (IsOn(settings, "ShowBackgroundImageOnWindow"))
            {
                var f = new SettingsFinding { Id = "windowBackground" };
                var parts = new List<string>();
                Layer(settings, "BlurWindowBackgroundImage", "BackgroundImageBlurAmount", "blur", parts, f);
                Layer(settings, "DarkenWindowBackgroundImage", "BackgroundImageDarkAmount", "darkening", parts, f);
                if (parts.Count > 0)
                {
                    f.Message = "Playnite's own copy of the window background applies " + string.Join(" and ", parts)
                        + " before the theme draws anything. The theme's blur and darkness come on top. To have none, Playnite's options must be ON with an amount of 0.";
                    found.Add(f);
                }
            }            if (IsOn(settings, "ShowBackgroundImageOnWindow"))
            {
                var f = new SettingsFinding { Id = "playniteBackground",
                    Message = "Playnite also draws its own copy of the game artwork behind the window. It stays visible when you turn the artwork off here, and it is drawn twice when the theme's glass is on." };
                f.Changes.Add(Off("ShowBackgroundImageOnWindow"));
                found.Add(f);
            }
            if (IsOn(settings, "HighQualityBackgroundBlur"))
            {
                var f = new SettingsFinding { Id = "highQualityBlur", IsPerformance = true,
                    Message = "Playnite's high quality background blur is on: it is slow, and the theme already blurs the background itself." };
                f.Changes.Add(Off("HighQualityBackgroundBlur"));
                found.Add(f);
            }
            if (IsOn(settings, "BackgroundImageAnimation"))
            {
                var f = new SettingsFinding { Id = "backgroundAnimation", IsPerformance = true,
                    Message = "Playnite animates the background image when it changes: it redraws the whole window while it fades." };
                f.Changes.Add(Off("BackgroundImageAnimation"));
                found.Add(f);
            }
            return found;
        }

        private static void Layer(IDictionary<string, object> d, string flag, string amount, string what, List<string> parts, SettingsFinding f)
        {
            if (!IsOn(d, flag))
            {
                parts.Add("a default " + what + " (the option is off)");
                f.Changes.Add(new KeyValuePair<string, object>(flag, true));
                f.Changes.Add(new KeyValuePair<string, object>(amount, 0.0));
                return;
            }
            double value = Amount(d, flag, amount);
            if (value > 0)
            {
                parts.Add(what + " (" + value.ToString("0.##", CultureInfo.InvariantCulture) + ")");
                f.Changes.Add(new KeyValuePair<string, object>(amount, 0.0));
            }
        }

        private static KeyValuePair<string, object> Off(string name)
        {
            return new KeyValuePair<string, object>(name, false);
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
