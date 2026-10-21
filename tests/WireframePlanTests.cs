using LayoutStudio.Editor;

public static class WireframePlanTests
{
    public static void A_panel_setting_lights_only_its_panel()
    {
        var p = WireframePlan.For("TopPanelGlassOpacity");
        Assert.True(p.LitTopBar && !p.LitIconBar && !p.LitList && !p.LitFilters && !p.LitHero && !p.LitSections);
    }

    public static void The_window_lights_every_rectangle()
    {
        var p = WireframePlan.For("WindowGlassBlurRadius");
        Assert.True(p.LitTopBar && p.LitIconBar && p.LitQuick && p.LitList && p.LitFilters && p.LitHero && p.LitCards && p.LitInfo && p.LitSections);
    }

    public static void The_game_page_lights_its_four_parts_and_nothing_else()
    {
        var p = WireframePlan.For("PageGlassScrimOpacity");
        Assert.True(p.LitHero && p.LitCards && p.LitInfo && p.LitSections);
        Assert.True(!p.LitList && !p.LitTopBar && !p.LitIconBar && !p.LitQuick && !p.LitFilters);
    }

    public static void An_unknown_setting_has_no_plan()
    {
        Assert.True(WireframePlan.For("SomethingNobodyListed") == null);
    }
}
