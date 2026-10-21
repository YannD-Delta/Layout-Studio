using System.Collections.Generic;
using System.IO;
using System.Linq;
using LayoutStudio.Core;

public static class SettingRegionsTests
{
    private static string Joined(string key)
    {
        var r = SettingRegions.For(key);
        return r == null ? null : string.Join(",", r.OrderBy(x => x));
    }

    public static void Each_panel_setting_lights_up_its_own_panel()
    {
        Assert.Equal("topbar", Joined("TopPanelGlassOpacity"));
        Assert.Equal("iconbar", Joined("SidebarGlassOpacity"));
        Assert.Equal("list", Joined("LibraryPanelGlassOpacity"));
        Assert.Equal("filters", Joined("FilterPanelGlassOpacity"));
        Assert.Equal("page", Joined("PageGlassScrimOpacity"));
    }

    public static void The_window_glass_lights_up_the_whole_window()
    {
        Assert.Equal("window", Joined("WindowGlassBlurRadius"));
        Assert.Equal("window", Joined("WindowGlassScrimOpacity"));
    }

    public static void The_side_panel_frost_lights_up_both_side_panels()
    {
        Assert.Equal("filters,quick", Joined("SidePanelFrostOpacity"));
    }

    public static void Game_page_families_light_up_their_part_of_the_page()
    {
        Assert.Equal("hero", Joined("HeroBlurRadius"));
        Assert.Equal("cards", Joined("HeaderTileBackgroundOpacity"));
        Assert.Equal("info", Joined("InfoPanelShowSource"));
        Assert.Equal("sections", Joined("SectionGlassOpacity"));
    }

    public static void The_title_bar_of_a_section_lights_up_the_sections_not_the_header_cards()
    {
        Assert.Equal("sections", Joined("DetailsViewShowSectionHeaders"));
        Assert.Equal("sections", Joined("DetailsViewShowGenresHeader"));
        Assert.Equal("cards", Joined("DetailsViewShowHeaderBar"));
    }

    public static void A_setting_the_map_knows_nothing_about_shows_no_wireframe()
    {
        Assert.True(SettingRegions.For("SomethingNobodyListed") == null);
        Assert.True(SettingRegions.For(null) == null);
    }

    public static void Every_region_name_is_one_the_wireframe_draws()
    {
        var drawn = new HashSet<string>(SettingRegions.All);
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        foreach (var key in schema.AllConstants().Select(c => c.Key))
        {
            var r = SettingRegions.For(key);
            if (r == null) continue;
            foreach (var name in r) Assert.True(drawn.Contains(name), key + " -> " + name + " is not a drawn region");
        }
    }

    public static void Most_of_the_real_settings_have_a_wireframe()
    {
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        var keys = schema.AllConstants().Select(c => c.Key).ToList();
        int covered = keys.Count(k => SettingRegions.For(k) != null);
        Assert.True(covered * 100 / keys.Count >= 55, covered + " of " + keys.Count + " settings have a wireframe: add families to SettingRegions");
    }
}
