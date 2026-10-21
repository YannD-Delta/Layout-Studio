using System;
using System.Collections.Generic;
using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>Whether an add-on the theme works with is installed on this machine.</summary>
    public enum AddonState
    {
        /// <summary>The editor cannot ask Playnite (the list of add-ons is not available here).</summary>
        Unknown,
        Missing,
        /// <summary>Installed but switched off in Playnite.</summary>
        Disabled,
        Installed
    }

    /// <summary>One installed add-on as Playnite lists it: its id and whether it is switched on.</summary>
    public struct InstalledAddon
    {
        public string Id;
        public bool Enabled;
        public InstalledAddon(string id, bool enabled) { Id = id; Enabled = enabled; }
    }

    /// <summary>
    /// Recognises the add-ons of the Add-ons page among the installed ones. The theme knows an add-on by words that appear in its id
    /// (the folder name of the extension), because the ids of the add-on database differ from one add-on to the next ("Review_Viewer_&lt;guid&gt;",
    /// "playnite-howlongtobeat-plugin", "felixkmh_Extras_Plugin"): both sides are compared as lower-case letters and digits.
    /// </summary>
    public static class AddonInventory
    {
        /// <summary>Letters and digits only, lower case: "Review_Viewer_ca24..." and "reviewviewer" are the same word.</summary>
        public static string Normalize(string text)
        {
            return text == null ? "" : new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        /// <summary>The state of an add-on, and the installed id that matched (null when none did). <paramref name="installed"/> null means the list is unavailable.</summary>
        public static AddonState Detect(AddonDef addon, IEnumerable<InstalledAddon> installed, out string matchedId)
        {
            matchedId = null;
            if (installed == null) return AddonState.Unknown;
            if (addon == null || addon.Match.Count == 0) return AddonState.Missing;
            var words = addon.Match.Select(Normalize).Where(w => w.Length > 0).ToList();
            var hits = installed.Where(i => i.Id != null && words.Any(w => Normalize(i.Id).IndexOf(w, StringComparison.Ordinal) >= 0)).ToList();
            if (hits.Count == 0) return AddonState.Missing;
            var on = hits.FirstOrDefault(h => h.Enabled);
            if (on.Id != null) { matchedId = on.Id; return AddonState.Installed; }
            matchedId = hits[0].Id;
            return AddonState.Disabled;
        }
    }
}
