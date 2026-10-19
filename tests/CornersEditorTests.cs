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
