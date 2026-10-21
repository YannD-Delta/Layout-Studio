using System;
using System.Collections.Generic;
using System.Globalization;

namespace LayoutStudio.Core
{
    /// <summary>How the colours of a generated backdrop relate to each other.</summary>
    public enum BackdropHarmony { Analogous, Complementary, Mono, Warm, Cold, Pastel }

    /// <summary>The kind of circles: big soft glows (aurora), small crisp dots (bokeh) or a bit of both.</summary>
    public enum BackdropStyle { Aurora, Bokeh, Mixed }

    /// <summary>One circle of the backdrop: X and Y are offsets from the centre of the window (0 = centre, 0.5 = the edge), Size is its diameter in window widths.</summary>
    public class BackdropBlob
    {
        public double X, Y, Size, Opacity;
        public string Color;
    }

    /// <summary>
    /// The result of the generator: everything the theme reads to draw a generated backdrop (four colours, the gradient angle and centre,
    /// the edge hardness and the table of eight circles). <see cref="ToConstants"/> turns it into theme constants.
    /// </summary>
    public class BackdropLook
    {
        public int Seed;
        public string[] Colors = new string[4];
        public double Angle, Midpoint, CenterX, CenterY, Hardness;
        public bool ThirdColor;
        public int SuggestedCount;
        public double SuggestedScale = 1;
        public BackdropBlob[] Blobs = new BackdropBlob[BackdropGenerator.MaxBlobs];

        /// <summary>Theme constant name -> invariant text value, for every constant the generator decides.</summary>
        public IDictionary<string, string> ToConstants()
        {
            var map = new Dictionary<string, string>();
            map["BackdropSeed"] = Seed.ToString(CultureInfo.InvariantCulture);
            for (int i = 0; i < 3; i++) map["BackdropColor" + (i + 1)] = Colors[i];   // the fourth colour of the palette only feeds the circles
            map["BackdropAngle"] = Num(Angle);
            map["BackdropMidpoint"] = Num(Midpoint);
            map["BackdropThirdColor"] = ThirdColor ? "True" : "False";
            map["BackdropCenterX"] = Num(CenterX);
            map["BackdropCenterY"] = Num(CenterY);
            map["BackdropHardness"] = Num(Hardness);
            for (int i = 0; i < Blobs.Length; i++)
            {
                string p = "BackdropBlob" + (i + 1);
                map[p + "X"] = Num(Blobs[i].X);
                map[p + "Y"] = Num(Blobs[i].Y);
                map[p + "Size"] = Num(Blobs[i].Size);
                map[p + "Opacity"] = Num(Blobs[i].Opacity);
                map[p + "Color"] = Blobs[i].Color;
            }
            return map;
        }

        private static string Num(double value)
        {
            return ConstantValue.ToInvariant(Math.Round(value, 3));
        }
    }

    /// <summary>
    /// Makes a harmonious backdrop out of a seed: the same seed, harmony and style always give the same look (the generator has its own
    /// random sequence, it never uses System.Random). Colours are built in HSL from rules per harmony so they never clash; the circles follow a
    /// low-discrepancy sequence, so they are spread over the window and any first N of the eight are already well spread.
    /// </summary>
    public static class BackdropGenerator
    {
        public const int MaxBlobs = 8;

        public static BackdropLook Generate(int seed, BackdropHarmony harmony, BackdropStyle style)
        {
            var rng = new Rng(seed, 11);
            double hue = rng.Range(0, 360);
            var look = FromPalette(seed, Palette(rng, hue, harmony), style, rng);
            return look;
        }

        /// <summary>The same look, built on a colour that is given (the accent, the dominant colour of a cover) instead of a random one.</summary>
        public static BackdropLook GenerateFrom(string hexColor, int seed, BackdropHarmony harmony, BackdropStyle style)
        {
            double h, s, l;
            Rgb.ToHsl(Rgb.Parse(hexColor), out h, out s, out l);
            var rng = new Rng(seed, 29);
            return FromPalette(seed, Palette(rng, h, harmony, true), style, rng);
        }

        private static BackdropLook FromPalette(int seed, string[] palette, BackdropStyle style, Rng rng)
        {
            var look = new BackdropLook { Seed = seed, Colors = palette };
            look.Angle = Math.Round(rng.Range(0, 24)) * 15;
            look.Midpoint = Math.Round(rng.Range(0.4, 0.7), 2);
            look.CenterX = Math.Round(rng.Range(-0.3, 0.3), 2);
            look.CenterY = Math.Round(rng.Range(-0.3, 0.3), 2);
            look.ThirdColor = true;
            double lo, hi, opLo, opHi;
            switch (style)
            {
                case BackdropStyle.Bokeh: lo = 0.14; hi = 0.4; opLo = 0.18; opHi = 0.5; look.Hardness = 0.55; look.SuggestedCount = 8; break;
                case BackdropStyle.Mixed: lo = 0.2; hi = 1.1; opLo = 0.25; opHi = 0.65; look.Hardness = 0.25; look.SuggestedCount = 6; break;
                default: lo = 0.8; hi = 1.5; opLo = 0.35; opHi = 0.75; look.Hardness = 0; look.SuggestedCount = 5; break;
            }
            // additive recurrence of the plastic number: a well spread sequence whose every prefix is spread too, shifted by the seed
            double a = rng.Next01(), b = rng.Next01();
            int order = rng.Pick(3);
            for (int i = 0; i < MaxBlobs; i++)
            {
                double x = Frac(a + i * 0.7548776662466927 + (rng.Next01() - 0.5) * 0.05);
                double y = Frac(b + i * 0.5698402909980532 + (rng.Next01() - 0.5) * 0.05);
                var blob = new BackdropBlob
                {
                    X = Math.Round((x - 0.5) * 0.9, 3),
                    Y = Math.Round((y - 0.5) * 0.9, 3),
                    Size = Math.Round(rng.Range(lo, hi), 3),
                    Opacity = Math.Round(rng.Range(opLo, opHi), 2)
                };
                // the circles take the accent colours of the palette, a little lighter or darker each time
                double h, s, l;
                Rgb.ToHsl(Rgb.Parse(palette[1 + (i + order) % 3]), out h, out s, out l);
                l = Clamp(l + rng.Range(-0.06, 0.08), 0.2, 0.7);
                blob.Color = Rgb.ToHex(Rgb.FromHsl(h, s, l));
                look.Blobs[i] = blob;
            }
            return look;
        }

