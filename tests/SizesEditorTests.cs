using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

public static class SizesEditorTests
{
    private class Env
    {
        public Schema Schema;
        public ConstantStore Store;
        public History History;
        public Dictionary<string, string> Editor = new Dictionary<string, string>();
        public SizesEditorViewModel Sizes;
    }

    private static Env Make(Dictionary<string, string> editor = null)
    {
        var e = new Env();
        e.Schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        e.Store = new ConstantStore(e.Schema.AllConstants());
        e.History = new History(e.Store);
        if (editor != null) e.Editor = editor;
        e.Sizes = new SizesEditorViewModel(e.Store, e.Schema, e.History, new CultureInfo("en-US"), e.Editor);
        return e;
    }

    private static SizeItemViewModel Item(Env e, string id) { return e.Sizes.Items.Single(i => i.Id == id); }

    // A drag of N real pixels is N * Scale display pixels.
    private static double Px(SizeItemViewModel item, double real) { return real * item.Scale; }

    // ---- axes -------------------------------------------------------------------------------------------------

    public static void Axes_tell_which_handles_to_draw()
    {
        var e = Make();
        Assert.Equal("H", Item(e, "topPanel").Axes);
        Assert.Equal("W", Item(e, "searchBox").Axes);
        Assert.Equal("WH", Item(e, "cover").Axes);
        Assert.Equal("Square", Item(e, "headerButton").Axes);
    }

    public static void A_width_only_item_ignores_a_vertical_movement()
    {
        var e = Make();
        var box = Item(e, "searchBox");
        box.Drag("w", Px(box, 30), Px(box, 500), DragModifiers.None);
        Assert.Equal("530", e.Store.Get("TopPanelSearchBoxWidthHelium"));
    }

    public static void A_height_only_item_ignores_a_horizontal_movement()
    {
        var e = Make();
        var bar = Item(e, "topPanel");
        bar.Drag("h", Px(bar, 500), Px(bar, 10), DragModifiers.None);
        Assert.Equal("60", e.Store.Get("TopPanelHeight"));
    }

    public static void A_square_item_follows_the_larger_movement_and_keeps_both_sides_equal()
    {
        var e = Make();
        var icon = Item(e, "headerButton");
        icon.Drag("wh", Px(icon, 4), Px(icon, 10), DragModifiers.None);
        Assert.Equal("42", e.Store.Get("HeaderButtonSize"));
    }

    // ---- precision and clamping ---------------------------------------------------------------------------------

    public static void Slow_drags_do_not_lose_movement()
    {
        var e = Make();
        var box = Item(e, "searchBox");
        box.BeginGesture();
        for (int i = 0; i < 40; i++) box.Drag("w", Px(box, 0.25), 0, DragModifiers.None);   // 10 real pixels in 40 tiny moves
        box.EndGesture();
        Assert.Equal("510", e.Store.Get("TopPanelSearchBoxWidthHelium"), "each tiny delta used to be rounded to nothing");
    }

    public static void Shift_snaps_to_tens_and_Ctrl_slows_the_pointer()
    {
        var e = Make();
        var box = Item(e, "searchBox");
        box.Drag("w", Px(box, 23), 0, DragModifiers.Shift);
        Assert.Equal("520", e.Store.Get("TopPanelSearchBoxWidthHelium"), "523 snapped to a multiple of 10");
        e.Store.Reset("TopPanelSearchBoxWidthHelium");
        box.Drag("w", Px(box, 40), 0, DragModifiers.Ctrl);
        Assert.Equal("510", e.Store.Get("TopPanelSearchBoxWidthHelium"), "40 pixels of pointer = 10 real pixels");
    }

