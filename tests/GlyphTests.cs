using System.Globalization;
using System.IO;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

/// <summary>The artwork card look settings carry a small schematic that follows the slider.</summary>
public static class GlyphTests
{
    private static SettingViewModel Setting(string key, out ConstantStore store)
    {
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        store = new ConstantStore(schema.AllConstants());
        var history = new History(store);
        var applier = new ProfileApplier(schema, store, new FakePresetFiles(), @"C:\theme");
        var control = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Single(c => c.Id == key);
        return new SettingViewModel(control, store, history, applier, new Profile { Name = "Default" }, k => null, new CultureInfo("en-US"));
    }

    public static void Every_artwork_card_look_slider_has_a_glyph_and_the_others_have_none()
    {
        ConstantStore store;
        foreach (var key in new[] { "HeroCardBorderOpacity", "HeroBlurRadius", "HeroBlurRadiusSoft", "HeroBlurOpacity", "HeroBlurStart", "HeroBlurMid", "HeroBlurFull", "HeroTintAccentOpacity", "HeroTintLightOpacity", "HeroFadeStart" })
        {
            var s = Setting(key, out store);
            Assert.True(s.HasGlyph, key + " has a glyph");
        }
        Assert.False(Setting("HeroMinHeight", out store).HasGlyph);
        Assert.Equal("opacity", SettingGlyphs.For("SectionGlassOpacity"), "an opacity shows a veil");
        Assert.Equal("text-size", SettingGlyphs.For("FontSizeLarge"));
        Assert.Equal("fade", SettingGlyphs.For("HeroFadeStart"), "a specific schematic wins over the family");
    }

    public static void The_glyph_value_is_the_position_of_the_slider_between_its_ends()
    {
        ConstantStore store;
        var s = Setting("HeroBlurStart", out store);   // 0..1
        s.SliderValue = 0.25;
        Assert.Equal(0.25, s.Fraction);
        Assert.Equal(0.75, s.FractionInverse);
        var radius = Setting("HeroBlurRadius", out store);   // 0..100
        radius.SliderValue = 50;
        Assert.Equal(0.5, radius.Fraction);
        radius.Text = "400";
        Assert.Equal(1.0, radius.Fraction, "a typed value beyond the end stays inside the picture");
        Assert.Equal(11.0, s.GlyphTopMargin.Top, "the marker sits a quarter of the way down the 44 px picture");
    }

    public static void Every_glyph_id_has_a_template_in_the_editor_window()
    {
        string xaml = File.ReadAllText(Path.Combine(TestPaths.RepoRoot(), @"layout-studio\src\Editor\EditorWindow.xaml"));
        foreach (var id in SettingGlyphs.Ids) Assert.True(xaml.Contains("x:Key=\"Glyph." + id + "\""), "Glyph." + id + " is drawn");
    }
}
