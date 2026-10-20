using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LayoutStudio.Core;

/// <summary>
/// The Modula customization options added by the options sector (cover fit, players count, band edges, panel frost, glass recipe...):
/// each one is declared in the schema with the default that keeps today's look, is read by a view, and has English and French texts.
/// </summary>
public static class ThemeCustomizationTests
{
    // key, type, default (the value that reproduces the look before the option existed), text key stem ("Opt" = LOCHNDOpt<key>, "Preset" = LOCHNDPreset<key>)
    private static readonly string[][] Options =
    {
        new[] { "HeroCoverShowWhole", "Boolean", "False", "Opt" },
        new[] { "HeroShowPlayersCount", "Boolean", "True", "Opt" },
        new[] { "HeroPlayersCountFontSize", "Double", "28", "Opt" },
        new[] { "HeaderBandEdge", "Int", "1", "Preset" },
        new[] { "WindowGlassBackgroundWarning", "Boolean", "True", "Opt" },
        new[] { "HeroCoverMargin", "Thickness", "20,0,0,6", "Opt" },
        new[] { "HeroCoverInsideMargin", "Thickness", "20,0,0,20", "Opt" },
        new[] { "HeroCoverBorderColor", "Color", "#59FFFFFF", "Opt" },
        new[] { "SidePanelFrostOpacity", "Double", "0.1", "Opt" },
        new[] { "AchievementBarColor", "Color", "#2d73ff", "Opt" },
        new[] { "AchievementBarTrackColor", "Color", "#151619", "Opt" },
        new[] { "DetailsTabMargin", "Thickness", "0,0,8,6", "Opt" },
        new[] { "DetailsTabPadding", "Thickness", "14,5,14,6", "Opt" },
        new[] { "HeroActionsMarginRight", "Thickness", "0,0,28,22", "Opt" },
        new[] { "HeroTopLeftMargin", "Thickness", "20,20,0,0", "Opt" },
        new[] { "ScrollbarThumbWidth", "Double", "6", "Preset", "Scrollbars" },
        new[] { "InfoPanelShowReleaseDate", "Boolean", "True", "Opt" },
        new[] { "InfoPanelShowPlatforms", "Boolean", "True", "Opt" },
        new[] { "InfoPanelShowDevelopers", "Boolean", "True", "Opt" },
        new[] { "InfoPanelShowInstallSize", "Boolean", "True", "Opt" },
        new[] { "SectionTitleMargin", "Thickness", "0,0,0,10", "Opt" },
        new[] { "HeaderLabelOpacity", "Double", "0.8", "Opt" },
        new[] { "HeaderTextOpacity", "Double", "0.65", "Opt" },
        new[] { "HeroCoverWidth", "Double", "230", "Preset", "HeroCoverShape" },
        new[] { "InfoPanelShowSource", "Boolean", "True", "Opt" },
        new[] { "ControlBorderThickness", "Thickness", "1", "Preset", "ControlOutline" },
    };

    private static string ThemeDir()
    {
        string dir = Path.Combine(TestPaths.RepoRoot(), "theme", "Modula");
        return Directory.Exists(dir) ? dir : null;   // the exported Layout Studio repository has no theme folder
    }

    private static Schema Load()
    {
        return Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
    }

    private static HashSet<string> LocKeys(string theme, string lang)
    {
        var text = File.ReadAllText(Path.Combine(theme, "Localization", lang + ".xaml"));
        return new HashSet<string>(Regex.Matches(text, "x:Key=\"([^\"]+)\"").Cast<Match>().Select(m => m.Groups[1].Value));
    }

    public static void New_options_exist_with_the_default_that_keeps_today_look()
    {
        var s = Load();
        foreach (var o in Options)
        {
            var c = s.Find(o[0]);
            Assert.True(c != null, "missing constant " + o[0]);
            Assert.Equal(o[1], c.Type.ToString(), o[0] + " type");
            Assert.Equal(o[2], c.Default, o[0] + " default");
        }
    }

    public static void New_options_are_shown_on_a_page()
    {
        var s = Load();
        foreach (var o in Options)
        {
            bool shown = s.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls)
                .Any(c => c.Keys.Contains(o[0]) || c.Choices.Any(ch => ch.Constants.ContainsKey(o[0])));
            Assert.True(shown, o[0] + " has no control on any page");
        }
    }

    public static void New_options_are_read_by_a_view()
    {
        string theme = ThemeDir();
        if (theme == null) return;
        var xaml = Directory.GetFiles(theme, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "Localization" + Path.DirectorySeparatorChar) && !f.EndsWith("Constants.xaml") && !f.Contains(Path.DirectorySeparatorChar + "Presets" + Path.DirectorySeparatorChar))
            .Select(File.ReadAllText).ToList();
        string constants = File.ReadAllText(Path.Combine(theme, "Constants.xaml"));
        foreach (var o in Options)
        {
            // read by a view, or by a brush resource of Constants.xaml (a color constant feeds a brush that the views use)
            bool viaBrush = constants.Contains("{DynamicResource " + o[0] + "}");
            Assert.True(viaBrush || xaml.Any(t => t.Contains(o[0])), o[0] + " is declared but nothing reads it");
        }
    }

    public static void New_options_have_english_and_french_texts()
    {
        string theme = ThemeDir();
        if (theme == null) return;
        foreach (var lang in new[] { "en_US", "fr_FR" })
        {
            var keys = LocKeys(theme, lang);
            foreach (var o in Options)
                {
                    string stem = o.Length > 4 ? o[4] : o[0];
                    Assert.True(keys.Contains("LOCHND" + o[3] + stem), lang + " lacks LOCHND" + o[3] + stem);
                }
        }
    }

    public static void Default_order_numbers_are_unique_in_each_column()
    {
        // two sections with the same order number would sit on the same Grid row: every section, in either column, starts on its own row
        var s = Load();
        foreach (var column in new[] { 0, 1 })
        {
            var byOrder = new Dictionary<string, string>();
            foreach (var d in s.Sections)
            {
                string key = column == 0 ? d.LeftKey : d.RightKey;
                string value = s.Find(key).Default;
                string other;
                Assert.False(byOrder.TryGetValue(value, out other), d.Id + " and " + other + " share order " + value + " in column " + column);
                byOrder[value] = d.Id;
            }
        }
    }

    public static void English_and_french_define_the_same_text_keys()
    {
        string theme = ThemeDir();
        if (theme == null) return;
        var en = LocKeys(theme, "en_US");
        var fr = LocKeys(theme, "fr_FR");
        var onlyEn = en.Except(fr).OrderBy(k => k).Take(6).ToList();
        var onlyFr = fr.Except(en).OrderBy(k => k).Take(6).ToList();
        Assert.Equal(0, onlyEn.Count + onlyFr.Count, "keys in one language only: " + string.Join(",", onlyEn.Concat(onlyFr)));
    }
}
