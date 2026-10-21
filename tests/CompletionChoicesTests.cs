using System.Collections.Generic;
using System.IO;
using System.Linq;
using LayoutStudio.Core;

public static class CompletionChoicesTests
{
    private static List<KeyValuePair<string, string>> Statuses()
    {
        return new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("11111111-1111-1111-1111-111111111111", "Playing"),
            new KeyValuePair<string, string>("22222222-2222-2222-2222-222222222222", "Beaten"),
            new KeyValuePair<string, string>("33333333-3333-3333-3333-333333333333", "  Abandoned ")
        };
    }

    public static void The_choices_start_with_none_then_the_statuses_in_their_order()
    {
        var c = CompletionChoices.Build(Statuses(), null, "—");
        Assert.Equal(4, c.Count);
        Assert.Equal("", c[0].Id);
        Assert.Equal("—", c[0].Name);
        Assert.Equal("Playing,Beaten,Abandoned", string.Join(",", c.Skip(1).Select(x => x.Name)), "the order of Playnite, names trimmed");
    }

    public static void The_current_status_is_marked()
    {
        var c = CompletionChoices.Build(Statuses(), "22222222-2222-2222-2222-222222222222", "—");
        Assert.Equal("Beaten", c.Single(x => x.IsCurrent).Name);
    }

    public static void No_status_or_an_unknown_one_marks_none()
    {
        Assert.True(CompletionChoices.Build(Statuses(), null, "—")[0].IsCurrent);
        Assert.True(CompletionChoices.Build(Statuses(), "00000000-0000-0000-0000-000000000000", "—")[0].IsCurrent);
        Assert.True(CompletionChoices.Build(Statuses(), "99999999-9999-9999-9999-999999999999", "—")[0].IsCurrent, "a status that no longer exists is none");
    }

    public static void The_id_the_picker_writes_back_maps_to_a_guid_or_to_none()
    {
        Assert.Equal("22222222-2222-2222-2222-222222222222", CompletionChoices.ToGuid("22222222-2222-2222-2222-222222222222").ToString());
        Assert.Equal(System.Guid.Empty, CompletionChoices.ToGuid(""));
        Assert.Equal(System.Guid.Empty, CompletionChoices.ToGuid(null));
        Assert.Equal(System.Guid.Empty, CompletionChoices.ToGuid("not a guid"));
    }

    // The picker is a ComboBox of the page's card: the list and the selection come from the extension (PluginSettings), not from a named page element.
    public static void The_completion_card_holds_a_picker_bound_to_the_extension_and_no_named_element()
    {
        string theme = Path.Combine(TestPaths.RepoRoot(), @"theme\Modula\Views\DetailsViewGameOverview.xaml");
        if (!File.Exists(theme)) return;   // the exported Layout Studio repository has no theme
        string xaml = File.ReadAllText(theme);
        int a = xaml.IndexOf("<DataTemplate x:Key=\"Card.CompletionStatus\">", System.StringComparison.Ordinal);
        int b = xaml.IndexOf("</DataTemplate>", a, System.StringComparison.Ordinal);
        string card = xaml.Substring(a, b - a);
        Assert.True(card.Contains("<ComboBox"), "the card has a selector");
        Assert.True(card.Contains("Path=Completion.Choices"), "its list comes from the extension");
        Assert.True(card.Contains("Path=Completion.StatusId, Mode=TwoWay"), "its selection is the extension's, two ways");
        Assert.True(card.Contains("Game.CompletionStatus.Name"), "without the extension the card still shows the value");
        Assert.True(!card.Contains("PART_") && !card.Contains("ThemeExtras_"), "no element the add-ons look up by name");
    }

    public static void Following_a_game_never_counts_as_a_choice()
    {
        var picker = new CompletionPicker();
        var picked = new List<string>();
        picker.Picked = id => picked.Add(id);
        var list = CompletionChoices.Build(Statuses(), "22222222-2222-2222-2222-222222222222", "-");
        // what the bound ComboBox does while its list is refilled: it reports "nothing selected"
        picker.Choices.CollectionChanged += (s, e) => picker.StatusId = null;
        picker.Show(list, "22222222-2222-2222-2222-222222222222");
        Assert.Equal(0, picked.Count, "the refill is not a choice");
        Assert.Equal("22222222-2222-2222-2222-222222222222", picker.StatusId, "the game's status is what the box shows");
        Assert.True(picker.HasChoices);
    }

    public static void A_choice_of_the_picker_is_reported_once()
    {
        var picker = new CompletionPicker();
        var picked = new List<string>();
        picker.Picked = id => picked.Add(id);
        picker.Show(CompletionChoices.Build(Statuses(), null, "-"), "");
        picker.StatusId = "11111111-1111-1111-1111-111111111111";
        picker.StatusId = "11111111-1111-1111-1111-111111111111";
        Assert.Equal("11111111-1111-1111-1111-111111111111", string.Join(",", picked));
        picker.StatusId = "";
        Assert.Equal(2, picked.Count, "choosing none is a choice too");
    }

    public static void Without_a_game_the_picker_is_empty()
    {
        var picker = new CompletionPicker();
        picker.Show(new List<CompletionChoice>(), "");
        Assert.False(picker.HasChoices);
    }
}
