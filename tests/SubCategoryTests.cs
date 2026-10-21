using System.Globalization;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

/// <summary>
/// The older layout (a schema without ui-layout.yaml, such as the Fullscreen theme's or the small test schema): a long page gets one button per group.
/// The ten pages of the Modula editor have blocks instead of buttons (see NavigationTests).
/// </summary>
public static class SubCategoryTests
{
    // an older-layout schema (no ui-layout homes): one long page of three groups, one short page, a blur slider to search for
    private const string Json = @"{
  ""version"": 1,
  ""constants"": [
    {""key"":""A1"",""type"":""Boolean"",""default"":""True""}, {""key"":""A2"",""type"":""Boolean"",""default"":""True""},
    {""key"":""B1"",""type"":""Boolean"",""default"":""True""}, {""key"":""B2"",""type"":""Boolean"",""default"":""True""},
    {""key"":""C1"",""type"":""Boolean"",""default"":""True""}, {""key"":""C2"",""type"":""Boolean"",""default"":""True""},
    {""key"":""SlotCount"",""type"":""Int"",""default"":""4"",""min"":1,""max"":12},
    {""key"":""WindowGlassBlurRadius"",""type"":""Double"",""default"":""30"",""min"":0,""max"":80,""step"":1}
  ],
  ""pages"": [
    {""id"":""details"",""title"":""Details view"",""groups"":[
      {""id"":""a"",""title"":""Details view - first"",""controls"":[
        {""kind"":""toggle"",""id"":""A1"",""keys"":[""A1""],""title"":""Alpha one"",""choices"":[]}, {""kind"":""toggle"",""id"":""A2"",""keys"":[""A2""],""title"":""Alpha two"",""choices"":[]}]},
      {""id"":""b"",""title"":""Details view - second"",""controls"":[
        {""kind"":""toggle"",""id"":""B1"",""keys"":[""B1""],""title"":""Beta one"",""choices"":[]}, {""kind"":""toggle"",""id"":""B2"",""keys"":[""B2""],""title"":""Beta two"",""choices"":[]},
        {""kind"":""number"",""id"":""SlotCount"",""keys"":[""SlotCount""],""title"":""Number of slots"",""choices"":[]}]},
      {""id"":""c"",""title"":""Details view - third"",""controls"":[
        {""kind"":""toggle"",""id"":""C1"",""keys"":[""C1""],""title"":""Gamma one"",""choices"":[]}, {""kind"":""toggle"",""id"":""C2"",""keys"":[""C2""],""title"":""Gamma two"",""choices"":[]}]}]},
    {""id"":""background"",""title"":""Background"",""groups"":[
      {""id"":""w"",""title"":""Window"",""controls"":[{""kind"":""slider"",""id"":""WindowGlassBlurRadius"",""keys"":[""WindowGlassBlurRadius""],""title"":""Window background blur"",""choices"":[]}]},
      {""id"":""x"",""title"":""Other"",""controls"":[{""kind"":""toggle"",""id"":""A1x"",""keys"":[""A1""],""title"":""Same toggle"",""choices"":[]}]}]}
  ]
}";

    private static ShellViewModel Make()
    {
        var schema = Schema.Load(Json);
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
        Assert.False(page.Chips.Any(c => c.Title.StartsWith("Details view")), "the page name is not repeated on each button");
        Assert.True(page.Chips[0].IsSelected, "everything is shown at first");
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
        shell.Search = "gamma";
        Assert.True(page.Groups.Count(g => g.Visible) >= 1);
        Assert.True(page.Groups.Where(g => g.Visible).All(g => g.Settings.Any(s => s.Visible)));
        shell.Search = "";
        Assert.Equal(1, page.Groups.Count(g => g.Visible), "the chosen sub-category applies again once the search is cleared");
    }

    public static void A_short_page_has_no_buttons()
    {
        var shell = Make();
        var page = shell.Pages.First(p => p.Id == "background");
        Assert.False(page.HasChips);
    }

    public static void A_page_with_no_match_is_dimmed_and_visual_pages_are_found_by_their_keywords()
    {
        var shell = Make();
        shell.Search = "hwinfo";
        var diagnostics = shell.Pages.First(p => p.Kind == "diagnostics");
        Assert.False(diagnostics.Dimmed, "the Diagnostics page is found through its keywords");
        var background = shell.Pages.First(p => p.Id == "background");
        Assert.True(background.Dimmed, "the background page has nothing about a sensor");
        shell.Search = "";
        Assert.False(background.Dimmed);
        Assert.False(diagnostics.Dimmed);
    }

    public static void Typing_a_search_jumps_to_a_page_that_has_matches_and_clear_empties_it()
    {
        var shell = Make();
        shell.SelectedPage = Details(shell);
        shell.Search = "blur";
        Assert.False(shell.SelectedPage.Dimmed, "the selection moved off a page with nothing to show");
        Assert.Equal("background", shell.SelectedPage.Id);
        Assert.True(shell.ClearSearchCommand.CanExecute(null));
        shell.ClearSearchCommand.Execute(null);
        Assert.Equal("", shell.Search);
    }

    public static void The_changed_count_follows_the_store_and_the_page_reset_puts_it_back()
    {
        var shell = Make();
        var page = Details(shell);
        shell.SelectedPage = page;
        Assert.Equal(0, page.ChangedCount);
        Assert.False(shell.ResetPageCommand.CanExecute(null));
        page.Settings.First(s => s.Id == "SlotCount").Text = "7";
        Assert.True(page.ChangedCount > 0);
        Assert.True(shell.ResetPageLabel.StartsWith("Reset this page ("), shell.ResetPageLabel);
        shell.ResetPageCommand.Execute(null);
        Assert.Equal(0, page.ChangedCount);
        Assert.Equal(0, shell.ChangedTotal);
    }
}
