using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public static class FullscreenThemeTests
{
    private static string Root { get { return TestPaths.RepoRoot(); } }

    public static void The_fullscreen_theme_declares_its_mode_and_has_an_id_of_its_own()
    {
        if (!Directory.Exists(Path.Combine(Root, @"theme\Modula-Fullscreen"))) return;   // the exported Layout Studio repository has no theme
        string yaml = File.ReadAllText(Path.Combine(Root, @"theme\Modula-Fullscreen\theme.yaml"));
        Assert.True(Regex.IsMatch(yaml, @"^Mode:\s*Fullscreen\s*$", RegexOptions.Multiline), "Mode: Fullscreen");
        var id = Regex.Match(yaml, @"^Id:\s*(\S+)\s*$", RegexOptions.Multiline).Groups[1].Value;
        string desktop = File.ReadAllText(Path.Combine(Root, @"theme\Modula\theme.yaml"));
        Assert.True(id.Length == 36, "a GUID");
        Assert.False(desktop.Contains(id), "the desktop theme has another id");
    }

    public static void The_palette_keeps_every_key_of_the_default_fullscreen_theme()
    {
        if (!Directory.Exists(Path.Combine(Root, @"theme\Modula-Fullscreen"))) return;   // the exported Layout Studio repository has no theme
        string stock = Path.Combine(Root, @"reference\Local\Themes\Fullscreen\Default\Constants.xaml");
        if (!File.Exists(stock)) return;   // the reference copy of Playnite's themes is not in every checkout
        var keys = new System.Func<string, string[]>(p => Regex.Matches(File.ReadAllText(p), "x:Key=\"(\\w+)\"").Cast<Match>().Select(m => m.Groups[1].Value).OrderBy(k => k).ToArray());
        string mine = Path.Combine(Root, @"theme\Modula-Fullscreen\Constants.xaml");
        var missing = keys(stock).Except(keys(mine)).ToList();
        Assert.Equal(0, missing.Count, "a missing key breaks the host: " + string.Join(",", missing));
    }

    public static void The_fullscreen_schema_loads_and_its_defaults_match_the_constants()
    {
        if (!Directory.Exists(Path.Combine(Root, @"theme\Modula-Fullscreen"))) return;   // the exported Layout Studio repository has no theme
        string dir = Path.Combine(Root, @"theme\Modula-Fullscreen");
        var schema = LayoutStudio.Core.Schema.Load(File.ReadAllText(Path.Combine(dir, "layout-studio.schema.json")));
        string xaml = File.ReadAllText(Path.Combine(dir, "Constants.xaml"));
        Assert.True(schema.AllConstants().Count() >= 10, "the fullscreen constants are in the schema");
        foreach (var c in schema.AllConstants())
        {
            var m = Regex.Match(xaml, "x:Key=\"" + c.Key + "\"[^>]*>([^<]*)<");
            Assert.True(m.Success, c.Key + " is defined in Constants.xaml");
            Assert.Equal(m.Groups[1].Value.Trim(), c.Default, c.Key + ": the schema default is the value of Constants.xaml");
        }
    }

    private static System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<LayoutStudio.Core.Choice>> PresetGroups(string themeDir)
    {
        var schema = LayoutStudio.Core.Schema.Load(File.ReadAllText(Path.Combine(themeDir, "layout-studio.schema.json")));
        return schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Where(c => c.Kind == "preset")
            .GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First().Choices.ToList());
    }

    /// <summary>The shared Look is one choice for both modes: a choice the Desktop offers but the TV lacks would be silently ignored there.</summary>
    public static void Every_choice_of_a_shared_group_resolves_in_the_fullscreen_theme()
    {
        string full = Path.Combine(Root, @"theme\Modula-Fullscreen");
        string desk = Path.Combine(Root, @"theme\Modula");
        if (!Directory.Exists(full) || !Directory.Exists(desk)) return;   // the exported Layout Studio repository has no theme
        var tokens = File.ReadAllText(Path.Combine(full, "look-tokens.json"));
        var list = Regex.Match(tokens, "\"sharedGroups\"\\s*:\\s*\\[([^\\]]*)\\]").Groups[1].Value;
        var shared = Regex.Matches(list, "\"(\\w+)\"").Cast<Match>().Select(x => x.Groups[1].Value).ToList();
        Assert.True(shared.Contains("ColorProfile"), "the colour profile is part of the shared Look");
        var d = PresetGroups(desk);
        var f = PresetGroups(full);
        foreach (var group in shared)
        {
            Assert.True(d.ContainsKey(group), "the Desktop has the group " + group);
            Assert.True(f.ContainsKey(group), "the Fullscreen theme has the group " + group);
            var missing = d[group].Select(c => c.Id).Except(f[group].Select(c => c.Id)).ToList();
            Assert.Equal(0, missing.Count, group + ": Desktop choices the Fullscreen theme lacks: " + string.Join(",", missing));
            foreach (var choice in f[group])
                foreach (var file in choice.Files)
                    Assert.True(File.Exists(Path.Combine(full, file)), group + "/" + choice.Id + ": the preset file " + file + " exists");
        }
    }

    /// <summary>A colour profile merges live and replaces the palette: every one carries the whole palette (text, panels, gradients, scrims, glass, pills, polarity flag).</summary>
    public static void Every_fullscreen_colour_profile_carries_the_whole_palette()
    {
        string dir = Path.Combine(Root, @"theme\Modula-Fullscreen\Presets\Colors");
        if (!Directory.Exists(dir)) return;   // the exported Layout Studio repository has no theme
        string[] required = { "TextColor", "TextBrush", "MainBackgourndBrush", "OverlayMenuBackgroundBrush", "FsHeroTextBrush", "FsScrimVerticalBrush", "FsScrimSideBrush", "FsTileFillBrush", "FsPillFillBrush", "FsPillPrimaryFillBrush", "FsLightProfile", "GlyphBrush", "SelectionBrush" };
        var files = Directory.GetFiles(dir, "*.xaml");
        Assert.True(files.Length >= 10, "the colour profiles are there");
        foreach (var file in files)
        {
            System.Xml.Linq.XDocument.Load(file);   // throws when the file is not well-formed
            string text = File.ReadAllText(file);
            foreach (var key in required)
                Assert.True(text.Contains("x:Key=\"" + key + "\""), Path.GetFileName(file) + " defines " + key);
        }
    }
}
