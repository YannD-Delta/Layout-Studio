using LayoutStudio.Core;

public static class ThemeGuardTests
{
    private const string Theme = "42f3aebe-d455-44a7-bd7f-2dd8afaec9a6";

    public static void Ok_for_helium_neon_dune()
    {
        Assert.Equal(GuardResult.Ok, ThemeGuard.Evaluate(Theme, new[] { "Steam_Builtin", "LayoutStudio_x" }));
    }

    public static void Wrong_theme_does_nothing()
    {
        Assert.Equal(GuardResult.WrongTheme, ThemeGuard.Evaluate("Playnite_builtin_DefaultDesktop", new string[0]));
        Assert.Equal(GuardResult.WrongTheme, ThemeGuard.Evaluate(null, new string[0]));
    }

    public static void ThemeId_comparison_ignores_case()
    {
        Assert.Equal(GuardResult.Ok, ThemeGuard.Evaluate(Theme.ToUpperInvariant(), new string[0]));
    }

    public static void ThemeOptions_active_warns()
    {
        Assert.Equal(GuardResult.ThemeOptionsActive, ThemeGuard.Evaluate(Theme, new[] { "ThemeOptions_904cbf3b-573f-48f8-9642-0a09d05c64ef" }));
    }

    public static void Wrong_theme_wins_over_ThemeOptions()
    {
        Assert.Equal(GuardResult.WrongTheme, ThemeGuard.Evaluate("other", new[] { "ThemeOptions_904cbf3b-573f-48f8-9642-0a09d05c64ef" }));
    }

    public static void Null_extension_list_is_fine()
    {
        Assert.Equal(GuardResult.Ok, ThemeGuard.Evaluate(Theme, null));
    }

    private const string FullscreenTheme = "f0680ff7-f5f3-4338-a545-5eb791243bb6";

    public static void The_fullscreen_theme_is_recognised_in_fullscreen_mode_only()
    {
        Assert.Equal(GuardResult.Ok, ThemeGuard.Evaluate(FullscreenTheme, new string[0], false));
        Assert.Equal(GuardResult.WrongTheme, ThemeGuard.Evaluate(FullscreenTheme, new string[0], true), "the fullscreen id means nothing in the desktop app");
        Assert.Equal(GuardResult.WrongTheme, ThemeGuard.Evaluate(Theme, new string[0], false), "the desktop id means nothing in the fullscreen app");
        Assert.Equal(GuardResult.Ok, ThemeGuard.Evaluate(Theme, new string[0], true));
    }

    public static void Each_mode_has_its_own_theme_folder_and_profile_folder()
    {
        Assert.Equal("Desktop", ThemeGuard.ModeFolder(true));
        Assert.Equal("Fullscreen", ThemeGuard.ModeFolder(false));
        Assert.Equal(Theme, ThemeGuard.IdFor(true));
        Assert.Equal(FullscreenTheme, ThemeGuard.IdFor(false));
    }
}
