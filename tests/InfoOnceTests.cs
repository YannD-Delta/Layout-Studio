using System.Collections.Generic;
using System.IO;
using System.Linq;
using LayoutStudio.Core;

/// <summary>Information shown once: where an item shows now, and what hiding the other copies changes.</summary>
public static class InfoOnceTests
{
    private static readonly string[] Slots = { "HeaderTileSlot1", "HeaderTileSlot2", "HeaderTileSlot3" };

    private class Rig
    {
        public Dictionary<string, string> Values = new Dictionary<string, string>();
        public HashSet<string> Extra = new HashSet<string>();   // constants the "theme" has beyond the values set
        public System.Func<string, string> Get { get { return k => Values.ContainsKey(k) ? Values[k] : "False"; } }
        public System.Func<string, bool> Has { get { return k => Values.ContainsKey(k) || Extra.Contains(k); } }
    }

    private static Rig Rig_(params string[] pairs)
    {
        var r = new Rig();
        for (int i = 0; i < pairs.Length; i += 2) r.Values[pairs[i]] = pairs[i + 1];
        return r;
    }

    private static List<string> Visible(string id, Rig r) { return InfoOnce.Visible(InfoOnce.Find(id), r.Get, r.Has, Slots); }

    public static void An_item_shown_on_chip_tile_and_panel_lists_all_three_in_priority_order()
    {
        var r = Rig_("HeroShowReleaseDate", "True", "HeroShowInfoLine", "True", "DetailsViewShowHeaderBar", "True", "HeaderTileSlot2", "13");
        Assert.Equal("tile,chips,panel", string.Join(",", Visible("ReleaseDate", r)));
    }

    public static void Hiding_the_chip_line_or_the_header_bar_hides_what_they_hold()
    {
        var r = Rig_("HeroShowReleaseDate", "True", "HeroShowInfoLine", "False", "DetailsViewShowHeaderBar", "False", "HeaderTileSlot1", "13");
        Assert.Equal("panel", string.Join(",", Visible("ReleaseDate", r)));
    }

    public static void Keeping_one_surface_hides_the_others_and_reports_the_panel_as_blocked_without_a_switch()
    {
        var r = Rig_("HeroShowReleaseDate", "True", "HeroShowInfoLine", "True", "DetailsViewShowHeaderBar", "True", "HeaderTileSlot2", "13");
        var plan = InfoOnce.Plan(InfoOnce.Find("ReleaseDate"), InfoOnce.Chips, r.Get, r.Has, Slots);
        Assert.Equal("HeaderTileSlot2=0", string.Join(",", plan.Changes.Select(c => c.Key + "=" + c.Value)));
        Assert.Equal("panel", string.Join(",", plan.Blocked));
    }

    public static void When_the_theme_has_a_panel_switch_the_panel_copy_can_be_hidden()
    {
        var r = Rig_("HeroShowPlatforms", "True", "HeroShowInfoLine", "True", "InfoPanelShowPlatforms", "True");
        var plan = InfoOnce.Plan(InfoOnce.Find("Platforms"), InfoOnce.Chips, r.Get, r.Has, Slots);
        Assert.Equal("InfoPanelShowPlatforms=False", string.Join(",", plan.Changes.Select(c => c.Key + "=" + c.Value)));
        Assert.Equal(0, plan.Blocked.Count);
        var keepPanel = InfoOnce.Plan(InfoOnce.Find("Platforms"), InfoOnce.Panel, r.Get, r.Has, Slots);
        Assert.Equal("HeroShowPlatforms=False", string.Join(",", keepPanel.Changes.Select(c => c.Key + "=" + c.Value)));
    }

    public static void A_section_without_a_switch_is_folded_at_the_bottom_instead_of_hidden()
    {
        var r = Rig_("HeroShowGenres", "True", "HeroShowInfoLine", "True", "DemoteSectionsShownElsewhere", "False", "DemoteGenresSection", "False");
        Assert.Equal("chips,section", string.Join(",", Visible("Genres", r)));
        var plan = InfoOnce.Plan(InfoOnce.Find("Genres"), InfoOnce.Chips, r.Get, r.Has, Slots);
        Assert.Equal("DemoteSectionsShownElsewhere=True,DemoteGenresSection=True", string.Join(",", plan.Changes.Select(c => c.Key + "=" + c.Value)));
    }

    public static void A_folded_section_is_not_counted_as_a_second_place()
    {
        var r = Rig_("HeroShowGenres", "True", "HeroShowInfoLine", "True", "DemoteSectionsShownElsewhere", "True", "DemoteGenresSection", "True");
        Assert.Equal("chips", string.Join(",", Visible("Genres", r)));
    }

    public static void A_section_with_a_show_switch_is_hidden_with_it()
    {
        var r = Rig_("HeroShowHltb", "True", "HeroShowInfoLine", "True", "DetailsViewShowHltbPanel", "True");
        var plan = InfoOnce.Plan(InfoOnce.Find("Hltb"), InfoOnce.Chips, r.Get, r.Has, Slots);
        Assert.Equal("DetailsViewShowHltbPanel=False", string.Join(",", plan.Changes.Select(c => c.Key + "=" + c.Value)));
    }

    public static void The_default_surface_to_keep_is_the_first_one_in_priority()
    {
        Assert.Equal("tile", InfoOnce.DefaultKeep(new List<string> { "chips", "tile", "panel" }));
        Assert.Equal("chips", InfoOnce.DefaultKeep(new List<string> { "panel", "chips" }));
        Assert.Equal(null, InfoOnce.DefaultKeep(new List<string>()));
    }

    public static void Every_constant_the_table_needs_exists_and_every_tile_type_is_a_real_tile()
    {
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        var store = new ConstantStore(schema.AllConstants());
        foreach (var key in InfoOnce.RequiredKeys()) Assert.True(store.Find(key) != null, key + " is a constant of the theme");
        var tiles = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Single(c => c.Kind == "tileSlots");
        var values = tiles.Choices.Select(c => c.Value).ToList();
        foreach (var item in InfoOnce.All.Where(i => i.TileValues != null))
            foreach (var v in item.TileValues) Assert.True(values.Contains(v.ToString()), item.Id + " tile " + v + " exists");
        Assert.True(tiles.Keys.Count >= 12);
    }

    public static void Every_item_with_a_card_family_points_at_a_real_family()
    {
        foreach (var item in InfoOnce.All.Where(i => i.FamilyId != null))
            Assert.True(ItemFamilies.All.Any(f => f.Id == item.FamilyId), item.Id + " family " + item.FamilyId);
    }
}
