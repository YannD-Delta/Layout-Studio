using System;
using System.Collections.Generic;
using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>
    /// Which part of the Playnite window a setting touches, for the editor's wireframe: hovering a setting lights that part up on a small plan
    /// of the window. One plan and one highlight colour everywhere in the editor, drawn from this table, no picture and no capture of the window.
    /// The regions are the plan's rectangles: window (all of it), topbar, iconbar, quick (quick filters), list, filters (right filter panel),
    /// page (the whole game page) and its four parts: hero (banner), cards (header cards), info (information panel), sections.
    /// </summary>
    public static class SettingRegions
    {
        /// <summary>Every region name the wireframe draws.</summary>
        public static readonly string[] All = { "window", "topbar", "iconbar", "quick", "list", "filters", "page", "hero", "cards", "info", "sections" };

        // first match wins: the most specific prefixes come first
        private static readonly KeyValuePair<string, string[]>[] Rules = new[]
        {
            Rule("WindowGlass", "window"), Rule("WindowBackground", "window"), Rule("Backdrop", "window"),
            Rule("TopPanel", "topbar"), Rule("Sidebar", "iconbar"),
            Rule("LibraryPanel", "list"), Rule("LibraryList", "list"), Rule("LibraryDetails", "list"), Rule("ListItem", "list"),
            Rule("SidePanelFrost", "quick", "filters"), Rule("ExplorerPanel", "quick"), Rule("FilterPanel", "filters"),
            Rule("PageGlass", "page"), Rule("DetailsPage", "page"), Rule("GridView", "page"), Rule("GridCover", "page"), Rule("GridItem", "page"),
            Rule("Hero", "hero"), Rule("GameLogo", "hero"), Rule("PlayButton", "hero"), Rule("ContextButton", "hero"),
            Rule("HeaderTile", "cards"), Rule("HeaderRow", "cards"), Rule("HeaderBar", "cards"), Rule("HeaderBand", "cards"),
            Rule("HeaderLabel", "cards"), Rule("HeaderText", "cards"), Rule("HeaderButton", "cards"),
            Rule("InfoPanel", "info"), Rule("InfoBackdrop", "info"), Rule("GameInfo", "info"),
            Rule("Section", "sections"), Rule("Demote", "sections"), Rule("DetailsTab", "sections"), Rule("Achievement", "sections"),
            Rule("GameActivity", "sections"), Rule("CheckDlc", "sections"), Rule("ChipGlass", "sections"),
            Rule("Glass", "window"), Rule("Control", "window"), Rule("Popup", "window"), Rule("Scrollbar", "window"),
            Rule("FontSize", "window"), Rule("Glyph", "window"), Rule("Accent", "window")
        };

        private static KeyValuePair<string, string[]> Rule(string prefix, params string[] regions)
        {
            return new KeyValuePair<string, string[]>(prefix, regions);
        }

        /// <summary>The regions a constant touches, or null when the table knows nothing about it (no wireframe is shown).</summary>
        public static string[] For(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            // the game page: anything in the header named so belongs to the cards, the rest of DetailsView* to the sections
            if (key.StartsWith("DetailsView", StringComparison.Ordinal))
            {
                // the title bar of a section (DetailsViewShow<Section>Header, DetailsViewShowSectionHeaders) is the sections', not the header cards'
                if (key.StartsWith("DetailsViewShow", StringComparison.Ordinal) && (key.EndsWith("Header", StringComparison.Ordinal) || key.EndsWith("SectionHeaders", StringComparison.Ordinal))) return new[] { "sections" };
                return key.Contains("Header") ? new[] { "cards" } : new[] { "sections" };
            }
            foreach (var rule in Rules)
                if (key.StartsWith(rule.Key, StringComparison.Ordinal)) return rule.Value;
            return null;
        }
    }
}
