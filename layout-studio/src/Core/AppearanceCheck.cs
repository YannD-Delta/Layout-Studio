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
        public double Blur;   // the Playnite blur amount that applies (0 when none)
        public double Dark;   // the Playnite darkening amount that applies (0 when none)
        public List<KeyValuePair<string, object>> Changes = new List<KeyValuePair<string, object>>();
    }

    /// <summary>
    /// Playnite processes the game artwork before it hands it to the theme: its Blur and Darken options act on that image, so they add to
    /// the blur and darkness of Layout Studio. Measured on the testbed (tools\testbed-appearance-matrix.ps1, screenshots in build\matrix under the local root):
    ///  - "show the background image on the window" OFF: the theme's window glass gets no artwork at all (it stays on a static background);
    ///  - an option OFF changes nothing on screen, exactly like ON with an amount of 0;
    ///  - Darken above 0 pulls the artwork toward a dark grey (0.85: Hollow Lantern 16,36,40 -> 12,15,17; Neon Drifter 15,16,33 -> 21,22,23), so it dims and loses its colour, before the theme's own scrim;
    ///  - Blur above 0 blurs it before the theme's blur: invisible under the theme's default blur, plain to see when the theme blur is 0;
    ///  - with the theme's glass OFF, Playnite paints nothing behind the window, whatever these options say.
    /// High quality blur and the background animation were not measured, so they are not judged.
    /// </summary>
    public static class AppearanceCheck
    {
        public static readonly string[] SettingNames =
        {
            "BlurWindowBackgroundImage", "BackgroundImageBlurAmount", "DarkenWindowBackgroundImage", "BackgroundImageDarkAmount",
            "HighQualityBackgroundBlur", "BackgroundImageAnimation", "ShowBackgroundImageOnWindow"
        };

        /// <param name="themeGlassOn">The theme's window glass (the artwork behind the window) is on.</param>
        public static IList<SettingsFinding> Evaluate(IDictionary<string, object> settings, bool themeGlassOn = true)
        {
            var found = new List<SettingsFinding>();
            if (settings == null) return found;

            if (!IsOn(settings, "ShowBackgroundImageOnWindow"))
            {
                // only report it when the key is really there and false: an unknown Playnite version must not raise a false alarm
                object value;
                if (themeGlassOn && settings.TryGetValue("ShowBackgroundImageOnWindow", out value) && value is bool)
                {
                    var f = new SettingsFinding { Id = "windowBackgroundOff",
                        Message = "Playnite's option to show the background image on the window is off, so the theme's window background has no artwork to draw and stays on a static image. Turn it on." };
                    f.Changes.Add(new KeyValuePair<string, object>("ShowBackgroundImageOnWindow", true));
                    found.Add(f);
                }
                return found;
            }

            var layers = new SettingsFinding { Id = "windowBackground" };
            var parts = new List<string>();
            Layer(settings, "BlurWindowBackgroundImage", "BackgroundImageBlurAmount", "blur", parts, layers);
            Layer(settings, "DarkenWindowBackgroundImage", "BackgroundImageDarkAmount", "darkening", parts, layers);
            if (parts.Count > 0)
            {
                layers.Message = "Playnite applies " + string.Join(" and ", parts)
                    + " to the artwork before the theme draws it. The theme's own blur and darkness come on top. Set the amount to 0 to leave it all to the theme.";
                found.Add(layers);
            }
            return found;
        }

        // An option that is off applies nothing (measured), so only an option that is ON with an amount above 0 is a finding.
        private static void Layer(IDictionary<string, object> d, string flag, string amount, string what, List<string> parts, SettingsFinding f)
        {
            double value = Amount(d, flag, amount);
            if (value > 0)
            {
                parts.Add(what + " (" + value.ToString("0.##", CultureInfo.InvariantCulture) + ")");
                if (what == "blur") f.Blur = value; else f.Dark = value;
                f.Changes.Add(new KeyValuePair<string, object>(amount, 0.0));
            }
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
