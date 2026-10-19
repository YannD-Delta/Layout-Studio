using System.Collections.Generic;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

public class FakeSettings : IPlayniteSettingsAccess
{
    public Dictionary<string, object> Values = new Dictionary<string, object>
    {
        { "BlurWindowBackgroundImage", true }, { "BackgroundImageBlurAmount", 39.0 },
        { "DarkenWindowBackgroundImage", true }, { "BackgroundImageDarkAmount", 0.85 },
        { "HighQualityBackgroundBlur", false }, { "BackgroundImageAnimation", false }, { "ShowBackgroundImageOnWindow", true }
    };
    public List<string> Writes = new List<string>();
    public bool Refuse;

    public IDictionary<string, object> Read() { return new Dictionary<string, object>(Values); }

    public bool TrySet(string name, object value, out string error)
    {
        error = null;
        if (Refuse) { error = "read only"; return false; }
        Writes.Add(name + "=" + value);
        Values[name] = value;
        return true;
    }
}

public static class PlayniteSettingsTests
{
    private static IDictionary<string, object> S(bool blur, double blurAmount, bool dark, double darkAmount, bool hq = false, bool anim = false, bool show = true)
    {
        return new Dictionary<string, object>
        {
            { "BlurWindowBackgroundImage", blur }, { "BackgroundImageBlurAmount", blurAmount },
            { "DarkenWindowBackgroundImage", dark }, { "BackgroundImageDarkAmount", darkAmount },
            { "HighQualityBackgroundBlur", hq }, { "BackgroundImageAnimation", anim }, { "ShowBackgroundImageOnWindow", show }
        };
    }

    // ---- the checks ---------------------------------------------------------------------------------------------

    public static void Blur_and_darken_of_the_window_background_are_one_conflict_with_the_amounts()
    {
        var found = AppearanceCheck.Evaluate(S(true, 39, true, 0.8473684));
        var f = found.Single(x => x.Id == "windowBackground");
        Assert.True(f.Message.Contains("39") && f.Message.Contains("0.85"), f.Message);
        Assert.False(f.IsPerformance);
        Assert.True(f.Changes.Any(c => c.Key == "BackgroundImageBlurAmount" && (double)c.Value == 0.0));
        Assert.True(f.Changes.Any(c => c.Key == "BackgroundImageDarkAmount" && (double)c.Value == 0.0));
    }

    // Measured on the testbed with a game whose artwork differs from the page colours (build\matrix, tools	estbed-appearance-matrix.ps1):
    // an option that is off changes nothing on screen, the same as on with an amount of 0.
    public static void An_option_that_is_off_applies_nothing_so_it_raises_nothing()
    {
        Assert.False(AppearanceCheck.Evaluate(S(false, 39, false, 0.85)).Any(x => x.Id == "windowBackground"));
    }

    public static void Only_the_option_that_is_on_is_named()
    {
        var f = AppearanceCheck.Evaluate(S(true, 20, true, 0)).Single(x => x.Id == "windowBackground");
        Assert.True(f.Message.Contains("blur") && !f.Message.Contains("darkening"), f.Message);
        Assert.Equal(1, f.Changes.Count, "only the blur amount goes back to 0");
    }

    public static void Options_that_are_off_or_zero_raise_nothing()
    {
        Assert.False(AppearanceCheck.Evaluate(S(true, 0, true, 0)).Any(x => x.Id == "windowBackground"), "on with 0 is the clean state");
        Assert.False(AppearanceCheck.Evaluate(S(true, 39, true, 0.8, show: false)).Any(x => x.Id == "windowBackground"), "the artwork is not drawn at all: its blur and darkening do not matter");
    }

    // Turning "show the background image on the window" off does not remove a second copy: it removes the only image the theme's glass is fed with.
    public static void The_window_background_option_off_is_reported_while_the_theme_glass_needs_it()
    {
        var f = AppearanceCheck.Evaluate(S(true, 0, true, 0, show: false)).Single(x => x.Id == "windowBackgroundOff");
        Assert.True(f.Changes.Any(c => c.Key == "ShowBackgroundImageOnWindow" && (bool)c.Value == true), "the fix turns it ON");
        Assert.False(AppearanceCheck.Evaluate(S(true, 0, true, 0, show: false), false).Any(x => x.Id == "windowBackgroundOff"), "the theme glass is off: nothing is lost");
    }

    public static void The_option_that_is_on_is_never_asked_to_be_turned_off()
    {
        var found = AppearanceCheck.Evaluate(S(true, 39, true, 0.85));
        Assert.False(found.Any(f => f.Changes.Any(c => c.Key == "ShowBackgroundImageOnWindow")), "turning it off empties the window background");
    }

