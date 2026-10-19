using System;
using System.Globalization;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

public static class SettingViewModelTests
{
    private class Env
    {
        public ConstantStore Store;
        public History History;
        public Profile Profile;
        public FakePresetFiles Files;
        public ShellViewModel Shell;
        public Schema Schema;
        public ProfileApplier Applier;
        public string Culture;
    }

    private static Env Make(string culture = "en-US")
    {
        var e = new Env();
        var schema = Schema.Load(TestSchema.Json);
        e.Store = new ConstantStore(schema.AllConstants());
        e.History = new History(e.Store);
        e.Profile = new Profile { Name = "Default" };
        e.Files = new FakePresetFiles();
        var applier = new ProfileApplier(schema, e.Store, e.Files, @"C:\theme");
        e.Schema = schema; e.Applier = applier; e.Culture = culture;
        e.Shell = new ShellViewModel(schema, e.Store, e.History, applier, e.Profile, k => null, new CultureInfo(culture));
        return e;
    }

    // A control a visual page already edits is not on the generated pages: build its view model directly (the Header cards page uses the same logic)
    private static SettingViewModel Direct(Env e, string id)
    {
        var control = e.Schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Single(c => c.Id == id);
        return new SettingViewModel(control, e.Store, e.History, e.Applier, e.Profile, k => null, new CultureInfo(e.Culture));
    }

