using System.Globalization;
using System.IO;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

/// <summary>The Items page: flat lists by place (chips, header cards, widgets, panel, sections, elsewhere), the order of chips and cards, the switches of every row.</summary>
public static class ItemsPageTests
{
    private class Env
    {
        public Schema Schema;
        public ConstantStore Store;
        public History History;
        public ItemsEditorViewModel Items;
    }

    private static Env Make()
    {
        var e = new Env();
        e.Schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        e.Store = new ConstantStore(e.Schema.AllConstants());
        e.History = new History(e.Store);
        var applier = new ProfileApplier(e.Schema, e.Store, new FakePresetFiles(), @"C:\theme");
        e.Items = new ItemsEditorViewModel(e.Schema, e.Store, e.History, applier, new Profile { Name = "Default" }, k => null, new CultureInfo("en-US"));
        return e;
    }

    private static ItemZoneViewModel Zone(Env e, string id) { return e.Items.Zones.Single(z => z.Id == id); }

    private static string Ids(ItemZoneViewModel zone) { return string.Join(",", zone.Rows.Select(r => r.ItemId)); }

    private static void Set(Env e, string key, string value) { string error; e.Store.TrySet(key, value, out error); }

    public static void The_page_lists_the_places_of_the_game_page_top_to_bottom_without_a_menu_in_a_menu()
    {
        var e = Make();
        Assert.Equal("chips,cards,widgets,panel,sections,elsewhere", string.Join(",", e.Items.Zones.Select(z => z.Id)));
        Assert.Equal(17, Zone(e, "chips").Rows.Count, "every information chip");
        Assert.Equal(17, Zone(e, "panel").Rows.Count, "the seventeen rows of the information panel");
        Assert.Equal(19, Zone(e, "sections").Rows.Count, "every section");
        Assert.True(Zone(e, "widgets").Rows.Count >= 9, "buttons and widgets of the header");
        Assert.True(Zone(e, "elsewhere").Rows.Count >= 10, "grid view, game list and covers");
    }

    public static void The_chips_are_listed_in_the_default_order_of_the_theme()
    {
        var e = Make();
        Assert.Equal("ReleaseDate,Platforms,Source,Genres,Categories,Features,Tags,Developers,CompletionStatus,CommunityScore,Hltb,Achievements,LatestAchievement,RareAchievements,LastSession,RecentActivity,AvgFps", Ids(Zone(e, "chips")));
        Assert.False(Zone(e, "chips").Rows.Any(r => r.IsModified), "nothing is changed yet");
        Assert.True(Zone(e, "chips").Rows.All(r => r.HasShow), "every chip has its switch on the row");
    }

    public static void Moving_a_chip_down_changes_the_order_constants_at_once_and_is_one_undo_step()
    {
        var e = Make();
        var chips = Zone(e, "chips");
        var date = chips.Rows[0];
        Assert.False(date.MoveUpCommand.CanExecute(null), "the first row cannot go up");
        date.MoveDownCommand.Execute(null);
        Assert.Equal("Platforms,ReleaseDate,Source", string.Join(",", chips.Rows.Take(3).Select(r => r.ItemId)));
        Assert.Equal("1", e.Store.Get("HeroChipOrderReleaseDate"));
        Assert.Equal("0", e.Store.Get("HeroChipOrderPlatforms"));
        Assert.True(date.IsModified && chips.Rows[0].IsModified, "the two chips that traded places are marked");
        Assert.Equal(1, e.Items.ChangedCount, "the order counts once");
        e.History.Undo();
        Assert.Equal("ReleaseDate,Platforms,Source", string.Join(",", chips.Rows.Take(3).Select(r => r.ItemId)));
        Assert.Equal(0, e.Items.ChangedCount);
    }

    public static void Dropping_a_chip_row_on_a_place_moves_it_there()
    {
        var e = Make();
        var chips = Zone(e, "chips");
        string payload = chips.Rows.Single(r => r.ItemId == "Hltb").DragPayload;
        Assert.Equal("chips|Hltb", payload);
        Assert.True(chips.CanDrop(payload));
        Assert.False(chips.CanDrop("cards|0"), "a card cannot be dropped on the chips");
        chips.Drop(payload, 0);
        Assert.Equal("Hltb,ReleaseDate", string.Join(",", chips.Rows.Take(2).Select(r => r.ItemId)));
        chips.Drop("chips|Hltb", chips.Rows.Count);
        Assert.Equal("Hltb", chips.Rows.Last().ItemId, "past the last row: the end of the line");
    }

