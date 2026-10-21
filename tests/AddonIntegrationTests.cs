using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LayoutStudio.Core;

/// <summary>
/// The add-on integrations of the Desktop theme added with the extensions round (ImageRotater, Now Playing, AnikiHelper library overview, hero trailer,
/// grid logos). The rules: a hosted control must carry a name the add-on really registers (a typo is silent in Playnite), every binding to an add-on is
/// guarded so nothing shows and no room is taken without it, and every option has English and French words.
/// The name tables below come from the add-ons' own source (AddCustomElementSupport) and public documentation, read on 2026-10-21.
/// </summary>
public static class AddonIntegrationTests
{
    private static string Theme() { return Path.Combine(TestPaths.RepoRoot(), "theme", "Modula"); }

    private static string Read(params string[] parts)
    {
        string path = Path.Combine(new[] { Theme() }.Concat(parts).ToArray());
        return File.Exists(path) ? File.ReadAllText(path) : null;   // the exported Layout Studio repository has no theme folder
    }

    // Element names an add-on registers: Playnite hosts a control only when "<SourceName>_<element>" is in this list (ControlTemplateTools.InitializePluginControls).
    private static readonly string[] ImageRotaterElements = { "Background", "Cover" };
    private static readonly string[] ExtraMetadataLoaderElements =
    {
        "VideoLoaderControl", "VideoLoaderControlAlternative", "VideoLoaderControl_Controls_Sound", "VideoLoaderControl_Controls_NoSound",
        "VideoLoaderControl_NoControls_NoSound", "VideoLoaderControl_NoControls_Sound", "LogoLoaderControl", "LogoLoaderControl_DisableOpacityAnimation"
    };

    // The public bindings of the AnikiHelper plugin the library overview reads (its wiki, "Global Library Statistics").
    private static readonly string[] AnikiHelperPublicPaths =
    {
        "TotalCount", "InstalledCount", "NotInstalledCount", "HiddenCount", "FavoriteCount", "TotalPlaytimeString", "AveragePlaytimeString",
        "ThisMonthPlayedTotalString", "ThisMonthPlayedCount", "ThisMonthTopGameName", "ThisMonthTopGamePlaytime", "ThisYearTopGameName",
        "SuggestedGameName", "SuggestedGameCoverPath", "SuggestedGameBannerText", "TopPlayed", "CompletionStates", "GameProviders", "RecentPlayed",
        "RecentAdded", "NeverPlayed", "SteamCurrentPlayersString", "SteamCurrentPlayersAvailable", "SteamUpdateTitle", "SteamUpdateDate",
        "SteamUpdateAvailable", "SteamUpdateIsNew", "SteamRecentUpdates"
    };

    // The public bindings of the Now Playing add-on (its readme and NowPlayingSettings).
    private static readonly string[] NowPlayingPublicPaths = { "IsGameRunning", "RunningGame.IconPath", "RunningGame.GameName", "SessionLength", "ReturnToGame", "CloseGame" };

    // Back To Game (its README, "Theme Integration"): the whole contract is two paths.
    private static readonly string[] BackToGamePublicPaths = { "IsRunning", "ActivateGame" };

    private static string[] ViewFiles()
    {
        return new[] { "DetailsViewGameOverview.xaml", "GridViewGameOverview.xaml" };
    }

    public static void Every_hosted_control_of_a_known_add_name_is_a_registered_element()
    {
        if (Theme() == null || !Directory.Exists(Theme())) return;
        foreach (string file in ViewFiles())
        {
            string text = Read("Views", file);
            if (text == null) return;
            foreach (Match m in Regex.Matches(text, "x:Name=\"ImageRotater_(\\w+)\""))
                Assert.True(ImageRotaterElements.Contains(m.Groups[1].Value), file + ": ImageRotater_" + m.Groups[1].Value + " is not an element ImageRotater registers");
            foreach (Match m in Regex.Matches(text, "x:Name=\"ExtraMetadataLoader_(\\w+)\""))
            {
                if (m.Groups[1].Value == "LogoLoaderControlGrid") continue;   // a theme-side wrapper name of other themes, never hosted here
                Assert.True(ExtraMetadataLoaderElements.Contains(m.Groups[1].Value), file + ": ExtraMetadataLoader_" + m.Groups[1].Value + " is not an element Extra Metadata Loader registers");
            }
        }
    }

