using System.Globalization;
using System.IO;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

/// <summary>A piece of information or a section is one card with all its options (where it shows, new line, section column...), not a lone checkbox in a long list.</summary>
public static class ItemsEditorTests
{
    private class Env
    {
        public Schema Schema;
        public ConstantStore Store;
        public History History;
        public ItemsEditorViewModel Items;
        public ShellViewModel Shell;
    }

    private static Env Make()
    {
        var e = new Env();
        e.Schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        e.Store = new ConstantStore(e.Schema.AllConstants());
        e.History = new History(e.Store);
        var profile = new Profile { Name = "Default" };
        var applier = new ProfileApplier(e.Schema, e.Store, new FakePresetFiles(), @"C:\theme");
        e.Items = new ItemsEditorViewModel(e.Schema, e.Store, e.History, applier, profile, k => null, new CultureInfo("en-US"));
        e.Shell = new ShellViewModel(e.Schema, e.Store, e.History, applier, profile, k => null, new CultureInfo("en-US"));
        return e;
    }

    private static string Family(string key) { var f = ItemFamilies.FamilyOf(key); return f == null ? null : f.Id; }

    public static void A_constant_belongs_to_the_item_it_is_about()
    {
        Assert.Equal("Genres", Family("HeroShowGenres"));
        Assert.Equal("Genres", Family("DetailsViewGenresInRightColumn"));
        Assert.Equal("Genres", Family("DemoteGenresSection"));
        Assert.Equal("RareAchievements", Family("HeroShowRareAchievements"));
        Assert.Equal("Achievements", Family("DetailsViewAchievementsShowPieChart"));
        Assert.Equal("Hltb", Family("HeroChipBreakHltbMain"));
        Assert.Equal("Hltb", Family("DetailsViewTabHowLongToBeat"));
        Assert.Equal("WebExplorer", Family("DetailsViewWebInRightColumn"));
        Assert.Equal("Notes", Family("DetailsViewExpandNotesPanel"));
    }

    public static void Layout_and_look_constants_belong_to_no_item()
    {
        Assert.Equal(null, Family("WindowGlassBlurRadius"));
        Assert.Equal(null, Family("SectionChipsMargin"));
        Assert.Equal(null, Family("HeroCardCornerRadius"));
        Assert.Equal(null, Family("DetailsViewSectionOrderRightNotes"));   // the order is the Sections page's job
    }

    public static void An_option_says_what_it_does_on_its_card()
    {
        var genres = ItemFamilies.FamilyOf("HeroShowGenres");
        Assert.Equal("Show it in the chips", ItemFamilies.OptionTitle("HeroShowGenres", genres));
        Assert.Equal("Start a new line before it", ItemFamilies.OptionTitle("HeroChipBreakGenres", genres));
        Assert.Equal("Section open by default", ItemFamilies.OptionTitle("DetailsViewExpandGenres", genres));
        Assert.Equal("Section in the right column", ItemFamilies.OptionTitle("DetailsViewGenresInRightColumn", genres));
        Assert.Equal(null, ItemFamilies.OptionTitle("DetailsViewTabGenres", genres), "a title that is already clear is kept");
    }

    public static void The_genres_card_holds_every_option_of_genres_in_order_of_where_they_show()
    {
        var card = Make().Items.Cards.Single(c => c.Id == "Genres");
        var keys = card.Settings.SelectMany(s => s.Keys).ToList();
        foreach (var k in new[] { "HeroShowGenres", "HeroChipBreakGenres", "DemoteGenresSection", "DetailsViewExpandGenres", "DetailsViewGenresInRightColumn" })
            Assert.True(keys.Contains(k), k + " is on the card");
        Assert.Equal("In the artwork chips", card.Surfaces[0].Title);
        Assert.Equal("Game page", card.Surfaces[1].Title);
        Assert.True(card.OptionCount >= 5);
        Assert.False(keys.Contains("DetailsViewTabGenres"), "which tab a section sits in is set with the tabs on the Sections page");
    }

    public static void Every_option_is_on_exactly_one_card()
    {
        var e = Make();
        var all = e.Items.AllSettings.SelectMany(s => s.Keys).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
        var expected = ItemFamilies.KeysOf(e.Schema);
        Assert.Equal(expected.Count, all.Count);
    }

