using System.Collections.Generic;

namespace LayoutStudio.Core
{
    /// <summary>Constants that were split or renamed: a profile or share code saved before the change keeps working.</summary>
    public static class LegacyKeys
    {
        /// <summary>PanelGlassOpacity became one opacity per zone (top bar, left icon bar, game list, filter panel).</summary>
        private static readonly string[] PanelZones = { "TopPanelGlassOpacity", "SidebarGlassOpacity", "LibraryPanelGlassOpacity", "FilterPanelGlassOpacity" };

        public static void Migrate(Profile profile)
        {
            if (profile == null || profile.Overrides == null) return;
            string value;
            if (!profile.Overrides.TryGetValue("PanelGlassOpacity", out value)) return;
            profile.Overrides.Remove("PanelGlassOpacity");
            foreach (var zone in PanelZones)
                if (!profile.Overrides.ContainsKey(zone)) profile.Overrides[zone] = value;
        }
    }
}
