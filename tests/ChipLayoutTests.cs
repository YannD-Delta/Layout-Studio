using System.Collections.Generic;
using System.IO;
using System.Linq;
using LayoutStudio.Core;

/// <summary>The order of the information chips in the artwork header: one order constant per chip, the default is the order the theme file lists them.</summary>
public static class ChipLayoutTests
{
    private static Schema Schema_() { return Schema.Load(File.ReadAllText(TestPaths.SchemaPath())); }

    private static string Join(IEnumerable<string> ids) { return string.Join(",", ids); }

    public static void The_default_order_is_the_order_the_theme_lists_the_chips()
    {
        var s = Schema_();
        var order = ChipLayout.Order(new ConstantStore(s.AllConstants()), s);
        Assert.Equal(17, order.Count);
        Assert.Equal("ReleaseDate,Platforms,Source,Genres,Categories,Features,Tags,Developers,CompletionStatus,CommunityScore,Hltb,Achievements,LatestAchievement,RareAchievements,LastSession,RecentActivity,AvgFps", Join(order));
    }

    public static void Every_chip_has_a_show_switch_and_an_order_constant()
    {
        var s = Schema_();
        foreach (var id in ChipLayout.Ids(s))
        {
            Assert.True(s.Find("HeroShow" + id) != null, "HeroShow" + id);
            Assert.True(s.Find(ChipLayout.OrderKey(id)) != null, ChipLayout.OrderKey(id));
        }
    }

    public static void Moving_a_chip_renumbers_the_line_and_keeps_every_number_once()
    {
        var s = Schema_();
        var store = new ConstantStore(s.AllConstants());
        Assert.True(ChipLayout.Move(store, s, "Hltb", 0));
        var order = ChipLayout.Order(store, s);
        Assert.Equal("Hltb,ReleaseDate,Platforms", Join(order.Take(3)));
        var numbers = order.Select(id => store.Get(ChipLayout.OrderKey(id))).ToList();
        Assert.Equal(string.Join(",", Enumerable.Range(0, 17)), string.Join(",", numbers));
        Assert.False(ChipLayout.Move(store, s, "Hltb", 0), "already there: nothing changes");
        Assert.False(ChipLayout.Move(store, s, "NoSuchChip", 3));
    }

    public static void An_index_past_the_end_puts_the_chip_last()
    {
        var s = Schema_();
        var store = new ConstantStore(s.AllConstants());
        Assert.True(ChipLayout.Move(store, s, "ReleaseDate", 99));
        Assert.Equal("ReleaseDate", ChipLayout.Order(store, s).Last());
    }

    public static void Two_chips_with_the_same_number_keep_the_default_order()
    {
        var s = Schema_();
        var store = new ConstantStore(s.AllConstants());
        string error;
        store.TrySet(ChipLayout.OrderKey("AvgFps"), "0", out error);   // ties with ReleaseDate (0)
        var order = ChipLayout.Order(store, s);
        Assert.Equal("ReleaseDate,AvgFps,Platforms", Join(order.Take(3)));
    }

    public static void Arranging_blocks_is_stable_and_leaves_untagged_blocks_in_place()
    {
        // blocks as (chip id) in the theme file: a chip is a line break block then the chip block, both tagged with the id; null = no tag
        var blocks = new List<string> { "A", "A", "B", "B", null, "C", "C" };
        var result = ChipLayout.Arrange(blocks, b => b, new[] { "C", "A", "B" });
        Assert.Equal("C,C,A,A,null,B,B", string.Join(",", result.Select(b => b ?? "null")), "tagged blocks fill the tagged places, the untagged one stays at its place");
    }

    public static void A_block_of_an_unknown_chip_goes_after_the_known_ones()
    {
        var result = ChipLayout.Arrange(new List<string> { "X", "A", "B" }, b => b, new[] { "B", "A" });
        Assert.Equal("B,A,X", string.Join(",", result));
    }

    public static void A_profile_saved_with_the_six_group_orders_keeps_its_order()
    {
        var s = Schema_();
        var p = new Profile { Name = "Old" };
        // groups: Game=0 Classification=1 Studio=2 Hltb=3 Achievements=4 Activity=5; here Hltb is shown first and Classification last
        p.Overrides["HeroChipsOrderHltb"] = "0";
        p.Overrides["HeroChipsOrderGame"] = "1";
        p.Overrides["HeroChipsOrderStudio"] = "2";
        p.Overrides["HeroChipsOrderAchievements"] = "3";
        p.Overrides["HeroChipsOrderActivity"] = "4";
        p.Overrides["HeroChipsOrderClassification"] = "5";
        LegacyKeys.Migrate(p);
        Assert.False(p.Overrides.Keys.Any(k => k.StartsWith("HeroChipsOrder")), "the old keys are gone");
        var store = new ConstantStore(s.AllConstants());
        foreach (var kv in p.Overrides) { string error; store.TrySet(kv.Key, kv.Value, out error); }
        Assert.Equal("Hltb,ReleaseDate,Platforms,Source,Developers,CompletionStatus,CommunityScore,Achievements,LatestAchievement,RareAchievements,LastSession,RecentActivity,AvgFps,Genres,Categories,Features,Tags",
            Join(ChipLayout.Order(store, s)));
    }

    public static void A_profile_that_already_has_chip_orders_ignores_the_old_group_orders()
    {
        var p = new Profile { Name = "Mixed" };
        p.Overrides["HeroChipOrderTags"] = "0";
        p.Overrides["HeroChipsOrderHltb"] = "0";
        LegacyKeys.Migrate(p);
        Assert.False(p.Overrides.ContainsKey("HeroChipsOrderHltb"));
        Assert.Equal("0", p.Overrides["HeroChipOrderTags"]);
        Assert.False(p.Overrides.ContainsKey("HeroChipOrderHltb"), "the new keys win: nothing is invented");
    }

    public static void The_theme_lists_every_chip_once_and_tags_the_line_for_the_plugin()
    {
        var s = Schema_();
        string view = Path.Combine(TestPaths.RepoRoot(), @"theme\Modula\Views\DetailsViewGameOverview.xaml");
        if (!File.Exists(view)) return;   // the exported Layout Studio repository has no theme folder
        string xaml = File.ReadAllText(view);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(xaml, "Tag=\"ls:chips\"").Count, "one chip line");
        foreach (var id in ChipLayout.Ids(s))
        {
            int blocks = System.Text.RegularExpressions.Regex.Matches(xaml, "Uid=\"ls:chip:" + id + "\"").Count;
            Assert.True(blocks >= 2, id + " has its line break block and its chip block: " + blocks);
            Assert.True(xaml.Contains("Options[HeroShow" + id + "]"), id + " is read from its switch");
        }
        Assert.False(xaml.Contains("HeroChipsOrder"), "no group order is left in the view");
        Assert.Equal(0, System.Text.RegularExpressions.Regex.Matches(xaml, "<!-- slot [0-9] -->").Count, "the chips are not repeated in slots any more");
    }
}
