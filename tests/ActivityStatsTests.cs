using System.Linq;
using LayoutStudio.Core;

public static class ActivityStatsTests
{
    private static string Json(params string[] sessions)
    {
        return "{\"Id\":\"g\",\"Name\":\"G\",\"Items\":[" + string.Join(",", sessions) + "]}";
    }

    private static string Session(params string[] samples)
    {
        return "{\"ElapsedSeconds\":100,\"Details\":[" + string.Join(",", samples) + "]}";
    }

    private static string Sample(int fps, int cpu, int gpu, int ram)
    {
        return "{\"datelog\":\"2025-09-19T15:58:34Z\",\"fps\":" + fps + ",\"fps1l\":0,\"fps01l\":0,\"cpu\":" + cpu + ",\"gpu\":" + gpu + ",\"ram\":" + ram + ",\"cpuT\":0,\"gpuT\":0,\"cpuP\":0,\"gpuP\":0}";
    }

    public static void The_median_of_an_odd_number_of_samples_is_the_middle_one()
    {
        var f = ActivityStats.FromJson(Json(Session(Sample(30, 10, 10, 10), Sample(90, 20, 20, 20), Sample(60, 30, 30, 30))));
        Assert.Equal(60.0, f.Fps.Median);
        Assert.Equal(30.0, f.Fps.Min);
        Assert.Equal(90.0, f.Fps.Max);
        Assert.Equal(3, f.Fps.Count);
    }

    public static void The_median_of_an_even_number_is_the_mean_of_the_two_middle_ones()
    {
        var f = ActivityStats.FromJson(Json(Session(Sample(40, 1, 1, 1), Sample(60, 1, 1, 1), Sample(80, 1, 1, 1), Sample(100, 1, 1, 1))));
        Assert.Equal(70.0, f.Fps.Median);
    }

    public static void Samples_from_all_sessions_are_pooled()
    {
        var f = ActivityStats.FromJson(Json(Session(Sample(10, 5, 5, 5)), Session(Sample(50, 5, 5, 5), Sample(70, 5, 5, 5))));
        Assert.Equal(3, f.Fps.Count);
        Assert.Equal(50.0, f.Fps.Median);
    }

    public static void A_zero_fps_means_not_measured_and_is_left_out()
    {
        var f = ActivityStats.FromJson(Json(Session(Sample(0, 10, 10, 10), Sample(0, 20, 20, 20), Sample(60, 30, 30, 30))));
        Assert.Equal(1, f.Fps.Count);
        Assert.Equal(60.0, f.Fps.Median, "zeros would have dragged the median down");
        Assert.Equal(3, f.Cpu.Count, "cpu was measured in all three");
    }

    public static void Rows_where_the_logger_had_nothing_are_ignored_for_cpu_and_ram()
    {
        var f = ActivityStats.FromJson(Json(Session(Sample(60, 0, 0, 0), Sample(60, 40, 70, 50))));
        Assert.Equal(1, f.Cpu.Count);
        Assert.Equal(40.0, f.Cpu.Median);
        Assert.Equal(50.0, f.Ram.Median);
        Assert.Equal(70.0, f.Gpu.Median);
    }

    public static void No_samples_means_no_data_not_zero()
    {
        var f = ActivityStats.FromJson(Json(Session()));
        Assert.False(f.Fps.HasData);
        Assert.False(f.HasAnyData);
        Assert.False(ActivityStats.FromJson("{\"Items\":[]}").HasAnyData);
    }

    public static void Garbage_or_missing_input_gives_empty_facts_and_no_exception()
    {
        Assert.False(ActivityStats.FromJson("not json").HasAnyData);
        Assert.False(ActivityStats.FromJson(null).HasAnyData);
        Assert.False(ActivityStats.FromJson("{}").HasAnyData);
        Assert.False(ActivityStats.FromJson("{\"Items\":[{\"Details\":[{\"fps\":\"x\"}]}]}").HasAnyData);
    }

