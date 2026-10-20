using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LayoutStudio.Core
{
    /// <summary>One thing the theme shows (a piece of game information or a section): everything the schema lets you decide about it is gathered on its card.</summary>
    public class ItemFamily
    {
        public string Id;
        public string Title;
        public string[] Tokens;   // CamelCase words of the constant names that belong to this item
    }

    /// <summary>
    /// Groups the schema's toggles, sliders, numbers and choices by the item they are about. "Genres" owns HeroShowGenres (the chip),
    /// HeroChipBreakGenres (new line before the chip), DetailsViewExpandGenres (the section opens by default), DetailsViewGenresInRightColumn...
    /// A constant goes to the item with the longest matching word, so "RareAchievements" does not end up with "Achievements".
    /// </summary>
    public static class ItemFamilies
    {
        public static readonly ItemFamily[] All =
        {
            F("ReleaseDate", "Release date", "ReleaseDate"), F("Platforms", "Platforms", "Platforms"), F("Source", "Source", "Source"),
            F("Developers", "Developers", "Developers"), F("Genres", "Genres", "Genres"), F("Categories", "Categories", "Categories"),
            F("Features", "Features", "Features"), F("Tags", "Tags", "Tags"), F("CompletionStatus", "Completion status", "CompletionStatus", "CompStatus"),
            F("CommunityScore", "Community score", "CommunityScore"), F("Hltb", "HowLongToBeat", "Hltb", "HowLongToBeat"),
            F("Achievements", "PlayniteAchievements", "Achievements"), F("LatestAchievement", "Latest achievement", "LatestAchievement"),
            F("RareAchievements", "Rare achievements", "RareAchievements"), F("GameActivity", "GameActivity", "GameActivity"),
            F("LastSession", "Last session", "LastSession"), F("RecentActivity", "Recent activity", "RecentActivity"), F("AvgFps", "Average FPS", "AvgFps"),
            F("Notes", "Play Notes", "Notes"), F("NewsViewer", "News Viewer", "NewsViewer", "NewsPanel"), F("Video", "Video", "Video"),
            F("CheckDlc", "CheckDlc", "CheckDlc"), F("GameRelations", "Game Relations", "GameRelations"), F("WebExplorer", "Web Explorer", "WebExplorer", "Web"),
            F("SteamScreenshots", "Steam Screenshots", "SteamScreenshots"), F("Vndb", "VNDB Nexus", "Vndb"),
            F("ScreenshotsVisualizer", "ScreenshotsVisualizer", "ScreenshotsVisualizer"), F("ReviewViewer", "Review Viewer", "ReviewViewer", "Reviews"),
            F("ScreenshotUtilities", "Screenshot Utilities", "ScreenshotUtilities")
        };

        private static ItemFamily F(string id, string title, params string[] tokens) { return new ItemFamily { Id = id, Title = title, Tokens = tokens }; }

        private static readonly Dictionary<string, Regex> Patterns = All.SelectMany(f => f.Tokens).Distinct()
            .ToDictionary(t => t, t => new Regex("(^|[a-z0-9])" + t + "([A-Z0-9]|$)", RegexOptions.CultureInvariant));

        /// <summary>The item a constant is about, or null.</summary>
        public static ItemFamily FamilyOf(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Contains("SectionOrder")) return null;   // the order of sections belongs to the Sections page
            ItemFamily best = null;
            int length = 0;
            foreach (var family in All)
                foreach (var token in family.Tokens)
                    if (token.Length > length && Patterns[token].IsMatch(key)) { best = family; length = token.Length; }
            return best;
        }

        /// <summary>The item a Details-view section is about (through its show / open / column constants), or null.</summary>
        public static ItemFamily FamilyOfSection(SectionDef section)
        {
            foreach (var key in new[] { section.ExpandKey, section.ShowKey, section.ColumnKey })
            {
                var family = FamilyOf(key);
                if (family != null) return family;
            }
            return null;
        }

        /// <summary>True for the control kinds a card can hold: one constant, edited as a toggle, slider, number or choice.</summary>
        public static bool IsItemControl(Control c)
        {
            if (c == null || c.Keys.Count != 1) return false;
            if (c.Kind != "toggle" && c.Kind != "slider" && c.Kind != "number" && c.Kind != "choice") return false;
            return FamilyOf(c.Keys[0]) != null;
        }

        /// <summary>Where an option shows, in words: the sub-heading of a card.</summary>
        public static string SurfaceOf(string key)
        {
            if (key.StartsWith("Hero", StringComparison.Ordinal)) return "In the artwork chips";
            if (key.StartsWith("GridView", StringComparison.Ordinal) || key.StartsWith("GridCover", StringComparison.Ordinal)) return "Grid view";
            if (key.StartsWith("LibraryList", StringComparison.Ordinal)) return "Game list";
            if (key.EndsWith("ShowOnCover", StringComparison.Ordinal)) return "On covers";
            return "Game page";
        }

        /// <summary>The order the sub-headings are listed in.</summary>
        public static int SurfaceRank(string surface)
        {
            switch (surface) { case "In the artwork chips": return 0; case "Game page": return 1; case "Grid view": return 2; case "Game list": return 3; default: return 4; }
        }

        /// <summary>
        /// What an option says on its card, where its schema title would be ambiguous there ("Genres" for both the section and the chip), or null
        /// to keep the schema's title.
        /// </summary>
        public static string OptionTitle(string key, ItemFamily family)
        {
            if (family == null) return null;
            if (key == "HeroShow" + family.Id) return "Show it in the chips";
            if (key == "HeroChipBreak" + family.Id) return "Start a new line before it";
            if (key.StartsWith("DetailsViewExpand", StringComparison.Ordinal) || (key.StartsWith("DetailsViewShow", StringComparison.Ordinal) && key.EndsWith("PanelDefault", StringComparison.Ordinal)))
                return "Section open by default";
            if (key.StartsWith("DetailsViewShow", StringComparison.Ordinal) && key.EndsWith("Panel", StringComparison.Ordinal)) return "Show the section";
            if (key.EndsWith("InRightColumn", StringComparison.Ordinal)) return "Section in the right column";
            if (key.StartsWith("Demote", StringComparison.Ordinal) && key.EndsWith("Section", StringComparison.Ordinal)) return "Fold the section at the bottom when its information is shown above";
            if (key.StartsWith("GridViewExpand", StringComparison.Ordinal) || (key.StartsWith("GridViewShow", StringComparison.Ordinal) && key.EndsWith("PanelDefault", StringComparison.Ordinal)))
                return "Section open by default";
            if (key.StartsWith("GridViewShow", StringComparison.Ordinal) && key.EndsWith("Panel", StringComparison.Ordinal)) return "Show the section";
            return null;
        }

        /// <summary>The language-file key of a card word: "Show it in the chips" is LOCLS_Item_Showitinthechips (letters only, as written).</summary>
        public static string LocKey(string english)
        {
            return "LOCLS_Item_" + Regex.Replace(english ?? "", "[^A-Za-z]", "");
        }

        /// <summary>Every English word a card shows that the language file can translate (titles of items, sub-headings, option titles).</summary>
        public static IEnumerable<string> Words()
        {
            var words = new List<string>(All.Select(f => f.Title));
            words.AddRange(new[] { "In the artwork chips", "Game page", "Grid view", "Game list", "On covers",
                "Show it in the chips", "Start a new line before it", "Section open by default", "Show the section", "Section in the right column",
                "Fold the section at the bottom when its information is shown above" });
            return words.Distinct();
        }

        /// <summary>The keys of every control a card holds.</summary>
        public static HashSet<string> KeysOf(Schema schema)
        {
            return new HashSet<string>(schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Where(IsItemControl).Select(c => c.Keys[0]));
        }
    }
}
