using System;
using System.Collections.Generic;
using System.Linq;
using LayoutStudio.Core;

public static class BackdropGeneratorTests
{
    private static readonly BackdropHarmony[] Harmonies = (BackdropHarmony[])Enum.GetValues(typeof(BackdropHarmony));
    private static readonly BackdropStyle[] Styles = (BackdropStyle[])Enum.GetValues(typeof(BackdropStyle));

    private static string Dump(BackdropLook look)
    {
        return string.Join(";", look.ToConstants().OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value));
    }

    public static void The_same_seed_harmony_and_style_always_give_the_same_look()
    {
        foreach (var h in Harmonies)
            foreach (var s in Styles)
                Assert.Equal(Dump(BackdropGenerator.Generate(4242, h, s)), Dump(BackdropGenerator.Generate(4242, h, s)), h + "/" + s);
    }

    public static void Another_seed_gives_another_look()
    {
        var seen = new HashSet<string>();
        for (int seed = 1; seed <= 50; seed++) seen.Add(Dump(BackdropGenerator.Generate(seed, BackdropHarmony.Analogous, BackdropStyle.Mixed)));
        Assert.Equal(50, seen.Count);
    }

    public static void Every_constant_it_writes_has_a_value_the_store_accepts()
    {
        var schema = Schema.Load(System.IO.File.ReadAllText(TestPaths.SchemaPath()));
        var store = new ConstantStore(schema.AllConstants());
        foreach (var h in Harmonies)
            foreach (var s in Styles)
                for (int seed = 1; seed <= 30; seed++)
                    foreach (var kv in BackdropGenerator.Generate(seed, h, s).ToConstants())
                    {
                        string error;
                        Assert.True(store.Find(kv.Key) != null, "the theme has no constant " + kv.Key);
                        Assert.True(store.TrySet(kv.Key, kv.Value, out error), kv.Key + " = " + kv.Value + ": " + error);
                        var def = store.Find(kv.Key);
                        double d;
                        if (def.Type == ConstantType.Double && double.TryParse(kv.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d))
                        {
                            Assert.True(!def.Min.HasValue || d >= def.Min.Value - 1e-9, kv.Key + " = " + kv.Value + " is under the minimum " + def.Min);
                            Assert.True(!def.Max.HasValue || d <= def.Max.Value + 1e-9, kv.Key + " = " + kv.Value + " is over the maximum " + def.Max);
                        }
                    }
    }

    public static void Any_first_circles_are_spread_over_the_window_and_not_clumped()
    {
        foreach (var style in Styles)
            for (int seed = 1; seed <= 200; seed++)
            {
                var look = BackdropGenerator.Generate(seed, BackdropHarmony.Analogous, style);
                for (int n = 3; n <= BackdropGenerator.MaxBlobs; n++)
                {
                    double nearest = double.MaxValue;
                    for (int i = 0; i < n; i++)
                        for (int j = i + 1; j < n; j++)
                        {
                            double dx = look.Blobs[i].X - look.Blobs[j].X, dy = look.Blobs[i].Y - look.Blobs[j].Y;
                            nearest = Math.Min(nearest, Math.Sqrt(dx * dx + dy * dy));
                        }
                    Assert.True(nearest > 0.08, "seed " + seed + " " + style + ": two of the first " + n + " circles are only " + nearest + " apart");
                }
            }
    }

    public static void Circles_stay_inside_a_range_that_can_be_seen()
    {
        for (int seed = 1; seed <= 100; seed++)
        {
            var look = BackdropGenerator.Generate(seed, BackdropHarmony.Complementary, BackdropStyle.Bokeh);
            foreach (var b in look.Blobs)
            {
                Assert.True(Math.Abs(b.X) <= 0.46 && Math.Abs(b.Y) <= 0.46, "a circle is out of the window");
                Assert.True(b.Size >= 0.1 && b.Size <= 2, "size " + b.Size);
                Assert.True(b.Opacity > 0 && b.Opacity <= 1, "opacity " + b.Opacity);
            }
        }
    }

    public static void The_colours_of_a_harmony_keep_their_rule()
    {
        for (int seed = 1; seed <= 100; seed++)
        {
            var mono = BackdropGenerator.Generate(seed, BackdropHarmony.Mono, BackdropStyle.Aurora).Colors.Select(Hsl).ToArray();
            foreach (var c in mono) Assert.True(HueDistance(mono[0][0], c[0]) <= 12 || c[1] < 0.08, "mono colours share a hue (seed " + seed + ")");
            var analogous = BackdropGenerator.Generate(seed, BackdropHarmony.Analogous, BackdropStyle.Aurora).Colors.Select(Hsl).ToArray();
            foreach (var c in analogous) Assert.True(HueDistance(analogous[0][0], c[0]) <= 50, "analogous hues stay within 50 degrees (seed " + seed + ")");
            var comp = BackdropGenerator.Generate(seed, BackdropHarmony.Complementary, BackdropStyle.Aurora).Colors.Select(Hsl).ToArray();
            Assert.True(Math.Abs(HueDistance(comp[0][0], comp[2][0]) - 180) <= 14, "complementary: the accent faces the base (seed " + seed + ")");
            var warm = BackdropGenerator.Generate(seed, BackdropHarmony.Warm, BackdropStyle.Aurora).Colors.Select(Hsl).ToArray();
            foreach (var c in warm) Assert.True(c[0] <= 80 || c[0] >= 330, "warm hues are red to amber, got " + c[0] + " (seed " + seed + ")");
            var cold = BackdropGenerator.Generate(seed, BackdropHarmony.Cold, BackdropStyle.Aurora).Colors.Select(Hsl).ToArray();
            foreach (var c in cold) Assert.True(c[0] >= 150 && c[0] <= 300, "cold hues are cyan to violet, got " + c[0] + " (seed " + seed + ")");
            var pastel = BackdropGenerator.Generate(seed, BackdropHarmony.Pastel, BackdropStyle.Aurora).Colors.Select(Hsl).ToArray();
            foreach (var c in pastel) Assert.True(c[1] <= 0.55, "pastel colours are muted, saturation " + c[1] + " (seed " + seed + ")");
        }
    }

    public static void The_base_colours_are_dark_enough_to_carry_light_text_for_every_harmony()
    {
        foreach (var h in Harmonies)
            for (int seed = 1; seed <= 60; seed++)
            {
                var look = BackdropGenerator.Generate(seed, h, BackdropStyle.Mixed);
                Assert.True(Hsl(look.Colors[0])[2] <= 0.34, h + " base colour too light for light text (seed " + seed + ")");
                Assert.True(Hsl(look.Colors[1])[2] <= 0.42, h + " second colour too light (seed " + seed + ")");
                foreach (var c in look.Colors) Assert.True(Hsl(c)[2] >= 0.08, "a colour is nearly black");
            }
    }

    public static void A_palette_can_be_built_on_a_given_colour_and_keeps_its_hue()
    {
        var look = BackdropGenerator.GenerateFrom("#FFD4A24C", 7, BackdropHarmony.Mono, BackdropStyle.Aurora);
        double accentHue = Hsl("#FFD4A24C")[0];
        foreach (var c in look.Colors.Select(Hsl)) Assert.True(HueDistance(accentHue, c[0]) <= 12, "the palette of an amber accent stays amber");
        Assert.Equal(Dump(look), Dump(BackdropGenerator.GenerateFrom("#FFD4A24C", 7, BackdropHarmony.Mono, BackdropStyle.Aurora)));
    }

    public static void A_palette_built_on_a_colour_keeps_its_hue_for_every_harmony()
    {
        double accent = Hsl("#FF3C8DD9")[0];   // a blue: the warm and cold harmonies must not replace it by a colour of their own
        foreach (var h in Harmonies)
            for (int seed = 1; seed <= 20; seed++)
            {
                var look = BackdropGenerator.GenerateFrom("#FF3C8DD9", seed, h, BackdropStyle.Mixed);
                Assert.True(HueDistance(accent, Hsl(look.Colors[0])[0]) <= 6, h + ": the base colour keeps the hue of the accent (seed " + seed + ")");
            }
    }

    public static void Colours_come_out_in_the_form_the_store_normalizes_to()
    {
        var look = BackdropGenerator.Generate(5, BackdropHarmony.Warm, BackdropStyle.Mixed);
        foreach (var c in look.Colors.Concat(look.Blobs.Select(b => b.Color)))
            Assert.True(System.Text.RegularExpressions.Regex.IsMatch(c, "^#FF[0-9A-F]{6}$"), c);
    }

    public static void Hsl_conversion_round_trips()
    {
        foreach (var hex in new[] { "#FF336699", "#FFAA3355", "#FF808080", "#FFFFD700" })
        {
            var hsl = Hsl(hex);
            Assert.Equal(hex, Rgb.ToHex(Rgb.FromHsl(hsl[0], hsl[1], hsl[2])), "round trip");
        }
    }

    private static double[] Hsl(string hex)
    {
        double h, s, l;
        Rgb.ToHsl(Rgb.Parse(hex), out h, out s, out l);
        return new[] { h, s, l };
    }

    private static double HueDistance(double a, double b)
    {
        double d = Math.Abs(a - b) % 360;
        return d > 180 ? 360 - d : d;
    }
}
