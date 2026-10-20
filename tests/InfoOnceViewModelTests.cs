using System.Globalization;
using System.IO;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

public static class InfoOnceViewModelTests
{
    private class Env { public Schema Schema; public ConstantStore Store; public History History; public Profile Profile; public ItemsEditorViewModel Items; }

    private static Env Make(System.Func<string, string> localize = null)
    {
        var e = new Env();
        e.Schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        e.Store = new ConstantStore(e.Schema.AllConstants());
        e.History = new History(e.Store);
        e.Profile = new Profile { Name = "Default" };
        var applier = new ProfileApplier(e.Schema, e.Store, new FakePresetFiles(), @"C:\theme");
        e.Items = new ItemsEditorViewModel(e.Schema, e.Store, e.History, applier, e.Profile, localize ?? (k => null), new CultureInfo("en-US"));
        return e;
    }

    private static void Set(Env e, string key, string value) { string error; e.Store.TrySet(key, value, out error); }

    private static string FirstTileSlot(Env e)
    {
        return e.Schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Single(c => c.Kind == "tileSlots").Keys[0];
    }

    public static void An_item_on_a_tile_and_a_chip_is_a_conflict_with_the_tile_kept_by_default()
    {
        var e = Make();
        Set(e, "HeroShowInfoLine", "True"); Set(e, "DetailsViewShowHeaderBar", "True");
        Set(e, "HeroShowLastSession", "True");
        Set(e, FirstTileSlot(e), "9");   // last session length tile
        var row = e.Items.Once.Rows.Single(r => r.Id == "LastSession");
        Assert.Equal("tile,chips", string.Join(",", row.Visible));
        Assert.Equal("tile", row.Keep);
        Assert.True(row.WhereText.Contains("Header tile"));
    }

    public static void Show_once_hides_the_other_copies_in_one_undo_step_and_remembers_the_assignment()
    {
        var e = Make();
        Set(e, "HeroShowInfoLine", "True"); Set(e, "DetailsViewShowHeaderBar", "True");
        Set(e, "HeroShowLastSession", "True");
        string slot = FirstTileSlot(e);
        Set(e, slot, "9");
        e.History.Clear();
        var row = e.Items.Once.Rows.Single(r => r.Id == "LastSession");
        row.ShowOnceCommand.Execute(null);   // keeps the tile
        Assert.Equal("False", e.Store.Get("HeroShowLastSession"));
        Assert.Equal("9", e.Store.Get(slot));
        Assert.Equal("tile", e.Profile.Editor["once.LastSession"]);
        Assert.False(e.Items.Once.Rows.Any(r => r.Id == "LastSession"), "it left the list");
        e.History.Undo();
        Assert.Equal("True", e.Store.Get("HeroShowLastSession"));
        Assert.False(e.Profile.Editor.ContainsKey("once.LastSession"), "the assignment goes with the undo");
        Assert.True(e.Items.Once.Rows.Any(r => r.Id == "LastSession"));
    }

    public static void Choosing_another_surface_to_keep_hides_the_tile_instead()
    {
        var e = Make();
        Set(e, "HeroShowInfoLine", "True"); Set(e, "DetailsViewShowHeaderBar", "True");
        Set(e, "HeroShowLastSession", "True");
        string slot = FirstTileSlot(e);
        Set(e, slot, "9");
        var row = e.Items.Once.Rows.Single(r => r.Id == "LastSession");
        row.Keep = "chips";
        row.ShowOnceCommand.Execute(null);
        Assert.Equal("0", e.Store.Get(slot), "the tile slot is emptied");
        Assert.Equal("True", e.Store.Get("HeroShowLastSession"));
    }

    public static void Force_keeps_an_item_on_several_surfaces_without_reporting_it()
    {
        var e = Make();
        Set(e, "HeroShowInfoLine", "True"); Set(e, "DetailsViewShowHeaderBar", "True");
        Set(e, "HeroShowLastSession", "True");
        Set(e, FirstTileSlot(e), "9");
        var row = e.Items.Once.Rows.Single(r => r.Id == "LastSession");
        row.Forced = true;
        Assert.False(e.Items.Once.Rows.Any(r => r.Id == "LastSession"));
        Assert.Equal("force", e.Profile.Editor["once.LastSession"]);
        e.History.Undo();
        Assert.True(e.Items.Once.Rows.Any(r => r.Id == "LastSession"), "undo brings the report back");
    }

    public static void The_information_panel_copy_is_hidden_when_the_theme_has_its_switch_and_blocked_with_the_missing_name_when_not()
    {
        var e = Make();
        Set(e, "HeroShowInfoLine", "True");
        Set(e, "HeroShowPlatforms", "True");
        Set(e, "HeroShowSource", "True");
        bool platformsSwitch = e.Store.Find("InfoPanelShowPlatforms") != null, sourceSwitch = e.Store.Find("InfoPanelShowSource") != null;
        var platforms = e.Items.Once.Rows.Single(r => r.Id == "Platforms");
        Assert.Equal("chips,panel", string.Join(",", platforms.Visible));
        if (platformsSwitch)
        {
            Assert.False(platforms.HasBlocked, "the theme has the panel switch");
            Assert.Equal(1, e.Items.Once.ShowOnce(InfoOnce.Find("Platforms"), "chips"));
            Assert.Equal("False", e.Store.Get("InfoPanelShowPlatforms"));
            Assert.False(e.Items.Once.Rows.Any(r => r.Id == "Platforms"), "it left the list");
        }
        var source = e.Items.Once.Rows.SingleOrDefault(r => r.Id == "Source");
        if (!sourceSwitch)
        {
            Assert.True(source.HasBlocked);
            Assert.True(source.BlockedText.Contains("InfoPanelShowSource"), source.BlockedText);
            Assert.Equal(0, e.Items.Once.ShowOnce(InfoOnce.Find("Source"), "chips"), "nothing can be changed without the switch");
        }
    }
    public static void Show_all_once_applies_every_row_in_one_undo_step()
    {
        var e = Make();
        Set(e, "HeroShowInfoLine", "True"); Set(e, "DetailsViewShowHeaderBar", "True");
        Set(e, "HeroShowLastSession", "True"); Set(e, "HeroShowRecentActivity", "True");
        string slot1 = FirstTileSlot(e);
        var slots = e.Schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Single(c => c.Kind == "tileSlots").Keys;
        Set(e, slots[0], "9"); Set(e, slots[1], "10");
        e.History.Clear();
        e.Items.Once.ShowAllCommand.Execute(null);
        Assert.Equal("False", e.Store.Get("HeroShowLastSession"));
        Assert.Equal("False", e.Store.Get("HeroShowRecentActivity"));
        e.History.Undo();
        Assert.Equal("True", e.Store.Get("HeroShowLastSession"));
        Assert.Equal("True", e.Store.Get("HeroShowRecentActivity"));
    }

    public static void The_summary_says_so_when_nothing_is_shown_twice_and_follows_the_language()
    {
        var e = Make(k => k == "LOCLS_Once_None" ? "Tout est unique" : null);
        foreach (var r in e.Items.Once.Rows.ToList()) r.Forced = true;
        Assert.Equal("Tout est unique", e.Items.Once.Summary);
    }
}
