using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

public static class DiagnosticsTests
{
    private static ShellViewModel Shell(string[] copied = null, FakeSettings fake = null)
    {
        var schema = Schema.Load(TestSchema.Json);
        var store = new ConstantStore(schema.AllConstants());
        var history = new History(store);
        var applier = new ProfileApplier(schema, store, new FakePresetFiles(), @"C:\theme");
        return new ShellViewModel(schema, store, history, applier, new Profile { Name = "Night" }, k => null, new CultureInfo("en-US"), null, null,
            text => { if (copied != null) copied[0] = text; }, fake == null ? null : new PlayniteSettingsViewModel(fake), null, null,
            () => new[] { new KeyValuePair<string, string>("Playnite", "10.62 (Desktop)") });
    }

    public static void The_page_lists_environment_theme_profile_and_restart_facts()
    {
        var page = Shell().Pages.Single(p => p.Kind == "diagnostics");
        var d = (DiagnosticsViewModel)page.Editor;
        Assert.Equal("Diagnostics", page.Title);
        Assert.Equal("10.62 (Desktop)", d.Lines.Single(l => l.Title == "Playnite").Value);
        Assert.True(d.Lines.Any(l => l.Title == "Profile in use" && l.Value.StartsWith("Night")));
        Assert.True(d.Lines.Any(l => l.Title == "Schema"));
        Assert.True(d.Lines.Single(l => l.Title == "Restart").IsOk);
    }

    public static void The_facts_are_read_again_each_time_the_page_is_shown()
    {
        var shell = Shell();
        var page = shell.Pages.Single(p => p.Kind == "diagnostics");
        var d = (DiagnosticsViewModel)page.Editor;
        Assert.Equal("0 settings", d.Lines.Single(l => l.Title == "Changed from the defaults").Value);
        var setting = shell.Pages.SelectMany(p => p.Settings).Single(s => s.Id == "WindowGlassBlurRadius");
        setting.Text = "12";
        shell.SelectedPage = shell.Pages.First(p => p.Kind != "diagnostics");
        shell.SelectedPage = page;
        Assert.Equal("1 settings", d.Lines.Single(l => l.Title == "Changed from the defaults").Value);
    }

    public static void Playnite_settings_that_conflict_show_as_a_warning()
    {
        var d = (DiagnosticsViewModel)Shell(null, new FakeSettings()).Pages.Single(p => p.Kind == "diagnostics").Editor;
        var line = d.Lines.Single(l => l.Title == "Playnite settings");
        Assert.True(line.IsWarning, "the default fake has a blur and a darkening on: " + line.Value);
        Assert.True(d.WarningCount >= 1);
    }

    public static void Copy_puts_the_whole_list_as_text_on_the_clipboard()
    {
        var copied = new string[1];
        var d = (DiagnosticsViewModel)Shell(copied).Pages.Single(p => p.Kind == "diagnostics").Editor;
        d.CopyCommand.Execute(null);
        Assert.True(copied[0].Contains("Playnite: 10.62 (Desktop)"), copied[0]);
        Assert.True(copied[0].Contains("Profile in use: Night"));
        Assert.True(d.Message != null && d.Message.StartsWith("Copied"));
    }

    public static void A_failing_collector_shows_one_warning_instead_of_throwing()
    {
        var d = new DiagnosticsViewModel(() => { throw new System.InvalidOperationException("boom"); }, null);
        Assert.Equal(1, d.Lines.Count);
        Assert.True(d.Lines[0].IsWarning);
        Assert.False(d.CopyCommand.CanExecute(null), "no clipboard, no copy button");
    }

    public static void Every_page_kind_has_a_template_in_the_editor_window()
    {
        string xaml = System.IO.File.ReadAllText(System.IO.Path.Combine(TestPaths.RepoRoot(), @"layout-studio\src\Editor\EditorWindow.xaml"));
        foreach (var page in new[] { "settings", "composite", "editorHost" })
            Assert.True(xaml.Contains("x:Key=\"Page." + page + "\""), "Page." + page + " exists");
        // a specialised editor has one template, whether it fills a page or sits in a block
        foreach (var kind in new[] { "sections", "items", "sizes", "background", "tiles", "colors", "corners", "rules", "advice", "share", "diagnostics", "globalpresets" })
            Assert.True(xaml.Contains("x:Key=\"Editor." + kind + "\""), "Editor." + kind + " exists");
    }
}
