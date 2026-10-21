using System.Linq;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    /// <summary>
    /// The plan of the Playnite window drawn when you hover a setting: which of its rectangles light up. The whole window lights every rectangle,
    /// the game page lights its four parts. Pure data: the editor draws it (one plan and one highlight colour on every page).
    /// </summary>
    public sealed class WireframePlan
    {
        private readonly string[] regions;

        private WireframePlan(string[] regions) { this.regions = regions; }

        /// <summary>The plan of a constant, or null when it has none.</summary>
        public static WireframePlan For(string key)
        {
            var r = SettingRegions.For(key);
            return r == null ? null : new WireframePlan(r);
        }

        /// <summary>The plan with nothing lit: a page that touches no part of the window (Profile) still starts with the same small plan.</summary>
        public static readonly WireframePlan None = new WireframePlan(new string[0]);

        /// <summary>The plan for a row of the Items page: the rectangles of the place the item is in.</summary>
        public static WireframePlan ForRegions(string[] regions)
        {
            return regions == null || regions.Length == 0 ? null : new WireframePlan(regions);
        }

        private bool Has(string name) { return regions.Contains(name); }
        private bool Window { get { return Has("window"); } }
        private bool Page { get { return Has("page"); } }

        public bool LitTopBar { get { return Window || Has("topbar"); } }
        public bool LitIconBar { get { return Window || Has("iconbar"); } }
        public bool LitQuick { get { return Window || Has("quick"); } }
        public bool LitList { get { return Window || Has("list"); } }
        public bool LitFilters { get { return Window || Has("filters"); } }
        public bool LitHero { get { return Window || Page || Has("hero"); } }
        public bool LitCards { get { return Window || Page || Has("cards"); } }
        public bool LitInfo { get { return Window || Page || Has("info"); } }
        public bool LitSections { get { return Window || Page || Has("sections"); } }
    }
}