    public static void The_views_never_host_the_inert_logo_wrapper_name_as_a_plugin_control()
    {
        foreach (string file in ViewFiles())
        {
            string text = Read("Views", file);
            if (text == null) return;
            Assert.False(text.Contains("ExtraMetadataLoader_LogoLoaderControlGrid"), file + " uses a name Extra Metadata Loader does not register (it would host nothing)");
        }
    }

    public static void ImageRotater_has_one_background_and_one_cover_in_the_details_view_and_a_background_in_the_grid_view()
    {
        string details = Read("Views", "DetailsViewGameOverview.xaml");
        string grid = Read("Views", "GridViewGameOverview.xaml");
        if (details == null || grid == null) return;
        Assert.Equal(1, Regex.Matches(details, "x:Name=\"ImageRotater_Background\"").Count, "details background host");
        Assert.Equal(1, Regex.Matches(details, "x:Name=\"ImageRotater_Cover\"").Count, "details cover host (the add-on registers one Cover element per view)");
        Assert.Equal(1, Regex.Matches(grid, "x:Name=\"ImageRotater_Background\"").Count, "grid background host");
        Assert.False(Regex.IsMatch(details, "x:Name=\"ImageRotater_Cover\\d"), "a numbered copy would not be recognised by the add-on");
    }

    public static void ImageRotater_hosts_show_only_when_the_option_and_the_add_ons_own_switch_say_so()
    {
        string styles = Read("Views", "LibraryDetailsView.xaml");
        if (styles == null) return;
        foreach (string key in new[] { "ImageRotaterBackgroundHost", "ImageRotaterCoverHostGrid" })
        {
            int at = styles.IndexOf("x:Key=\"" + key + "\"");
            Assert.True(at > 0, key + " is missing");
            string block = styles.Substring(at, styles.IndexOf("</Style>", at) - at);
            Assert.True(block.Contains("Options[UseImageRotater]"), key + " does not read the option");
            Assert.True(block.Contains("Plugin=ImageRotater, Path=" + (key.StartsWith("ImageRotaterBackground") ? "EnableBackgroundImage" : "EnableCoverImage")), key + " does not read the add-on switch");
            Assert.True(block.Contains("Value=\"Collapsed\""), key + " must start collapsed");
        }
    }

    public static void The_ImageRotater_option_is_on_by_default_and_has_a_home_on_the_add_ons_page()
    {
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        var c = schema.Find("UseImageRotater");
        Assert.True(c != null, "UseImageRotater is missing");
        Assert.Equal("True", c.Default, "harmless without the add-on, so on by default");
    }

    public static void Both_top_bar_variants_place_the_Now_Playing_chip_and_the_library_overview()
    {
        foreach (string file in new[] { @"Views\TopPanel.xaml", @"Presets\TopPanel\Dune.xaml" })
        {
            string text = Read(file.Split('\\'));
            if (text == null) return;
            Assert.True(text.Contains("Style=\"{DynamicResource TopPanelNowPlayingChip}\""), file + " does not place the Now Playing chip");
            Assert.True(text.Contains("Style=\"{DynamicResource TopPanelLibraryOverview}\""), file + " does not place the library overview");
        }
    }

    private static string ChipStyles() { return Read("CustomControls", "TopPanelItem.xaml"); }