    public static void The_shell_lists_the_items_page_and_no_longer_repeats_its_options_on_the_generated_pages()
    {
        var e = Make();
        var page = e.Shell.PageOf("items");
        Assert.True(e.Shell.Block("items").Editor is ItemsEditorViewModel);
        Assert.Equal("Items", page.Title);
        var generated = e.Shell.AllSettings().SelectMany(s => s.Keys).ToList();
        Assert.False(generated.Contains("HeroShowGenres"), "the chip toggle lives on the card now");
        Assert.False(generated.Contains("HeroInfoLineMaxWidth"), "the look of the chips line is on the Items page too, not repeated");
        Assert.True(generated.Contains("HeroCardBorderOpacity"), "a setting about no item and no chip stays where it was");
    }

    public static void Changing_an_option_counts_on_its_card_and_on_the_page_and_reset_puts_it_back()
    {
        var e = Make();
        var card = e.Items.Cards.Single(c => c.Id == "Genres");
        var chip = card.Settings.Single(s => s.Keys[0] == "HeroChipBreakGenres");
        Assert.Equal(0, card.ChangedCount);
        chip.BoolValue = !chip.BoolValue;
        Assert.Equal(1, card.ChangedCount);
        Assert.Equal(1, e.Items.ChangedCount);
        Assert.Equal(1, e.Shell.PageOf("items").ChangedCount);
        e.Items.ResetAll();
        Assert.Equal(0, e.Items.ChangedCount);
        e.History.Undo();
        Assert.Equal(1, e.Items.ChangedCount, "the reset is one undo step");
    }

    public static void Every_word_of_a_card_is_translated_in_both_language_files()
    {
        string dir = Path.Combine(TestPaths.RepoRoot(), @"layout-studio\src\Localization");
        foreach (var file in new[] { "en_US.xaml", "fr_FR.xaml" })
        {
            string text = File.ReadAllText(Path.Combine(dir, file));
            foreach (var word in ItemFamilies.Words())
                Assert.True(text.Contains("x:Key=\"" + ItemFamilies.LocKey(word) + "\""), file + " lacks " + ItemFamilies.LocKey(word) + " for '" + word + "'");
        }
    }

    public static void A_card_uses_the_words_of_the_language_file()
    {
        var e = Make();
        var applier = new ProfileApplier(e.Schema, e.Store, new FakePresetFiles(), @"C:\theme");
        var fr = new ItemsEditorViewModel(e.Schema, e.Store, e.History, applier, new Profile { Name = "Default" },
            k => k == ItemFamilies.LocKey("Genres") ? "Genres FR" : k == ItemFamilies.LocKey("Show it in the chips") ? "Dans les pastilles" : null, new CultureInfo("fr-FR"));
        var card = fr.Cards.Single(c => c.Id == "Genres");
        Assert.Equal("Genres FR", card.Title);
        Assert.True(card.Settings.Any(s => s.Title == "Dans les pastilles"));
    }

    public static void A_section_of_the_sections_page_opens_its_item_card_alone_on_the_items_page()
    {
        var e = Make();
        var sections = e.Shell.EditorOf<SectionsEditorViewModel>();
        var genres = sections.Left.Concat(sections.Right).Single(b => b.Id == "Genres");
        Assert.True(genres.HasOptions, "Genres is an item with options");
        genres.OptionsCommand.Execute(null);
        Assert.Equal("items", e.Shell.SelectedPage.Id);
        var items = e.Shell.EditorOf<ItemsEditorViewModel>();
        Assert.Equal("Genres", items.FocusedId);
        Assert.True(items.Cards.Single(c => c.Id == "Genres").Visible);
        var rows = items.Zones.SelectMany(z => z.Rows).Where(r => r.ItemId == "Genres").ToList();
        Assert.True(rows.Count >= 2 && rows.All(r => r.Visible), "Genres is a chip and a section: both rows show");
        Assert.True(rows.Where(r => r.HasMore).All(r => r.IsMoreOpen), "its options are open");
        Assert.False(items.Cards.Single(c => c.Id == "Tags").Visible, "the other items step aside");
        Assert.False(items.Zones.SelectMany(z => z.Rows).Where(r => r.ItemId == "Tags").Any(r => r.Visible));
        items.ShowAllCommand.Execute(null);
        Assert.False(items.IsFocused);
        Assert.True(items.Cards.Single(c => c.Id == "Tags").Visible);
    }

