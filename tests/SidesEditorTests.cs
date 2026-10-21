using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using LayoutStudio.Core;
using LayoutStudio.Editor;

/// <summary>Margins, paddings and corner radii are edited as four sides, not as typed text like "12,7,12,7".</summary>
public static class SidesEditorTests
{
    private const string CornerSchema = @"{
  ""version"": 1,
  ""constants"": [
    {""key"":""PanelCorners"",""type"":""CornerRadius"",""default"":""14,14,0,0""},
    {""key"":""Gap"",""type"":""Thickness"",""default"":""10""},
    {""key"":""Pair"",""type"":""Thickness"",""default"":""10,4""}
  ],
  ""pages"": [{""id"":""p"",""title"":""P"",""groups"":[{""id"":""g"",""title"":""G"",""controls"":[
    {""kind"":""thickness"",""id"":""PanelCorners"",""keys"":[""PanelCorners""],""title"":""Panel corners"",""choices"":[]},
    {""kind"":""thickness"",""id"":""Gap"",""keys"":[""Gap""],""title"":""Gap"",""choices"":[]},
    {""kind"":""thickness"",""id"":""Pair"",""keys"":[""Pair""],""title"":""Pair"",""choices"":[]}]}]}]
}";

    private class Env
    {
        public ConstantStore Store;
        public History History;
        public SettingViewModel Margin, Corners, Gap, Pair;
    }

    private static Env Make(string culture = "en-US")
    {
        var e = new Env();
        var schema = Schema.Load(TestSchema.Json);
        e.Store = new ConstantStore(schema.AllConstants());
        e.History = new History(e.Store);
        var profile = new Profile { Name = "Default" };
        var applier = new ProfileApplier(schema, e.Store, new FakePresetFiles(), @"C:\theme");
        e.Margin = new SettingViewModel(schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Single(c => c.Id == "PageSideMargin"), e.Store, e.History, applier, profile, k => null, new CultureInfo(culture));

        var cs = Schema.Load(CornerSchema);
        var cstore = new ConstantStore(cs.AllConstants());
        var chistory = new History(cstore);
        var capplier = new ProfileApplier(cs, cstore, new FakePresetFiles(), @"C:\theme");
        Func<string, SettingViewModel> vm = id => new SettingViewModel(cs.Pages[0].Groups[0].Controls.Single(c => c.Id == id), cstore, chistory, capplier, profile, k => null, new CultureInfo(culture));
        e.Corners = vm("PanelCorners"); e.Gap = vm("Gap"); e.Pair = vm("Pair");
        cornerStore = cstore; cornerHistory = chistory;
        return e;
    }

    private static ConstantStore cornerStore;
    private static History cornerHistory;

    public static void A_thickness_shows_its_four_sides()
    {
        var e = Make();
        Assert.Equal("sides", e.Margin.TemplateName);
        Assert.Equal("20", e.Margin.Side1Text);   // left
        Assert.Equal("0", e.Margin.Side2Text);    // top
        Assert.Equal("20", e.Margin.Side3Text);   // right
        Assert.Equal("0", e.Margin.Side4Text);    // bottom
        Assert.Equal("Left", e.Margin.Side1Label);
        Assert.Equal("Bottom", e.Margin.Side4Label);
    }

    public static void One_or_two_numbers_are_spread_over_the_four_sides()
    {
        var e = Make();
        Assert.Equal("10", e.Gap.Side1Text);
        Assert.Equal("10", e.Gap.Side4Text);
        Assert.Equal("10", e.Pair.Side1Text);   // left and right
        Assert.Equal("4", e.Pair.Side2Text);    // top and bottom
        Assert.Equal("10", e.Pair.Side3Text);
        Assert.Equal("4", e.Pair.Side4Text);
    }

    public static void Typing_one_side_writes_all_four_numbers_in_one_undo_step()
    {
        var e = Make();
        e.Margin.Side2Text = "8";
        Assert.Equal("20,8,20,0", e.Store.Get("PageSideMargin"));
        e.Margin.Side4Text = "6";
        Assert.Equal("20,8,20,6", e.Store.Get("PageSideMargin"));
        e.History.Undo();
        Assert.Equal("20,8,20,0", e.Store.Get("PageSideMargin"));
        Assert.Equal("0", e.Margin.Side4Text, "the field follows the undo");
    }

    public static void The_all_field_sets_every_side_and_shows_nothing_while_they_differ()
    {
        var e = Make();
        Assert.Equal("", e.Margin.AllText, "20,0,20,0 is mixed");
        e.Margin.AllText = "12";
        Assert.Equal("12", e.Store.Get("PageSideMargin"), "equal sides are stored as one number");
        Assert.Equal("12", e.Margin.AllText);
        Assert.Equal("12", e.Margin.Side3Text);
    }

    public static void A_French_comma_is_understood_and_stored_with_a_point()
    {
        var e = Make("fr-FR");
        e.Margin.Side1Text = "7,5";
        Assert.Equal("7.5,0,20,0", e.Store.Get("PageSideMargin"));
        Assert.Equal("7,5", e.Margin.Side1Text);
    }

    public static void A_bad_side_keeps_the_value_and_says_why()
    {
        var e = Make();
        e.Margin.Side1Text = "abc";
        Assert.True(e.Margin.Error != null);
        Assert.Equal("20,0,20,0", e.Store.Get("PageSideMargin"));
        e.Margin.Side1Text = "5";
        Assert.Equal(null, e.Margin.Error);
    }

    public static void The_small_picture_scales_the_values_and_stays_inside_its_frame()
    {
        var e = Make();
        var t = e.Margin.SidesPreview;
        Assert.True(t.Left > 0 && t.Right > 0 && t.Top == 0 && t.Bottom == 0, "20 left/right, 0 top/bottom: " + t);
        e.Margin.Side1Text = "5000";
        Assert.True(e.Margin.SidesPreview.Left <= 30, "the picture never exceeds its frame: " + e.Margin.SidesPreview.Left);
        e.Margin.Side2Text = "-14";
        Assert.True(e.Margin.SidesPreview.Top < 0, "a negative margin pulls the box out of its frame");
    }

    public static void A_corner_radius_is_four_corners_with_their_own_names()
    {
        var e = Make();
        Assert.Equal("corners", e.Corners.TemplateName);
        Assert.Equal("Top left", e.Corners.Side1Label);
        Assert.Equal("Top right", e.Corners.Side2Label);
        Assert.Equal("Bottom right", e.Corners.Side3Label);
        Assert.Equal("Bottom left", e.Corners.Side4Label);
        Assert.Equal("14", e.Corners.Side1Text);
        Assert.Equal("0", e.Corners.Side3Text);
        e.Corners.Side3Text = "9";
        Assert.Equal("14,14,9,0", cornerStore.Get("PanelCorners"));
        e.Corners.Side3Text = "-4";
        Assert.True(e.Corners.Error != null, "a corner cannot be negative");
        Assert.Equal(new CornerRadius(14, 14, 9, 0), e.Corners.CornersPreview);
    }

    public static void Every_template_a_setting_can_ask_for_exists_in_the_editor_window()
    {
        string xaml = File.ReadAllText(Path.Combine(TestPaths.RepoRoot(), @"layout-studio\src\Editor\EditorWindow.xaml"));
        foreach (var name in new[] { "toggle", "slider", "number", "text", "color", "choice", "presetTiles", "segmented", "orderedList", "tileSlots", "sides", "corners", "unsupported" })
            Assert.True(xaml.Contains("x:Key=\"Template." + name + "\""), "Template." + name + " is defined");
        var e = Make();
        Assert.True(xaml.Contains("x:Key=\"Template." + e.Margin.TemplateName + "\""));
        Assert.True(xaml.Contains("x:Key=\"Template." + e.Corners.TemplateName + "\""));
    }
}
