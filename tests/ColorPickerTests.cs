using System.Globalization;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

/// <summary>A color constant opens a picker: swatches and sliders for alpha, red, green and blue.</summary>
public static class ColorPickerTests
{
    private static SettingViewModel Accent(out ConstantStore store, out History history)
    {
        var schema = Schema.Load(TestSchema.Json);
        store = new ConstantStore(schema.AllConstants());
        history = new History(store);
        var applier = new ProfileApplier(schema, store, new FakePresetFiles(), @"C:\theme");
        var control = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Single(c => c.Id == "Accent");
        return new SettingViewModel(control, store, history, applier, new Profile { Name = "Default" }, k => null, new CultureInfo("en-US"));
    }

    public static void The_channels_are_read_from_the_stored_color()
    {
        ConstantStore store; History history;
        var accent = Accent(out store, out history);   // #FF1E90FF
        Assert.Equal(255.0, accent.ColorA);
        Assert.Equal(30.0, accent.ColorR);
        Assert.Equal(144.0, accent.ColorG);
        Assert.Equal(255.0, accent.ColorB);
    }

    public static void Moving_a_slider_rewrites_that_channel_only()
    {
        ConstantStore store; History history;
        var accent = Accent(out store, out history);
        accent.ColorR = 255;
        Assert.Equal("#FFFF90FF", store.Get("Accent"));
        accent.ColorA = 128.4;
        Assert.Equal("#80FF90FF", store.Get("Accent"), "alpha is a byte: rounded");
        accent.ColorB = 999;
        Assert.Equal("#80FF90FF", store.Get("Accent"), "clamped to 255");
        accent.ColorG = -5;
        Assert.Equal("#80FF00FF", store.Get("Accent"));
    }

    public static void A_swatch_sets_the_color_and_keeps_the_alpha()
    {
        ConstantStore store; History history;
        var accent = Accent(out store, out history);
        accent.ColorA = 100;
        accent.SetColorCommand.Execute("#ff5252");
        Assert.Equal("#64FF5252", store.Get("Accent"));
        accent.SetColorCommand.Execute("#80112233");
        Assert.Equal("#80112233", store.Get("Accent"));
        accent.SetColorCommand.Execute("red");
        Assert.Equal("#80112233", store.Get("Accent"), "not a hex color: ignored");
    }

    public static void A_picker_drag_is_one_undo_step()
    {
        ConstantStore store; History history;
        var accent = Accent(out store, out history);
        accent.BeginGesture();
        for (int v = 0; v <= 200; v += 20) accent.ColorR = v;
        accent.EndGesture();
        history.Undo();
        Assert.Equal("#FF1E90FF", store.Get("Accent"));
        Assert.False(history.CanUndo);
    }

    public static void The_custom_accent_has_a_picker_with_red_green_blue_and_swatches()
    {
        var schema = Schema.Load(System.IO.File.ReadAllText(TestPaths.SchemaPath()));
        var store = new ConstantStore(schema.AllConstants());
        var history = new History(store);
        var applier = new ProfileApplier(schema, store, new FakePresetFiles(), @"C:\theme");
        var colors = new ColorsEditorViewModel(store, schema, history, applier, new Profile { Name = "Default" });
        colors.CustomAccent = "#FF102030";
        Assert.Equal(16.0, colors.AccentR);
        Assert.Equal(32.0, colors.AccentG);
        Assert.Equal(48.0, colors.AccentB);
        colors.AccentG = 200;
        Assert.Equal("#FF10C830", colors.CustomAccent);
        colors.SetAccentCommand.Execute("#ff5252");
        Assert.Equal("#FFFF5252", colors.CustomAccent);
        Assert.Equal("#FFFF5252".Substring(3), store.Get("GlyphSubColor").Substring(3), "the highlight follows like a typed accent");
        colors.PickerOpen = true;
        Assert.True(colors.PickerOpen);
    }

    public static void The_picker_opens_and_closes()
    {
        ConstantStore store; History history;
        var accent = Accent(out store, out history);
        Assert.False(accent.PickerOpen);
        accent.PickerOpen = true;
        Assert.True(accent.PickerOpen);
    }
}
