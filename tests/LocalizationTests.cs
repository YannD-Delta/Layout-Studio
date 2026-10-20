using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LayoutStudio.Core;

/// <summary>The editor says its words through LOCLS_ keys: a key used by the window but missing from a language file shows as an empty label.</summary>
public static class LocalizationTests
{
    private static string Read(string relative) { return File.ReadAllText(Path.Combine(TestPaths.RepoRoot(), relative)); }

    private static string[] Keys(string text)
    {
        return Regex.Matches(text, "x:Key=\"(LOCLS_[A-Za-z0-9_]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
    }

    public static void Every_key_the_window_uses_exists_in_both_language_files()
    {
        string xaml = Read(@"layout-studio\src\Editor\EditorWindow.xaml");
        var used = Regex.Matches(xaml, @"DynamicResource (LOCLS_[A-Za-z0-9_]+)").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToArray();
        var en = Keys(Read(@"layout-studio\src\Localization\en_US.xaml"));
        var fr = Keys(Read(@"layout-studio\src\Localization\fr_FR.xaml"));
        var missingEn = used.Where(k => !en.Contains(k)).ToArray();
        var missingFr = used.Where(k => !fr.Contains(k)).ToArray();
        Assert.True(missingEn.Length == 0, "missing in en_US: " + string.Join(", ", missingEn));
        Assert.True(missingFr.Length == 0, "missing in fr_FR: " + string.Join(", ", missingFr));
    }

    public static void French_has_every_key_English_has_and_no_key_is_defined_twice()
    {
        var en = Keys(Read(@"layout-studio\src\Localization\en_US.xaml"));
        var fr = Keys(Read(@"layout-studio\src\Localization\fr_FR.xaml"));
        Assert.True(en.Length == en.Distinct().Count(), "duplicate key in en_US: " + string.Join(", ", en.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key)));
        Assert.True(fr.Length == fr.Distinct().Count(), "duplicate key in fr_FR: " + string.Join(", ", fr.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key)));
        var onlyEn = en.Where(k => !fr.Contains(k)).ToArray();
        Assert.True(onlyEn.Length == 0, "English has them, French lacks: " + string.Join(", ", onlyEn));
    }
}
