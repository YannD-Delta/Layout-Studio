using System.Linq;
using LayoutStudio.Core;

public static class RecommendationsTests
{
    public static void Installed_add_ons_are_not_recommended_again()
    {
        var all = Recommendations.For(true).Select(r => r.Id).ToList();
        Assert.Equal(0, Recommendations.Missing(all, true).Count);
        var missing = Recommendations.Missing(all.Skip(1), true);
        Assert.Equal(1, missing.Count);
        Assert.Equal(all[0], missing[0].Id);
    }

    public static void Ids_are_compared_without_case()
    {
        var first = Recommendations.For(true).First().Id;
        Assert.False(Recommendations.Missing(new[] { first.ToUpperInvariant() }, true).Any(r => r.Id == first));
    }

    public static void Desktop_and_fullscreen_have_their_own_lists()
    {
        var desktop = Recommendations.For(true).Select(r => r.Id).ToList();
        var fullscreen = Recommendations.For(false).Select(r => r.Id).ToList();
        Assert.True(desktop.Contains("playnite-howlongtobeat-plugin"), "the desktop theme has a HowLongToBeat section");
        foreach (var id in new[] { "NowPlaying_db4e7ade-57fb-426c-8392-60e2347a0209", "ControllerSessionManager_6f3e7a21-98f4-4f2b-92ad-3fc0e6e941dc", "PlayniteAudioSwitcher_708b6ec4-bf96-4c0d-bd9d-fe0aa04d6bf1", "MoData_5a0a672a-998a-4cf3-94ea-44543de5afcb" })
            Assert.True(fullscreen.Contains(id), "the fullscreen top bar shows data of " + id);
        Assert.True(fullscreen.Contains("UniPlaySong.a1b2c3d4-e5f6-7890-abcd-ef1234567890"), "the fullscreen home plays game music through UniPlaySong");
    }

    public static void Every_recommendation_has_a_name_and_a_reason_and_none_is_layout_studio_itself()
    {
        foreach (var r in Recommendations.For(true).Concat(Recommendations.For(false)))
        {
            Assert.True(!string.IsNullOrEmpty(r.Name) && !string.IsNullOrEmpty(r.Why), r.Id);
            Assert.False(r.Id.StartsWith("LayoutStudio"), "it is already installed when this runs");
        }
    }

    public static void The_message_names_every_missing_add_on_and_says_none_is_required()
    {
        var text = Recommendations.Message(Recommendations.Missing(new string[0], true));
        foreach (var r in Recommendations.For(true)) Assert.True(text.Contains(r.Name), r.Name);
        Assert.True(text.Contains("optional"), text);
        Assert.Equal("", Recommendations.Message(new Recommendation[0]));
    }

    public static void The_signature_changes_when_the_missing_list_changes()
    {
        var all = Recommendations.Missing(new string[0], true);
        var fewer = Recommendations.Missing(new[] { Recommendations.For(true).First().Id }, true);
        Assert.False(Recommendations.Signature(all) == Recommendations.Signature(fewer), "a new list is shown again");
        Assert.Equal(Recommendations.Signature(all), Recommendations.Signature(Recommendations.Missing(new string[0], true)));
    }

    public static void The_message_can_be_french_and_still_names_every_add_on()
    {
        var text = Recommendations.Message(Recommendations.Missing(new string[0], true), true);
        Assert.True(text.StartsWith("Modula fonctionne sans eux"), text);
        foreach (var r in Recommendations.For(true)) Assert.True(text.Contains(r.Name) && !string.IsNullOrEmpty(r.WhyFr), r.Name);
    }
}