    public static void The_value_stops_at_the_limit_and_says_so()
    {
        var e = Make();
        var box = Item(e, "searchBox");
        box.Drag("w", Px(box, 5000), 0, DragModifiers.None);
        Assert.Equal("600", e.Store.Get("TopPanelSearchBoxWidthHelium"));
        Assert.True(box.IsWidthClamped, "the handle can turn red");
        box.Drag("w", Px(box, -5000), 0, DragModifiers.None);
        Assert.Equal("80", e.Store.Get("TopPanelSearchBoxWidthHelium"));
        Assert.True(box.IsWidthClamped);
        box.Drag("w", Px(box, 10), 0, DragModifiers.None);
        Assert.False(box.IsWidthClamped, "inside the range again");
    }

    public static void A_whole_gesture_is_one_undo_step()
    {
        var e = Make();
        var cover = Item(e, "cover");
        cover.BeginGesture();
        for (int i = 0; i < 5; i++) cover.Drag("wh", Px(cover, 3), Px(cover, 3), DragModifiers.None);
        cover.EndGesture();
        e.History.Undo();
        Assert.Equal("230", e.Store.Get("HeroCoverWidth"));
        Assert.Equal("345", e.Store.Get("HeroCoverHeight"));
        Assert.False(e.History.CanUndo);
    }

    public static void The_scale_is_a_ladder_step_that_fits_the_largest_value()
    {
        var e = Make();
        var ladder = new[] { 4.0, 2.0, 1.0, 1 / 2.0, 1 / 3.0, 1 / 4.0, 1 / 6.0, 1 / 8.0 };
        foreach (var item in e.Sizes.Items)
        {
            Assert.True(ladder.Any(s => System.Math.Abs(s - item.Scale) < 1e-9), item.Id + " uses a ladder scale");
            Assert.True(item.MaxDisplayWidth <= 340.5 || !item.HasWidth, item.Id + " max width fits the stage");
            Assert.True(item.MaxDisplayHeight <= 160.5 || !item.HasHeight, item.Id + " max height fits the stage");
        }
        Assert.True(Item(e, "linkIcon").Scale >= 2, "small items are drawn larger so they can be grabbed");
    }

    // ---- ratio ------------------------------------------------------------------------------------------------

    public static void The_cover_starts_locked_to_two_by_three()
    {
        var e = Make();
        var cover = Item(e, "cover");
        Assert.True(cover.HasRatio);
        Assert.True(cover.IsRatioLocked);
        Assert.Equal("2:3", cover.Ratios.Single(r => r.IsSelected).Label);
        Assert.False(Item(e, "searchBox").HasRatio);
    }

    public static void With_the_lock_on_the_other_side_follows()
    {
        var e = Make();
        var cover = Item(e, "cover");
        cover.Drag("w", Px(cover, 30), 0, DragModifiers.None);
        Assert.Equal("260", e.Store.Get("HeroCoverWidth"));
        Assert.Equal("390", e.Store.Get("HeroCoverHeight"), "260 x 3/2");
        cover.Drag("h", 0, Px(cover, -30), DragModifiers.None);
        Assert.Equal("360", e.Store.Get("HeroCoverHeight"));
        Assert.Equal("240", e.Store.Get("HeroCoverWidth"), "360 x 2/3");
    }

    public static void The_corner_is_driven_by_the_larger_movement_under_a_lock()
    {
        var e = Make();
        var cover = Item(e, "cover");
        cover.Drag("wh", Px(cover, 30), Px(cover, 3), DragModifiers.None);
        Assert.Equal("260", e.Store.Get("HeroCoverWidth"));
        Assert.Equal("390", e.Store.Get("HeroCoverHeight"));
    }

    public static void Alt_ignores_the_lock_for_one_drag()
    {
        var e = Make();
        var cover = Item(e, "cover");
        cover.Drag("w", Px(cover, 30), 0, DragModifiers.Alt);
        Assert.Equal("260", e.Store.Get("HeroCoverWidth"));
        Assert.Equal("345", e.Store.Get("HeroCoverHeight"), "only the width moved");
    }