    private static SettingViewModel Setting(Env e, string id)
    {
        return e.Shell.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Settings).Single(s => s.Id == id);
    }

    // ---- numbers -------------------------------------------------------------------------------------------

    public static void Number_text_with_a_french_comma_is_stored_invariant()
    {
        var e = Make("fr-FR");
        var blur = Setting(e, "WindowGlassBlurRadius");
        blur.Text = "12,5";
        Assert.Equal("12.5", e.Store.Get("WindowGlassBlurRadius"));
        Assert.Equal("12,5", blur.Text, "the field shows the user's separator");
        Assert.Equal(null, blur.Error);
    }

    public static void Invalid_number_text_sets_an_error_and_keeps_the_value()
    {
        var e = Make();
        var blur = Setting(e, "WindowGlassBlurRadius");
        blur.Text = "abc";
        Assert.True(blur.Error != null, "the user sees why");
        Assert.Equal("30", e.Store.Get("WindowGlassBlurRadius"));
        Assert.Equal("30", blur.Text, "the field goes back to the stored value");
        blur.Text = "20";
        Assert.Equal(null, blur.Error, "a valid value clears the error");
    }

    public static void Presets_show_as_picture_tiles_and_short_choices_as_a_segmented_bar()
    {
        var e = Make();
        Assert.Equal("presetTiles", Direct(e, "Corners").TemplateName);
        var choices = e.Schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Where(c => c.Kind == "choice").ToList();
        foreach (var c in choices)
        {
            var vm = Direct(e, c.Id);
            Assert.Equal(c.Choices.Count <= 4 ? "segmented" : "choice", vm.TemplateName, c.Id);
        }
        Assert.Equal("slider", Direct(e, "WindowGlassBlurRadius").TemplateName);
    }

    public static void A_typed_value_can_go_beyond_the_slider_range()
    {
        var e = Make();
        var blur = Setting(e, "WindowGlassBlurRadius");
        blur.Text = "500";
        Assert.Equal("500", e.Store.Get("WindowGlassBlurRadius"), "the maximum is the end of the slider, not a wall");
    }

    public static void Slider_value_is_clamped_to_the_schema_range()
    {
        var e = Make();
        var blur = Setting(e, "WindowGlassBlurRadius");
        blur.SliderValue = 500;
        Assert.Equal("80", e.Store.Get("WindowGlassBlurRadius"));
        Assert.Equal(80.0, blur.SliderValue);
        Assert.Equal(0.0, blur.Min);
        Assert.Equal(80.0, blur.Max);
    }

    public static void Slider_value_snaps_to_the_step_of_the_schema()
    {
        var e = Make();
        var blur = Setting(e, "WindowGlassBlurRadius");   // step 1
        blur.SliderValue = 52.745098039215669;
        Assert.Equal("53", e.Store.Get("WindowGlassBlurRadius"), "a drag lands on whole numbers");
        blur.SliderValue = 12.4;
        Assert.Equal("12", e.Store.Get("WindowGlassBlurRadius"));
    }

    public static void Arrow_buttons_nudge_by_the_schema_step_and_stay_in_range()
    {
        var e = Make();
        var blur = Setting(e, "WindowGlassBlurRadius");   // step 1, 0..80
        blur.StepUpCommand.Execute(null);
        Assert.Equal("31", e.Store.Get("WindowGlassBlurRadius"));
        blur.StepDownCommand.Execute(null); blur.StepDownCommand.Execute(null);
        Assert.Equal("29", e.Store.Get("WindowGlassBlurRadius"));
        blur.SliderValue = 80;
        blur.StepUpCommand.Execute(null);
        Assert.Equal("80", e.Store.Get("WindowGlassBlurRadius"), "the maximum holds");
    }

    public static void Arrow_buttons_on_an_integer_move_by_one()
    {
        var e = Make();
        var slots = Setting(e, "SlotCount");   // 1..12
        slots.StepUpCommand.Execute(null);
        Assert.Equal("5", e.Store.Get("SlotCount"));
        for (int i = 0; i < 10; i++) slots.StepDownCommand.Execute(null);
        Assert.Equal("1", e.Store.Get("SlotCount"), "the minimum holds");
    }

    public static void Arrow_buttons_are_undoable_one_step_each()
    {
        var e = Make();
        var blur = Setting(e, "WindowGlassBlurRadius");
        blur.StepUpCommand.Execute(null);
        e.History.Undo();
        Assert.Equal("30", e.Store.Get("WindowGlassBlurRadius"));
    }

    public static void Integer_setting_rejects_decimals()
    {
        var e = Make();
        var slots = Setting(e, "SlotCount");
        slots.Text = "2.5";
        Assert.True(slots.Error != null);
        Assert.Equal("4", e.Store.Get("SlotCount"));
        slots.Text = "7";
        Assert.Equal("7", e.Store.Get("SlotCount"));
    }

    public static void Gesture_groups_many_slider_values_into_one_undo_step()
    {
        var e = Make();
        var blur = Setting(e, "WindowGlassBlurRadius");
        blur.BeginGesture();
        blur.SliderValue = 10;
        blur.SliderValue = 20;
        blur.SliderValue = 25;
        blur.EndGesture();
        e.Shell.UndoCommand.Execute(null);
        Assert.Equal("30", e.Store.Get("WindowGlassBlurRadius"));
        Assert.False(e.Shell.UndoCommand.CanExecute(null), "one drag is one undo step");
    }

    // ---- toggle, color, thickness, choice ------------------------------------------------------------------

    public static void Toggle_writes_True_and_False()
    {
        var e = Make();
        var glass = Setting(e, "WindowGlassEnabled");
        glass.BoolValue = false;
        Assert.Equal("False", e.Store.Get("WindowGlassEnabled"));
        glass.BoolValue = true;
        Assert.Equal("True", e.Store.Get("WindowGlassEnabled"));
    }

    public static void Color_text_is_validated_and_normalized()
    {
        var e = Make();
        var accent = Setting(e, "Accent");
        accent.Text = "#ff0000";
        Assert.Equal("#FFFF0000", e.Store.Get("Accent"));
        accent.Text = "red";
        Assert.True(accent.Error != null);
        Assert.Equal("#FFFF0000", e.Store.Get("Accent"));
    }

    public static void Thickness_text_is_validated()
    {
        var e = Make();
        var margin = Setting(e, "PageSideMargin");
        margin.Text = "10,5";
        Assert.Equal("10,5", e.Store.Get("PageSideMargin"));
        margin.Text = "1,2,3";
        Assert.True(margin.Error != null);
    }

    public static void Choice_selects_the_stored_value_and_writes_the_picked_one()
    {
        var e = Make();
        var vis = Setting(e, "CoverSubtitleVisibility");
        Assert.Equal("Collapsed", vis.SelectedChoice.Id);
        vis.SelectedChoice = vis.Choices.Single(c => c.Id == "Visible");
        Assert.Equal("Visible", e.Store.Get("CoverSubtitleVisibility"));
        e.Shell.UndoCommand.Execute(null);
        Assert.Equal("Collapsed", vis.SelectedChoice.Id, "undo moves the combo back");
    }

    // ---- presets -------------------------------------------------------------------------------------------

    public static void Preset_starts_on_its_default_choice_and_applies_the_picked_one()
    {
        var e = Make();
        var corners = Setting(e, "Corners");
        Assert.Equal("Default", corners.SelectedChoice.Id);
        corners.SelectedChoice = corners.Choices.Single(c => c.Id == "Sharp");
        Assert.Equal("0", e.Store.Get("Radius"));
        Assert.Equal("Sharp", e.Profile.Presets["Corners"]);
        corners.SelectedChoice = corners.Choices.Single(c => c.Id == "Default");
        Assert.Equal("14", e.Store.Get("Radius"));
    }

    public static void Preset_selection_follows_the_profile()
    {
        var e = Make();
        e.Profile.Presets["Corners"] = "Sharp";
        var corners = new SettingViewModel(Schema.Load(TestSchema.Json).Pages[0].Groups[1].Controls[0], e.Store, e.History,
            new ProfileApplier(Schema.Load(TestSchema.Json), e.Store, e.Files, @"C:\theme"), e.Profile, k => null, new CultureInfo("en-US"));
        Assert.Equal("Sharp", corners.SelectedChoice.Id);
    }

    // ---- ordered list --------------------------------------------------------------------------------------

    public static void Ordered_list_is_sorted_by_stored_position()
    {
        var e = Make();
        var order = Setting(e, "Order");
        Assert.Equal(3, order.Items.Count);
        Assert.Equal("OrderNotes", order.Items[0].Key);
        Assert.Equal("OrderTags", order.Items[2].Key);
    }

    public static void MoveUp_swaps_two_positions_and_keeps_them_unique()
    {
        var e = Make();
        var order = Setting(e, "Order");
        order.MoveUp(order.Items[2]);
        Assert.Equal("2", e.Store.Get("OrderGenres"));
        Assert.Equal("1", e.Store.Get("OrderTags"));
        Assert.Equal("OrderTags", order.Items[1].Key);
        var values = new[] { "OrderNotes", "OrderGenres", "OrderTags" }.Select(k => e.Store.Get(k)).OrderBy(v => v).ToList();
        Assert.Equal("0,1,2", string.Join(",", values));
    }

    public static void MoveDown_on_the_last_item_does_nothing()
    {
        var e = Make();
        var order = Setting(e, "Order");
        order.MoveDown(order.Items[2]);
        Assert.Equal("2", e.Store.Get("OrderTags"));
        Assert.False(e.Shell.UndoCommand.CanExecute(null), "no change, no history step");
    }

    public static void Move_is_one_undo_step()
    {
        var e = Make();
        var order = Setting(e, "Order");
        order.MoveUp(order.Items[1]);
        e.Shell.UndoCommand.Execute(null);
        Assert.Equal("0", e.Store.Get("OrderNotes"));
        Assert.Equal("1", e.Store.Get("OrderGenres"));
        Assert.Equal("OrderGenres", order.Items[1].Key, "the list view follows the undo");
    }

    // ---- header tile slots ---------------------------------------------------------------------------------

    public static void Tile_slots_show_the_chosen_type_per_slot()
    {
        var e = Make();
        var tiles = Direct(e, "HeaderTiles");
        Assert.Equal(2, tiles.Slots.Count);
        Assert.Equal("PlayTime", tiles.Slots[0].SelectedChoice.Id);
        Assert.Equal("LastPlayed", tiles.Slots[1].SelectedChoice.Id);
    }

    public static void Picking_a_tile_type_writes_the_slot_constant()
    {
        var e = Make();
        var tiles = Direct(e, "HeaderTiles");
        tiles.Slots[0].SelectedChoice = tiles.Slots[0].Choices.Single(c => c.Id == "Empty");
        Assert.Equal("0", e.Store.Get("HeaderTileSlot1"));
        tiles.Slots[1].SelectedChoice = tiles.Slots[1].Choices.Single(c => c.Id == "PlayTime");
        Assert.Equal("1", e.Store.Get("HeaderTileSlot2"));
    }

    public static void Unknown_stored_tile_value_selects_nothing_without_crashing()
    {
        var e = Make();
        string error;
        e.Store.TrySet("HeaderTileSlot1", "99", out error);
        var tiles = Direct(e, "HeaderTiles");
        Assert.Equal(null, tiles.Slots[0].SelectedChoice);
    }

    // ---- reset ---------------------------------------------------------------------------------------------

    public static void Reset_on_a_preset_returns_to_its_default_choice()
    {
        var e = Make();
        var corners = Setting(e, "Corners");
        corners.SelectedChoice = corners.Choices.Single(c => c.Id == "Sharp");
        corners.Reset();
        Assert.Equal("Default", corners.SelectedChoice.Id);
        Assert.Equal("14", e.Store.Get("Radius"));
    }
}
