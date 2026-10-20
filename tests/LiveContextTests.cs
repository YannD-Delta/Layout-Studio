using System.Globalization;
using System.IO;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

/// <summary>The Sizes page knows which sizes can be seen in the real window right now, and previews the others in a dock.</summary>
public static class LiveContextTests
{
    private static LiveContext Details(bool game = true, bool logo = false, bool desc = false, bool links = false)
    {
        return new LiveContext { View = "details", HasGame = game, HasLogo = logo, HasDescription = desc, HasLinks = links };
    }

    public static void Always_is_live_in_any_view_even_without_knowing_the_view()
    {
        Assert.True(LiveRule.IsLive("always", new LiveContext { View = "grid" }));
        Assert.True(LiveRule.IsLive("always", null));
        Assert.True(LiveRule.IsLive(null, null));
    }

    public static void A_view_rule_follows_the_active_view()
    {
        Assert.True(LiveRule.IsLive("view:details", Details()));
        Assert.False(LiveRule.IsLive("view:details", new LiveContext { View = "grid", HasGame = true }));
        Assert.True(LiveRule.WhyNot("view:details", new LiveContext { View = "grid" }).Contains("Grid"));
    }

    public static void A_game_rule_needs_the_selected_game_to_have_it()
    {
        Assert.False(LiveRule.IsLive("view:details & game:links", Details(links: false)));
        Assert.True(LiveRule.IsLive("view:details & game:links", Details(links: true)));
        Assert.True(LiveRule.WhyNot("view:details & game:logo", Details()).Contains("logo"));
        Assert.True(LiveRule.WhyNot("view:details & game:description", Details(game: false)).Contains("Select a game"));
        Assert.True(LiveRule.IsLive("view:details & game:description", Details(desc: true)));
    }

    public static void Without_a_context_a_conditional_item_is_not_called_live()
    {
        Assert.False(LiveRule.IsLive("view:details", null));
    }

    private static SizesEditorViewModel Sizes()
    {
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        var store = new ConstantStore(schema.AllConstants());
        return new SizesEditorViewModel(store, schema, new History(store), new CultureInfo("en-US"));
    }

    public static void The_dock_is_hidden_until_a_card_is_active_and_a_context_is_known()
    {
        var sizes = Sizes();
        Assert.Equal("Hidden", sizes.DockState);
        sizes.Activate(sizes.Items.Single(i => i.Id == "linkIcon"));
        Assert.Equal("Hidden", sizes.DockState, "no context yet");
    }

    public static void An_item_that_is_not_on_screen_gets_the_full_dock_with_the_reason()
    {
        var sizes = Sizes();
        sizes.Context = Details(links: false);
        sizes.Activate(sizes.Items.Single(i => i.Id == "linkIcon"));
        Assert.Equal("Full", sizes.DockState);
        Assert.True(sizes.DockText.Contains("links"), sizes.DockText);
        sizes.Context = Details(links: true);
        Assert.Equal("Slim", sizes.DockState, "the selected game now has links: it is live");
        Assert.True(sizes.DockText.Contains("live"), sizes.DockText);
    }

    public static void A_live_item_gets_the_slim_notice_and_the_pin_forces_the_full_dock()
    {
        var sizes = Sizes();
        sizes.Context = Details();
        sizes.Activate(sizes.Items.Single(i => i.Id == "searchBox"));
        Assert.Equal("Slim", sizes.DockState);
        sizes.PinPreview = true;
        Assert.Equal("Full", sizes.DockState);
        sizes.PinPreview = false;
        sizes.CloseDock();
        Assert.Equal("Hidden", sizes.DockState, "closed for the session");
    }

    public static void Every_dock_scene_belongs_to_a_size_of_the_schema_and_the_plain_box_exists()
    {
        string xaml = File.ReadAllText(Path.Combine(TestPaths.RepoRoot(), @"layout-studio\src\Editor\EditorWindow.xaml"));
        Assert.True(xaml.Contains("x:Key=\"Scene.box\""), "the fallback scene exists");
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        var scenes = System.Text.RegularExpressions.Regex.Matches(xaml, "x:Key=\"Scene\\.([A-Za-z0-9]+)\"").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).Where(s => s != "box").ToList();
        Assert.True(scenes.Count >= 3, "linkIcon, description and gameLogo have a scene");
        foreach (var id in scenes) Assert.True(schema.Sizes.Any(s => s.Id == id), "Scene." + id + " matches no size");
    }

    public static void The_dock_sentences_follow_the_language()
    {
        var sizes = Sizes();
        sizes.Localize = k => k == "LOCLS_Dock_Preview" ? "Aperçu seulement. {0}." : k == "LOCLS_Live_Links" ? "Pas de liens" : null;
        sizes.Context = Details(links: false);
        sizes.Activate(sizes.Items.Single(i => i.Id == "linkIcon"));
        Assert.Equal("Aperçu seulement. Pas de liens.", sizes.DockText);
    }

    public static void Cards_know_if_they_are_live()
    {
        var sizes = Sizes();
        sizes.Context = new LiveContext { View = "grid", HasGame = true };
        Assert.True(sizes.Items.Single(i => i.Id == "searchBox").IsLive);
        Assert.False(sizes.Items.Single(i => i.Id == "cover").IsLive);
        Assert.True(sizes.Items.Single(i => i.Id == "cover").NotLiveReason.Contains("Details"));
    }
}
