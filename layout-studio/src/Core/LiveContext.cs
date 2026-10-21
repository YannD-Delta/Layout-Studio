using System;
using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>What Playnite shows right now, as far as the Sizes page cares: the desktop view and what the selected game has.</summary>
    public class LiveContext
    {
        /// <summary>"details", "grid" or "list".</summary>
        public string View = "details";
        public bool HasGame;
        public bool HasLogo;
        public bool HasDescription;
        public bool HasLinks;
    }

    /// <summary>What keeps a size off the screen: the view it needs, or something the selected game lacks.</summary>
    public class LiveGap
    {
        /// <summary>"view", "nogame", "logo", "description" or "links".</summary>
        public string Kind;
        public string Wanted;    // view: the view the size is shown in
        public string Current;   // view: the view Playnite is on
    }

    /// <summary>
    /// A schema "live" rule says when a size can be seen in the real window: "always", "view:details", or terms joined by "&amp;"
    /// ("view:details &amp; game:links"). Terms: view:&lt;details|grid|list&gt;, game:&lt;logo|description|links&gt;.
    /// </summary>
    public static class LiveRule
    {
        /// <summary>The first thing that keeps the size off the screen, or null when it is on screen.</summary>
        public static LiveGap Gap(string rule, LiveContext context)
        {
            if (string.IsNullOrWhiteSpace(rule) || rule.Trim() == "always") return null;
            if (context == null) return new LiveGap { Kind = "view", Wanted = "", Current = "" };
            foreach (var raw in rule.Split('&'))
            {
                string term = raw.Trim();
                if (term.Length == 0 || term == "always") continue;
                if (term.StartsWith("view:", StringComparison.Ordinal))
                {
                    string wanted = term.Substring(5);
                    if (!string.Equals(context.View, wanted, StringComparison.OrdinalIgnoreCase)) return new LiveGap { Kind = "view", Wanted = wanted, Current = context.View };
                }
                else if (term.StartsWith("game:", StringComparison.Ordinal))
                {
                    if (!context.HasGame) return new LiveGap { Kind = "nogame" };
                    string what = term.Substring(5);
                    bool has = what == "logo" ? context.HasLogo : what == "description" ? context.HasDescription : what == "links" ? context.HasLinks : true;
                    if (!has) return new LiveGap { Kind = what };
                }
            }
            return null;
        }

        public static bool IsLive(string rule, LiveContext context)
        {
            return Gap(rule, context) == null;
        }

        /// <summary>Null when the item is on screen; otherwise a sentence saying what is missing, in English.</summary>
        public static string WhyNot(string rule, LiveContext context)
        {
            return Describe(Gap(rule, context), null);
        }

        /// <summary>
        /// The sentence for a gap. <paramref name="word"/> (key, English) gives the language file's text for a key, or null to use the English:
        /// LOCLS_Live_View ({0} view needed, {1} view now), LOCLS_Live_NoGame, LOCLS_Live_Logo, LOCLS_Live_Description, LOCLS_Live_Links, LOCLS_View_details/grid/list.
        /// </summary>
        public static string Describe(LiveGap gap, Func<string, string, string> word)
        {
            if (gap == null) return null;
            Func<string, string, string> w = word ?? ((key, english) => english);
            switch (gap.Kind)
            {
                case "view":
                    if (string.IsNullOrEmpty(gap.Wanted)) return w("LOCLS_Live_Unknown", "Playnite's current view is not known");
                    return string.Format(System.Globalization.CultureInfo.InvariantCulture, w("LOCLS_Live_View", "Shown in the {0} view; Playnite is on the {1} view now"),
                        View(gap.Wanted, w), View(gap.Current, w));
                case "nogame": return w("LOCLS_Live_NoGame", "Select a game to see it");
                case "logo": return w("LOCLS_Live_Logo", "Shown when the selected game has a logo; this one has none");
                case "description": return w("LOCLS_Live_Description", "Shown when the selected game has a description; this one has none");
                default: return w("LOCLS_Live_Links", "Shown when the selected game has links; this one has none");
            }
        }

        private static string View(string view, Func<string, string, string> w)
        {
            string key = (view ?? "").ToLowerInvariant();
            string english = string.IsNullOrEmpty(key) ? "" : char.ToUpperInvariant(key[0]) + key.Substring(1);
            return w("LOCLS_View_" + key, english);
        }
    }
}