    public static void Fps_scale_is_red_orange_green_blue_at_the_owners_limits()
    {
        var scale = ColorScale.Fps(40, 120, 150);
        Assert.Equal(ColorLevel.Red, scale.Level(39));
        Assert.Equal(ColorLevel.Orange, scale.Level(40));
        Assert.Equal(ColorLevel.Orange, scale.Level(120));
        Assert.Equal(ColorLevel.Green, scale.Level(121));
        Assert.Equal(ColorLevel.Green, scale.Level(149));
        Assert.Equal(ColorLevel.Blue, scale.Level(150));
        Assert.Equal(ColorLevel.Blue, scale.Level(240));
    }

    public static void A_load_scale_is_green_when_low_and_red_when_high()
    {
        var scale = ColorScale.Load(75, 95);
        Assert.Equal(ColorLevel.Green, scale.Level(60));
        Assert.Equal(ColorLevel.Orange, scale.Level(80));
        Assert.Equal(ColorLevel.Orange, scale.Level(95));
        Assert.Equal(ColorLevel.Red, scale.Level(96));
    }

    public static void A_scale_with_unordered_limits_is_repaired_instead_of_throwing()
    {
        var scale = ColorScale.Fps(150, 120, 40);   // typed the wrong way round
        Assert.Equal(ColorLevel.Red, scale.Level(10));
        Assert.Equal(ColorLevel.Blue, scale.Level(200));
    }

    public static void No_value_has_no_level()
    {
        Assert.Equal(ColorLevel.None, ColorScale.Fps(40, 120, 150).Level(null));
    }

    private static string DatedSample(string when, int fps)
    {
        return "{\"datelog\":\"" + when + "\",\"fps\":" + fps + ",\"cpu\":10,\"gpu\":10,\"ram\":10}";
    }

    public static void Samples_older_than_the_cut_off_are_ignored()
    {
        var json = Json(Session(DatedSample("2026-10-01T10:00:00Z", 30), DatedSample("2026-10-18T10:00:00Z", 90), DatedSample("2026-10-19T10:00:00Z", 110)));
        var all = ActivityStats.FromJson(json);
        Assert.Equal(3, all.Fps.Count);
        var recent = ActivityStats.FromJson(json, new System.DateTime(2026, 10, 18, 0, 0, 0, System.DateTimeKind.Utc));
        Assert.Equal(2, recent.Fps.Count);
        Assert.Equal(100.0, recent.Fps.Median, "the old 30 would have pulled it down");
    }

    public static void A_sample_without_a_readable_date_is_kept_only_when_there_is_no_cut_off()
    {
        var json = Json(Session("{\"fps\":50,\"cpu\":5,\"gpu\":5,\"ram\":5}"));
        Assert.Equal(1, ActivityStats.FromJson(json).Fps.Count);
        Assert.Equal(0, ActivityStats.FromJson(json, new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)).Fps.Count);
    }

    public static void A_sensor_that_is_not_measured_is_a_missing_value_not_a_zero_load()
    {
        var f = ActivityStats.FromJson(Json(Session(Sample(60, 40, 10, 0), Sample(60, 0, 10, 50))));
        Assert.Equal(1, f.Cpu.Count, "the second row has no cpu reading");
        Assert.Equal(40.0, f.Cpu.Median);
        Assert.Equal(1, f.Ram.Count, "the first row has no ram reading");
        Assert.Equal(50.0, f.Ram.Median);
    }

    public static void The_loader_parses_a_file_once_until_it_changes()
    {
        string path = System.IO.Path.GetTempFileName();
        try
        {
            System.IO.File.WriteAllText(path, Json(Session(Sample(60, 10, 10, 10))));
            var loader = new FactsLoader();
            var a = loader.Load(path, null);
            var b = loader.Load(path, null);
            Assert.Equal(1, loader.Parses, "the second call came from the cache");
            Assert.True(object.ReferenceEquals(a, b));
            loader.Load(path, new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc));
            Assert.Equal(2, loader.Parses, "another cut-off date is another answer");
            System.IO.File.WriteAllText(path, Json(Session(Sample(60, 10, 10, 10), Sample(90, 10, 10, 10))));
            System.IO.File.SetLastWriteTimeUtc(path, System.DateTime.UtcNow.AddSeconds(5));
            Assert.Equal(2, loader.Load(path, null).Fps.Count);
            Assert.Equal(3, loader.Parses, "a changed file is read again");
            Assert.False(loader.Load(path + ".missing", null).HasAnyData, "no file, no facts");
        }
        finally { System.IO.File.Delete(path); }
    }}
