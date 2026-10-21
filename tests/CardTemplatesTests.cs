using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public static class CardTemplatesTests
{
    // Playnite finds its PART_ elements and the add-on controls (ThemeExtras_..., HowLongToBeat_...) by name in the page template. A card is a DataTemplate:
    // its names live in a scope of their own, nothing resolves them and the card stays empty (the Completion status card lost its value that way).
    public static void No_card_template_holds_a_part_or_add_on_control_name()
    {
        string theme = Path.Combine(TestPaths.RepoRoot(), @"theme\Modula\Views\DetailsViewGameOverview.xaml");
        if (!File.Exists(theme)) return;   // the exported Layout Studio repository has no theme
        string xaml = File.ReadAllText(theme);
        var bad = new List<string>();
        foreach (Match m in Regex.Matches(xaml, @"<DataTemplate x:Key=""(Card\.\w+)"">"))
        {
            int end = xaml.IndexOf("</DataTemplate>", m.Index, System.StringComparison.Ordinal);
            string body = xaml.Substring(m.Index, end - m.Index);
            foreach (Match n in Regex.Matches(body, @"\b(?:x:)?Name=""(PART_\w+|ThemeExtras_\w+|HowLongToBeat_\w+|PlayniteAchievements_\w+|GameActivity_\w+|ExtraMetadataLoader_\w+|NewsViewer_\w+|CheckDlc_\w+|PlayState_\w+)"""))
                bad.Add(m.Groups[1].Value + " holds " + n.Groups[1].Value);
        }
        Assert.Equal("", string.Join("; ", bad));
    }
}
