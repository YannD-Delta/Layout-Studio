using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using LayoutStudio.Core;

namespace LayoutStudio.Engine
{
    /// <summary>Turns a normalized constant string into the WPF value the theme's resource expects. No UI thread needed.</summary>
    public static class WpfValues
    {
        public static object ToWpf(ConstantType type, string normalized)
        {
            switch (type)
            {
                case ConstantType.Double: return double.Parse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture);
                case ConstantType.Int: return int.Parse(normalized, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                case ConstantType.Boolean: return bool.Parse(normalized);
                case ConstantType.Thickness:
                {
                    var n = Numbers(normalized, 1, 2, 4);
                    if (n.Length == 1) return new Thickness(n[0]);
                    if (n.Length == 2) return new Thickness(n[0], n[1], n[0], n[1]);
                    return new Thickness(n[0], n[1], n[2], n[3]);
                }
                case ConstantType.CornerRadius:
                {
                    var n = Numbers(normalized, 1, 4);
                    return n.Length == 1 ? new CornerRadius(n[0]) : new CornerRadius(n[0], n[1], n[2], n[3]);
                }
                case ConstantType.Visibility: return (Visibility)Enum.Parse(typeof(Visibility), normalized);
                case ConstantType.Color: return (Color)ColorConverter.ConvertFromString(normalized);
                default: return normalized;
            }
        }

        private static double[] Numbers(string text, params int[] allowedCounts)
        {
            var parts = text.Split(',');
            if (!allowedCounts.Contains(parts.Length)) throw new FormatException("expected " + string.Join(" or ", allowedCounts) + " numbers in '" + text + "'");
            return parts.Select(p => double.Parse(p, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
        }
    }
}