    public static void Options_without_a_measured_effect_raise_nothing()
    {
        Assert.Equal(0, AppearanceCheck.Evaluate(S(true, 0, true, 0, hq: true, anim: true)).Count, "high quality blur and the animation are not judged");
    }

    public static void Missing_keys_raise_nothing()
    {
        Assert.Equal(0, AppearanceCheck.Evaluate(new Dictionary<string, object>()).Count);
        Assert.Equal(0, AppearanceCheck.Evaluate(null).Count);
    }

    // ---- the page of buttons ----------------------------------------------------------------------------------

    public static void The_view_model_lists_the_findings_of_the_live_settings()
    {
        var vm = new PlayniteSettingsViewModel(new FakeSettings());
        Assert.Equal(1, vm.Findings.Count);   // the blur and darkening Playnite applies to the artwork before the theme
        Assert.True(vm.HasFindings);
    }

    public static void Fixing_a_finding_writes_the_options_off_and_confirms_by_reading_back()
    {
        var fake = new FakeSettings();
        var vm = new PlayniteSettingsViewModel(fake);
        vm.Findings[0].FixCommand.Execute(null);
        Assert.True(fake.Writes.Contains("BackgroundImageBlurAmount=0"));
        Assert.True(fake.Writes.Contains("BackgroundImageDarkAmount=0"));
        Assert.False(vm.Findings.Any(x => x.Id == "windowBackground"), "read back: the blur and darkening are gone");
        Assert.False(vm.IsError);
        Assert.True(vm.Status.Length > 5);
    }

    public static void Fix_all_handles_every_finding_at_once()
    {
        var fake = new FakeSettings();
        var vm = new PlayniteSettingsViewModel(fake);
        Assert.Equal(1, vm.Findings.Count);
        vm.FixAllCommand.Execute(null);
        Assert.Equal(0, vm.Findings.Count);
    }

    public static void Restore_puts_the_previous_values_back()
    {
        var fake = new FakeSettings();
        var vm = new PlayniteSettingsViewModel(fake);
        Assert.False(vm.RestoreCommand.CanExecute(null), "nothing to restore yet");
        vm.FixAllCommand.Execute(null);
        Assert.True(vm.RestoreCommand.CanExecute(null));
        vm.RestoreCommand.Execute(null);
        Assert.Equal(39.0, (double)fake.Values["BackgroundImageBlurAmount"]);
        Assert.Equal(0.85, (double)fake.Values["BackgroundImageDarkAmount"]);
        Assert.True(vm.HasFindings, "the warning is back");
    }

    public static void A_refused_write_is_reported_not_thrown()
    {
        var fake = new FakeSettings { Refuse = true };
        var vm = new PlayniteSettingsViewModel(fake);
        vm.FixAllCommand.Execute(null);
        Assert.True(vm.IsError);
        Assert.True(vm.HasFindings, "still on, and the user is told why it did not work");
    }

    public static void No_access_means_no_findings_and_no_exception()
    {
        var vm = new PlayniteSettingsViewModel(null);
        Assert.False(vm.HasFindings);
        Assert.False(vm.FixAllCommand.CanExecute(null));
    }

    public static void Restore_still_works_after_the_editor_was_closed()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ls-backup-" + System.Guid.NewGuid().ToString("N") + ".json");
        var fake = new FakeSettings();
        var first = new PlayniteSettingsViewModel(fake, path);
        first.FixAllCommand.Execute(null);
        Assert.Equal(0.0, System.Convert.ToDouble(fake.Values["BackgroundImageBlurAmount"]), "the fix zeroed the blur");
        Assert.True(System.IO.File.Exists(path), "the original values are on disk");

        var second = new PlayniteSettingsViewModel(fake, path);   // the editor was closed and opened again
        Assert.True(second.RestoreCommand.CanExecute(null), "the restore button is still there");
        second.RestoreCommand.Execute(null);
        Assert.Equal(39.0, System.Convert.ToDouble(fake.Values["BackgroundImageBlurAmount"]));
        Assert.Equal(0.85, System.Convert.ToDouble(fake.Values["BackgroundImageDarkAmount"]));
        Assert.True((bool)fake.Values["ShowBackgroundImageOnWindow"]);
        Assert.False(System.IO.File.Exists(path), "nothing left to restore");
    }

    public static void A_damaged_backup_file_is_ignored()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ls-backup-" + System.Guid.NewGuid().ToString("N") + ".json");
        System.IO.File.WriteAllText(path, "{ not json");
        var vm = new PlayniteSettingsViewModel(new FakeSettings(), path);
        Assert.False(vm.RestoreCommand.CanExecute(null));
    }
}
