using System.Globalization;
using System.IO;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

public static class SubCategoryTests
{
    private static ShellViewModel Make()
    {
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        var store = new ConstantStore(schema.AllConstants());
        var applier = new ProfileApplier(schema, store, new FakePresetFiles(), @"C:\theme");
        return new ShellViewModel(schema, store, new History(store), applier, new Profile { Name = "Default" }, k => null, new CultureInfo("en-US"));
    }

    private static PageViewModel Details(ShellViewModel shell) { return shell.Pages.First(p => p.Id == "details"); }

    public static void A_long_page_gets_one_button_per_group_plus_All()
    {
        var page = Details(Make());
        Assert.True(page.HasChips);
        Assert.Equal(page.Groups.Count + 1, page.Chips.Count);
        Assert.Equal("All", page.Chips[0].Title);
        Assert.True(page.Chips[0].IsSelected, "everything is shown at first");
        Assert.False(page.Chips.Any(c => c.Title.StartsWith("Details view")), "the page name is not repeated on each button");
        Assert.Equal(page.Settings.Count(), page.Chips[0].Count);
    }

    public static void Choosing_a_sub_category_shows_only_that_group_and_All_shows_everything_again()
    {
        var shell = Make();
        var page = Details(shell);
        var chip = page.Chips[2];
        chip.SelectCommand.Execute(null);
        Assert.True(chip.IsSelected);
        Assert.False(page.Chips[0].IsSelected);
        Assert.Equal(1, page.Groups.Count(g => g.Visible));
        Assert.True(page.Groups.Single(g => g.Visible) == chip.Group);
        page.Chips[0].SelectCommand.Execute(null);
        Assert.Equal(page.Groups.Count, page.Groups.Count(g => g.Visible));
    }

    public static void Search_lists_every_match_whatever_sub_category_is_chosen()
    {
        var shell = Make();
        var page = Details(shell);
        page.Chips[1].SelectCommand.Execute(null);
        shell.Search = "tabs";
        Assert.True(page.Groups.Count(g => g.Visible) >= 1);
        Assert.True(page.Groups.Where(g => g.Visible).All(g => g.Settings.Any(s => s.Visible)));
        shell.Search = "";
        Assert.Equal(1, page.Groups.Count(g => g.Visible), "the chosen sub-category applies again once the search is cleared");
    }

    public static void A_short_page_has_no_buttons()
    {
        var shell = Make();
        var page = shell.Pages.First(p => p.Id == "appearance");
        Assert.False(page.HasChips);
    }

    public static void A_page_with_no_match_is_dimmed_and_visual_pages_are_found_by_their_keywords()
    {
        var shell = Make();
        shell.Search = "radius";
        var corners = shell.Pages.First(p => p.Kind == "corners");
        Assert.False(corners.Dimmed, "the Corners page is found through its keywords");
        var sections = shell.Pages.First(p => p.Kind == "sections");
        Assert.True(sections.Dimmed, "Sections has nothing about a radius");
        shell.Search = "";
        Assert.False(sections.Dimmed);
        Assert.False(corners.Dimmed);
    }

    public static void Typing_a_search_jumps_to_a_page_that_has_matches_and_clear_empties_it()
    {
        var shell = Make();
        shell.SelectedPage = shell.Pages.First(p => p.Kind == "sections");
        shell.Search = "radius";
        Assert.False(shell.SelectedPage.Dimmed, "the selection moved off a page with nothing to show");
        Assert.True(shell.ClearSearchCommand.CanExecute(null));
        shell.ClearSearchCommand.Execute(null);
        Assert.Equal("", shell.Search);
    }

    public static void The_changed_count_follows_the_store_and_the_page_reset_puts_it_back()
    {
        var shell = Make();
        var page = shell.Pages.First(p => p.Kind == "corners");
        shell.SelectedPage = page;
        Assert.Equal(0, page.ChangedCount);
        Assert.False(shell.ResetPageCommand.CanExecute(null));
        var corners = (CornersEditorViewModel)page.Editor;
        corners.Items[0].Radius = 3;
        Assert.True(page.ChangedCount > 0);
        Assert.True(shell.ResetPageLabel.StartsWith("Reset this page ("), shell.ResetPageLabel);
        shell.ResetPageCommand.Execute(null);
        Assert.Equal(0, page.ChangedCount);
        Assert.Equal(0, shell.ChangedTotal);
    }
}
