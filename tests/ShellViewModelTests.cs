using System;
using System.Globalization;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

public static class ShellViewModelTests
{
    private class Env
    {
        public Schema Schema;
        public ConstantStore Store;
        public History History;
        public Profile Profile;
        public FakePresetFiles Files;
        public ShellViewModel Shell;
    }

    private static Env Make(Func<string, string> localize = null)
    {
        var e = new Env();
        e.Schema = Schema.Load(TestSchema.Json);
        e.Store = new ConstantStore(e.Schema.AllConstants());
        e.History = new History(e.Store);
        e.Profile = new Profile { Name = "Default" };
        e.Files = new FakePresetFiles();
        var applier = new ProfileApplier(e.Schema, e.Store, e.Files, @"C:\theme");
        e.Shell = new ShellViewModel(e.Schema, e.Store, e.History, applier, e.Profile, localize ?? (k => null), new CultureInfo("en-US"));
        return e;
    }

    // Visual editor pages come first in the list; these tests are about the pages generated from the schema.
    private static System.Collections.Generic.List<PageViewModel> SettingsPages(Env e)
    {
        return e.Shell.Pages.Where(p => p.Kind == "settings").ToList();
    }

    private static SettingViewModel Setting(Env e, string id)
    {
        return e.Shell.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Settings).Single(s => s.Id == id);
    }

    public static void Pages_come_from_the_schema()
    {
        var e = Make();
        Assert.Equal(2, SettingsPages(e).Count);
        Assert.Equal("Background and glass", SettingsPages(e)[0].Title);
        Assert.Equal(2, SettingsPages(e)[0].Groups.Count);
        Assert.True(ReferenceEquals(e.Shell.Pages[0], e.Shell.SelectedPage), "the first page is selected at start");
    }

    public static void Localized_titles_are_used_when_available()
    {
        var e = Make(key => key == "LOCT_Glass" ? "Fond de la fenetre" : null);
        Assert.Equal("Fond de la fenetre", Setting(e, "WindowGlassEnabled").Title);
        Assert.Equal("Window background blur", Setting(e, "WindowGlassBlurRadius").Title, "falls back to the schema title");
    }

    public static void Search_filters_settings_and_hides_empty_groups()
    {
        var e = Make();
        e.Shell.Search = "blur";
        Assert.True(Setting(e, "WindowGlassBlurRadius").Visible);
        Assert.False(Setting(e, "WindowGlassEnabled").Visible);
        Assert.True(SettingsPages(e)[0].Groups[0].Visible);
        Assert.False(SettingsPages(e)[0].Groups[1].Visible, "a group without a match is hidden");
        Assert.Equal(1, SettingsPages(e)[0].MatchCount);
        Assert.Equal(0, SettingsPages(e)[1].MatchCount);
    }

    public static void Search_is_case_insensitive_and_matches_constant_keys()
    {
        var e = Make();
        e.Shell.Search = "PAGESIDEMARGIN";
        Assert.True(Setting(e, "PageSideMargin").Visible);
        e.Shell.Search = "BLUR";
        Assert.True(Setting(e, "WindowGlassBlurRadius").Visible);
    }

    public static void Clearing_search_shows_everything()
    {
        var e = Make();
        e.Shell.Search = "blur";
        e.Shell.Search = "";
        Assert.True(e.Shell.Pages.SelectMany(p => p.Groups).All(g => g.Visible && g.Settings.All(s => s.Visible)));
    }

    public static void Undo_and_redo_commands_follow_the_history()
    {
        var e = Make();
        Assert.False(e.Shell.UndoCommand.CanExecute(null));
        Setting(e, "WindowGlassBlurRadius").Text = "10";
        Assert.True(e.Shell.UndoCommand.CanExecute(null));
        e.Shell.UndoCommand.Execute(null);
        Assert.False(e.Shell.UndoCommand.CanExecute(null));
        Assert.True(e.Shell.RedoCommand.CanExecute(null));
    }

    public static void Undo_refreshes_the_setting_view_models()
    {
        var e = Make();
        var blur = Setting(e, "WindowGlassBlurRadius");
        blur.Text = "10";
        Assert.Equal("10", blur.Text);
        e.Shell.UndoCommand.Execute(null);
        Assert.Equal("30", blur.Text, "the view model follows the store after an undo");
    }

    public static void ResetPage_restores_defaults_only_for_that_page_and_is_one_undo_step()
    {
        var e = Make();
        string error;
        e.Store.TrySet("WindowGlassBlurRadius", "5", out error);
        e.Store.TrySet("WindowGlassEnabled", "False", out error);
        e.Store.TrySet("SlotCount", "9", out error);
        e.History.Clear();
        e.Shell.SelectedPage = SettingsPages(e)[0];
        e.Shell.ResetPageCommand.Execute(null);
        Assert.Equal("30", e.Store.Get("WindowGlassBlurRadius"));
        Assert.Equal("True", e.Store.Get("WindowGlassEnabled"));
        Assert.Equal("9", e.Store.Get("SlotCount"), "the other page is untouched");
        e.Shell.UndoCommand.Execute(null);
        Assert.Equal("5", e.Store.Get("WindowGlassBlurRadius"), "one undo restores the whole page reset");
        Assert.Equal("False", e.Store.Get("WindowGlassEnabled"));
    }

    public static void Undo_command_becomes_available_after_a_batched_change()
    {
        // A preset or a slider drag changes several values inside one history batch; the step only exists once the batch ends.
        // A WPF button re-queries CanExecute at each notification, so the LAST notification must see the final state.
        var e = Make();
        var corners = Setting(e, "Corners");
        bool lastSeenUndo = false, lastSeenRedo = false;
        e.Shell.UndoCommand.CanExecuteChanged += (s, a) => lastSeenUndo = e.Shell.UndoCommand.CanExecute(null);
        e.Shell.RedoCommand.CanExecuteChanged += (s, a) => lastSeenRedo = e.Shell.RedoCommand.CanExecute(null);
        corners.SelectedChoice = corners.Choices.Single(c => c.Id == "Sharp");
        Assert.True(lastSeenUndo, "the button must end up enabled after the batch");
        e.Shell.UndoCommand.Execute(null);
        Assert.False(lastSeenUndo, "and disabled again after undoing the only step");
        Assert.True(lastSeenRedo, "while redo becomes available");
    }

    // The page content is hosted with DataContext = the PageViewModel, so the generated pages must bind to its own Groups.
    // (A binding to SelectedPage.Groups made every generated page blank in the real Playnite.)
    public static void Generated_pages_bind_to_the_groups_of_the_page_they_are_hosted_in()
    {
        string xaml = System.IO.File.ReadAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\layout-studio\src\Editor\EditorWindow.xaml"));
        int start = xaml.IndexOf("x:Key=\"Page.settings\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "Page.settings template exists");
        string template = xaml.Substring(start, xaml.IndexOf("x:Key=\"Block.section\"", start, StringComparison.Ordinal) - start);
        Assert.True(template.Contains("ItemsSource=\"{Binding Groups}\""), "binds to the hosted page's Groups");
        Assert.False(template.Contains("SelectedPage"), "SelectedPage is a property of the shell, not of the page");
        Assert.False(template.Contains("StaticResource GroupTemplate"), "GroupTemplate is defined further down: a StaticResource cannot see it");
    }
}