    public static void Every_binding_to_Now_Playing_is_guarded_and_uses_a_public_path()
    {
        string text = ChipStyles();
        if (text == null) return;
        var found = Regex.Matches(text, "\\{PluginSettings Plugin=NowPlaying, Path=([\\w.]+)([^}]*)\\}").Cast<Match>().ToList();
        Assert.True(found.Count >= 6, "the chip reads the add-on");
        foreach (Match m in found)
        {
            Assert.True(NowPlayingPublicPaths.Contains(m.Groups[1].Value), "NowPlaying path " + m.Groups[1].Value + " is not a documented public binding");
            Assert.True(m.Groups[2].Value.Contains("FallbackValue"), "NowPlaying " + m.Groups[1].Value + " has no FallbackValue: it would show a binding error without the add-on");
        }
    }

    public static void Every_binding_to_AnikiHelper_is_guarded_and_uses_a_public_path()
    {
        string text = ChipStyles();
        if (text == null) return;
        var found = Regex.Matches(text, "\\{PluginSettings Plugin=AnikiHelper, Path=([\\w.]+)([^}]*)\\}").Cast<Match>().ToList();
        Assert.True(found.Count >= 15, "the overview reads the plugin");
        foreach (Match m in found)
        {
            Assert.True(AnikiHelperPublicPaths.Contains(m.Groups[1].Value), "AnikiHelper path " + m.Groups[1].Value + " is not a documented public binding");
            Assert.True(m.Groups[2].Value.Contains("FallbackValue"), "AnikiHelper " + m.Groups[1].Value + " has no FallbackValue");
        }
        Assert.True(text.Contains("{PluginStatus Plugin=AnikiHelper_96a983a3-3f13-4dce-a474-4052b718bb52, Status=Installed}"), "the button checks that the plugin is installed");
        Assert.False(text.Contains("Aniki_ThemeMarker"), "the marker switches on intrusive Fullscreen systems of the plugin: Modula Desktop must not expose it");
    }

    public static void The_new_top_bar_options_default_to_harmless_values()
    {
        var schema = Load();
        Assert.Equal("True", schema.Find("TopPanelShowNowPlaying").Default, "the chip only exists while a game runs with the add-on");
        Assert.Equal("False", schema.Find("NowPlayingShowCloseButton").Default, "a game is never closed by a stray click");
        Assert.Equal("True", schema.Find("TopPanelShowLibraryOverview").Default, "the button only exists with the plugin");
    }

    private static Schema Load() { return Schema.Load(File.ReadAllText(TestPaths.SchemaPath())); }

    public static void Every_word_the_new_chips_use_exists_in_both_languages()
    {
        string chips = ChipStyles();
        string en = Read("Localization", "en_US.xaml");
        string fr = Read("Localization", "fr_FR.xaml");
        if (chips == null || en == null || fr == null) return;
        var keys = Regex.Matches(chips, "\\{DynamicResource (LOCHND(?:NowPlaying|LibraryOverview|Overview)\\w*)\\}").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.True(keys.Count >= 15, "the chips use their own words");
        foreach (string k in keys)
        {
            Assert.True(en.Contains("x:Key=\"" + k + "\""), k + " is missing in en_US");
            Assert.True(fr.Contains("x:Key=\"" + k + "\""), k + " is missing in fr_FR");
        }
        // the plugin writes the suggestion banner itself and looks its words up in the theme's language files (its wiki, "Localization Keys for Suggested Game Banner")
        foreach (string k in new[] { "SuggestBanner_SameGenre", "SuggestBanner_SimilarTags", "SuggestBanner_SameDeveloper", "SuggestBanner_SamePublisher" })
        {
            Assert.True(en.Contains("x:Key=\"" + k + "\""), k + " is missing in en_US");
            Assert.True(fr.Contains("x:Key=\"" + k + "\""), k + " is missing in fr_FR");
        }
    }