    public static void A_free_ratio_moves_each_axis_alone()
    {
        var e = Make();
        var cover = Item(e, "cover");
        cover.SelectRatio("Free");
        Assert.False(cover.IsRatioLocked);
        cover.Drag("w", Px(cover, 30), 0, DragModifiers.None);
        Assert.Equal("260", e.Store.Get("HeroCoverWidth"));
        Assert.Equal("345", e.Store.Get("HeroCoverHeight"));
    }

    public static void The_lock_stops_where_the_ratio_cannot_go_further()
    {
        var e = Make();
        var cover = Item(e, "cover");
        cover.Drag("w", Px(cover, 5000), 0, DragModifiers.None);
        Assert.Equal("400", e.Store.Get("HeroCoverWidth"), "the height limit 600 x 2/3 caps the width");
        Assert.Equal("600", e.Store.Get("HeroCoverHeight"));
        Assert.True(cover.IsWidthClamped);
    }

    public static void Choosing_a_ratio_keeps_the_width_and_recomputes_the_height()
    {
        var e = Make();
        var cover = Item(e, "cover");
        cover.SelectRatio("3:4");
        Assert.Equal("230", e.Store.Get("HeroCoverWidth"));
        Assert.Equal("307", e.Store.Get("HeroCoverHeight"), "230 x 4/3, rounded");
        Assert.True(cover.IsRatioLocked);
        Assert.Equal("3:4", cover.Ratios.Single(r => r.IsSelected).Label);
    }

    public static void A_ratio_that_does_not_fit_the_current_width_lands_on_the_nearest_possible_size()
    {
        var e = Make();
        var cover = Item(e, "cover");
        cover.SelectRatio("16:9");   // width 230 would give a height of 129, below the minimum 150
        Assert.Equal("267", e.Store.Get("HeroCoverWidth"));
        Assert.Equal("150", e.Store.Get("HeroCoverHeight"));
    }

    public static void An_impossible_ratio_is_disabled_with_the_reason()
    {
        var e = Make();
        var play = Item(e, "playButton");
        var square = play.Ratios.Single(r => r.Label == "1:1");
        Assert.False(square.IsEnabled, "a play button 200 to 600 wide cannot be as tall as wide (max height 72)");
        Assert.True(square.DisabledReason != null && square.DisabledReason.Length > 10);
        play.SelectRatio("1:1");
        Assert.False(play.IsRatioLocked, "choosing a disabled chip does nothing");
        Assert.True(Item(e, "cover").Ratios.Single(r => r.Label == "1:1").IsEnabled);
    }

    public static void The_ratio_choice_is_saved_in_the_profile_and_restored()
    {
        var editor = new Dictionary<string, string>();
        var e = Make(editor);
        Item(e, "cover").SelectRatio("3:4");
        Assert.True(editor.ContainsKey("ratio.cover"));
        var again = Make(editor);   // same profile, new editor session
        var cover = Item(again, "cover");
        Assert.True(cover.IsRatioLocked);
        Assert.Equal("3:4", cover.Ratios.Single(r => r.IsSelected).Label);
    }

    public static void Choosing_a_ratio_is_one_undo_step_that_also_restores_the_selection()
    {
        var editor = new Dictionary<string, string>();
        var e = Make(editor);
        var cover = Item(e, "cover");
        cover.SelectRatio("3:4");
        e.History.Undo();
        Assert.Equal("345", e.Store.Get("HeroCoverHeight"));
        Assert.Equal("2:3", cover.Ratios.Single(r => r.IsSelected).Label, "the chip follows the undo");
        Assert.Equal("2:3", editor["ratio.cover"]);
    }

    // ---- typed values and arrows --------------------------------------------------------------------------------

    public static void Typing_a_width_under_a_lock_recomputes_the_height()
    {
        var e = Make();
        var cover = Item(e, "cover");
        cover.WidthText = "300";
        Assert.Equal("300", e.Store.Get("HeroCoverWidth"));
        Assert.Equal("450", e.Store.Get("HeroCoverHeight"));
    }

