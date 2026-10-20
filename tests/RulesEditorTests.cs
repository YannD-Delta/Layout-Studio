using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using LayoutStudio.Core;
using LayoutStudio.Editor;

public static class RulesEditorTests
{
    private class Env
    {
        public FactsViewModel Facts;
        public History History;
        public Dictionary<string, string> Editor = new Dictionary<string, string>();
        public RulesEditorViewModel Rules;
    }

    private static Env Make()
    {
        var e = new Env();
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        var store = new ConstantStore(schema.AllConstants());
        e.History = new History(store);
        e.Facts = new FactsViewModel(new FactsSettings());
        e.Rules = new RulesEditorViewModel(e.Facts, e.Editor, e.History);
        return e;
    }

    private static GameFacts Fps(double fps)
    {
        var f = new GameFacts();
        f.Fps = MetricSummary.From(new List<double> { fps, fps, fps });
        return f;
    }

    private static RuleRowViewModel Row(Env e, string group, string title) { return e.Rules.Rows.First(r => r.Group == group && r.Title == title); }

    public static void Changing_a_limit_recolours_the_figure_at_once_and_saves_it_in_the_profile()
    {
        var e = Make();
        e.Facts.Update(Fps(100));
        Assert.Equal(ColorLevel.Orange, e.Facts.FpsLevel);
        Row(e, "Median FPS", "Orange up to").Value = 90;
        Assert.Equal(ColorLevel.Green, e.Facts.FpsLevel, "100 FPS is above the new orange limit");
        Assert.Equal("90", e.Editor["facts.fps.orangeUpTo"]);
    }

    public static void A_change_is_one_undo_step()
    {
        var e = Make();
        e.Facts.Update(Fps(100));
        Row(e, "Median FPS", "Orange up to").Value = 90;
        e.History.Undo();
        Assert.Equal(120.0, e.Facts.Settings.FpsOrangeUpTo);
        Assert.Equal(ColorLevel.Orange, e.Facts.FpsLevel);
        Assert.Equal("120", e.Editor["facts.fps.orangeUpTo"]);
        e.History.Redo();
        Assert.Equal(90.0, e.Facts.Settings.FpsOrangeUpTo);
    }

    public static void A_limit_outside_the_slider_range_is_clamped()
    {
        var e = Make();
        Row(e, "CPU load", "Red above").Value = 500;
        Assert.Equal(100.0, e.Facts.Settings.CpuCrit);
        Row(e, "CPU load", "Red above").Value = double.NaN;
        Assert.Equal(100.0, e.Facts.Settings.CpuCrit, "NaN is ignored");
    }

    public static void A_colour_can_be_changed_and_a_bad_one_is_refused_with_a_message()
    {
        var e = Make();
        var red = e.Rules.Colors.First(c => c.Title == "Red");
        red.Hex = "#112233";
        Assert.Equal(Color.FromRgb(0x11, 0x22, 0x33), e.Facts.Settings.Red);
        Assert.Equal("#112233", e.Editor["facts.color.red"]);
        red.Hex = "banana";
        Assert.True(red.Error != null, "an error message is shown");
        Assert.Equal(Color.FromRgb(0x11, 0x22, 0x33), e.Facts.Settings.Red, "the colour is unchanged");
        e.Facts.Update(Fps(10));
        Assert.Equal(Color.FromRgb(0x11, 0x22, 0x33), ((SolidColorBrush)e.Facts.FpsBrush).Color, "the theme uses the new red");
    }

    public static void Limits_and_colours_survive_a_restart_through_the_profile()
    {
        var e = Make();
        Row(e, "RAM load", "Orange above").Value = 70;
        e.Rules.Colors.First(c => c.Title == "Blue").Hex = "#00FFAA";
        var again = FactsSettings.From(e.Editor);
        Assert.Equal(70.0, again.RamWarn);
        Assert.Equal(Color.FromRgb(0x00, 0xFF, 0xAA), again.Blue);
    }

