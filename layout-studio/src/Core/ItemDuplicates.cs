using System;
using System.Collections.Generic;
using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>
    /// Some items can show twice on the game page: as a chip in the artwork card and as their own section. The theme answers with
    /// "fold the section at the bottom when its information is shown above" (a global switch and one switch per section).
    /// This table says which constants decide it for an item, so a card can say "shown twice" and fold the section in one click.
    /// </summary>
    public static class ItemDuplicates
    {
        public class Rule
        {
            public string FamilyId;
            public string ChipKey;          // the chip is shown in the artwork card
            public string SectionShowKey;   // the section is shown at all (null: always)
            public string DemoteKey;        // fold this section at the bottom when its information is shown above
        }

        public const string GlobalDemoteKey = "DemoteSectionsShownElsewhere";

        public static readonly Rule[] Rules =
        {
            new Rule { FamilyId = "Genres", ChipKey = "HeroShowGenres", DemoteKey = "DemoteGenresSection" },
            new Rule { FamilyId = "Categories", ChipKey = "HeroShowCategories", DemoteKey = "DemoteCategoriesSection" },
            new Rule { FamilyId = "Features", ChipKey = "HeroShowFeatures", DemoteKey = "DemoteFeaturesSection" },
            new Rule { FamilyId = "Tags", ChipKey = "HeroShowTags", DemoteKey = "DemoteTagsSection" },
            new Rule { FamilyId = "Hltb", ChipKey = "HeroShowHltb", SectionShowKey = "DetailsViewShowHltbPanel", DemoteKey = "DemoteHltbSection" },
            new Rule { FamilyId = "Achievements", ChipKey = "HeroShowAchievements", SectionShowKey = "DetailsViewShowAchievementsPanel", DemoteKey = "DemoteAchievementsSection" }
        };

        public static Rule For(string familyId) { return Rules.FirstOrDefault(r => r.FamilyId == familyId); }

        /// <summary>True when the chip and the section both show and the section is not folded at the bottom. <paramref name="get"/> reads a constant.</summary>
        public static bool ShowsTwice(Rule rule, Func<string, string> get)
        {
            if (rule == null) return false;
            if (get(rule.ChipKey) != "True") return false;
            if (rule.SectionShowKey != null && get(rule.SectionShowKey) != "True") return false;
            return !(get(GlobalDemoteKey) == "True" && get(rule.DemoteKey) == "True");
        }

        /// <summary>The constants to set to "True" so the section folds at the bottom.</summary>
        public static IEnumerable<string> FoldKeys(Rule rule) { return new[] { GlobalDemoteKey, rule.DemoteKey }; }

        /// <summary>Every key the table reads, to check against the schema.</summary>
        public static IEnumerable<string> AllKeys()
        {
            return Rules.SelectMany(r => new[] { r.ChipKey, r.SectionShowKey, r.DemoteKey }).Concat(new[] { GlobalDemoteKey }).Where(k => k != null).Distinct();
        }
    }
}
