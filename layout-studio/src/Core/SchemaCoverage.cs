using System.Collections.Generic;
using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>Which constants a visual page (sections, sizes, corners, background, header cards) already edits,
    /// so the generated settings pages do not offer the same choice a second time.</summary>
    public static class SchemaCoverage
    {
        public static HashSet<string> VisualKeys(Schema schema)
        {
            var keys = new HashSet<string>();
            Add(keys, schema.Sections.SelectMany(s => new[] { s.LeftKey, s.RightKey, s.ShowKey, s.ExpandKey, s.ColumnKey }));
            Add(keys, schema.Sizes.SelectMany(s => new[] { s.WidthKey, s.HeightKey }));
            Add(keys, schema.Corners.Select(c => c.Key));
            Add(keys, schema.Tuning.Select(t => t.Key));
            foreach (var tiles in schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Where(c => c.Kind == "tileSlots"))
            {
                Add(keys, tiles.Keys);
                Add(keys, tiles.ExtraKeys);
            }
            return keys;
        }

        private static void Add(HashSet<string> set, IEnumerable<string> keys)
        {
            foreach (var k in keys) if (!string.IsNullOrEmpty(k)) set.Add(k);
        }

        /// <summary>True when the control has constants and every one of them is covered.</summary>
        public static bool IsCovered(Control control, HashSet<string> covered)
        {
            return control.Keys.Count > 0 && control.Keys.All(covered.Contains);
        }

        /// <summary>Presets that the Colors and Corners pages already offer (a preset has no constants of its own, so it is matched by id).</summary>
        public static bool IsPresetOfAVisualPage(Control control, bool hasColorsPage, bool hasCornersPage)
        {
            if (control.Kind != "preset") return false;
            return (hasColorsPage && (control.Id == "ColorProfile" || control.Id == "Accent")) || (hasCornersPage && control.Id == "Corners");
        }

        /// <summary>"Details view - information chips" on the Details page becomes "Information chips": the page name is not repeated.</summary>
        public static string ShortTitle(string title)
        {
            foreach (var prefix in new[] { "Details view - ", "Grid view - " })
                if (title != null && title.StartsWith(prefix, System.StringComparison.Ordinal) && title.Length > prefix.Length)
                {
                    string rest = title.Substring(prefix.Length);
                    return char.ToUpperInvariant(rest[0]) + rest.Substring(1);
                }
            return title;
        }
    }
}
