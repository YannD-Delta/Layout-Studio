using System.Globalization;
using System.IO;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

/// <summary>The Header cards page holds every header parameter: the cells, a table of the information chips, and the other header settings.</summary>
public static class HeaderExtrasTests
{
    private static ShellViewModel Shell(out ConstantStore store)
    {
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        store = new ConstantStore(schema.AllConstants());
        var history = new History(store);
        var applier = new ProfileApplier(schema, store, new FakePresetFiles(), @"C:\theme");
        return new ShellViewModel(schema, store, history, applier, new Profile { Name = "Default" }, k => null, new CultureInfo("en-US"));
    }

    private static HeaderExtrasViewModel Extras(ShellViewModel shell)
    {
        return ((TilesEditorViewModel)shell.Pages.Single(p => p.Kind == "tiles").Editor).Extras;
    }

    public static void Every_information_chip_is_one_row_with_its_show_and_new_line_switches()
    {
        ConstantStore store;
        var chips = Extras(Shell(out store)).Chips;
        Assert.True(chips.Count >= 15, "the chips of the artwork card: " + chips.Count);
        var genres = chips.Single(c => c.Title == "Genres");
        Assert.Equal("HeroShowGenres", genres.Show.Keys[0]);
        Assert.Equal(1, genres.Breaks.Count);
        Assert.Equal("HeroChipBreakGenres", genres.Breaks[0].Keys[0]);
        var hltb = chips.Single(c => c.Title == "HowLongToBeat");
        Assert.Equal(3, hltb.Breaks.Count, "main, main + extra, 100 %");
        Assert.True(hltb.SeveralBreaks);
    }

    public static void A_chip_switch_edits_the_same_constant_as_the_generated_page_and_the_items_card()
    {
        ConstantStore store;
        var shell = Shell(out store);
        var row = Extras(shell).Chips.Single(c => c.Title == "Genres");
        bool before = store.Get("HeroShowGenres") == "True";
        row.Show.BoolValue = !before;
        Assert.Equal(before ? "False" : "True", store.Get("HeroShowGenres"));
        var card = ((ItemsEditorViewModel)shell.Pages.Single(p => p.Kind == "items").Editor).Cards.Single(c => c.Id == "Genres");
        Assert.Equal(!before, card.Settings.Single(s => s.Keys[0] == "HeroShowGenres").BoolValue, "same store, same value");
    }

    public static void The_page_also_lists_the_other_header_settings_but_not_the_chip_switches_twice()
    {
        ConstantStore store;
        var groups = Extras(Shell(out store)).Groups;
        var keys = groups.SelectMany(g => g.Settings).SelectMany(s => s.Keys).ToList();
        Assert.True(keys.Contains("HeroBlurRadius"), "the artwork card look");
        Assert.True(keys.Contains("HeaderTileValueFontSize"), "the tile look");
        Assert.True(keys.Contains("DetailsViewShowHeaderBar"), "the header bar");
        Assert.True(keys.Contains("HeroInfoLineMaxWidth"), "the chip line width");
        Assert.False(keys.Contains("HeroShowGenres"), "the chip switches are the table above");
        Assert.True(groups.SelectMany(g => g.Settings).Any(s => s.Kind == "preset"), "the header presets are there too");
        Assert.False(groups.SelectMany(g => g.Settings).Any(s => s.Kind == "tileSlots"), "the cells are the editor above");
    }

    public static void Every_header_control_of_the_schema_is_on_the_page_once_or_in_the_chip_table()
    {
        ConstantStore store;
        var shell = Shell(out store);
        var extras = Extras(shell);
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        var header = schema.Pages.Single(p => p.Id == "header").Groups.SelectMany(g => g.Controls).Where(c => c.Kind != "tileSlots").ToList();
        var onPage = extras.Groups.SelectMany(g => g.Settings).Select(s => s.Id)
            .Concat(extras.Chips.SelectMany(r => new[] { r.Show.Id }.Concat(r.Breaks.Select(b => b.Id)))).ToList();
        var missing = header.Where(c => !onPage.Contains(c.Id)).Select(c => c.Id).ToList();
        // chip switches of items the table does not know stay on the generated page only: allowed, but few
        Assert.True(missing.Count <= 0, "header controls not on the page: " + string.Join(", ", missing));
    }
}
