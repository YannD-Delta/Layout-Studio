using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>Constants that were split or renamed: a profile or share code saved before the change keeps working.</summary>
    public static class LegacyKeys
    {
        /// <summary>PanelGlassOpacity became one opacity per zone (top bar, left icon bar, game list, filter panel).</summary>
        private static readonly string[] PanelZones = { "TopPanelGlassOpacity", "SidebarGlassOpacity", "LibraryPanelGlassOpacity", "FilterPanelGlassOpacity" };

        /// <summary>
        /// Version 2: the Links section took row 0 of both columns and every other section order moved down by one. A profile saved before keeps
        /// its own order numbers one row further (so its order is what it was) and is stamped, so this happens once. Only for a theme that has
        /// the Links section: another theme (Fullscreen, an older Modula) keeps its profile untouched.
        /// </summary>
        public static void ShiftSectionOrders(Profile profile, Schema schema)
        {
            if (profile == null || profile.Overrides == null || schema == null || profile.Version >= 2) return;
            if (!schema.Sections.Any(d => d.Id == "Links")) return;
            foreach (var d in schema.Sections)
                foreach (var key in new[] { d.LeftKey, d.RightKey })
                {
                    string value; int n;
                    if (!string.IsNullOrEmpty(key) && profile.Overrides.TryGetValue(key, out value)
                        && int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out n))
                        profile.Overrides[key] = (n + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            profile.Version = 2;
        }

        /// <summary>The six chip groups of the artwork header (each with the chips it held, in the order the theme lists them), in their default places.</summary>
        private static readonly KeyValuePair<string, string[]>[] ChipGroups =
        {
            new KeyValuePair<string, string[]>("HeroChipsOrderGame", new[] { "ReleaseDate", "Platforms", "Source" }),
            new KeyValuePair<string, string[]>("HeroChipsOrderClassification", new[] { "Genres", "Categories", "Features", "Tags" }),
            new KeyValuePair<string, string[]>("HeroChipsOrderStudio", new[] { "Developers", "CompletionStatus", "CommunityScore" }),
            new KeyValuePair<string, string[]>("HeroChipsOrderHltb", new[] { "Hltb" }),
            new KeyValuePair<string, string[]>("HeroChipsOrderAchievements", new[] { "Achievements", "LatestAchievement", "RareAchievements" }),
            new KeyValuePair<string, string[]>("HeroChipsOrderActivity", new[] { "LastSession", "RecentActivity", "AvgFps" })
        };

        public static void Migrate(Profile profile)
        {
            if (profile == null || profile.Overrides == null) return;
            MigratePanelGlass(profile);
            MigrateChipGroups(profile);
        }

        private static void MigratePanelGlass(Profile profile)
        {
            string value;
            if (!profile.Overrides.TryGetValue("PanelGlassOpacity", out value)) return;
            profile.Overrides.Remove("PanelGlassOpacity");
            foreach (var zone in PanelZones)
                if (!profile.Overrides.ContainsKey(zone)) profile.Overrides[zone] = value;
        }

        /// <summary>The six chip group orders became one order number per chip: the chips take the places their group had.</summary>
        private static void MigrateChipGroups(Profile profile)
        {
            if (!ChipGroups.Any(g => profile.Overrides.ContainsKey(g.Key))) return;
            bool hasChipOrders = profile.Overrides.Keys.Any(k => k.StartsWith(ChipLayout.OrderPrefix, StringComparison.Ordinal));
            if (!hasChipOrders)
            {
                // groups by their place (an unset group keeps its default place), then the chips of the groups in the theme's order
                var chips = new List<string>();
                foreach (var g in ChipGroups.Select((g, i) => new { Group = g, Default = i, Place = PlaceOf(profile, g.Key, i) }).OrderBy(x => x.Place).ThenBy(x => x.Default))
                    chips.AddRange(g.Group.Value);
                var theme = ChipGroups.SelectMany(g => g.Value).ToList();
                if (!chips.SequenceEqual(theme))
                    for (int n = 0; n < chips.Count; n++)   // all numbers, so each stays unique
                        profile.Overrides[ChipLayout.OrderKey(chips[n])] = n.ToString(CultureInfo.InvariantCulture);
            }
            foreach (var g in ChipGroups) profile.Overrides.Remove(g.Key);
        }

        private static int PlaceOf(Profile profile, string key, int fallback)
        {
            string text;
            int n;
            return profile.Overrides.TryGetValue(key, out text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : fallback;
        }
    }
}
