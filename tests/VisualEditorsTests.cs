using System.Globalization;
using System.IO;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

public static class VisualEditorsTests
{
    private class Env
    {
        public Schema Schema;
        public ConstantStore Store;
        public History History;
        public Profile Profile;
        public FakePresetFiles Files;
        public ProfileApplier Applier;
    }

    private static Env Make()
    {
        var e = new Env();
        e.Schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        e.Store = new ConstantStore(e.Schema.AllConstants());
        e.History = new History(e.Store);
        e.Profile = new Profile { Name = "Default" };
        e.Files = new FakePresetFiles();
        e.Applier = new ProfileApplier(e.Schema, e.Store, e.Files, @"C:\theme");
        return e;
    }

    // ---- sizes (Task 15) -----------------------------------------------------------------------------------

    private static SizeItemViewModel Size(Env e, SizesEditorViewModel ed, string id)
    {
        return ed.Items.Single(i => i.Id == id);
    }

    public static void Sizes_items_come_from_the_schema()
    {
        var e = Make();
        var ed = new SizesEditorViewModel(e.Store, e.Schema, e.History, new CultureInfo("en-US"));
        Assert.Equal(11, ed.Items.Count);
        var cover = Size(e, ed, "cover");
        Assert.True(cover.HasWidth && cover.HasHeight);
        Assert.False(Size(e, ed, "topPanel").HasWidth);
        Assert.False(Size(e, ed, "topPanel").NeedsRestart, "TopPanelHeight follows live through the Options bridge");
    }

    // ---- background (Task 16) ------------------------------------------------------------------------------

    public static void Background_editor_lists_the_tuning_settings()
    {
        var e = Make();
        var ed = new BackgroundEditorViewModel(e.Store, e.Schema, e.History, e.Applier, e.Profile, k => null, new CultureInfo("en-US"), null);
        Assert.Equal(10, ed.Items.Count);
        Assert.Equal("toggle", ed.Items[0].Kind);
        Assert.Equal("slider", ed.Items[2].Kind);
    }

    public static void Darkness_slider_writes_the_scrim_constant_within_zero_and_one()
    {
        var e = Make();
        var ed = new BackgroundEditorViewModel(e.Store, e.Schema, e.History, e.Applier, e.Profile, k => null, new CultureInfo("en-US"), null);
        var darkness = ed.Items.Single(i => i.Id == "WindowGlassScrimOpacity");
        darkness.SliderValue = 0.7;
        Assert.Equal("0.7", e.Store.Get("WindowGlassScrimOpacity"));
        darkness.SliderValue = 5;
        Assert.Equal("1", e.Store.Get("WindowGlassScrimOpacity"));
        Assert.Equal(0.0, darkness.Min);
        Assert.Equal(1.0, darkness.Max);
    }

    public static void Preview_values_follow_the_store()
    {
        var e = Make();
        var ed = new BackgroundEditorViewModel(e.Store, e.Schema, e.History, e.Applier, e.Profile, k => null, new CultureInfo("en-US"), null);
        int notifications = 0;
        ed.PropertyChanged += (s, a) => notifications++;
        string error;
        e.Store.TrySet("WindowGlassScrimOpacity", "0.25", out error);
        e.Store.TrySet("WindowGlassBlurRadius", "50", out error);
        e.Store.TrySet("TopPanelGlassOpacity", "0.1", out error);
        e.Store.TrySet("WindowGlassEnabled", "False", out error);
        Assert.True(ed.PreviewEnabled, "the page layer still shows the artwork");
        e.Store.TrySet("PageGlassOpacity", "0", out error);
        Assert.Equal(0.25, ed.PreviewScrimOpacity);
        Assert.Equal(0.1, ed.PreviewTopOpacity);
        Assert.True(ed.PreviewBlurRadius > 0 && ed.PreviewBlurRadius < 50, "the preview is smaller than the window, so the blur is scaled down");
        Assert.False(ed.PreviewEnabled);
        Assert.True(notifications > 0);
    }

    public static void The_preview_image_is_optional()
    {
        var e = Make();
        var none = new BackgroundEditorViewModel(e.Store, e.Schema, e.History, e.Applier, e.Profile, k => null, new CultureInfo("en-US"), null);
        Assert.False(none.HasPreviewImage);
        var missing = new BackgroundEditorViewModel(e.Store, e.Schema, e.History, e.Applier, e.Profile, k => null, new CultureInfo("en-US"), () => @"C:\no\such\file.jpg");
        Assert.False(missing.HasPreviewImage, "a missing file is not an error");
    }

