using System;
using System.Collections.Generic;
using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>
    /// The places of the game page where an item can show, from the top of the page to the bottom. The Items page lists each place as a flat list of rows
    /// (the order of the rows is the order on the page); this class says which place a constant belongs to and which rectangles of the window plan it lights.
    /// </summary>
    public static class ItemZones
    {
        /// <summary>The information chips of the artwork header.</summary>
        public const string Chips = "chips";
        /// <summary>The header cards (the glass boxes under the artwork).</summary>
        public const string Cards = "cards";
        /// <summary>The rows of the information panel.</summary>
        public const string Panel = "panel";
        /// <summary>Widgets and buttons of the header (players online, music player, buttons, rating stars...).</summary>
        public const string Widgets = "widgets";
        /// <summary>The sections of the game page.</summary>
        public const string Sections = "sections";
        /// <summary>The grid view, the game list and the covers.</summary>
        public const string Elsewhere = "elsewhere";

        public static readonly string[] All = { Chips, Cards, Widgets, Panel, Sections, Elsewhere };

        /// <summary>The widgets of the artwork header and header bar (their constants carry the name of a chip or a section but are neither).</summary>
        private static readonly HashSet<string> WidgetFamilies = new HashSet<string>
        {
            "PlayersCount", "MusicPlayer", "SteamShortcuts", "UpdateStatus", "Logo", "EditButton", "FavoriteButton", "HiddenButton", "InfoButton", "UserScore"
        };

        /// <summary>Constants that decide how a whole place looks or works (no item owns them): the chips line and the cards row.</summary>
        private static readonly HashSet<string> ChipsZoneKeys = new HashSet<string>
        {
            "HeroShowInfoLine", "HeroInfoLineMaxWidth", "HeroStyleShowInfoLine", "HeroChipsOpenSections",
            "HeroChipPadding", "HeroChipSpacing", "HeroChipFontSize", "HeroChipCornerRadius"
        };

        private static readonly HashSet<string> CardsZoneKeys = new HashSet<string>
        {
            "HeaderTileScoreGlow", "HeaderTileScoreAnimation", "HeaderTileBackgroundOpacity", "HeaderTileCardGap", "HeaderTileCardGapBack", "HeaderTileCornerRadius",
            "HeaderTileLabelFontSize", "HeaderTileValueFontSize", "HeaderTileMargin", "HeaderTilePadding", "HeaderRowBackgroundOpacity", "HeaderRowPadding",
            "DetailsViewShowHeaderBar"
        };

        private static readonly HashSet<string> PanelZoneKeys = new HashSet<string> { "DetailsViewShowInfoPanelDefault" };

        private static readonly HashSet<string> SectionsZoneKeys = new HashSet<string> { "DetailsViewTabsMode", "DemoteSectionsShownElsewhere", "DetailsViewUseMetadataUtilities" };

        /// <summary>The place that owns a constant that is about the place itself and not about an item, or null.</summary>
        public static string ZoneOwning(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (ChipsZoneKeys.Contains(key)) return Chips;
            if (CardsZoneKeys.Contains(key) || key.StartsWith("HeaderTileSlot", StringComparison.Ordinal)) return Cards;
            if (PanelZoneKeys.Contains(key)) return Panel;
            if (SectionsZoneKeys.Contains(key)) return Sections;
            return null;
        }

        /// <summary>The constant that switches a whole place on or off (the chips line, the header bar), or null.</summary>
        public static string MasterKey(string zone)
        {
            switch (zone) { case Chips: return "HeroShowInfoLine"; case Cards: return "DetailsViewShowHeaderBar"; default: return null; }
        }

        /// <summary>The place of a constant that belongs to an item, or null when it is not an item's.</summary>
        public static string ZoneOf(string key)
        {
            var family = ItemFamilies.FamilyOf(key);
            if (family == null || ZoneOwning(key) != null) return null;
            if (key.StartsWith("InfoPanelShow", StringComparison.Ordinal)) return Panel;
            if (key.StartsWith("GridView", StringComparison.Ordinal) || key.StartsWith("GridCover", StringComparison.Ordinal)
                || key.StartsWith("LibraryList", StringComparison.Ordinal) || key.EndsWith("ShowOnCover", StringComparison.Ordinal)) return Elsewhere;
            if (WidgetFamilies.Contains(family.Id)) return Widgets;
            if (key.StartsWith("HeroShow", StringComparison.Ordinal) || key.StartsWith("HeroChipBreak", StringComparison.Ordinal)
                || key.StartsWith(ChipLayout.OrderPrefix, StringComparison.Ordinal)) return Chips;
            if (key.StartsWith("HeaderTile", StringComparison.Ordinal)) return Cards;
            return Sections;
        }

        /// <summary>The rectangles of the window plan an item lights while it is in this place (see <see cref="SettingRegions"/>).</summary>
        public static string[] RegionsOf(string zone)
        {
            switch (zone)
            {
                case Chips: return new[] { "hero" };
                case Cards: return new[] { "cards" };
                case Widgets: return new[] { "hero", "cards" };
                case Panel: return new[] { "info" };
                case Sections: return new[] { "sections" };
                default: return new[] { "list", "page" };
            }
        }
    }
}