        /// <summary>Four colours: two dark base colours for the background, two lighter accents for the circles and the third gradient colour.</summary>
        private static string[] Palette(Rng rng, double hue, BackdropHarmony harmony, bool keepHue = false)
        {
            double[] hues = new double[4];
            double sat, lightShift = 0;
            double[] light = { 0.15, 0.22, 0.4, 0.5 };
            switch (harmony)
            {
                case BackdropHarmony.Analogous:
                    hues = new[] { hue, hue + 20, hue + 45, hue - 25 }; sat = rng.Range(0.45, 0.65); break;
                case BackdropHarmony.Complementary:
                    hues = new[] { hue, hue + 10, hue + 180, hue }; sat = rng.Range(0.4, 0.6); light[2] = 0.45; break;
                case BackdropHarmony.Mono:
                    hues = new[] { hue, hue, hue + 6, hue - 6 }; sat = rng.Range(0.3, 0.5); light = new[] { 0.13, 0.2, 0.36, 0.5 }; break;
                case BackdropHarmony.Warm:
                    if (!keepHue) hue = Wrap(rng.Range(-12, 22));
                    hues = new[] { hue, hue + 10, hue + 20, hue + 6 }; sat = rng.Range(0.5, 0.7); break;
                case BackdropHarmony.Cold:
                    if (!keepHue) hue = rng.Range(180, 255);
                    hues = new[] { hue, hue + 18, hue + 40, hue - 20 }; sat = rng.Range(0.45, 0.65); break;
                default:   // Pastel: soft, muted, mid-light colours that still carry light text
                    hues = new[] { hue, hue + 30, hue + 150, hue + 210 }; sat = rng.Range(0.28, 0.4); light = new[] { 0.3, 0.38, 0.52, 0.58 }; break;
            }
            var colors = new string[4];
            for (int i = 0; i < 4; i++)
            {
                double s = Clamp(sat + (i >= 2 ? 0.08 : -0.05) + rng.Range(-0.04, 0.04), 0.15, 0.85);
                double l = Clamp(light[i] + lightShift + rng.Range(-0.03, 0.03), 0.08, 0.75);
                colors[i] = Rgb.ToHex(Rgb.FromHsl(Wrap(hues[i]), s, l));
            }
            return colors;
        }

        private static double Frac(double v) { return v - Math.Floor(v); }
        private static double Wrap(double hue) { hue %= 360; return hue < 0 ? hue + 360 : hue; }
        private static double Clamp(double v, double lo, double hi) { return v < lo ? lo : v > hi ? hi : v; }

        /// <summary>SplitMix64: small, fast and the same on every runtime.</summary>
        private class Rng
        {
            private ulong state;

            public Rng(int seed, int salt)
            {
                state = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + (ulong)salt;
                Next(); Next();
            }

            private ulong Next()
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong z = state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }

            public double Next01() { return (Next() >> 11) / (double)(1UL << 53); }
            public double Range(double lo, double hi) { return lo + (hi - lo) * Next01(); }
            public int Pick(int count) { return (int)(Next01() * count) % count; }
        }
    }

    /// <summary>Small colour helpers: "#AARRGGBB" or "#RRGGBB" text, and HSL.</summary>
    public static class Rgb
    {
        public static double[] Parse(string hex)
        {
            string h = hex.TrimStart('#');
            if (h.Length == 8) h = h.Substring(2);
            return new[]
            {
                int.Parse(h.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0,
                int.Parse(h.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0,
                int.Parse(h.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0
            };
        }

        public static string ToHex(double[] rgb)
        {
            return "#FF" + B(rgb[0]) + B(rgb[1]) + B(rgb[2]);
        }

        private static string B(double v)
        {
            int n = (int)Math.Round(Math.Max(0, Math.Min(1, v)) * 255);
            return n.ToString("X2", CultureInfo.InvariantCulture);
        }

        public static void ToHsl(double[] c, out double h, out double s, out double l)
        {
            double r = c[0], g = c[1], b = c[2];
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2;
            double d = max - min;
            if (d < 1e-9) { h = 0; s = 0; return; }
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h *= 60;
        }

        public static double[] FromHsl(double h, double s, double l)
        {
            h = ((h % 360) + 360) % 360 / 360.0;
            if (s <= 0) return new[] { l, l, l };
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
            return new[] { Hue(p, q, h + 1.0 / 3), Hue(p, q, h), Hue(p, q, h - 1.0 / 3) };
        }

        private static double Hue(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 0.5) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }
    }
}
