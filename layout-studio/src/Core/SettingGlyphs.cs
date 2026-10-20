using System.Collections.Generic;
using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>
    /// Which settings get a small schematic next to their slider (the artwork card look: outline, blur, tint, fade...), and which schematic.
    /// The editor draws it from the current value, so the picture moves while the slider does.
    /// </summary>
    public static class SettingGlyphs
    {
        private static readonly Dictionary<string, string> ByKey = new Dictionary<string, string>
        {
            { "HeroCardBorderOpacity", "outline" },
            { "HeroBlurRadius", "blur-strong" },
            { "HeroBlurRadiusSoft", "blur-light" },
            { "HeroBlurOpacity", "blur-opacity" },
            { "HeroBlurStart", "blur-start" },
            { "HeroBlurMid", "blur-mid" },
            { "HeroBlurFull", "blur-full" },
            { "HeroTintAccentOpacity", "tint-accent" },
            { "HeroTintLightOpacity", "frost" },
            { "HeroFadeStart", "fade" },
            { "WindowGlassBlurRadius", "blur-strong" }
        };

        /// <summary>The glyph id of a constant, or null when it has none.</summary>
        public static string For(string key)
        {
            string id;
            if (key == null) return null;
            if (ByKey.TryGetValue(key, out id)) return id;
            // the families: an opacity shows the picture through a veil, a font size shows a sample of text at that size
            if (key.EndsWith("Opacity", System.StringComparison.Ordinal)) return "opacity";
            if (key.Contains("FontSize")) return "text-size";
            return null;
        }

        public static IEnumerable<string> Ids { get { return new HashSet<string>(ByKey.Values.Concat(new[] { "opacity", "text-size" })); } }
    }
}
