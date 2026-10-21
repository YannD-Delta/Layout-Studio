using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LayoutStudio.Core;

/// <summary>The grid mode (covers, group headers, game panel): static checks on the theme files so a hard-coded colour, a lost option or a cut-off panel cannot come back.</summary>
public static class GridViewTests
{
    private static string Read(string relative) { return File.ReadAllText(Path.Combine(TestPaths.RepoRoot(), relative)); }
    private static string Theme(string file) { return Read(@"theme\Modula\" + file); }

    private static readonly string[] NewOptions = { "GridViewCoverTitleOverlay", "GridViewCoverTitleOverlayOnHover", "GridViewShowZoomSlider" };

    public static void The_new_grid_options_have_the_whole_chain_and_keep_todays_look_by_default()
    {
        string constants = Theme("Constants.xaml"), options = Theme("options.yaml"), layout = Theme("ui-layout.yaml"),
               en = Theme(@"Localization\en_US.xaml"), fr = Theme(@"Localization\fr_FR.xaml"), schema = Theme("layout-studio.schema.json");
        foreach (string key in NewOptions)
        {
            Assert.True(Regex.IsMatch(constants, "x:Key=\"" + key + "\""), key + " missing in Constants.xaml");
            Assert.True(Regex.IsMatch(options, "(?m)^    " + key + ":"), key + " missing in options.yaml");
            Assert.True(layout.Contains(key), key + " has no home in ui-layout.yaml");
            Assert.True(schema.Contains("\"" + key + "\""), key + " missing in the generated schema (run tools/convert_options.py)");
            Assert.True(en.Contains("LOCHNDOpt" + key + "\""), key + " has no English label");
            Assert.True(fr.Contains("LOCHNDOpt" + key + "\""), key + " has no French label");
        }
        Assert.True(Regex.IsMatch(constants, "GridViewCoverTitleOverlay\">False<"), "the title overlay is off by default");
        Assert.True(Regex.IsMatch(constants, "GridViewCoverTitleOverlayOnHover\">False<"), "the overlay on hover is off by default");
        Assert.True(Regex.IsMatch(constants, "GridViewShowZoomSlider\">True<"), "the zoom slider stays visible by default");
    }

    public static void The_group_header_of_the_grid_follows_the_colour_profile()
    {
        string xaml = Theme(@"DerivedStyles\GridViewGroupStyle.xaml");
        var literal = Regex.Matches(xaml, "(Background|Foreground|BorderBrush|Fill|Stroke)=\"#[0-9A-Fa-f]{6,8}\"").Cast<Match>().Select(m => m.Value).ToArray();
        Assert.True(literal.Length == 0, "hard-coded colours in the grid group header: " + string.Join(", ", literal));
    }

    public static void The_cover_placeholder_reads_on_light_and_dark_profiles()
    {
        string xaml = Theme(@"DerivedStyles\GridViewItemTemplate.xaml");
        int at = xaml.IndexOf("x:Name=\"TextGameName\"");
        Assert.True(at > 0, "the placeholder element exists");
        string block = xaml.Substring(at, xaml.IndexOf("x:Name=\"TitleOverlayProxy\"", at) > 0 ? xaml.IndexOf("x:Name=\"TitleOverlayProxy\"", at) - at : 1500);
        Assert.False(block.Contains("#99000000"), "no black plate behind the title of a cover-less game (unreadable with dark text on light profiles)");
        Assert.True(block.Contains("DynamicResource TextBrush"), "the placeholder title takes the profile text colour");
    }

    public static void Every_colour_profile_defines_the_brushes_the_grid_paints_with()
    {
        string[] brushes = { "GridViewItemMaskBackgroundBrush", "GroupCountBackgroundBrush", "GridViewCoverSubtitleBrush", "GridItemBackgroundBrush", "GlassSelectionFillBrush" };
        string dir = Path.Combine(TestPaths.RepoRoot(), @"theme\Modula\Presets\Colors");
        string constants = Theme("Constants.xaml");
        foreach (string brush in brushes)
            Assert.True(constants.Contains("x:Key=\"" + brush + "\""), brush + " has a default in Constants.xaml");
        foreach (string file in Directory.GetFiles(dir, "*.xaml"))
        {
            string text = File.ReadAllText(file);
            foreach (string brush in brushes.Take(4))
                Assert.True(text.Contains("x:Key=\"" + brush + "\""), Path.GetFileName(file) + " does not define " + brush + ": the grid would keep the dark default on this profile");
        }
    }

    public static void The_grid_panel_is_never_narrower_than_its_content_needs()
    {
        string view = Theme(@"Views\LibraryGridView.xaml");
        Assert.True(Regex.IsMatch(view, @"<GridViewGameOverview[^>]*MinWidth=""\d+"""), "the panel has a MinWidth");
        string panel = Theme(@"Views\GridViewGameOverview.xaml");
        int row = panel.IndexOf("x:Name=\"HeaderRow\"");
        Assert.True(row > 0, "header row exists");
        string after = panel.Substring(row, 400);
        Assert.True(after.Contains("<WrapPanel"), "the Play / stats row wraps instead of cutting the stats at narrow widths");
    }

    public static void The_grid_and_the_details_list_use_the_same_separator_width_next_to_the_panel()
    {
        string grid = Theme(@"Views\LibraryGridView.xaml"), details = Theme(@"Views\LibraryDetailsView.xaml");
        Assert.True(grid.Contains("2.5,0,0,0") && details.Contains("2.5,0,0,0"), "same 2.5 px separator in the grid and the details view");
    }
}
