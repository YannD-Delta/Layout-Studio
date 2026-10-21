using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

/// <summary>The ten pages of the editor: every option has one home, pages are made of plain headed blocks, counts and resets work per block, search opens the right page.</summary>
public static class NavigationTests
{
    private class Env
    {
        public Schema Schema;
        public ConstantStore Store;
        public History History;
        public Profile Profile;
        public ShellViewModel Shell;
    }

    private static Env Make(IList<InstalledAddon> addons = null, Func<string, string> localize = null)
    {
        var e = new Env();
        e.Schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        e.Store = new ConstantStore(e.Schema.AllConstants());
        e.History = new History(e.Store);
        e.Profile = new Profile { Name = "Default" };
        var applier = new ProfileApplier(e.Schema, e.Store, new FakePresetFiles(), @"C:\theme");
        Func<IEnumerable<InstalledAddon>> installed = addons == null ? (Func<IEnumerable<InstalledAddon>>)null : () => addons;
        e.Shell = new ShellViewModel(e.Schema, e.Store, e.History, applier, e.Profile, localize ?? (k => null), new CultureInfo("en-US"), null, FakeGlobalSharing.FromTheme(), null, null, null, null, null, null, installed);
        return e;
    }

    private static SettingViewModel Setting(Env e, string id)
    {
        return e.Shell.AllSettings().Single(s => s.Id == id);
    }

    private static readonly string[] PageIds = { "presets", "window", "gamepage", "sections", "items", "colors", "sizes", "grid", "addons", "performance", "profile" };

    // ---- the shape: ten pages, blocks, no sub-category buttons, no accordion -----------------------------------------------------------

    public static void The_editor_has_the_eleven_pages_of_the_agreed_tree_in_order()
    {
        var e = Make();
        var ids = e.Shell.Pages.Select(p => p.Id).ToList();
        Assert.Equal(string.Join(",", PageIds), string.Join(",", ids), "in the order of the tree");
        Assert.True(e.Shell.SelectedPage.Id == "window", "the editor opens on the window page: the global presets are one click away, not in the way");
    }

    public static void A_page_is_blocks_only_two_levels_and_no_buttons_to_pick_a_group()
    {
        var e = Make();
        foreach (var page in e.Shell.Pages)
        {
            Assert.True(page.IsComposite, page.Id + " is a page of blocks");
            Assert.False(page.HasChips, page.Id + " has no sub-category buttons: the blocks are the second level");
            Assert.True(page.Groups.Count > 0 && page.Groups.All(g => !string.IsNullOrEmpty(g.Title)), page.Id + " has headed blocks");
            Assert.True(page.Groups.Select(g => g.Id).Distinct().Count() == page.Groups.Count, page.Id + " block ids are unique");
        }
    }

    public static void Every_page_has_a_summary_and_the_plan_of_the_window_lights_the_area_it_controls()
    {
        var e = Make();
        foreach (var page in e.Shell.Pages)
        {
            Assert.True(!string.IsNullOrEmpty(page.Summary), page.Id + " has a summary");
            Assert.True(page.HasPlan, page.Id + " starts with the plan of the window");
        }
        Assert.False(e.Shell.PageOf("profile").Plan.LitTopBar || e.Shell.PageOf("profile").Plan.LitList, "the Profile page touches no part of the window: nothing is lit");
        Assert.True(e.Shell.PageOf("window").Plan.LitTopBar && e.Shell.PageOf("window").Plan.LitList && e.Shell.PageOf("window").Plan.LitHero, "the window page lights the whole window");
        var sections = e.Shell.PageOf("sections").Plan;
        Assert.True(sections.LitSections && !sections.LitTopBar && !sections.LitList, "the sections page lights the sections only");
        Assert.True(e.Shell.Block("top-bar").Regions.Contains("topbar"), "a block names the rectangle it lights");
    }

    // ---- every option has exactly one home -------------------------------------------------------------------------------------------

    public static void Every_option_has_a_home_that_is_a_block_of_the_schema()
    {
        var e = Make();
        Assert.True(e.Schema.HasLayout);
        var problems = e.Schema.Validate();
        Assert.True(problems.Count == 0, string.Join("; ", problems.Take(5)));
        foreach (var def in e.Schema.AllConstants()) Assert.True(e.Schema.HomeOf(def.Key) != null, def.Key + " has no home");
    }

