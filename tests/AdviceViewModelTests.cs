using System.Collections.Generic;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

public static class AdviceViewModelTests
{
    private class Env
    {
        public FakeSettings Fake;
        public PlayniteSettingsViewModel Playnite;
        public ConstantStore Store;
        public History History;
        public Profile Profile;
        public AdviceViewModel Advice;
        public List<string> Opened = new List<string>();
    }

    private static Env Make()
    {
        var e = new Env();
        e.Fake = new FakeSettings();   // blur 39 and darkening 0.85 on: two things to fix
        e.Playnite = new PlayniteSettingsViewModel(e.Fake);
        e.Store = new ConstantStore(new[]
        {
            new ConstantDef { Key = "HeaderTileScoreAnimation", Type = ConstantType.Boolean, Default = "True" },
            new ConstantDef { Key = "WindowGlassBlurRadius", Type = ConstantType.Double, Default = "36", Min = 0, Max = 150 },
            new ConstantDef { Key = "WindowGlassEnabled", Type = ConstantType.Boolean, Default = "True" }
        });
        e.History = new History(e.Store);
        e.Profile = new Profile { Name = "Default" };
        e.Advice = new AdviceViewModel(e.Playnite, e.Store, e.History, e.Profile, page => e.Opened.Add(page));
        return e;
    }

    private static AdviceItemViewModel Item(Env e, string id) { return e.Advice.Items.Single(i => i.Id == id); }

    public static void The_page_lists_every_check_and_counts_what_needs_attention()
    {
        var e = Make();
        Assert.True(e.Advice.Items.Count >= 8);
        Assert.Equal(2, e.Advice.AttentionCount);
        Assert.Equal("attention", Item(e, "playniteDarken").StatusName);
        Assert.Equal("ok", Item(e, "windowBackground").StatusName);
        Assert.True(e.Advice.Summary.Contains("2"), e.Advice.Summary);
    }

    public static void The_impact_is_worded_as_graphics_performance_or_both()
    {
        var e = Make();
        Assert.Equal("Graphics", Item(e, "playniteDarken").ImpactText);
        Assert.Equal("Graphics and performance", Item(e, "themeAnimations").ImpactText);
        Assert.True(Item(e, "themeAnimations").HasPerformance && Item(e, "themeAnimations").HasGraphics);
    }

    public static void Fixing_writes_the_playnite_setting_and_the_item_turns_green()
    {
        var e = Make();
        Item(e, "playniteDarken").FixCommand.Execute(null);
        Assert.True(e.Fake.Writes.Contains("BackgroundImageDarkAmount=0"));
        Assert.Equal("ok", Item(e, "playniteDarken").StatusName);
        Assert.Equal(1, e.Advice.AttentionCount);
        Assert.False(e.Playnite.Findings.Any(f => f.Id == "windowBackground" && f.Message.Contains("darkening")), "the top banner follows");
    }

    public static void A_theme_item_opens_its_page_and_a_changed_theme_value_is_noticed()
    {
        var e = Make();
        Item(e, "themeScorePulse").OpenPageCommand.Execute(null);
        Assert.Equal("items", e.Opened.Single());
        Assert.Equal("tip", Item(e, "themeScorePulse").StatusName);
        e.Store.TrySet("HeaderTileScoreAnimation", "False", out var error);
        Assert.Equal("ok", Item(e, "themeScorePulse").StatusName);
    }

    public static void The_page_still_works_when_playnites_settings_cannot_be_read()
    {
        var vm = new AdviceViewModel(null, new ConstantStore(new ConstantDef[0]), null, new Profile { Name = "Default" }, null);
        Assert.True(vm.Items.Any(i => i.Id == "themeAnimations"));
        Assert.False(vm.Items.Any(i => i.Id == "playniteBlur"));
    }
}