    public static void Opening_a_card_from_a_section_clears_a_search_that_would_hide_it()
    {
        var e = Make();
        e.Shell.Search = "xyz-nothing-matches";
        e.Shell.ShowChangedOnly = true;
        var sections = e.Shell.EditorOf<SectionsEditorViewModel>();
        sections.Left.Concat(sections.Right).Single(b => b.Id == "Genres").OptionsCommand.Execute(null);
        Assert.Equal("", e.Shell.Search);
        Assert.False(e.Shell.ShowChangedOnly);
        var items = e.Shell.EditorOf<ItemsEditorViewModel>();
        Assert.True(items.Cards.Single(c => c.Id == "Genres").Visible, "the card shows its options");
    }

    public static void Reaching_the_items_page_from_the_sidebar_shows_every_card_again()
    {
        var e = Make();
        var items = e.Shell.EditorOf<ItemsEditorViewModel>();
        items.ShowOnly("Genres");
        e.Shell.SelectedPage = e.Shell.PageOf("sections");
        e.Shell.SelectedPage = e.Shell.PageOf("items");
        Assert.False(items.IsFocused);
    }

    public static void A_search_matching_only_a_hidden_card_counts_no_match()
    {
        var e = Make();
        e.Items.ShowOnly("Genres");
        Assert.Equal(0, e.Items.Filter("release date", false));
    }

    public static void Every_section_that_is_an_item_knows_its_card()
    {
        var e = Make();
        var sections = e.Shell.EditorOf<SectionsEditorViewModel>();
        var withCard = sections.Left.Concat(sections.Right).Where(b => b.HasOptions).Select(b => b.Id).ToList();
        Assert.True(withCard.Count >= 10, "most sections have a card: " + string.Join(",", withCard));
        foreach (var b in sections.Left.Concat(sections.Right).Where(b => b.HasOptions))
        {
            var def = e.Schema.Sections.Single(d => d.Id == b.Id);
            Assert.True(e.Items.Cards.Any(c => c.Id == ItemFamilies.FamilyOfSection(def).Id), b.Id + " points at a card that exists");
        }
    }

    public static void The_option_count_of_a_card_and_the_setting_count_of_a_tile_follow_the_language()
    {
        var e = Make();
        var applier = new ProfileApplier(e.Schema, e.Store, new FakePresetFiles(), @"C:\theme");
        var fr = new ItemsEditorViewModel(e.Schema, e.Store, e.History, applier, new Profile { Name = "Default" },
            k => k == "LOCLS_OptionMany" ? "{0} options FR" : null, new CultureInfo("fr-FR"));
        Assert.Equal("5 options FR", fr.Cards.Single(c => c.Id == "Genres").OptionsText);
        Assert.Equal("5 options", e.Items.Cards.Single(c => c.Id == "Genres").OptionsText, "English without a translation");
    }

    public static void Searching_keeps_the_rows_with_a_match_and_opens_the_options_that_matched()
    {
        var e = Make();
        int matches = e.Items.Filter("new line", false);
        Assert.True(matches > 10, "every chip has a new line option: " + matches);
        var chips = e.Items.Zones.Single(z => z.Id == "chips");
        Assert.True(chips.Rows.Single(r => r.ItemId == "Genres").Visible);
        Assert.False(e.Items.Zones.Single(z => z.Id == "sections").Rows.Single(r => r.ItemId == "Notes").Visible, "Play Notes has no new line option");
        Assert.True(chips.Rows.Single(r => r.ItemId == "Hltb").IsMoreOpen, "HowLongToBeat keeps its three new line switches behind more: the search opens them");
        int all = e.Items.Zones.Sum(z => z.Rows.Count);
        Assert.Equal(all, e.Items.Filter("", false), "an empty search shows everything");
    }
}