    public static void A_control_sits_in_the_block_that_is_the_home_of_its_options_and_in_no_other()
    {
        var e = Make();
        var seen = new Dictionary<string, string>();
        foreach (var page in e.Schema.Pages)
            foreach (var group in page.Groups)
                foreach (var control in group.Controls)
                {
                    string where = page.Id + "/" + group.Id;
                    Assert.False(seen.ContainsKey(control.Id), "control " + control.Id + " sits in " + (seen.ContainsKey(control.Id) ? seen[control.Id] : "") + " and in " + where);
                    seen[control.Id] = where;
                    if (control.Kind == "preset") { Assert.Equal(where, e.Schema.PresetHomeOf(control.Id), "home of the preset group " + control.Id); continue; }
                    foreach (var key in control.Keys) Assert.Equal(where, e.Schema.HomeOf(key), "home of " + key);
                }
    }

    public static void Every_preset_group_has_one_home_and_shows_as_tiles_at_the_top_of_its_block()
    {
        var e = Make();
        foreach (var block in e.Shell.Pages.SelectMany(p => p.Groups))
        {
            var main = block.Main.ToList();
            int firstPlain = main.FindIndex(s => s.Kind != "preset");
            if (firstPlain < 0) firstPlain = main.Count;
            Assert.True(main.Skip(firstPlain).All(s => s.Kind != "preset"), "in " + block.Id + " the preset tiles come before the other settings");
        }
        Assert.Equal("glass", e.Shell.Block("glass").Id);
        Assert.True(e.Shell.Block("glass").Main.Any(s => s.Id == "Material") && e.Shell.Block("glass").Main.Any(s => s.Id == "Style") && e.Shell.Block("glass").Main.Any(s => s.Id == "Glass"));
        Assert.True(e.Shell.Block("density").Main.Any(s => s.Id == "Density"));
        Assert.True(e.Shell.Block("advice").Main.Any(s => s.Id == "Animations"), "the Animations preset lives with the performance advice");
        var all = e.Shell.AllSettings().Where(s => s.Kind == "preset").Select(s => s.Id).ToList();
        Assert.Equal(all.Count, all.Distinct().Count(), "no preset group is offered twice");
        foreach (var id in new[] { "ColorProfile", "Accent", "Corners" })
            Assert.False(all.Contains(id), id + " is drawn by its own editor, not as a second set of tiles");
    }

    public static void An_option_is_never_offered_twice_as_a_setting()
    {
        var e = Make();
        var keys = e.Shell.AllSettings().SelectMany(s => s.Keys).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        var owned = e.Schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Where(c => c.Owned && c.Kind != "preset").SelectMany(c => c.Keys).ToList();
        Assert.False(keys.Any(owned.Contains), "an option edited by a block's editor has no generated copy");
    }

    public static void More_holds_the_rare_options_one_link_per_block()
    {
        var e = Make();
        var banner = e.Shell.Block("banner");
        Assert.True(banner.HasExtra && banner.HasMain);
        Assert.True(banner.Extra.All(s => s.IsMore) && banner.Main.All(s => !s.IsMore));
        Assert.False(banner.ExtraShown, "closed at first");
        banner.ToggleMoreCommand.Execute(null);
        Assert.True(banner.ExtraShown);
        Assert.True(banner.MoreLabel == "Less", banner.MoreLabel);
        banner.ToggleMoreCommand.Execute(null);
        Assert.True(banner.MoreLabel.StartsWith("More ("), banner.MoreLabel);
        Assert.Equal(banner.Extra.Count, banner.Settings.Count - banner.Main.Count);
    }

    // ---- counts and resets of a block ------------------------------------------------------------------------------------------------

    public static void A_changed_setting_counts_on_its_block_and_its_page_and_the_block_reset_puts_it_back_in_one_undo_step()
    {
        var e = Make();
        var window = e.Shell.PageOf("window");
        Assert.Equal(0, window.ChangedCount);
        Setting(e, "ShowNavigationButtons").BoolValue = !Setting(e, "ShowNavigationButtons").BoolValue;
        Setting(e, "TopPanelAutoHide").BoolValue = !Setting(e, "TopPanelAutoHide").BoolValue;
        Setting(e, "SidebarAutoHide").BoolValue = !Setting(e, "SidebarAutoHide").BoolValue;
        Assert.Equal(2, e.Shell.Block("top-bar").ChangedCount);
        Assert.Equal(1, e.Shell.Block("icon-bar").ChangedCount);
        Assert.Equal(3, window.ChangedCount);
        Assert.True(e.Shell.Block("top-bar").ResetCommand.CanExecute(null));
        Assert.False(e.Shell.Block("popups").ResetCommand.CanExecute(null), "a block with no change has nothing to reset");
        e.Shell.Block("top-bar").ResetCommand.Execute(null);
        Assert.Equal(0, e.Shell.Block("top-bar").ChangedCount);
        Assert.Equal(1, e.Shell.Block("icon-bar").ChangedCount, "the other block is untouched");
        e.History.Undo();
        Assert.Equal(2, e.Shell.Block("top-bar").ChangedCount, "one undo step brings the block back");
    }