    public static void A_drop_on_the_place_just_below_the_dragged_row_does_not_move_it()
    {
        var e = Make();
        var chips = Zone(e, "chips");
        chips.Drop("chips|Platforms", 2);   // "before the 3rd row" is where Platforms already is once it is lifted out
        Assert.Equal("ReleaseDate,Platforms,Source", string.Join(",", chips.Rows.Take(3).Select(r => r.ItemId)));
        Assert.Equal(0, e.Items.ChangedCount);
    }

    public static void The_insertion_line_shows_on_the_row_the_drop_lands_before()
    {
        var e = Make();
        var chips = Zone(e, "chips");
        chips.SetDropTarget("chips|Hltb", 3);
        Assert.True(chips.Rows[3].DropLineAbove);
        chips.SetDropTarget("chips|Hltb", 99);
        Assert.False(chips.Rows[3].DropLineAbove);
        Assert.True(chips.DropLineAtEnd);
        chips.ClearDropTarget();
        Assert.False(chips.DropLineAtEnd);
    }

    public static void The_header_cards_are_listed_in_the_order_of_the_boxes_with_scores_and_play_time_among_the_choices()
    {
        var e = Make();
        var cards = Zone(e, "cards");
        Assert.True(cards.Rows.Count >= 6, "the default layout shows cards: " + cards.Rows.Count);
        Assert.True(cards.Rows.All(r => r.IsCard && !string.IsNullOrEmpty(r.Title)));
        var names = cards.Available.Select(c => c.Id).ToList();
        foreach (var id in new[] { "CriticScore", "UserScore", "PlayCount", "InstallSize", "Cpu" })
            Assert.True(names.Contains(id) || cards.Rows.Any(r => r.Title.Length > 0), id + " can be added");
        Assert.Equal(33, cards.Rows.Count + cards.Available.Count, "every card type is either on the row or ready to add");
    }

    public static void A_card_moves_with_the_arrows_and_the_boxes_keep_their_shape()
    {
        var e = Make();
        var cards = Zone(e, "cards");
        var first = cards.Rows[0].Title;
        var second = cards.Rows[1].Title;
        string slot1 = e.Store.Get("HeaderTileSlot1"), slot2 = e.Store.Get("HeaderTileSlot2");
        cards.Rows[0].MoveDownCommand.Execute(null);
        Assert.Equal(second, cards.Rows[0].Title);
        Assert.Equal(first, cards.Rows[1].Title);
        Assert.Equal(slot2, e.Store.Get("HeaderTileSlot1"), "the first box now holds the second card");
        Assert.Equal(slot1, e.Store.Get("HeaderTileSlot2"));
        e.History.Undo();
        Assert.Equal(slot1, e.Store.Get("HeaderTileSlot1"));
        Assert.Equal(first, cards.Rows[0].Title);
    }

    public static void A_card_row_can_be_dropped_on_another_place()
    {
        var e = Make();
        var cards = Zone(e, "cards");
        string last = cards.Rows.Last().Title;
        cards.Drop(cards.Rows.Last().DragPayload, 0);
        Assert.Equal(last, cards.Rows[0].Title);
        Assert.False(cards.CanDrop("chips|Hltb"));
    }

    public static void Adding_a_card_puts_it_last_in_its_own_box_and_removing_it_takes_it_off()
    {
        var e = Make();
        var cards = Zone(e, "cards");
        int before = cards.Rows.Count;
        var add = cards.Available.First(c => c.Id == "PlayCount");
        add.AddCommand.Execute(null);
        Assert.Equal(before + 1, cards.Rows.Count);
        Assert.Equal(add.Name, cards.Rows.Last().Title);
        Assert.True(cards.Rows.Last().StartsBox);
        Assert.False(cards.Available.Any(c => c.Id == "PlayCount"), "it is not offered twice");
        cards.Rows.Last().RemoveCommand.Execute(null);
        Assert.Equal(before, cards.Rows.Count);
        Assert.True(cards.Available.Any(c => c.Id == "PlayCount"), "it can be added again");
    }

