using LayoutStudio.Core;

public static class MigrationTests
{
    private const string Theme = "42f3aebe-d455-44a7-bd7f-2dd8afaec9a6";

    // Same shape as the real ThemeOptions config: SelectedPresets and UserSettings, both keyed by theme id.
    private static string Config(string selectedPresets, string userSettings)
    {
        return "{\"SelectedPresets\":{\"" + Theme + "\":[" + selectedPresets + "],\"OtherTheme\":[\"Style.Dune\"]},"
             + "\"UserSettings\":{\"" + Theme + "\":{" + userSettings + "},\"OtherTheme\":{\"WindowGlassBlurRadius\":{\"Type\":\"Double\",\"Value\":\"1\"}}}}";
    }

    private static Schema Schema_()
    {
        return Schema.Load(TestSchema.Json);
    }

    public static void Maps_known_preset_choice()
    {
        var p = Migration.FromThemeOptions(Config("\"Corners.Sharp\"", ""), Theme, Schema_());
        Assert.Equal("Sharp", p.Presets["Corners"]);
        Assert.Equal("Migrated from ThemeOptions", p.Name);
    }

    public static void Maps_user_settings_to_overrides()
    {
        var p = Migration.FromThemeOptions(Config("", "\"WindowGlassBlurRadius\":{\"Type\":\"Double\",\"Value\":\"12\"},\"SlotCount\":{\"Type\":\"Int32\",\"Value\":\"9\"}"), Theme, Schema_());
        Assert.Equal("12", p.Overrides["WindowGlassBlurRadius"]);
        Assert.Equal("9", p.Overrides["SlotCount"]);
    }

    public static void Only_the_requested_theme_is_read()
    {
        var p = Migration.FromThemeOptions(Config("", ""), Theme, Schema_());
        Assert.False(p.Overrides.ContainsKey("WindowGlassBlurRadius"), "settings of another theme are ignored");
        Assert.False(p.Presets.ContainsKey("Style"));
    }

    public static void Ignores_unknown_keys_and_help_rows()
    {
        var p = Migration.FromThemeOptions(Config("\"NoGroup.X\",\"Corners.NoChoice\"", "\"GhostSetting\":{\"Type\":\"Double\",\"Value\":\"1\"},\"HelpSlotCount\":{\"Type\":\"Thickness\",\"Value\":\"0,0,0,0\"}"), Theme, Schema_());
        Assert.Equal(0, p.Overrides.Count);
        Assert.Equal(0, p.Presets.Count, "unknown groups and unknown choices are dropped");
    }

    public static void Drops_values_that_equal_the_default_and_invalid_ones()
    {
        var p = Migration.FromThemeOptions(Config("", "\"WindowGlassBlurRadius\":{\"Type\":\"Double\",\"Value\":\"30\"},\"SlotCount\":{\"Type\":\"Int32\",\"Value\":\"abc\"},\"WindowGlassEnabled\":{\"Type\":\"Boolean\",\"Value\":\"False\"}"), Theme, Schema_());
        Assert.False(p.Overrides.ContainsKey("WindowGlassBlurRadius"), "30 is the default");
        Assert.False(p.Overrides.ContainsKey("SlotCount"), "an invalid value is skipped");
        Assert.Equal("False", p.Overrides["WindowGlassEnabled"]);
    }

    public static void Values_out_of_range_are_clamped()
    {
        var p = Migration.FromThemeOptions(Config("", "\"WindowGlassBlurRadius\":{\"Type\":\"Double\",\"Value\":\"500\"}"), Theme, Schema_());
        Assert.Equal("80", p.Overrides["WindowGlassBlurRadius"]);
    }

    public static void Empty_or_garbage_input_gives_an_empty_profile()
    {
        Assert.Equal(0, Migration.FromThemeOptions("", Theme, Schema_()).Overrides.Count);
        Assert.Equal(0, Migration.FromThemeOptions("{ not json", Theme, Schema_()).Overrides.Count);
        Assert.Equal(0, Migration.FromThemeOptions("{\"UserSettings\":{}}", Theme, Schema_()).Presets.Count);
        Assert.Equal(0, Migration.FromThemeOptions(null, Theme, Schema_()).Overrides.Count);
    }

    public static void Missing_theme_section_gives_an_empty_profile()
    {
        var p = Migration.FromThemeOptions(Config("\"Corners.Sharp\"", ""), "no-such-theme", Schema_());
        Assert.Equal(0, p.Presets.Count);
    }
}