    public static void The_options_of_an_editor_count_and_reset_with_its_block()
    {
        var e = Make();
        var corners = e.Shell.EditorOf<CornersEditorViewModel>();
        corners.Items[0].Radius = 3;
        Assert.True(e.Shell.Block("corners").ChangedCount >= 1, "a corner radius counts on the Corners block");
        Assert.True(e.Shell.PageOf("sizes").ChangedCount >= 1);
        var before = e.Shell.Block("dock").ChangedCount;
        e.Shell.Block("corners").ResetCommand.Execute(null);
        Assert.Equal(0, e.Shell.Block("corners").ChangedCount);
        Assert.Equal(before, e.Shell.Block("dock").ChangedCount, "the Sizes block is untouched");
        // a preset drawn by the editor counts too
        var colors = e.Shell.EditorOf<ColorsEditorViewModel>();
        colors.SelectProfile(e.Schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).First(c => c.Id == "ColorProfile").Choices.First(c => c.Id != "Default").Id);
        Assert.True(e.Shell.Block("colors").ChangedCount >= 1);
        e.Shell.Block("colors").ResetCommand.Execute(null);
        Assert.Equal(0, e.Shell.Block("colors").ChangedCount);
    }

    public static void A_big_block_reset_asks_for_a_second_click()
    {
        var e = Make();
        e.Shell.BigResetThreshold = 2;
        var block = e.Shell.Block("top-bar");
        Setting(e, "ShowNavigationButtons").BoolValue = !Setting(e, "ShowNavigationButtons").BoolValue;
        Setting(e, "TopPanelAutoHide").BoolValue = !Setting(e, "TopPanelAutoHide").BoolValue;
        block.ResetCommand.Execute(null);
        Assert.True(block.ResetPending, "first click only asks");
        Assert.Equal("Click again to reset 2", block.ResetLabel);
        Assert.Equal(2, block.ChangedCount);
        block.ResetCommand.Execute(null);
        Assert.False(block.ResetPending);
        Assert.Equal(0, block.ChangedCount);
    }

    public static void Resetting_the_page_resets_every_block_in_one_undo_step()
    {
        var e = Make();
        Setting(e, "ShowNavigationButtons").BoolValue = !Setting(e, "ShowNavigationButtons").BoolValue;
        Setting(e, "SidebarAutoHide").BoolValue = !Setting(e, "SidebarAutoHide").BoolValue;
        e.Shell.SelectedPage = e.Shell.PageOf("window");
        e.Shell.ResetPageCommand.Execute(null);
        Assert.Equal(0, e.Shell.PageOf("window").ChangedCount);
        e.History.Undo();
        Assert.Equal(2, e.Shell.PageOf("window").ChangedCount);
    }

    // ---- search and filter -----------------------------------------------------------------------------------------------------------

    public static void Search_opens_the_page_that_has_the_setting_and_highlights_it()
    {
        var e = Make();
        e.Shell.SelectedPage = e.Shell.PageOf("sections");
        e.Shell.Search = "search field glass";
        Assert.Equal("window", e.Shell.SelectedPage.Id);
        var setting = Setting(e, "SearchBoxGlassOpacity");
        Assert.True(setting.Visible && setting.Highlighted, "the match is highlighted");
        Assert.True(e.Shell.Block("top-bar").Visible);
        Assert.False(e.Shell.Block("icon-bar").Visible, "blocks without a match step aside");
        Assert.True(e.Shell.Block("top-bar").ScrollPending, "the page scrolls to the first block that matches");
        e.Shell.Search = "";
        Assert.False(setting.Highlighted);
        Assert.True(e.Shell.PageOf("window").Groups.All(g => g.Visible));
    }

    public static void A_match_behind_more_opens_it()
    {
        var e = Make();
        e.Shell.Search = "HeroCoverMargin";
        var banner = e.Shell.Block("banner");
        Assert.Equal("gamepage", e.Shell.SelectedPage.Id);
        Assert.True(banner.Extra.Any(s => s.Id == "HeroCoverMargin" && s.Visible));
        Assert.True(banner.ExtraShown, "More opens by itself for a match");
        Assert.False(banner.MoreOpen, "without touching the user's choice");
        e.Shell.Search = "";
        Assert.False(banner.ExtraShown);
    }

    public static void Search_finds_what_an_editor_owns_by_the_title_the_user_knows()
    {
        var e = Make();
        e.Shell.Search = "Sidebar icon rounding";
        Assert.True(e.Shell.Block("corners").Visible, "the Corners block is found by the title of a corner");
        Assert.Equal("sizes", e.Shell.SelectedPage.Id);
        e.Shell.Search = "play button";
        Assert.True(e.Shell.Block("dock").Visible, "the Sizes block is found through the Play button");
        e.Shell.Search = "genres";
        Assert.False(e.Shell.PageOf("items").Dimmed, "the Items page matches genres");
        e.Shell.Search = "howlongtobeat";
        Assert.True(e.Shell.Block("addon-hltb").Visible, "an add-on is found by its name");
    }

    public static void Changed_only_keeps_the_blocks_that_have_a_change_and_opens_more_for_them()
    {
        var e = Make();
        Setting(e, "HeroCoverMargin").Text = "1,2,3,4";
        e.Shell.ShowChangedOnly = true;
        Assert.True(e.Shell.Block("banner").Visible && e.Shell.Block("banner").ExtraShown, "a changed option behind More shows");
        Assert.False(e.Shell.Block("top-bar").Visible, "a block with no change steps aside");
        Assert.True(e.Shell.PageOf("gamepage").Dimmed == false);
        e.Shell.ShowChangedOnly = false;
        Assert.True(e.Shell.Block("top-bar").Visible);
    }

    public static void Searching_the_name_of_any_option_finds_the_page_and_block_that_is_its_home()
    {
        var e = Make();
        var missed = new List<string>();
        foreach (var def in e.Schema.AllConstants())
        {
            e.Shell.Search = def.Key;
            string[] home = e.Schema.HomeOf(def.Key).Split('/');
            var page = e.Shell.PageOf(home[0]);
            var block = page.Groups.FirstOrDefault(g => g.Id == home[1]);
            if (block == null || page.Dimmed || !block.Visible) missed.Add(def.Key + " (" + string.Join("/", home) + ")");
        }
        e.Shell.Search = "";
        Assert.True(missed.Count == 0, "not found where they live: " + string.Join(", ", missed.Take(12)) + (missed.Count > 12 ? " ... " + missed.Count : ""));
    }

    // ---- links and older ids ----------------------------------------------------------------------------------------------------------

    public static void A_see_also_link_opens_the_other_place_and_never_holds_a_second_copy()
    {
        var e = Make();
        var link = e.Shell.Block("top-bar").SeeAlso.First(l => l.Target.StartsWith("Sizes"));
        link.OpenCommand.Execute(null);
        Assert.Equal("sizes", e.Shell.SelectedPage.Id);
        Assert.True(e.Shell.Block("dock").ScrollPending, "the block comes into view");
        foreach (var block in e.Shell.Pages.SelectMany(p => p.Groups))
            foreach (var l in block.SeeAlso) Assert.True(l.OpenCommand != null && !string.IsNullOrEmpty(l.Text) && !string.IsNullOrEmpty(l.Target), block.Id + " link");
    }

    public static void The_page_ids_the_theme_and_the_advice_texts_use_still_open_the_right_place()
    {
        var e = Make();
        e.Shell.OpenPage("visual-sections");
        Assert.Equal("sections", e.Shell.SelectedPage.Id);
        e.Shell.OpenPage("header");
        Assert.Equal("gamepage", e.Shell.SelectedPage.Id);
        Assert.True(e.Shell.Block("banner").ScrollPending);
        e.Shell.OpenPage("visual-sizes");
        Assert.Equal("sizes", e.Shell.SelectedPage.Id);
        e.Shell.OpenPage("visual-background");
        Assert.Equal("window", e.Shell.SelectedPage.Id);
        e.Shell.OpenPage("window/top-bar");
        Assert.True(e.Shell.Block("top-bar").ScrollPending);
        e.Shell.OpenPage("advanced");
        Assert.Equal("addons", e.Shell.SelectedPage.Id);
        e.Shell.OpenPage("no-such-page");
        Assert.Equal("addons", e.Shell.SelectedPage.Id, "an unknown id changes nothing");
        foreach (var alias in new[] { "visual-sections", "visual-items", "visual-sizes", "visual-background", "visual-tiles", "visual-colors", "visual-corners", "visual-rules", "advice", "diagnostics", "profile", "appearance", "background", "bars", "header", "details", "glass", "advanced" })
            Assert.True(e.Schema.ResolveAlias(alias) != null, alias + " has a place");
    }

    public static void The_places_the_advice_texts_link_to_exist()
    {
        var e = Make();
        foreach (var pair in new[] { new[] { "performance/advice", "performance" }, new[] { "items", "items" }, new[] { "window/glass", "window" }, new[] { "window/window-background", "window" } })
        {
            e.Shell.SelectedPage = e.Shell.PageOf("profile");
            e.Shell.OpenPage(pair[0]);
            Assert.Equal(pair[1], e.Shell.SelectedPage.Id, pair[0]);
        }
    }

    public static void A_section_opens_its_item_card_alone_through_the_new_items_page()
    {
        var e = Make();
        e.Shell.EditorOf<SectionsEditorViewModel>().Left.Concat(e.Shell.EditorOf<SectionsEditorViewModel>().Right).Single(b => b.Id == "Genres").OptionsCommand.Execute(null);
        Assert.Equal("items", e.Shell.SelectedPage.Id);
        Assert.Equal("Genres", e.Shell.EditorOf<ItemsEditorViewModel>().FocusedId);
    }

    // ---- the Add-ons page ------------------------------------------------------------------------------------------------------------

    public static void The_add_ons_detection_reads_words_of_the_installed_ids()
    {
        var list = new List<InstalledAddon>
        {
            new InstalledAddon("playnite-howlongtobeat-plugin", true), new InstalledAddon("PlayniteAchievements", false), new InstalledAddon("Some.Other.Addon", true)
        };
        string id;
        var hltb = new AddonDef { Match = { "howlongtobeat" } };
        Assert.Equal(AddonState.Installed, AddonInventory.Detect(hltb, list, out id));
        Assert.Equal("playnite-howlongtobeat-plugin", id);
        Assert.Equal(AddonState.Disabled, AddonInventory.Detect(new AddonDef { Match = { "achievements" } }, list, out id));
        Assert.Equal(AddonState.Missing, AddonInventory.Detect(new AddonDef { Match = { "gameactivity" } }, list, out id));
        Assert.Equal(AddonState.Unknown, AddonInventory.Detect(hltb, null, out id), "no list: the editor says it cannot tell");
        Assert.Equal(AddonState.Missing, AddonInventory.Detect(new AddonDef(), list, out id), "an add-on with no words is never found");
        var folders = new List<InstalledAddon> { new InstalledAddon("Review_Viewer_ca24e37a-76d9-49bf-89ab-d3cba4a54bd1", true), new InstalledAddon("felixkmh_Extras_Plugin", true) };
        Assert.Equal(AddonState.Installed, AddonInventory.Detect(new AddonDef { Match = { "reviewviewer", "steamreviews" } }, folders, out id), "underscores and dashes do not matter");
        Assert.Equal(AddonState.Installed, AddonInventory.Detect(new AddonDef { Match = { "extras_plugin" } }, folders, out id));
    }

    public static void Every_add_on_block_says_whether_it_is_installed_what_it_provides_and_where_to_see_it()
    {
        var list = new List<InstalledAddon> { new InstalledAddon("playnite-howlongtobeat-plugin", true), new InstalledAddon("PlayniteAchievements", false) };
        var e = Make(list);
        var page = e.Shell.PageOf("addons");
        Assert.True(page.Groups.Count >= 15, "one block per add-on the theme supports: " + page.Groups.Count);
        Assert.True(page.Groups.All(g => g.HasAddon && !string.IsNullOrEmpty(g.Addon.Provides) && !string.IsNullOrEmpty(g.Addon.StatusText)), "no add-on block is empty");
        Assert.True(e.Shell.Block("addon-hltb").Addon.IsInstalled);
        Assert.True(e.Shell.Block("addon-achievements").Addon.IsDisabled);
        Assert.True(e.Shell.Block("addon-gameactivity").Addon.IsMissing);
        var hltb = e.Shell.Block("addon-hltb").Addon;
        Assert.True(hltb.HasItems && hltb.Items[0].Where.Contains("chip") && hltb.Items[0].OpenCommand != null, "it lists the card it feeds, with a link to it");
        hltb.Items[0].OpenCommand.Execute(null);
        Assert.Equal("items", e.Shell.SelectedPage.Id);
        Assert.Equal("Hltb", e.Shell.EditorOf<ItemsEditorViewModel>().FocusedId);
        Assert.True(e.Shell.Block("addon-achievements").Settings.Count >= 20, "the add-on's own options sit in its block");
        Assert.True(e.Shell.Block("addon-hltb").Settings.Any(s => s.Id == "DetailsViewShowHltbDataGrid"));
        Assert.Equal(AddonState.Unknown, Make().Shell.Block("addon-hltb").Addon.State, "without the list of add-ons the state is unknown, not 'missing'");
        Assert.True(Make().Shell.Block("addon-sounds").Addon.Provides.Contains("informational"), "informational add-ons say so");
    }

    // ---- the sidebar -------------------------------------------------------------------------------------------------------------------

    public static void The_current_page_lists_its_blocks_in_the_sidebar_and_a_block_entry_scrolls_to_it()
    {
        var e = Make();
        Assert.True(e.Shell.PageOf("window").IsCurrent);
        e.Shell.SelectedPage = e.Shell.PageOf("gamepage");
        Assert.False(e.Shell.PageOf("window").IsCurrent);
        Assert.True(e.Shell.PageOf("gamepage").IsCurrent);
        bool scrolled = false;
        var links = e.Shell.Block("links");
        links.ScrollRequested += () => scrolled = true;
        links.NavigateCommand.Execute(null);
        Assert.True(scrolled && links.ScrollPending);
    }

    public static void The_sidebar_shows_the_number_of_options_of_each_page_and_the_numbers_add_up()
    {
        var e = Make();
        int total = e.Shell.Pages.Sum(p => p.OptionCount);
        Assert.Equal(e.Schema.AllConstants().Count(), total);
        Assert.True(e.Shell.PageOf("items").OptionCount > 100, "the Items page carries the chips, cards and sections options");
        Assert.True(e.Shell.PageOf("gamepage").OptionCount > 50);
    }

    public static void Titles_come_from_the_language_file_when_it_has_them()
    {
        var e = Make(null, k => k == "LOCLS_Nav_window" ? "Fenetre et panneaux" : k == "LOCLS_Block_topbar" ? "Barre du haut" : null);
        Assert.Equal("Fenetre et panneaux", e.Shell.PageOf("window").Title);
        Assert.Equal("Barre du haut", e.Shell.Block("top-bar").Title);
        Assert.Equal("Left icon bar", e.Shell.Block("icon-bar").Title, "the English name is the fallback");
    }

    public static void The_layout_file_and_the_language_files_know_the_same_pages_and_blocks()
    {
        var e = Make();
        string dir = Path.Combine(TestPaths.RepoRoot(), @"layout-studio\src\Localization");
        foreach (var file in new[] { "en_US.xaml", "fr_FR.xaml" })
        {
            string text = File.ReadAllText(Path.Combine(dir, file));
            foreach (var page in e.Schema.Pages)
            {
                Assert.True(text.Contains("x:Key=\"LOCLS_Nav_" + page.Id + "\""), file + " lacks the title of page " + page.Id);
                Assert.True(text.Contains("x:Key=\"LOCLS_NavSummary_" + page.Id + "\""), file + " lacks the summary of page " + page.Id);
                foreach (var group in page.Groups)
                {
                    Assert.True(text.Contains("x:Key=\"" + ShellViewModel.BlockKey(group.Id) + "\""), file + " lacks the title of block " + group.Id);
                    foreach (var link in group.SeeAlso)
                        Assert.True(text.Contains("x:Key=\"" + ShellViewModel.SeeAlsoKey(new SeeAlso { Page = link.Page, Block = link.Block, Text = link.Text }) + "\""), file + " lacks a see-also text of " + group.Id + ": " + link.Text);
                    if (group.Addon != null)
                        Assert.True(text.Contains("x:Key=\"LOCLS_AddonProvides_" + group.Addon.Id + "\""), file + " lacks what " + group.Addon.Id + " provides");
                }
            }
        }
    }
}
