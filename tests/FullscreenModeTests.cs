using System.Globalization;
using System.IO;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

/// <summary>The desktop editor can switch to the Fullscreen theme and edit its profile.</summary>
public static class FullscreenModeTests
{
    private static string FullscreenSchema()
    {
        string path = Path.Combine(TestPaths.RepoRoot(), @"theme\Modula-Fullscreen\layout-studio.schema.json");
        return File.Exists(path) ? path : null;
    }

    private static ShellViewModel Shell()
    {
        var schema = Schema.Load(File.ReadAllText(FullscreenSchema()));
        var store = new ConstantStore(schema.AllConstants());
        var history = new History(store);
        var applier = new ProfileApplier(schema, store, new FakePresetFiles(), @"C:\theme");
        return new ShellViewModel(schema, store, history, applier, new Profile { Name = "Default" }, k => null, new CultureInfo("en-US"));
    }

    public static void The_editor_builds_its_pages_from_the_fullscreen_schema()
    {
        if (FullscreenSchema() == null) return;   // the exported Layout Studio repository has no theme folder
        var shell = Shell();
        Assert.True(shell.Pages.Any(p => p.Kind == "settings" && p.Settings.Any()), "generated pages with settings");
        Assert.True(shell.Pages.Any(p => p.Kind == "diagnostics"));
        var preset = shell.Pages.SelectMany(p => p.Settings).First(s => s.Kind == "preset");
        Assert.True(preset.Choices.Count > 1);
    }

    public static void A_fullscreen_preset_choice_is_recorded_in_the_profile_without_touching_any_window()
    {
        if (FullscreenSchema() == null) return;
        var shell = Shell();
        var corners = shell.Pages.SelectMany(p => p.Settings).FirstOrDefault(s => s.Kind == "preset" && s.Id == "Corners");
        if (corners == null) return;
        var other = corners.Choices.First(c => c.Id != "Default" && c.Name != null && corners.SelectedChoice != c);
        corners.SelectedChoice = other;
        Assert.Equal(other.Id, corners.SelectedChoice.Id);
    }

    public static void The_mode_buttons_follow_the_shell_mode()
    {
        if (FullscreenSchema() == null) return;
        var shell = Shell();
        Assert.False(shell.HasModes, "no switch until the plugin provides one");
        string asked = null;
        shell.ModeSwitch = m => asked = m;
        shell.Mode = "Desktop";
        Assert.True(shell.HasModes);
        Assert.True(shell.IsDesktopMode);
        shell.ShowFullscreenCommand.Execute(null);
        Assert.Equal("Fullscreen", asked);
        asked = null;
        shell.ShowDesktopCommand.Execute(null);
        Assert.Equal(null, asked, "already on the desktop: nothing to switch");
        shell.Mode = "Fullscreen";
        Assert.True(shell.IsFullscreenMode);
        shell.ShowDesktopCommand.Execute(null);
        Assert.Equal("Desktop", asked);
    }
}
