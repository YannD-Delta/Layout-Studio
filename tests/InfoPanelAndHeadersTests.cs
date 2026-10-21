using System.IO;
using System.Linq;
using LayoutStudio.Core;

public static class InfoPanelAndHeadersTests
{
    private static string ViewText()
    {
        string path = Path.Combine(TestPaths.RepoRoot(), "theme", "Modula", "Views", "DetailsViewGameOverview.xaml");
        return File.Exists(path) ? File.ReadAllText(path) : null;   // the exported Layout Studio repository has no theme folder
    }

    private static int Reads(string view, string key)
    {
        int n = 0, at = 0;
        string needle = "Options[" + key + "]";
        while ((at = view.IndexOf(needle, at, System.StringComparison.Ordinal)) >= 0) { n++; at += needle.Length; }
        return n;
    }

    private static Schema Load() { return Schema.Load(File.ReadAllText(TestPaths.SchemaPath())); }

    private static readonly string[] Rows =
    {
        "ReleaseDate", "Platforms", "Developers", "InstallSize", "Source", "RecentActivity", "Added", "Library", "Publishers", "InstallDirectory",
        "Series", "Regions", "Version", "UserScore", "CommunityScore", "CriticScore", "AgeRatings"
    };

    public static void Every_row_of_the_information_panel_has_its_own_switch_on_by_default()
    {
        var schema = Load();
        foreach (var row in Rows)
        {
            var c = schema.Find("InfoPanelShow" + row);
            Assert.True(c != null, "InfoPanelShow" + row + " is missing");
            Assert.Equal("True", c.Default, row + " defaults to shown: nothing changes for anyone");
        }
    }

    public static void The_view_reads_every_row_switch_and_hides_the_whole_panel_when_all_are_off()
    {
        var view = ViewText();
        if (view == null) return;
        foreach (var row in Rows)
            Assert.True(view.Contains("Options[InfoPanelShow" + row + "]"), "the view never reads InfoPanelShow" + row);
        // the panel container: one trigger holds the 17 conditions
        int at = view.IndexOf("every row of the information panel switched off");
        Assert.True(at > 0, "no whole-panel trigger");
        string block = view.Substring(at, view.IndexOf("</DockPanel.Style>", at) - at);
        Assert.Equal(Rows.Length, Rows.Sum(r => Reads(block, "InfoPanelShow" + r)), "all rows are conditions of the whole-panel trigger");
        Assert.True(block.Contains("Value=\"Collapsed\""), "the panel collapses");
    }

    public static void A_hidden_row_is_collapsed_label_and_value_both()
    {
        var view = ViewText();
        if (view == null) return;
        foreach (var row in new[] { "RecentActivity", "Added", "Library", "Publishers", "InstallDirectory", "Series", "Regions", "Version", "UserScore", "CommunityScore", "CriticScore", "AgeRatings" })
        {
            // the label trigger and the value trigger each read the option once: at least two readings (the whole-panel trigger makes three)
            Assert.True(Reads(view, "InfoPanelShow" + row) >= 3, row + " needs a label trigger, a value trigger and the whole-panel condition");
        }
    }

    private static readonly string[] Sections =
    {
        "Notes", "Genres", "Categories", "Features", "Tags", "NewsViewer", "Video", "GameActivity", "CheckDlc", "GameRelations", "Web",
        "HowLongToBeat", "Achievements", "SteamScreenshots", "Vndb", "ScreenshotsVisualizer", "ReviewViewer", "ScreenshotUtilities", "PlayerActivities"
    };

    public static void Every_section_with_a_header_has_a_global_and_an_own_header_switch_on_by_default()
    {
        var schema = Load();
        Assert.Equal("True", schema.Find("DetailsViewShowSectionHeaders").Default);
        foreach (var s in Sections)
        {
            var c = schema.Find("DetailsViewShow" + s + "Header");
            Assert.True(c != null, "DetailsViewShow" + s + "Header is missing");
            Assert.Equal("True", c.Default);
        }
    }

    public static void The_view_hides_the_header_and_keeps_the_body_open_for_each_section()
    {
        var view = ViewText();
        if (view == null) return;
        // 23 header grids (the four mirrored copies of Genres..Tags included): each collapses the header and forces the toggle open, for the global switch and its own
        Assert.Equal(23 * 2, Reads(view, "DetailsViewShowSectionHeaders"), "the global switch is read by every header twice (grid and toggle button)");
        foreach (var s in Sections)
        {
            int n = Reads(view, "DetailsViewShow" + s + "Header");
            Assert.True(n >= 2 && n % 2 == 0, s + " header switch is read " + n + " times (grid and toggle button, once per copy)");
        }

    }

    public static void The_header_switches_live_in_the_section_look_block()
    {
        var schema = Load();
        var homes = schema.Homes.ToDictionary(h => h.Key, h => h.Value);
        string where = homes["DetailsViewShowSectionHeaders"];
        Assert.Equal("sections/look", where);
        foreach (var s in Sections) Assert.Equal("sections/look", homes["DetailsViewShow" + s + "Header"]);
    }
}