    public static void Size_text_boxes_parse_the_users_culture_and_refuse_text()
    {
        var e = Make();
        var cover = Item(e, "cover");
        cover.SelectRatio("Free");
        cover.HeightText = "abc";
        Assert.Equal("345", e.Store.Get("HeroCoverHeight"), "invalid text keeps the value");
        Assert.True(cover.Error != null);
    }

    public static void Arrow_keys_nudge_by_the_step_and_ten_with_Shift()
    {
        var e = Make();
        var box = Item(e, "searchBox");
        box.Nudge("w", +1, DragModifiers.None);
        Assert.Equal("501", e.Store.Get("TopPanelSearchBoxWidthHelium"));
        box.Nudge("w", -1, DragModifiers.Shift);
        Assert.Equal("491", e.Store.Get("TopPanelSearchBoxWidthHelium"), "Shift moves by 10");
        e.History.Undo();
        Assert.Equal("501", e.Store.Get("TopPanelSearchBoxWidthHelium"), "one undo step per nudge");
    }

    // ---- card information -------------------------------------------------------------------------------------

    public static void The_card_knows_what_it_is_where_it_is_and_whether_it_changed()
    {
        var e = Make();
        var box = Item(e, "searchBox");
        Assert.True(box.Description.Length > 10);
        Assert.True(box.WhereLabel.Length > 5);
        Assert.Equal("80 - 600 · default 500", box.RangeText);
        Assert.False(box.IsModified);
        box.Drag("w", Px(box, 20), 0, DragModifiers.None);
        Assert.True(box.IsModified);
        box.ResetCommand.Execute(null);
        Assert.False(box.IsModified);
        Assert.Equal("500", e.Store.Get("TopPanelSearchBoxWidthHelium"));
    }

    public static void Small_zones_get_a_beacon_in_the_locator()
    {
        var e = Make();
        Assert.True(Item(e, "headerButton").LocatorIsSmall);
        Assert.False(Item(e, "heroHeader").LocatorIsSmall);
        Assert.True(Item(e, "topPanel").LocatorWidth > 150, "the top bar spans the locator");
    }

    // ---- groups and filter ------------------------------------------------------------------------------------

    public static void Items_are_grouped_by_zone_of_the_window()
    {
        var e = Make();
        Assert.Equal(3, e.Sizes.Groups.Count);
        Assert.Equal(2, e.Sizes.Groups.Single(g => g.Id == "topBar").Items.Count);
        Assert.Equal(11, e.Sizes.Groups.Sum(g => g.Items.Count));
    }

    public static void The_filter_matches_titles_descriptions_and_places()
    {
        var e = Make();
        e.Sizes.Filter = "search";
        Assert.Equal(1, e.Sizes.VisibleCount);
        e.Sizes.Filter = "banner";
        Assert.True(e.Sizes.VisibleCount >= 3, "the word is in the where-labels of several items");
        e.Sizes.Filter = "zzzz";
        Assert.Equal(0, e.Sizes.VisibleCount);
        Assert.True(e.Sizes.IsEmpty);
    }

    public static void Changed_only_hides_items_at_their_default()
    {
        var e = Make();
        var cover = Item(e, "cover");
        cover.Drag("w", Px(cover, 10), 0, DragModifiers.None);
        e.Sizes.ChangedOnly = true;
        Assert.Equal(1, e.Sizes.VisibleCount);
        Assert.Equal("cover", e.Sizes.Groups.SelectMany(g => g.Items).Single(i => i.IsVisible).Id);
    }

    public static void Clicking_a_card_makes_it_the_active_one()
    {
        var e = Make();
        Assert.True(e.Sizes.Active == null);
        e.Sizes.Activate(Item(e, "cover"));
        Assert.True(ReferenceEquals(e.Sizes.Active, Item(e, "cover")));
        Assert.True(Item(e, "cover").IsActive);
        e.Sizes.Activate(Item(e, "searchBox"));
        Assert.False(Item(e, "cover").IsActive);
    }
}
