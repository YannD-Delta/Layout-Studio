using System;
using System.Collections.Generic;
using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>One piece of game information and the constants that show it on each surface of the game page.</summary>
    public class InfoItem
    {
        public string Id;
        public string Title;
        public string FamilyId;           // the Items card of the same information (ItemFamilies), may be null
        public string ChipKey;            // a chip in the artwork card (HeroShow...)
        public int[] TileValues;          // header tile types that show it (values of the HeaderTileSlot constants)
        public string SectionShowKey;     // its own section is shown (null: no switch to hide it)
        public string DemoteKey;          // its own section folds at the bottom when the information is shown above
        public bool InPanel;              // the information panel shows it (the panel has no switch per item yet)
    }

    /// <summary>What a "show once" would change.</summary>
    public class OncePlan
    {
        public readonly List<KeyValuePair<string, string>> Changes = new List<KeyValuePair<string, string>>();
        /// <summary>Surfaces that show the information but cannot be hidden because the theme has no constant for them.</summary>
        public readonly List<string> Blocked = new List<string>();
    }

    /// <summary>
    /// "Information shown once": each information item is assigned to one surface (artwork chips, header tile, its own section, the information panel);
    /// the copies on the other surfaces are hidden. Pure logic over the theme's constants: the table below says which constant shows what.
    /// The information panel has no switch per item in the theme: <c>InfoPanelShow&lt;Id&gt;</c> is used when the theme has such a constant
    /// (the plan reports the panel as blocked until it does).
    /// </summary>
    public static class InfoOnce
    {
        public const string Chips = "chips", Tile = "tile", Section = "section", Panel = "panel";

        /// <summary>The surfaces from the one that is kept by default to the one that is hidden first.</summary>
        public static readonly string[] Priority = { Tile, Chips, Section, Panel };

        public const string ChipLineKey = "HeroShowInfoLine";
        public const string TileBarKey = "DetailsViewShowHeaderBar";
        public const string GlobalDemoteKey = "DemoteSectionsShownElsewhere";

        public static readonly InfoItem[] All =
        {
            new InfoItem { Id = "ReleaseDate", Title = "Release date", FamilyId = "ReleaseDate", ChipKey = "HeroShowReleaseDate", TileValues = new[] { 13 }, InPanel = true },
            new InfoItem { Id = "Platforms", Title = "Platforms", FamilyId = "Platforms", ChipKey = "HeroShowPlatforms", TileValues = new[] { 15 }, InPanel = true },
            new InfoItem { Id = "Source", Title = "Source", FamilyId = "Source", ChipKey = "HeroShowSource", TileValues = new[] { 14 }, InPanel = true },
            new InfoItem { Id = "Developers", Title = "Developers", FamilyId = "Developers", ChipKey = "HeroShowDevelopers", TileValues = new[] { 16 }, InPanel = true },
            new InfoItem { Id = "InstallSize", Title = "Install size", TileValues = new[] { 7 }, InPanel = true },
            new InfoItem { Id = "Genres", Title = "Genres", FamilyId = "Genres", ChipKey = "HeroShowGenres", TileValues = new[] { 18 }, DemoteKey = "DemoteGenresSection" },
            new InfoItem { Id = "Categories", Title = "Categories", FamilyId = "Categories", ChipKey = "HeroShowCategories", DemoteKey = "DemoteCategoriesSection" },
            new InfoItem { Id = "Features", Title = "Features", FamilyId = "Features", ChipKey = "HeroShowFeatures", DemoteKey = "DemoteFeaturesSection" },
            new InfoItem { Id = "Tags", Title = "Tags", FamilyId = "Tags", ChipKey = "HeroShowTags", DemoteKey = "DemoteTagsSection" },
            new InfoItem { Id = "CompletionStatus", Title = "Completion status", FamilyId = "CompletionStatus", ChipKey = "HeroShowCompletionStatus", TileValues = new[] { 3 } },
            new InfoItem { Id = "CommunityScore", Title = "Community score", FamilyId = "CommunityScore", ChipKey = "HeroShowCommunityScore", TileValues = new[] { 6, 24 }, InPanel = true },
            new InfoItem { Id = "Hltb", Title = "HowLongToBeat", FamilyId = "Hltb", ChipKey = "HeroShowHltb", TileValues = new[] { 5, 25, 26, 27 }, SectionShowKey = "DetailsViewShowHltbPanel", DemoteKey = "DemoteHltbSection" },
            new InfoItem { Id = "Achievements", Title = "Achievements", FamilyId = "Achievements", ChipKey = "HeroShowAchievements", TileValues = new[] { 4, 28 }, SectionShowKey = "DetailsViewShowAchievementsPanel", DemoteKey = "DemoteAchievementsSection" },
            new InfoItem { Id = "LatestAchievement", Title = "Latest achievement", FamilyId = "LatestAchievement", ChipKey = "HeroShowLatestAchievement", TileValues = new[] { 29 } },
            new InfoItem { Id = "RareAchievements", Title = "Rare achievements", FamilyId = "RareAchievements", ChipKey = "HeroShowRareAchievements", TileValues = new[] { 30 } },
            new InfoItem { Id = "LastSession", Title = "Last session", FamilyId = "LastSession", ChipKey = "HeroShowLastSession", TileValues = new[] { 9 } },
            new InfoItem { Id = "RecentActivity", Title = "Recent activity", FamilyId = "RecentActivity", ChipKey = "HeroShowRecentActivity", TileValues = new[] { 10 }, InPanel = true }
        };

        public static InfoItem Find(string id) { return All.FirstOrDefault(i => i.Id == id); }

        /// <summary>The panel switch of an item, when the theme has one.</summary>
        public static string PanelKey(InfoItem item) { return "InfoPanelShow" + item.Id; }

        /// <summary>
        /// The surfaces that show the information now. <paramref name="get"/> reads a constant, <paramref name="has"/> says whether the theme has it,
        /// <paramref name="tileSlots"/> are the HeaderTileSlot constants. A section that folds at the bottom (because its information is shown above) does not count.
        /// </summary>
        public static List<string> Visible(InfoItem item, Func<string, string> get, Func<string, bool> has, IList<string> tileSlots)
        {
            var list = new List<string>();
            foreach (var surface in Priority)
                if (IsVisible(item, surface, get, has, tileSlots)) list.Add(surface);
            // a section folded away at the bottom is the theme's own answer to a duplicate: it is not one more place
            if (list.Contains(Section) && list.Count > 1 && Folded(item, get)) list.Remove(Section);
            return list;
        }

        private static bool Folded(InfoItem item, Func<string, string> get)
        {
            return item.DemoteKey != null && get(GlobalDemoteKey) == "True" && get(item.DemoteKey) == "True";
        }

        private static bool IsVisible(InfoItem item, string surface, Func<string, string> get, Func<string, bool> has, IList<string> tileSlots)
        {
            switch (surface)
            {
                case Chips:
                    return item.ChipKey != null && get(item.ChipKey) == "True" && (!has(ChipLineKey) || get(ChipLineKey) == "True");
                case Tile:
                    if (item.TileValues == null || (has(TileBarKey) && get(TileBarKey) != "True")) return false;
                    return tileSlots.Any(k => { int v; return int.TryParse(get(k), out v) && item.TileValues.Contains(v); });
                case Section:
                    return (item.SectionShowKey != null || item.DemoteKey != null) && (item.SectionShowKey == null || get(item.SectionShowKey) == "True");
                default:
                    if (!item.InPanel) return false;
                    string key = PanelKey(item);
                    return !has(key) || get(key) == "True";
            }
        }

        /// <summary>The constants to set so that only <paramref name="keep"/> shows the information, and the surfaces that cannot be hidden.</summary>
        public static OncePlan Plan(InfoItem item, string keep, Func<string, string> get, Func<string, bool> has, IList<string> tileSlots)
        {
            var plan = new OncePlan();
            foreach (var surface in Priority)
            {
                if (surface == keep || !IsVisible(item, surface, get, has, tileSlots)) continue;
                switch (surface)
                {
                    case Chips:
                        plan.Changes.Add(new KeyValuePair<string, string>(item.ChipKey, "False"));
                        break;
                    case Tile:
                        foreach (var k in tileSlots)
                        {
                            int v;
                            if (int.TryParse(get(k), out v) && item.TileValues.Contains(v)) plan.Changes.Add(new KeyValuePair<string, string>(k, "0"));
                        }
                        break;
                    case Section:
                        if (item.SectionShowKey != null) plan.Changes.Add(new KeyValuePair<string, string>(item.SectionShowKey, "False"));
                        else if (item.DemoteKey != null && has(GlobalDemoteKey))
                        {
                            plan.Changes.Add(new KeyValuePair<string, string>(GlobalDemoteKey, "True"));
                            plan.Changes.Add(new KeyValuePair<string, string>(item.DemoteKey, "True"));
                        }
                        else plan.Blocked.Add(surface);
                        break;
                    default:
                        if (has(PanelKey(item))) plan.Changes.Add(new KeyValuePair<string, string>(PanelKey(item), "False"));
                        else plan.Blocked.Add(surface);
                        break;
                }
            }
            return plan;
        }

        /// <summary>The surface kept by default: the first of <see cref="Priority"/> that shows the information.</summary>
        public static string DefaultKeep(IList<string> visible)
        {
            return Priority.FirstOrDefault(visible.Contains);
        }

        /// <summary>Every constant the table reads that must exist in the theme (the panel switches are optional).</summary>
        public static IEnumerable<string> RequiredKeys()
        {
            return All.SelectMany(i => new[] { i.ChipKey, i.SectionShowKey, i.DemoteKey }).Where(k => k != null)
                .Concat(new[] { ChipLineKey, TileBarKey, GlobalDemoteKey }).Distinct();
        }
    }
}
