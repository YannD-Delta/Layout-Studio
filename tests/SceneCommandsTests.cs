using System;
using LayoutStudio.Core;

public static class SceneCommandsTests
{
    public static void Parse_reads_one_command_per_line_and_skips_comments()
    {
        var list = SceneCommands.Parse("# comment\r\nView=Grid\n\nselect=Airship Gardens\n");
        Assert.Equal(2, list.Count);
        Assert.Equal("view", list[0].Key);
        Assert.Equal("Grid", list[0].Value);
        Assert.Equal("select", list[1].Key);
        Assert.Equal("Airship Gardens", list[1].Value);
    }

    public static void Parse_keeps_equals_signs_inside_the_value()
    {
        var list = SceneCommands.Parse("set=ViewSettings.GamesViewType:Grid\nsearch=a=b");
        Assert.Equal("ViewSettings.GamesViewType:Grid", list[0].Value);
        Assert.Equal("a=b", list[1].Value);
    }

    public static void Parse_rejects_a_line_without_a_key()
    {
        Assert.Throws<FormatException>(() => SceneCommands.Parse("justtext"));
    }

    public static void Size_parses_width_and_height_only()
    {
        int w, h;
        Assert.True(SceneCommands.TryParseSize("1920x1080", out w, out h));
        Assert.Equal(1920, w); Assert.Equal(1080, h);
        Assert.False(SceneCommands.TryParseSize("maximized", out w, out h));
        Assert.False(SceneCommands.TryParseSize("0x10", out w, out h));
    }

    public static void Switch_accepts_on_off_and_booleans()
    {
        Assert.True(SceneCommands.ParseSwitch("ON"));
        Assert.False(SceneCommands.ParseSwitch("off"));
        Assert.True(SceneCommands.ParseSwitch("true"));
        Assert.Throws<FormatException>(() => SceneCommands.ParseSwitch("maybe"));
    }

    public static void Filter_splits_a_field_and_its_names()
    {
        string field; string[] names;
        SceneCommands.ParseFilter("Genre:Action, Adventure", out field, out names);
        Assert.Equal("Genre", field);
        Assert.Equal(2, names.Length);
        Assert.Equal("Adventure", names[1]);
        SceneCommands.ParseFilter("Favorite", out field, out names);
        Assert.Equal("Favorite", field); Assert.Equal(0, names.Length);
        SceneCommands.ParseFilter("clear", out field, out names);
        Assert.Equal("clear", field);
    }

    public static void Scroll_takes_top_bottom_or_a_fraction()
    {
        Assert.Equal(0.0, SceneCommands.ParseScroll("top"));
        Assert.Equal(1.0, SceneCommands.ParseScroll("bottom"));
        Assert.Equal(0.5, SceneCommands.ParseScroll("0.5"));
        Assert.Throws<FormatException>(() => SceneCommands.ParseScroll("2"));
    }
}