    public static void Scores_glow_from_the_glow_limit_and_pulse_from_the_top_limit()
    {
        var e = Make();
        e.Facts.SetScores(79, 86, 95);
        Assert.Equal("None", e.Facts.CriticScoreLevel);
        Assert.Equal("Glow", e.Facts.CommunityScoreLevel);
        Assert.Equal("Top", e.Facts.UserScoreLevel);
        Row(e, "Scores", "Glow from").Value = 70;
        Assert.Equal("Glow", e.Facts.CriticScoreLevel, "the limit moved with the slider");
        e.Facts.SetScores(null, null, null);
        Assert.Equal("None", e.Facts.UserScoreLevel, "no score, no glow");
    }

    public static void The_shell_has_a_rules_page_hosted_by_a_template_that_exists()
    {
        var schema = Schema.Load(TestSchema.Json);
        var store = new ConstantStore(schema.AllConstants());
        var history = new History(store);
        var profile = new Profile { Name = "Default" };
        var applier = new ProfileApplier(schema, store, new FakePresetFiles(), @"C:\theme");
        var shell = new ShellViewModel(schema, store, history, applier, profile, k => null, new System.Globalization.CultureInfo("en-US"), null, null, null, null, null, new FactsViewModel(new FactsSettings()));
        var page = shell.Pages.First(p => p.Kind == "rules");
        Assert.True(page.Editor is RulesEditorViewModel, "the page edits the rules");
        string xaml = File.ReadAllText(Path.Combine(TestPaths.RepoRoot(), @"layout-studio\src\Editor\EditorWindow.xaml"));
        Assert.True(xaml.Contains("x:Key=\"Page.rules\""), "Page.rules template exists");
    }

    public static void A_slow_slider_drag_on_a_limit_still_moves_it()
    {
        var e = Make();
        var row = Row(e, "Median FPS", "Orange up to");
        double start = row.Value;
        for (int i = 0; i < 20; i++) row.SliderValue = row.SliderValue + 0.2;   // the slider adds a fraction of a unit to the value it was given back
        Assert.True(row.Value >= start + 3, "20 x 0.2 = +4: " + start + " -> " + row.Value);
    }

    public static void The_rules_page_is_called_Thresholds_and_follows_the_language()
    {
        var schema = Schema.Load(TestSchema.Json);
        var store = new ConstantStore(schema.AllConstants());
        Func<System.Func<string, string>, ShellViewModel> make = loc => new ShellViewModel(schema, store, new History(store), new ProfileApplier(schema, store, new FakePresetFiles(), @"C:\theme"),
            new Profile { Name = "Default" }, loc, new System.Globalization.CultureInfo("en-US"), null, null, null, null, null, new FactsViewModel(new FactsSettings()));
        Assert.Equal("Thresholds", make(k => null).Pages.First(p => p.Kind == "rules").Title);
        Assert.Equal("Seuils", make(k => k == "LOCLS_Page_Thresholds" ? "Seuils" : null).Pages.First(p => p.Kind == "rules").Title);
        Assert.True(make(k => null).Pages.First(p => p.Kind == "rules").Keywords.Contains("thresholds"), "the search finds it by its new name");
    }

    public static void Reset_puts_the_defaults_back_and_keeps_the_cut_off_date()
    {
        var e = Make();
        e.Facts.Settings.Since = new System.DateTime(2026, 10, 19, 0, 0, 0, System.DateTimeKind.Utc);
        Row(e, "GPU load", "Red above").Value = 90;
        e.Rules.Colors.First(c => c.Title == "Red").Hex = "#010203";
        e.Rules.ResetCommand.Execute(null);
        Assert.Equal(98.0, e.Facts.Settings.GpuCrit);
        Assert.Equal(FactsSettings.DefaultRed, e.Facts.Settings.Red);
        Assert.True(e.Facts.Settings.Since.HasValue, "the cut-off date is not a rule");
    }
}