    // ---- tiles (Task 17) -----------------------------------------------------------------------------------

    public static void Tiles_editor_has_twelve_slots_and_a_palette()
    {
        var e = Make();
        var ed = new TilesEditorViewModel(e.Store, e.Schema, e.History);
        Assert.Equal(12, ed.Slots.Count);
        Assert.Equal(34, ed.Palette.Count);
        Assert.True(ed.Slots[0].Tile != null, "the first slot shows its current tile type");
    }

    public static void Assign_sets_the_slot_constant_in_one_undo_step()
    {
        var e = Make();
        var ed = new TilesEditorViewModel(e.Store, e.Schema, e.History);
        ed.Assign(0, "Empty");
        Assert.Equal("0", e.Store.Get("HeaderTileSlot1"));
        Assert.Equal("Empty", ed.Slots[0].Tile.Id);
        e.History.Undo();
        Assert.False(e.History.CanUndo);
        Assert.True(ed.Slots[0].Tile.Id != "Empty");
    }

    public static void Swap_exchanges_two_slots()
    {
        var e = Make();
        var ed = new TilesEditorViewModel(e.Store, e.Schema, e.History);
        string a = e.Store.Get("HeaderTileSlot1"), b = e.Store.Get("HeaderTileSlot2");
        ed.Swap(0, 1);
        Assert.Equal(b, e.Store.Get("HeaderTileSlot1"));
        Assert.Equal(a, e.Store.Get("HeaderTileSlot2"));
        e.History.Undo();
        Assert.Equal(a, e.Store.Get("HeaderTileSlot1"));
        Assert.False(e.History.CanUndo, "a swap is one step");
    }

    public static void Dropping_a_palette_tile_on_a_slot_assigns_it()
    {
        var e = Make();
        var ed = new TilesEditorViewModel(e.Store, e.Schema, e.History);
        var tile = ed.Palette.Single(t => t.Id == "Genres");
        ed.Slots[3].Drop(DragFormats.Tile, tile.Id);
        Assert.Equal(tile.Value, e.Store.Get("HeaderTileSlot4"));
    }

    public static void Dropping_a_slot_on_another_slot_swaps_them()
    {
        var e = Make();
        var ed = new TilesEditorViewModel(e.Store, e.Schema, e.History);
        string a = e.Store.Get("HeaderTileSlot1"), c = e.Store.Get("HeaderTileSlot3");
        ed.Slots[2].Drop(DragFormats.Slot, "0");
        Assert.Equal(c, e.Store.Get("HeaderTileSlot1"));
        Assert.Equal(a, e.Store.Get("HeaderTileSlot3"));
    }

    public static void Clear_sets_the_empty_tile_and_unknown_ids_are_ignored()
    {
        var e = Make();
        var ed = new TilesEditorViewModel(e.Store, e.Schema, e.History);
        ed.Clear(1);
        Assert.Equal("0", e.Store.Get("HeaderTileSlot2"));
        ed.Assign(0, "NoSuchTile");
        ed.Assign(99, "Empty");
        ed.Swap(0, 99);
        ed.Slots[0].Drop("garbage", "x");
        Assert.Equal(1, e.History.CanUndo ? 1 : 0, "only the clear is in the history");
    }

    // ---- colors (Task 18) ----------------------------------------------------------------------------------

    public static void Colors_editor_lists_profiles_and_accents_with_swatches()
    {
        var e = Make();
        var ed = new ColorsEditorViewModel(e.Store, e.Schema, e.History, e.Applier, e.Profile);
        Assert.Equal(14, ed.Profiles.Count);
        Assert.Equal(9, ed.Accents.Count);
        Assert.True(ed.Profiles.All(p => p.Accent != null && p.Background1 != null), "every profile has a swatch");
        Assert.True(ed.Accents.All(a => a.Accent != null));
        Assert.True(ed.Profiles.Single(p => p.Id == "Default").IsSelected, "nothing chosen yet: the default");
    }

