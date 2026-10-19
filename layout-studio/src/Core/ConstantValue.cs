using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace LayoutStudio.Core
{
    public enum ConstantType { Double, Int, Boolean, Thickness, CornerRadius, Visibility, String, Color }

    public class ConstantDef
    {
        public string Key;
        public ConstantType Type;
        public string Default;
        public double? Min;
        public double? Max;
        public double? Step;
    }

    /// <summary>Parsing and normalization of constant values. Stored values are always invariant-culture strings.</summary>
    public static class ConstantValue
    {
        private static readonly Regex HexColor = new Regex("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$");

        public static string ToInvariant(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>Parses text typed by the user with the culture's decimal separator.</summary>
        public static double ParseUi(string text, CultureInfo culture)
        {
            return double.Parse(text.Trim(), NumberStyles.Float, culture);
        }

        public static string FormatUi(double value, CultureInfo culture)
        {
            return value.ToString("R", culture);
        }

        /// <summary>Validates a raw invariant string for a definition. On success returns the normalized value.</summary>
        public static bool TryNormalize(ConstantDef def, string raw, out string normalized, out string error)
        {
            normalized = null;
            error = null;
            if (raw == null) { error = "value is missing"; return false; }
            raw = raw.Trim();
            switch (def.Type)
            {
                case ConstantType.Double:
                {
                    double d;
                    if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out d) || double.IsNaN(d) || double.IsInfinity(d))
                    {
                        error = "'" + raw + "' is not a number";
                        return false;
                    }
                    normalized = ToInvariant(Clamp(def, d));
                    return true;
                }
                case ConstantType.Int:
                {
                    int i;
                    if (!int.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out i))
                    {
                        error = "'" + raw + "' is not a whole number";
                        return false;
                    }
                    normalized = ((int)Clamp(def, i)).ToString(CultureInfo.InvariantCulture);
                    return true;
                }
                case ConstantType.Boolean:
                {
                    if (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)) { normalized = "True"; return true; }
                    if (string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase)) { normalized = "False"; return true; }
                    error = "'" + raw + "' is not True or False";
                    return false;
                }
                case ConstantType.Thickness:
                {
                    var parts = raw.Split(',').Select(p => p.Trim()).ToArray();
                    if (parts.Length != 1 && parts.Length != 2 && parts.Length != 4)
                    {
                        error = "a Thickness has 1, 2 or 4 numbers";
                        return false;
                    }
                    var numbers = new double[parts.Length];
                    for (int n = 0; n < parts.Length; n++)
                    {
                        if (!double.TryParse(parts[n], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[n]) || double.IsNaN(numbers[n]) || double.IsInfinity(numbers[n]))
                        {
                            error = "'" + parts[n] + "' is not a number";
                            return false;
                        }
                        numbers[n] = Clamp(def, numbers[n]);
                    }
                    normalized = string.Join(",", numbers.Select(ToInvariant));
                    return true;
                }
                case ConstantType.CornerRadius:
                {
                    var parts = raw.Split(',').Select(p => p.Trim()).ToArray();
                    if (parts.Length != 1 && parts.Length != 4)
                    {
                        error = "a CornerRadius has 1 or 4 numbers";
                        return false;
                    }
                    var numbers = new double[parts.Length];
                    for (int n = 0; n < parts.Length; n++)
                    {
                        if (!double.TryParse(parts[n], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[n]) || double.IsNaN(numbers[n]) || double.IsInfinity(numbers[n]) || numbers[n] < 0)
                        {
                            error = "'" + parts[n] + "' is not a non-negative number";
                            return false;
                        }
                        numbers[n] = Clamp(def, numbers[n]);
                    }
                    normalized = string.Join(",", numbers.Select(ToInvariant));
                    return true;
                }
                case ConstantType.Visibility:
                {
                    foreach (var word in new[] { "Visible", "Collapsed", "Hidden" })
                    {
                        if (string.Equals(raw, word, StringComparison.OrdinalIgnoreCase)) { normalized = word; return true; }
                    }
                    error = "'" + raw + "' is not Visible, Collapsed or Hidden";
                    return false;
                }
                case ConstantType.Color:
                {
                    if (!HexColor.IsMatch(raw))
                    {
                        error = "'" + raw + "' is not a hex color (#RRGGBB or #AARRGGBB)";
                        return false;
                    }
                    normalized = raw.Length == 7 ? "#FF" + raw.Substring(1).ToUpperInvariant() : raw.ToUpperInvariant();
                    return true;
                }
                default:
                    normalized = raw;
                    return true;
            }
        }

        /// <summary>Only the lower bound is enforced (a negative width or border would throw in WPF). The schema's maximum is the end of the slider, not a wall: a typed value can go beyond it.</summary>
        private static double Clamp(ConstantDef def, double value)
        {
            if (def.Min.HasValue && value < def.Min.Value) value = def.Min.Value;
            return value;
        }
    }
}