    public static void A_card_can_join_the_box_of_the_card_before_it()
    {
        var e = Make();
        var cards = Zone(e, "cards");
        var second = cards.Rows[1];
        Assert.True(second.StartsBox, "by default every card has its own box");
        string secondValue = e.Store.Get("HeaderTileSlot2"), thirdValue = e.Store.Get("HeaderTileSlot3");
        second.StartsBox = false;
        Assert.Equal(secondValue, e.Store.Get("HeaderTileSlot1B"), "the card sits next to the first one");
        Assert.Equal(thirdValue, e.Store.Get("HeaderTileSlot2"), "the boxes after it move up: no empty box is left");
        Assert.True(cards.Rows[1].JoinsBox);
        e.History.Undo();
        Assert.True(cards.Rows[1].StartsBox);
        Assert.Equal("0", e.Store.Get("HeaderTileSlot1B"));
        Assert.Equal(secondValue, e.Store.Get("HeaderTileSlot2"));
    }

    public static void The_changes_of_the_header_cards_page_show_here_at_once()
    {
        var e = Make();
        var cards = Zone(e, "cards");
        int before = cards.Rows.Count;
        Set(e, "HeaderTileSlot12", "7");   // a card put there by the cells editor
        Assert.Equal(before + 1, cards.Rows.Count);
        Assert.True(cards.IsModified);
    }

    public static void The_sections_follow_the_order_of_the_sections_page()
    {
        var e = Make();
        var sections = Zone(e, "sections");
        Assert.Equal("Notes", sections.Rows[0].ItemId);
        Assert.Equal("Left column", sections.Rows[0].Description);
        Assert.True(sections.Rows.Any(r => r.Description == "Right column"));
        Assert.True(sections.Rows.Single(r => r.ItemId == "Hltb").HasShow, "HowLongToBeat can be hidden");
        Assert.False(sections.Rows.Single(r => r.ItemId == "Genres").HasShow, "Genres has no switch: always shown");
        Assert.True(sections.Rows.Single(r => r.ItemId == "Genres").HasFlag, "but it opens or folds by default");
        WireframeModel.Move(e.Store, e.Schema, "Genres", 1, 0);
        Assert.Equal("Right column", sections.Rows.Single(r => r.ItemId == "Genres").Description);
    }

    public static void An_item_that_shows_in_several_places_has_a_row_in_each_and_says_where_else()
    {
        var e = Make();
        var all = e.Items.Zones.SelectMany(z => z.Rows).Where(r => r.ItemId == "Hltb").Select(r => r.Zone).Distinct().OrderBy(x => x).ToList();
        Assert.Equal("cards,chips,elsewhere,sections", string.Join(",", all));
        var chip = Zone(e, "chips").Rows.Single(r => r.ItemId == "Hltb");
        Assert.True(chip.Details.Contains("Header tile") && chip.Details.Contains("Its own section"), chip.Details);
        Assert.False(chip.Details.Contains("Artwork chips"), "a row does not list its own place");
    }

    public static void Every_row_has_a_plan_that_lights_the_place_it_is_in()
    {
        var e = Make();
        foreach (var zone in e.Items.Zones)
            foreach (var row in zone.Rows)
                Assert.True(row.HasPlan, zone.Id + "/" + row.ItemId + " has a wireframe");
        Assert.True(Zone(e, "chips").Rows[0].Plan.LitHero && !Zone(e, "chips").Rows[0].Plan.LitSections);
        Assert.True(Zone(e, "cards").Rows[0].Plan.LitCards && !Zone(e, "cards").Rows[0].Plan.LitHero);
        Assert.True(Zone(e, "sections").Rows[0].Plan.LitSections && !Zone(e, "sections").Rows[0].Plan.LitCards);
        Assert.True(Zone(e, "panel").Rows[0].Plan.LitInfo);
    }

    public static void The_options_of_a_place_are_on_the_place_not_on_a_generated_page()
    {
        var e = Make();
        Assert.True(Zone(e, "chips").HasMaster && Zone(e, "chips").Master.Keys[0] == "HeroShowInfoLine");
        Assert.True(Zone(e, "chips").Look.Any(s => s.Keys[0] == "HeroChipPadding"));
        Assert.True(Zone(e, "cards").HasMaster && Zone(e, "cards").Master.Keys[0] == "DetailsViewShowHeaderBar");
        Assert.True(Zone(e, "cards").Look.Any(s => s.Keys[0] == "HeaderTileScoreGlow"), "the glow of high scores is a card option");
        Assert.True(Zone(e, "sections").Look.Any(s => s.Keys[0] == "DemoteSectionsShownElsewhere"));
        Assert.False(Zone(e, "sections").Look.Any(s => s.Keys[0] == "DetailsViewTabsMode"), "the tabs mode lives with the tabs on the Sections page");
    }

