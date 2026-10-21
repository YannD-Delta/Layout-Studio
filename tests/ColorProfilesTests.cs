using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LayoutStudio.Core;

/// <summary>
/// The color profiles of the Modula theme (theme\Modula\Presets\Colors): every one defines the same resources, keeps its text readable on its own
/// surfaces, keeps ONE uniform window color, and a LIGHT one sets the layers that dim or lift the artwork (veils, shade, icon colors) so that dark
/// text never lands on a dark veil.
/// </summary>
public static class ColorProfilesTests
{
    private static string ThemeDir()
    {
        string dir = Path.Combine(TestPaths.RepoRoot(), "theme", "Modula");
        return Directory.Exists(dir) ? dir : null;   // the exported Layout Studio repository has no theme folder
    }

    private static Dictionary<string, string> Colors(string file)
    {
        var text = File.ReadAllText(file);
        return Regex.Matches(text, "<Color x:Key=\"(\\w+)\">(#[0-9A-Fa-f]{6,8})</Color>").Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
    }

    private static List<string> Keys(string file)
    {
        return Regex.Matches(File.ReadAllText(file), "x:Key=\"(\\w+)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
    }

    private static double Luminance(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 8) hex = hex.Substring(2);
        Func<int, double> channel = i =>
        {
            double v = Convert.ToInt32(hex.Substring(i, 2), 16) / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        };
        return 0.2126 * channel(0) + 0.7152 * channel(2) + 0.0722 * channel(4);
    }

    private static double Contrast(string a, string b)
    {
        double la = Luminance(a), lb = Luminance(b);
        if (la < lb) { var t = la; la = lb; lb = t; }
        return (la + 0.05) / (lb + 0.05);
    }

    private static IEnumerable<KeyValuePair<string, string>> Profiles(string theme)
    {
        return Directory.GetFiles(Path.Combine(theme, "Presets", "Colors"), "*.xaml").OrderBy(f => f).Select(f => new KeyValuePair<string, string>(Path.GetFileNameWithoutExtension(f), f));
    }

    public static void Every_color_profile_defines_the_same_resources()
    {
        string theme = ThemeDir();
        if (theme == null) return;
        var reference = Keys(Path.Combine(theme, "Presets", "Colors", "Nord.xaml")).OrderBy(k => k).ToList();
        foreach (var p in Profiles(theme))
        {
            var keys = Keys(p.Value);
            var missing = reference.Where(k => !keys.Contains(k)).ToList();
            Assert.True(missing.Count == 0, p.Key + " lacks " + string.Join(", ", missing.Take(5)));
            Assert.True(keys.Count == keys.Distinct().Count(), p.Key + " defines a key twice");
        }
    }

    public static void Every_profile_choice_has_its_file_and_a_swatch()
    {
        string theme = ThemeDir();
        if (theme == null) return;
        var s = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        var group = s.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Single(c => c.Id == "ColorProfile");
        var files = Profiles(theme).Select(p => p.Key).ToList();
        foreach (var choice in group.Choices.Where(c => c.Id != "Default"))
        {
            Assert.True(choice.Files.Count == 1 && File.Exists(Path.Combine(theme, choice.Files[0])), choice.Id + " names a profile file that exists");
            Assert.True(files.Contains(Path.GetFileNameWithoutExtension(choice.Files[0])), choice.Id + " is a file of Presets\\Colors");
            Assert.True(choice.Swatch.ContainsKey("bg1") && choice.Swatch.ContainsKey("accent") && choice.Swatch.ContainsKey("text"), choice.Id + " has a swatch");
        }
        foreach (var f in files) Assert.True(group.Choices.Any(c => c.Files.Count > 0 && Path.GetFileNameWithoutExtension(c.Files[0]) == f), f + ".xaml is offered as a choice");
        Assert.True(group.Choices.Count >= 25, "13 original profiles, 11 new ones and the default: " + group.Choices.Count);
    }

    public static void Text_is_readable_on_the_window_color_of_every_profile()
    {
        string theme = ThemeDir();
        if (theme == null) return;
        foreach (var p in Profiles(theme))
        {
            var c = Colors(p.Value);
            foreach (var bg in new[] { "WindowBackgroundGradientColor1", "WindowBackgroundGradientColor2", "ControlBackgroundColor", "PopupBackgroundColor" })
            {
                Assert.True(Contrast(c["TextColor"], c[bg]) >= 7.0, p.Key + ": TextColor on " + bg + " is " + Contrast(c["TextColor"], c[bg]).ToString("0.0"));
                Assert.True(Contrast(c["TextColorDarker"], c[bg]) >= 4.5, p.Key + ": TextColorDarker on " + bg + " is " + Contrast(c["TextColorDarker"], c[bg]).ToString("0.0"));
            }
        }
    }

    public static void The_window_gradient_of_every_profile_is_one_uniform_color()
    {
        string theme = ThemeDir();
        if (theme == null) return;
        foreach (var p in Profiles(theme))
        {
            var c = Colors(p.Value);
            double step = Math.Abs(Luminance(c["WindowBackgroundGradientColor1"]) - Luminance(c["WindowBackgroundGradientColor2"]));
            Assert.True(step < 0.06, p.Key + ": window gradient centre and edge differ by " + step.ToString("0.000") + " in luminance");
        }
    }

    public static void A_light_profile_sets_the_layers_that_dim_or_lift_the_artwork()
    {
        string theme = ThemeDir();
        if (theme == null) return;
        var s = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        var group = s.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Single(c => c.Id == "ColorProfile");
        int light = 0;
        foreach (var p in Profiles(theme))
        {
            var c = Colors(p.Value);
            bool isLight = Luminance(c["TextColor"]) < Luminance(c["WindowBackgroundGradientColor1"]);
            var choice = group.Choices.Single(ch => ch.Files.Count > 0 && Path.GetFileNameWithoutExtension(ch.Files[0]) == p.Key);
            if (!isLight)
            {
                Assert.False(choice.Constants.ContainsKey("WindowGlassScrimColor"), p.Key + " is dark: the black veil stays");
                continue;
            }
            light++;
            foreach (var key in new[] { "WindowGlassScrimColor", "PageGlassScrimColor", "HeroShadeColor", "HeroBaseColor", "SidebarIconColor", "HeroTitleShadowColor" })
            {
                string value;
                Assert.True(choice.Constants.TryGetValue(key, out value), p.Key + " is light but does not set " + key);
                Assert.True(Luminance(value) > 0.3 || key == "SidebarIconColor", p.Key + ": " + key + " " + value + " is not a light color");
            }
            Assert.True(Luminance(choice.Constants["SidebarIconColor"]) < 0.3, p.Key + ": the sidebar icons of a light profile are dark");
            string file = File.ReadAllText(p.Value);
            foreach (var brush in new[] { "GlassRimBrush", "HeroChipTextBrush", "HeroTitleBrush", "LibraryDetailsViewInstalledForegroundBrush", "HeroSecondaryButtonForegroundBrush" })
                Assert.True(file.Contains("x:Key=\"" + brush + "\""), p.Key + " redraws " + brush + " in ink");
        }
        Assert.True(light >= 7, "at least seven light profiles, found " + light);
    }
}
