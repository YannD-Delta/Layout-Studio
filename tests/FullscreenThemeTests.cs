using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public static class FullscreenThemeTests
{
    private static string Root { get { return Path.GetFullPath(Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "..")); } }

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
}
