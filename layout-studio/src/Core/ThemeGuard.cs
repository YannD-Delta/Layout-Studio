using System;
using System.Collections.Generic;
using System.Linq;

namespace LayoutStudio.Core
{
    public enum GuardResult { Ok, WrongTheme, ThemeOptionsActive }

    /// <summary>Decides whether Layout Studio may touch the UI: only for Modula, and not while ThemeOptions is active.</summary>
    public static class ThemeGuard
    {
        public const string ThemeId = "42f3aebe-d455-44a7-bd7f-2dd8afaec9a6";
        public const string FullscreenThemeId = "f0680ff7-f5f3-4338-a545-5eb791243bb6";

        /// <summary>The theme this application mode is allowed to touch.</summary>
        public static string IdFor(bool isDesktop) { return isDesktop ? ThemeId : FullscreenThemeId; }

        /// <summary>Folder name under Themes\ (and under the profiles) for this mode.</summary>
        public static string ModeFolder(bool isDesktop) { return isDesktop ? "Desktop" : "Fullscreen"; }

        public static GuardResult Evaluate(string activeThemeId, IEnumerable<string> enabledExtensionIds)
        {
            return Evaluate(activeThemeId, enabledExtensionIds, true);
        }

        public static GuardResult Evaluate(string activeThemeId, IEnumerable<string> enabledExtensionIds, bool isDesktop)
        {
            if (!string.Equals(activeThemeId, IdFor(isDesktop), StringComparison.OrdinalIgnoreCase)) return GuardResult.WrongTheme;
            if (enabledExtensionIds != null && enabledExtensionIds.Any(id => id != null && id.StartsWith("ThemeOptions_", StringComparison.OrdinalIgnoreCase)))
                return GuardResult.ThemeOptionsActive;
            return GuardResult.Ok;
        }
    }
}
