using System;
using System.Globalization;
using System.IO;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

public static class CornersEditorTests
{
    private class Env
    {
        public Schema Schema;
        public ConstantStore Store;
        public History History;
        public CornersEditorViewModel Corners;
    }

    private static Env Make()
    {
        var e = new Env();
        e.Schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        e.Store = new ConstantStore(e.Schema.AllConstants());
        e.History = new History(e.Store);
        e.Corners = new CornersEditorViewModel(e.Store, e.Schema, e.History);
        return e;
    }

    public static void The_schema_lists_the_rounded_elements_with_their_round_value()
    {
        var e = Make();
        Assert.Equal(14, e.Schema.Corners.Count);
        Assert.Equal(14, e.Corners.Items.Count);
        Assert.Equal(26.0, e.Schema.Corners.Single(c => c.Key == "HeroCardCornerRadius").Round);
    }

    public static void An_element_slider_writes_a_whole_number_radius()
    {
        var e = Make();
        var card = e.Corners.Items.Single(i => i.Key == "HeroCardCornerRadius");
        card.Radius = 12.4;
        Assert.Equal("12", e.Store.Get("HeroCardCornerRadius"));
        Assert.Equal(12.0, card.Radius);
        card.Radius = 500;
        Assert.Equal("60", e.Store.Get("HeroCardCornerRadius"), "the slider tops out at 60");
        card.Radius = -3;
        Assert.Equal("0", e.Store.Get("HeroCardCornerRadius"));
    }

    public static void The_master_slider_scales_every_element_continuously()
    {
        var e = Make();
        e.Corners.Master = 100;
        Assert.Equal("26", e.Store.Get("HeroCardCornerRadius"));
        Assert.Equal("14", e.Store.Get("HeroChipCornerRadius"));
        e.Corners.Master = 50;
        Assert.Equal("13", e.Store.Get("HeroCardCornerRadius"));
        Assert.Equal("7", e.Store.Get("HeroChipCornerRadius"));
        e.Corners.Master = 0;
        Assert.True(e.Corners.Items.All(i => i.Radius == 0), "0 % is fully square");
        e.Corners.Master = 130;
        Assert.Equal("34", e.Store.Get("HeroCardCornerRadius"), "130 % of 26, rounded");
    }

    public static void Neighbouring_master_values_give_neighbouring_radii_no_jump()
    {
        var e = Make();
        double previous = -1;
        for (int pct = 0; pct <= 150; pct += 5)
        {
            e.Corners.Master = pct;
            double r = e.Corners.Items.Single(i => i.Key == "HeroCardCornerRadius").Radius;
            Assert.True(r >= previous, "the radius never goes back down as the slider goes up");
            Assert.True(r - previous <= 3, "at most 3 px per 5 %: " + previous + " -> " + r);
            previous = r;
        }
    }

    public static void The_master_reads_back_what_the_slider_wrote_so_the_thumb_never_moves_on_its_own()
    {
        var e = Make();
        for (int pct = 0; pct <= 150; pct += 1)
        {
            e.Corners.Master = pct;
            Assert.True(System.Math.Abs(e.Corners.Master - pct) <= 1, "slider wrote " + pct + " but reads back " + e.Corners.Master);
        }
        for (int pct = 150; pct >= 0; pct -= 1)
        {
            e.Corners.Master = pct;
            Assert.True(System.Math.Abs(e.Corners.Master - pct) <= 1, "slider wrote " + pct + " but reads back " + e.Corners.Master);
        }
    }

    public static void The_master_never_reports_a_value_while_it_is_still_applying_one()
    {
        var e = Make();
        var seen = new System.Collections.Generic.List<double>();
        e.Corners.PropertyChanged += (s, a) => { if (a.PropertyName == "Master") seen.Add(e.Corners.Master); };
        e.Corners.Master = 120;
        Assert.True(seen.Count > 0, "the master notifies");
        Assert.True(seen.All(v => System.Math.Abs(v - 120) <= 1), "every notification shows the final value, got: " + string.Join(",", seen.Select(v => v.ToString(CultureInfo.InvariantCulture))));
    }

    public static void The_four_corner_looks_set_every_element_in_one_undo_step()
    {
        var e = Make();
        Assert.True(e.Corners.SharpCommand.CanExecute(null));
        e.Corners.SharpCommand.Execute(null);
        Assert.True(e.Corners.Items.All(i => i.Radius == 0), "Sharp makes every corner square");
        Assert.Equal(0.0, e.Corners.Master);
        e.Corners.AppleCommand.Execute(null);
        Assert.Equal(28.0, e.Corners.Items.Single(i => i.Key == "HeroCardCornerRadius").Radius, "Designed: 28 on the card");
        Assert.Equal(20.0, e.Corners.Items.Single(i => i.Key == "SectionCornerRadius").Radius, "20 on sections");
        Assert.Equal(14.0, e.Corners.Items.Single(i => i.Key == "HeroChipCornerRadius").Radius, "14 on chips");
        e.Corners.SoftCommand.Execute(null);
        Assert.Equal(17.0, e.Corners.Items.Single(i => i.Key == "HeroCardCornerRadius").Radius, "Soft: 60 % of 28, rounded");
        e.Corners.PillCommand.Execute(null);
        Assert.Equal(42.0, e.Corners.Items.Single(i => i.Key == "HeroCardCornerRadius").Radius, "Pill: 150 % of 28");
        Assert.True(e.Corners.Items.All(i => i.Radius <= CornerItemViewModel.MaxRadius));
        e.History.Undo();   // Pill
        Assert.Equal(17.0, e.Corners.Items.Single(i => i.Key == "HeroCardCornerRadius").Radius, "one undo step per look");
    }

