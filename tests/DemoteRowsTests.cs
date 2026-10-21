using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public static class DemoteRowsTests
{
    // A section shown elsewhere (as chips) is demoted to a fixed row at the bottom of its column. Two sections that share such a row are drawn on top of
    // each other (seen on the owner's page: GENRE over HOWLONGTOBEAT). Every demoted section has a row of its own in each column.
    public static void Demoted_sections_never_share_a_row_in_the_same_column()
    {
        string theme = Path.Combine(TestPaths.RepoRoot(), @"theme\Modula\Views\DetailsViewGameOverview.xaml");
        if (!File.Exists(theme)) return;   // the exported Layout Studio repository has no theme
        string xaml = File.ReadAllText(theme);
        int rightStart = xaml.IndexOf("DetailsViewSectionOrderRightGenres", System.StringComparison.Ordinal);
        Assert.True(rightStart > 0, "the right column grid starts with its Genres block");
        var seen = new List<string>();
        foreach (Match m in Regex.Matches(xaml, @"Options\[Demote(\w+?)Section\]"))
        {
            var row = Regex.Match(xaml.Substring(m.Index, System.Math.Min(2500, xaml.Length - m.Index)), @"<Setter Property=""Grid\.Row"" Value=""(\d+)"" />");
            if (!row.Success) continue;
            string entry = (m.Index < rightStart ? "left" : "right") + " " + m.Groups[1].Value + " " + row.Groups[1].Value;
            if (!seen.Contains(entry)) seen.Add(entry);
        }
        Assert.True(seen.Count >= 8, "the demotion rows were found (" + seen.Count + ")");
        foreach (var column in new[] { "left", "right" })
        {
            var rows = seen.Where(s => s.StartsWith(column + " ")).Select(s => s.Split(' ')).ToList();
            foreach (var group in rows.GroupBy(r => r[2]))
                Assert.True(group.Select(r => r[1]).Distinct().Count() == 1, column + " column: row " + group.Key + " is shared by " + string.Join(" and ", group.Select(r => r[1]).Distinct()));
            foreach (var group in rows.GroupBy(r => r[1]))
                Assert.True(group.Select(r => r[2]).Distinct().Count() == 1, column + " column: " + group.Key + " is demoted to several rows " + string.Join(",", group.Select(r => r[2]).Distinct()));
        }
    }
}
