using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

/// <summary>An item shown both as a chip and as its own section says so on its card, and one click folds the section at the bottom.</summary>
public static class ItemDuplicatesTests
{
    private static Dictionary<string, string> Values(params string[] pairs)
    {
        var d = new Dictionary<string, string>();
        for (int i = 0; i < pairs.Length; i += 2) d[pairs[i]] = pairs[i + 1];
        return d;
    }

    private static bool Twice(string family, Dictionary<string, string> values)
    {
        return ItemDuplicates.ShowsTwice(ItemDuplicates.For(family), k => values.ContainsKey(k) ? values[k] : "False");
    }

    public static void An_item_is_shown_twice_when_chip_and_section_show_and_the_section_is_not_folded()
    {
        Assert.True(Twice("Genres", Values("HeroShowGenres", "True")), "no demote switch on");
        Assert.True(Twice("Genres", Values("HeroShowGenres", "True", "DemoteSectionsShownElsewhere", "True")), "the global switch alone is not enough");
        Assert.True(Twice("Genres", Values("HeroShowGenres", "True", "DemoteGenresSection", "True")), "the item switch alone is not enough");
        Assert.False(Twice("Genres", Values("HeroShowGenres", "True", "DemoteSectionsShownElsewhere", "True", "DemoteGenresSection", "True")), "folded at the bottom");
        Assert.False(Twice("Genres", Values("HeroShowGenres", "False")), "no chip");
    }

    public static void A_hidden_section_cannot_double_a_chip()
    {
        Assert.True(Twice("Hltb", Values("HeroShowHltb", "True", "DetailsViewShowHltbPanel", "True")));
        Assert.False(Twice("Hltb", Values("HeroShowHltb", "True", "DetailsViewShowHltbPanel", "False")));
    }

    public static void An_item_without_a_rule_is_never_shown_twice()
    {
        Assert.False(ItemDuplicates.ShowsTwice(ItemDuplicates.For("ReleaseDate"), k => "True"));
    }

    public static void Every_key_the_rules_read_exists_in_the_theme_schema()
    {
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        var store = new ConstantStore(schema.AllConstants());
        foreach (var key in ItemDuplicates.AllKeys()) Assert.True(store.Find(key) != null, key + " is a constant of the theme");
        foreach (var rule in ItemDuplicates.Rules) Assert.True(ItemFamilies.All.Any(f => f.Id == rule.FamilyId), rule.FamilyId + " is an item");
    }

    private static ItemsEditorViewModel Items(out ConstantStore store, out History history)
    {
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        store = new ConstantStore(schema.AllConstants());
        history = new History(store);
        var applier = new ProfileApplier(schema, store, new FakePresetFiles(), @"C:\theme");
        return new ItemsEditorViewModel(schema, store, history, applier, new Profile { Name = "Default" }, k => null, new CultureInfo("en-US"));
    }

    public static void The_card_says_it_and_one_click_folds_the_section_in_one_undo_step()
    {
        ConstantStore store; History history;
        var items = Items(out store, out history);
        var card = items.Cards.Single(c => c.Id == "Achievements");
        string error;
        store.TrySet("HeroShowAchievements", "True", out error);
        store.TrySet("DetailsViewShowAchievementsPanel", "True", out error);
        store.TrySet("DemoteSectionsShownElsewhere", "False", out error);
        Assert.True(card.ShowsTwice);
        Assert.True(card.TwiceText.Contains("section"));
        card.FoldSectionCommand.Execute(null);
        Assert.Equal("True", store.Get("DemoteSectionsShownElsewhere"));
        Assert.Equal("True", store.Get("DemoteAchievementsSection"));
        Assert.False(card.ShowsTwice);
        history.Undo();
        Assert.True(card.ShowsTwice, "one undo step unfolds it");
    }

    public static void A_card_without_a_rule_has_no_notice()
    {
        ConstantStore store; History history;
        var card = Items(out store, out history).Cards.Single(c => c.Id == "ReleaseDate");
        Assert.False(card.ShowsTwice);
    }
}