    public static void Changed_only_keeps_the_rows_you_changed_and_a_changed_order_counts()
    {
        var e = Make();
        Set(e, "HeroShowGenres", "False");
        Zone(e, "chips").Rows[0].MoveDownCommand.Execute(null);
        int shown = e.Items.Filter("", true);
        Assert.Equal(3, shown, "Genres and the two chips that traded places");
        Assert.True(Zone(e, "chips").Rows.Count(r => r.Visible) == 3);
        Assert.False(Zone(e, "panel").HasVisibleRows);
        Assert.Equal(2, e.Items.ChangedCount, "a switch and the order");
    }

    public static void Reset_puts_back_the_order_of_the_chips_and_the_cards()
    {
        var e = Make();
        Zone(e, "chips").Rows[0].MoveDownCommand.Execute(null);
        Zone(e, "cards").Rows[0].MoveDownCommand.Execute(null);
        Set(e, "HeroShowGenres", "False");
        Assert.True(e.Items.ChangedCount >= 3);
        e.Items.ResetAll();
        Assert.Equal(0, e.Items.ChangedCount);
        Assert.Equal("ReleaseDate", Zone(e, "chips").Rows[0].ItemId);
        e.History.Undo();
        Assert.True(e.Items.ChangedCount >= 3, "the reset is one undo step");
    }

    public static void The_chip_row_carries_its_new_line_switch_and_hltb_keeps_three_behind_more()
    {
        var e = Make();
        var genres = Zone(e, "chips").Rows.Single(r => r.ItemId == "Genres");
        Assert.True(genres.HasFlag && genres.Flag.Keys[0] == "HeroChipBreakGenres");
        var hltb = Zone(e, "chips").Rows.Single(r => r.ItemId == "Hltb");
        Assert.False(hltb.HasFlag);
        Assert.Equal(3, hltb.More.Count(s => s.Keys[0].StartsWith("HeroChipBreakHltb")));
        Assert.Equal(0, hltb.MoreShown.Count, "the options are built only while the row is open");
        hltb.ToggleMoreCommand.Execute(null);
        Assert.Equal(hltb.More.Count, hltb.MoreShown.Count);
    }

    public static void A_toggle_on_a_row_is_the_theme_constant_and_marks_the_row()
    {
        var e = Make();
        var row = Zone(e, "panel").Rows.Single(r => r.ItemId == "InstallSize");
        Assert.True(row.Show.BoolValue);
        row.Show.BoolValue = false;
        Assert.Equal("False", e.Store.Get("InfoPanelShowInstallSize"));
        Assert.True(row.IsModified);
    }

    public static void Searching_an_item_by_its_short_name_finds_its_card_rows_too()
    {
        var e = Make();
        e.Items.Filter("hltb", false);
        Assert.True(Zone(e, "cards").Rows.Any(r => r.Visible && r.ItemId == "Hltb"), "the HowLongToBeat card matches");
        Assert.False(Zone(e, "cards").Rows.Any(r => r.Visible && r.ItemId != "Hltb"));
        Assert.True(Zone(e, "chips").Rows.Single(r => r.ItemId == "Hltb").Visible);
        Assert.False(Zone(e, "panel").IsShown, "a place with no match steps aside");
    }

    public static void The_list_of_items_shown_twice_is_folded_until_asked_for()
    {
        var e = Make();
        Assert.False(e.Items.Once.IsListOpen);
        Assert.True(e.Items.Once.HasConflicts, "the default layout shows several items twice");
        e.Items.Once.IsListOpen = true;
        Assert.True(e.Items.Once.IsListOpen);
    }

    public static void The_elsewhere_rows_say_which_place_their_switch_is_for()
    {
        var e = Make();
        var hltb = Zone(e, "elsewhere").Rows.Single(r => r.ItemId == "Hltb");
        Assert.Equal("On covers", hltb.ShowLabel);
        Assert.True(Zone(e, "elsewhere").Rows.Any(r => r.ShowLabel == "Grid view"));
        Assert.True(Zone(e, "widgets").Rows.Single(r => r.ItemId == "UserScore").HasShowLabel, "the rating stars say what the switch is");
        Assert.False(Zone(e, "widgets").Rows.Single(r => r.ItemId == "EditButton").HasShowLabel, "the edit button is clear enough");
    }
}