    public static void Every_look_keeps_the_nested_corners_stepping_down()
    {
        var e = Make();
        e.Corners.AppleCommand.Execute(null);
        Func<string, double> r = k => e.Corners.Items.Single(i => i.Key == k).Radius;
        Assert.True(r("HeroCardCornerRadius") > r("SectionCornerRadius"), "card > section");
        Assert.True(r("SectionCornerRadius") > r("HeaderTileCornerRadius"), "section > tile");
        Assert.True(r("HeaderTileCornerRadius") > r("HeroButtonCornerRadius"), "tile > button");
        Assert.True(r("HeroButtonCornerRadius") > r("HeroChipCornerRadius"), "button > chip");
    }

    public static void A_slow_drag_of_an_element_slider_still_moves_the_radius()
    {
        var e = Make();
        var card = e.Corners.Items.Single(i => i.Key == "HeroCardCornerRadius");
        double position = card.SliderRadius;   // the slider starts where the radius is
        // the thumb moves 0.2 px of radius per mouse event: the slider adds it to the value it was last given
        for (int i = 0; i < 20; i++) { position = card.SliderRadius + 0.2; card.SliderRadius = position; }
        Assert.True(card.Radius >= 29, "20 x 0.2 = +4 after a slow drag, got " + card.Radius);
    }

    public static void An_element_slider_gives_the_exact_stored_radius_after_the_value_changes_elsewhere()
    {
        var e = Make();
        var card = e.Corners.Items.Single(i => i.Key == "HeroCardCornerRadius");
        card.SliderRadius = 12.4;
        Assert.Equal(12.4, card.SliderRadius);
        e.Store.TrySet("HeroCardCornerRadius", "30", out string error);
        Assert.Equal(30.0, card.SliderRadius, "once the value moved on its own the slider shows the stored value");
    }

    public static void The_master_follows_the_individual_values()
    {
        var e = Make();
        e.Corners.Master = 100;
        Assert.Equal(100.0, e.Corners.Master);
        e.Corners.Items.Single(i => i.Key == "HeroCardCornerRadius").Radius = 13;   // half of 26 on one element only
        Assert.True(e.Corners.Master < 100 && e.Corners.Master > 80, "the average moved a little: " + e.Corners.Master);
    }

    public static void Moving_the_master_is_one_undo_step_per_gesture()
    {
        var e = Make();
        e.Corners.BeginGesture();
        for (int pct = 0; pct <= 100; pct += 10) e.Corners.Master = pct;
        e.Corners.EndGesture();
        e.History.Undo();
        Assert.Equal(e.Store.GetDefault("HeroCardCornerRadius"), e.Store.Get("HeroCardCornerRadius"));
        Assert.False(e.History.CanUndo);
    }

    public static void An_element_with_four_different_corners_is_flagged_custom()
    {
        var e = Make();
        string error;
        e.Store.TrySet("HeroCoverCornerRadius", "20,20,0,0", out error);
        var cover = e.Corners.Items.Single(i => i.Key == "HeroCoverCornerRadius");
        Assert.True(cover.IsCustom);
        Assert.Equal(20.0, cover.Radius, "the slider shows the first corner");
        cover.Radius = 8;
        Assert.Equal("8", e.Store.Get("HeroCoverCornerRadius"), "moving the slider makes all four corners equal again");
        Assert.False(cover.IsCustom);
    }

    public static void Each_element_resets_to_the_theme_default()
    {
        var e = Make();
        var chip = e.Corners.Items.Single(i => i.Key == "HeroChipCornerRadius");
        string before = e.Store.Get("HeroChipCornerRadius");
        chip.Radius = 30;
        Assert.True(chip.IsModified);
        chip.ResetCommand.Execute(null);
        Assert.Equal(before, e.Store.Get("HeroChipCornerRadius"));
        Assert.False(chip.IsModified);
    }

    public static void The_grid_cover_radius_drives_its_three_companion_corners_and_the_rounded_flag_in_one_undo_step()
    {
        var e = Make();
        var cover = e.Corners.Items.Single(i => i.Key == "GridViewCoverCornerRadius");
        cover.Radius = 10;
        Assert.Equal("0,0,10,10", e.Store.Get("GridViewShowGameNameCornerRadius"));
        Assert.Equal("10,10,0,0", e.Store.Get("GridViewCoverGridBackgroundDisabledCornerRadius"));
        Assert.Equal("13", e.Store.Get("GridViewSelectionBorderCornerRadius"));
        Assert.Equal("True", e.Store.Get("GridViewCoverUseRoundedCorners"));
        cover.Radius = 0;
        Assert.Equal("False", e.Store.Get("GridViewCoverUseRoundedCorners"), "square corners switch the rounding off");
        e.History.Undo();
        Assert.Equal("13", e.Store.Get("GridViewSelectionBorderCornerRadius"), "one undo step puts the whole set back");
    }}