    public static void Selecting_a_profile_applies_the_preset_and_moves_the_selection()
    {
        var e = Make();
        var ed = new ColorsEditorViewModel(e.Store, e.Schema, e.History, e.Applier, e.Profile);
        ed.SelectProfile("Midnight");
        Assert.Equal("Midnight", e.Profile.Presets["ColorProfile"]);
        Assert.Equal("merge ColorProfile Midnight.xaml", e.Files.Calls[e.Files.Calls.Count - 1]);
        Assert.True(ed.Profiles.Single(p => p.Id == "Midnight").IsSelected);
        Assert.False(ed.Profiles.Single(p => p.Id == "Default").IsSelected);
    }

    public static void Selecting_an_accent_sets_the_accent_constants()
    {
        var e = Make();
        var ed = new ColorsEditorViewModel(e.Store, e.Schema, e.History, e.Applier, e.Profile);
        ed.SelectAccent("Amber");
        Assert.Equal("#FFFFB547", e.Store.Get("GlyphColor"));
        e.History.Undo();
        Assert.True(e.Store.Get("GlyphColor") != "#FFFFB547", "undo restores the previous accent");
    }

    public static void Undoing_an_accent_choice_also_restores_the_recorded_selection()
    {
        var e = Make();
        var ed = new ColorsEditorViewModel(e.Store, e.Schema, e.History, e.Applier, e.Profile);
        ed.SelectAccent("Amber");
        Assert.Equal("Amber", e.Profile.Presets["Accent"]);
        e.History.Undo();
        Assert.True(!e.Profile.Presets.ContainsKey("Accent") || e.Profile.Presets["Accent"] == "Default", "the profile no longer says Amber");
        Assert.True(ed.Accents.Single(x => x.Id == "Default").IsSelected, "and the swatch follows");
        e.History.Redo();
        Assert.Equal("Amber", e.Profile.Presets["Accent"]);
        Assert.True(ed.Accents.Single(x => x.Id == "Amber").IsSelected);
    }

    public static void A_custom_accent_is_validated_and_written()
    {
        var e = Make();
        var ed = new ColorsEditorViewModel(e.Store, e.Schema, e.History, e.Applier, e.Profile);
        ed.CustomAccent = "#112233";
        Assert.Equal("#FF112233", e.Store.Get("GlyphColor"));
        Assert.Equal(null, ed.Error);
        ed.CustomAccent = "red";
        Assert.True(ed.Error != null);
        Assert.Equal("#FF112233", e.Store.Get("GlyphColor"));
    }

    // ---- per-setting state (modified dot, reset, details) ---------------------------------------------------

    public static void A_setting_is_modified_only_while_it_differs_from_its_default_and_resets_in_one_undo_step()
    {
        var e = Make();
        var vm = new SettingViewModel(new Control { Kind = "slider", Id = "WindowGlassBlurRadius", Keys = new System.Collections.Generic.List<string> { "WindowGlassBlurRadius" }, Title = "Blur" },
            e.Store, e.History, e.Applier, e.Profile, k => null, new CultureInfo("en-US"));
        Assert.False(vm.IsModified);
        Assert.False(vm.ResetCommand.CanExecute(null), "nothing to reset yet");
        vm.Text = "55";
        Assert.True(vm.IsModified);
        Assert.True(vm.ResetCommand.CanExecute(null));
        vm.ResetCommand.Execute(null);
        Assert.False(vm.IsModified);
        Assert.Equal(e.Store.GetDefault("WindowGlassBlurRadius"), e.Store.Get("WindowGlassBlurRadius"));
        e.History.Undo();
        Assert.Equal("55", e.Store.Get("WindowGlassBlurRadius"), "the reset is one undo step");
    }

    public static void The_details_text_gives_the_default_the_range_and_the_constant_name()
    {
        var e = Make();
        var vm = new SettingViewModel(new Control { Kind = "slider", Id = "WindowGlassBlurRadius", Keys = new System.Collections.Generic.List<string> { "WindowGlassBlurRadius" }, Title = "Blur" },
            e.Store, e.History, e.Applier, e.Profile, k => null, new CultureInfo("en-US"));
        Assert.True(vm.Details.Contains("Default: 36") && vm.Details.Contains("Range: 0 to 80") && vm.Details.Contains("WindowGlassBlurRadius"), vm.Details);
        Assert.True(vm.ResetTooltip.Contains("36"), vm.ResetTooltip);
    }}
