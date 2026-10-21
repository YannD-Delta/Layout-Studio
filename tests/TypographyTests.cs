using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LayoutStudio.Core;

/// <summary>The Typography preset group: Windows-only fonts, the same resources in every file, merged after the Material fonts.</summary>
public static class TypographyTests
{
    private static readonly string[] Keys = { "FontFamily", "FontFamilyDisplay", "FontFamilyOverline", "WeightDisplay", "WeightSection", "WeightValue", "WeightLabel", "WeightButton" };
    // fonts that ship with Windows 10/11 (no Office-only fonts)
    private static readonly string[] Allowed =
    {
        "Segoe UI", "Segoe UI Variable Text", "Segoe UI Variable Display", "Bahnschrift", "Candara", "Corbel", "Georgia", "Constantia", "Trebuchet MS",
        "Verdana", "Tahoma", "Cascadia Mono", "Consolas", "Calibri"
    };

    private static string ThemeDir()
    {
        string dir = Path.Combine(TestPaths.RepoRoot(), "theme", "Modula");
        return Directory.Exists(dir) ? dir : null;
    }

    public static void The_typography_group_offers_a_default_and_windows_only_fonts()
    {
        string theme = ThemeDir();
        if (theme == null) return;
        var s = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        var group = s.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Single(c => c.Id == "Typography");
        Assert.True(group.Choices.Count >= 10, "at least ten typography choices, found " + group.Choices.Count);
        Assert.True(group.Choices.Any(c => c.Id == "Default" && c.Files.Count == 0), "the default is the theme's own console look: no file");
        foreach (var choice in group.Choices.Where(c => c.Id != "Default"))
        {
            Assert.True(choice.Files.Count == 1 && File.Exists(Path.Combine(theme, choice.Files[0])), choice.Id + " has its file");
            string text = File.ReadAllText(Path.Combine(theme, choice.Files[0]));
            foreach (var key in Keys) Assert.True(text.Contains("x:Key=\"" + key + "\""), choice.Id + " lacks " + key);
            foreach (Match m in Regex.Matches(text, "<FontFamily x:Key=\"\\w+\">([^<]+)</FontFamily>"))
                foreach (var name in m.Groups[1].Value.Split(',').Select(n => n.Trim()))
                    Assert.True(Allowed.Contains(name), choice.Id + " uses " + name + ", which is not a Windows font of the allowed list");
            Assert.True(Regex.IsMatch(text, "<FontFamily x:Key=\"FontFamily\">[^<]*Segoe UI</FontFamily>"), choice.Id + " ends its stack with Segoe UI");
            foreach (var kv in choice.Constants) Assert.True(kv.Key.StartsWith("FontSize") && kv.Key != "FontSizeLarger" && kv.Key != "FontSizeLargest",
                choice.Id + " only scales the text sizes the Material presets leave alone, not " + kv.Key);
        }
    }

    public static void Typography_is_merged_after_material_so_it_wins_over_a_material_font()
    {
        var s = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        Assert.True(s.PresetRank("Material") < s.PresetRank("Typography"), "the later group wins where two files define the same font");
        Assert.True(s.PresetRank("NoSuchGroup") == int.MaxValue, "an unknown group ranks last");
    }
}
