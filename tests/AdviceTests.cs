using System.Collections.Generic;
using System.Linq;
using LayoutStudio.Core;

public static class AdviceTests
{
    private static IDictionary<string, object> Clean()
    {
        return new Dictionary<string, object>
        {
            { "ShowBackgroundImageOnWindow", true }, { "BlurWindowBackgroundImage", true }, { "BackgroundImageBlurAmount", 0.0 },
            { "DarkenWindowBackgroundImage", true }, { "BackgroundImageDarkAmount", 0.0 },
            { "HighQualityBackgroundBlur", false }, { "BackgroundImageAnimation", false }
        };
    }

    private static string Theme(string key)
    {
        switch (key)
        {
            case "HeaderTileScoreAnimation": return "False";
            case "WindowGlassBlurRadius": return "36";
            default: return null;
        }
    }

    private static readonly IDictionary<string, string> LitePresets = new Dictionary<string, string> { { "Material", "Light" }, { "Animations", "Off" } };

    private static IList<AdviceItem> Run(IDictionary<string, object> playnite, System.Func<string, string> theme = null, IDictionary<string, string> presets = null, bool glass = true)
    {
        return Advice.Evaluate(playnite, theme ?? Theme, presets ?? LitePresets, glass);
    }

    private static AdviceItem Item(IList<AdviceItem> items, string id) { return items.Single(i => i.Id == id); }

    public static void A_clean_setup_has_nothing_to_fix()
    {
        var items = Run(Clean());
        Assert.Equal(0, items.Count(i => i.Status != AdviceStatus.Ok), string.Join(",", items.Where(i => i.Status != AdviceStatus.Ok).Select(i => i.Id)));
        Assert.True(items.Count >= 8, "every check is listed, green ones included");
    }

    public static void The_window_background_option_off_is_a_graphics_problem_while_the_glass_needs_it()
    {
        var p = Clean(); p["ShowBackgroundImageOnWindow"] = false;
        var item = Item(Run(p), "windowBackground");
        Assert.Equal(AdviceStatus.Attention, item.Status);
        Assert.True((item.Impact & AdviceImpact.Graphics) != 0);
        Assert.True(item.Changes.Any(c => c.Key == "ShowBackgroundImageOnWindow" && (bool)c.Value == true), "the fix turns it ON");
        Assert.Equal(AdviceStatus.Ok, Item(Run(p, glass: false), "windowBackground").Status);
    }

    public static void Darkening_and_blur_above_zero_are_graphics_findings_with_a_fix_to_zero()
    {
        var p = Clean(); p["BackgroundImageDarkAmount"] = 0.85; p["BackgroundImageBlurAmount"] = 39.0;
        var items = Run(p);
        Assert.Equal(AdviceStatus.Attention, Item(items, "playniteDarken").Status);
        Assert.Equal(AdviceStatus.Attention, Item(items, "playniteBlur").Status);
        Assert.True(Item(items, "playniteDarken").Changes.Any(c => c.Key == "BackgroundImageDarkAmount" && (double)c.Value == 0.0));
        Assert.True((Item(items, "playniteDarken").Impact & AdviceImpact.Performance) == 0, "no measurable performance cost");
        p["DarkenWindowBackgroundImage"] = false; p["BlurWindowBackgroundImage"] = false;
        Assert.Equal(AdviceStatus.Ok, Item(Run(p), "playniteDarken").Status, "an option that is off applies nothing");
        Assert.Equal(AdviceStatus.Ok, Item(Run(p), "playniteBlur").Status);
    }

    public static void Darkening_and_blur_do_nothing_while_the_background_image_is_off()
    {
        var p = Clean(); p["ShowBackgroundImageOnWindow"] = false; p["BackgroundImageDarkAmount"] = 0.85; p["BackgroundImageBlurAmount"] = 39.0;
        var items = Run(p);
        Assert.Equal(AdviceStatus.Ok, Item(items, "playniteDarken").Status, "nothing is drawn, so nothing is darkened");
        Assert.Equal(AdviceStatus.Ok, Item(items, "playniteBlur").Status);
        Assert.Equal(1, Advice.AttentionCount(items), "only the option that is off");
    }

    public static void The_background_transition_animation_is_a_performance_problem_with_the_measured_cost()
    {
        var p = Clean(); p["BackgroundImageAnimation"] = true;
        var item = Item(Run(p), "playniteTransition");
        Assert.Equal(AdviceStatus.Attention, item.Status);
        Assert.True((item.Impact & AdviceImpact.Performance) != 0);
        Assert.True(item.Measured.Contains("60"), item.Measured);
        Assert.True(item.Changes.Any(c => c.Key == "BackgroundImageAnimation" && (bool)c.Value == false));
    }

    public static void High_quality_blur_is_only_a_tip_because_no_cost_was_measured()
    {
        var p = Clean(); p["HighQualityBackgroundBlur"] = true;
        var item = Item(Run(p), "playniteHighQuality");
        Assert.Equal(AdviceStatus.Tip, item.Status);
        Assert.True(item.Measured.ToLowerInvariant().Contains("no measurable"), item.Measured);
    }

    public static void Theme_choices_that_cost_performance_are_tips_with_a_page_to_change_them()
    {
        var presets = new Dictionary<string, string>();
        System.Func<string, string> theme = k => k == "HeaderTileScoreAnimation" ? "True" : k == "WindowGlassBlurRadius" ? "100" : null;
        var items = Run(Clean(), theme, presets);
        foreach (var id in new[] { "themeAnimations", "themeScorePulse", "themeMaterial", "themeBlurRadius" })
        {
            var item = Item(items, id);
            Assert.Equal(AdviceStatus.Tip, item.Status, id);
            Assert.True(!string.IsNullOrEmpty(item.PageId), id + " links to the page that changes it");
            Assert.True((item.Impact & AdviceImpact.Performance) != 0, id);
        }
        Assert.True(Item(items, "themeAnimations").Measured.Contains("31"), "measured: -31 % CPU");
        Assert.Equal(AdviceStatus.Ok, Item(Run(Clean(), Theme, LitePresets), "themeAnimations").Status);
    }

    public static void Every_item_explains_itself_and_every_problem_has_a_way_out()
    {
        var p = Clean(); p["ShowBackgroundImageOnWindow"] = false; p["BackgroundImageAnimation"] = true; p["BackgroundImageDarkAmount"] = 0.5;
        foreach (var item in Run(p, k => "True", new Dictionary<string, string>()))
        {
            Assert.True(!string.IsNullOrEmpty(item.Title) && !string.IsNullOrEmpty(item.What) && !string.IsNullOrEmpty(item.Measured), item.Id);
            Assert.True(item.Impact != 0, item.Id + " says what it touches");
            if (item.Status != AdviceStatus.Ok) Assert.True(item.Changes.Count > 0 || !string.IsNullOrEmpty(item.PageId), item.Id + " has a fix or a page");
        }
    }

    public static void Without_access_to_Playnites_settings_only_the_theme_checks_remain()
    {
        var items = Run(null);
        Assert.False(items.Any(i => i.Id.StartsWith("playnite") || i.Id == "windowBackground"));
        Assert.True(items.Any(i => i.Id == "themeAnimations"));
    }

    public static void The_summary_counts_what_needs_attention()
    {
        var p = Clean(); p["BackgroundImageAnimation"] = true;
        var items = Run(p);
        Assert.Equal(1, Advice.AttentionCount(items));
        Assert.Equal(0, Advice.AttentionCount(Run(Clean())));
    }
}
