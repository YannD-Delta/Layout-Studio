using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;
using LayoutStudio.Core;
using LayoutStudio.Editor;

public static class FactsViewModelTests
{
    private static GameFacts Facts(double fps, double cpu, double gpu, double ram)
    {
        var f = new GameFacts();
        if (fps > 0) f.Fps = MetricSummary.From(new List<double> { fps, fps, fps });
        if (cpu > 0) f.Cpu = MetricSummary.From(new List<double> { cpu });
        if (gpu > 0) f.Gpu = MetricSummary.From(new List<double> { gpu });
        if (ram > 0) f.Ram = MetricSummary.From(new List<double> { ram });
        return f;
    }

    private static Color ColorOf(Brush b) { return ((SolidColorBrush)b).Color; }

    public static void Median_fps_is_shown_as_a_whole_number_with_the_matching_colour()
    {
        var vm = new FactsViewModel(new FactsSettings());
        vm.Update(Facts(72.4, 0, 0, 0));
        Assert.Equal("72", vm.MedianFpsText);
        Assert.Equal(ColorLevel.Orange, vm.FpsLevel);
        Assert.Equal(FactsSettings.DefaultOrange, ColorOf(vm.FpsBrush));
        vm.Update(Facts(155, 0, 0, 0));
        Assert.Equal(ColorLevel.Blue, vm.FpsLevel);
        vm.Update(Facts(30, 0, 0, 0));
        Assert.Equal(ColorLevel.Red, vm.FpsLevel);
        vm.Update(Facts(130, 0, 0, 0));
        Assert.Equal(ColorLevel.Green, vm.FpsLevel);
    }

    public static void Load_values_get_a_percent_sign_and_the_inverse_colours()
    {
        var vm = new FactsViewModel(new FactsSettings());
        vm.Update(Facts(60, 19, 95, 61));
        Assert.Equal("19 %", vm.CpuText);
        Assert.Equal(ColorLevel.Green, vm.CpuLevel);
        Assert.Equal(ColorLevel.None, vm.GpuLevel, "a busy GPU is good news in a game");
        vm.Update(Facts(60, 19, 97, 61));
        Assert.Equal(ColorLevel.Orange, vm.GpuLevel);
        vm.Update(Facts(60, 19, 99, 61));
        Assert.Equal(ColorLevel.Red, vm.GpuLevel);
        Assert.Equal("61 %", vm.RamText);
        Assert.Equal(ColorLevel.Green, vm.RamLevel);
    }

    public static void Without_data_the_text_is_a_dash_and_there_is_no_colour_level()
    {
        var vm = new FactsViewModel(new FactsSettings());
        vm.Update(new GameFacts());
        Assert.Equal("—", vm.MedianFpsText);
        Assert.False(vm.HasFps);
        Assert.Equal(ColorLevel.None, vm.FpsLevel);
        Assert.False(vm.HasAny);
        vm.Update(null);
        Assert.Equal("—", vm.CpuText);
    }

    public static void Changing_the_game_raises_notifications_for_every_displayed_value()
    {
        var vm = new FactsViewModel(new FactsSettings());
        var raised = new HashSet<string>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (s, e) => raised.Add(e.PropertyName);
        vm.Update(Facts(80, 10, 20, 30));
        foreach (var name in new[] { "MedianFpsText", "FpsBrush", "CpuText", "CpuBrush", "GpuText", "RamText", "HasFps", "HasAny" })
            Assert.True(raised.Contains(name), name + " is announced");
    }

    public static void Limits_come_from_the_profile_and_survive_a_round_trip()
    {
        var editor = new Dictionary<string, string>();
        var settings = new FactsSettings { FpsRedBelow = 30, FpsOrangeUpTo = 90, FpsBlueFrom = 200 };
        settings.SaveTo(editor);
        var back = FactsSettings.From(editor);
        Assert.Equal(30.0, back.FpsRedBelow);
        Assert.Equal(90.0, back.FpsOrangeUpTo);
        Assert.Equal(200.0, back.FpsBlueFrom);
        var vm = new FactsViewModel(back);
        vm.Update(Facts(100, 0, 0, 0));
        Assert.Equal(ColorLevel.Green, vm.FpsLevel, "100 is above the new orange limit of 90");
    }

    public static void Missing_or_bad_limits_fall_back_to_the_defaults()
    {
        var editor = new Dictionary<string, string> { { "facts.fps.redBelow", "abc" } };
        var s = FactsSettings.From(editor);
        Assert.Equal(40.0, s.FpsRedBelow);
        Assert.Equal(120.0, s.FpsOrangeUpTo);
        Assert.Equal(150.0, s.FpsBlueFrom);
    }

    public static void Changing_the_limits_recolours_the_current_game()
    {
        var settings = new FactsSettings();
        var vm = new FactsViewModel(settings);
        vm.Update(Facts(100, 0, 0, 0));
        Assert.Equal(ColorLevel.Orange, vm.FpsLevel);
        settings.FpsOrangeUpTo = 80;
        vm.Refresh();
        Assert.Equal(ColorLevel.Green, vm.FpsLevel);
    }

    public static void Score_brushes_follow_the_same_levels_as_the_desktop_glow()
    {
        var vm = new FactsViewModel(new FactsSettings());
        vm.SetScores(95, 85, 40);   // top, glow, none with the default limits (90 and 80)
        Assert.Equal(FactsSettings.DefaultBlue, ColorOf(vm.CriticScoreBrush), "a top score is blue");
        Assert.Equal(FactsSettings.DefaultGreen, ColorOf(vm.CommunityScoreBrush), "a high score is green");
        Assert.Equal(FactsSettings.DefaultNeutral, ColorOf(vm.UserScoreBrush), "other scores keep the neutral colour");
    }
}