    public static void The_views_host_no_configured_video_player_next_to_the_regular_one()
    {
        // The configured players (VideoLoaderControl_NoControls_*, _Controls_*) are created with the view whatever their visibility, and every Extra Metadata Loader
        // player writes the SAME settings flags (IsVideoPlaying, NewContextVideoAvailable...) that the regular player of the Video section shows: a second one would
        // flip the play / pause button of the regular one for every user. Only the regular player and the add-on's own alternative player are hosted.
        foreach (string file in ViewFiles())
        {
            string text = Read("Views", file);
            if (text == null) return;
            foreach (Match m in Regex.Matches(text, "x:Name=\"ExtraMetadataLoader_(VideoLoaderControl\\w*)\""))
                Assert.True(m.Groups[1].Value == "VideoLoaderControl" || m.Groups[1].Value == "VideoLoaderControlAlternative", file + " hosts " + m.Groups[1].Value);
        }
    }

    public static void The_two_new_recommendations_are_in_the_desktop_list_and_in_extensions_yaml_and_never_required()
    {
        string[] ids = { "ImageRotater.72b7d457-0621-429b-8368-665bc53ff896", "NowPlaying_db4e7ade-57fb-426c-8392-60e2347a0209" };
        foreach (string id in ids)
        {
            Assert.True(Recommendations.For(true).Any(r => r.Id == id), id + " is not recommended on Desktop");
            if (id.StartsWith("ImageRotater")) Assert.False(Recommendations.For(false).Any(r => r.Id == id), id + " is a Desktop integration: not for the Fullscreen list");
        }
        string yaml = Read("extensions.yaml");
        if (yaml == null) return;
        foreach (string id in ids) Assert.True(yaml.Contains("- " + id), id + " is missing from extensions.yaml");
        Assert.False(Regex.IsMatch(yaml, "^Required:", RegexOptions.Multiline), "Required shows a blocking dialog at every start: nothing is required");
    }

    public static void The_theme_bridge_files_only_name_brushes_and_styles_the_theme_defines()
    {
        string constants = Read("Constants.xaml");
        string common = Read("Common.xaml");
        if (constants == null || common == null) return;
        foreach (string file in new[] { "AudioSwitcher", "ControllerManager" })
        {
            string json = Read(file, "theme-bridge.json");
            Assert.True(json != null, file + @"\theme-bridge.json is missing");
            var roles = Regex.Matches(json, "\"(?:Background|Gradient|Text|SecondaryText|Accent|Border|BorderEnd|Warning|TextStyle|TitleStyle|MessageStyle)\"\\s*:\\s*\"(\\w+)\"");
            Assert.True(roles.Count >= 6, file + " maps the colour roles");
            foreach (Match m in roles)
            {
                string key = m.Groups[1].Value;
                Assert.True(constants.Contains("x:Key=\"" + key + "\"") || common.Contains("x:Key=\"" + key + "\""), file + " maps a role to " + key + ", which the theme does not define");
            }
            Assert.True(json.Contains("\"Notification\""), file + " maps the notification");
        }
    }

    public static void The_Back_To_Game_fallback_uses_only_its_two_documented_paths_and_never_shows_beside_Now_Playing()
    {
        string text = ChipStyles();
        if (text == null) return;
        var found = Regex.Matches(text, "\\{PluginSettings Plugin=BackToGame, Path=([\\w.]+)([^}]*)\\}").Cast<Match>().ToList();
        Assert.True(found.Count >= 2, "the fallback reads the add-on");
        foreach (Match m in found)
        {
            Assert.True(BackToGamePublicPaths.Contains(m.Groups[1].Value), "BackToGame path " + m.Groups[1].Value + " is not documented");
            Assert.True(m.Groups[2].Value.Contains("FallbackValue"), "BackToGame " + m.Groups[1].Value + " has no FallbackValue");
        }
        int at = text.IndexOf("Plugin=BackToGame, Path=IsRunning");
        string trigger = text.Substring(at, 400);
        Assert.True(trigger.Contains("Plugin=NowPlaying, Path=IsGameRunning") && trigger.Contains("Value=\"False\""), "the fallback hides while Now Playing shows its chip");
    }
}
