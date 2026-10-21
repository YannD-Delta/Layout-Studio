using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace LayoutStudio.Engine
{
    /// <summary>
    /// WPF does not refresh a brush property that reads a resource through DynamicResource when that resource changes.
    /// The fix is to publish a rebuilt copy of the brush under the same key, which DynamicResource consumers do pick up.
    /// A property is a plain name ("Color", "Opacity") or the path of one gradient stop ("GradientStops[2].Color").
    /// </summary>
    public static class DependentBrushes
    {
        private static readonly Regex StopPath = new Regex(@"^GradientStops\[(\d+)\]\.Color$", RegexOptions.CultureInvariant);

        /// <summary>Returns an editable copy of <paramref name="current"/> with one property replaced. The original is untouched.</summary>
        public static Freezable Rebuild(Freezable current, string property, object value)
        {
            var stop = StopPath.Match(property ?? "");
            if (stop.Success)
            {
                int index = int.Parse(stop.Groups[1].Value);
                var gradient = current as GradientBrush;
                if (gradient == null) throw new ArgumentException("'" + current.GetType().Name + "' has no gradient stops");
                if (index >= gradient.GradientStops.Count) throw new ArgumentException("the brush has " + gradient.GradientStops.Count + " gradient stops, not " + (index + 1));
                if (!(value is Color)) throw new ArgumentException("property '" + property + "' expects Color but got " + (value == null ? "null" : value.GetType().Name));
                var gradientCopy = (GradientBrush)current.Clone();
                gradientCopy.GradientStops[index].Color = (Color)value;
                return gradientCopy;
            }
            var info = current.GetType().GetProperty(property);
            if (info == null || !info.CanWrite) throw new ArgumentException("'" + current.GetType().Name + "' has no writable property '" + property + "'");
            if (value == null || !info.PropertyType.IsAssignableFrom(value.GetType()))
                throw new ArgumentException("property '" + property + "' expects " + info.PropertyType.Name + " but got " + (value == null ? "null" : value.GetType().Name));
            var copy = current.Clone();   // Clone() is never frozen and keeps the current (resolved) values
            info.SetValue(copy, value, null);
            return copy;
        }

        /// <summary>The value a property (or gradient stop path) has now; null when the brush has no such property.</summary>
        public static object Read(Freezable current, string property)
        {
            var stop = StopPath.Match(property ?? "");
            if (stop.Success)
            {
                var gradient = current as GradientBrush;
                int index = int.Parse(stop.Groups[1].Value);
                return gradient != null && index < gradient.GradientStops.Count ? (object)gradient.GradientStops[index].Color : null;
            }
            var info = current.GetType().GetProperty(property);
            return info == null || !info.CanRead ? null : info.GetValue(current, null);
        }

        /// <summary>
        /// Brings a brush in line with the live resources that feed it: every (property, live value) pair that differs from what the brush holds
        /// is written into a copy. Returns <paramref name="original"/> itself when nothing differs, so no copy is published for nothing.
        /// A pair the brush cannot take is skipped.
        /// </summary>
        public static Freezable Sync(Freezable original, IEnumerable<KeyValuePair<string, object>> live)
        {
            Freezable result = original;
            foreach (var feed in live)
            {
                if (feed.Value == null) continue;
                var now = Read(result, feed.Key);
                if (now == null || Same(now, feed.Value)) continue;
                try { result = Rebuild(result, feed.Key, feed.Value); }
                catch (ArgumentException) { /* the value type does not fit this property: the brush keeps its own */ }
            }
            return result;
        }

        private static bool Same(object a, object b)
        {
            if (a is double && b is double) return Math.Abs((double)a - (double)b) < 1e-9;
            return a.Equals(b);
        }
    }
}
