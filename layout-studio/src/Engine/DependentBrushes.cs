using System;
using System.Windows;

namespace LayoutStudio.Engine
{
    /// <summary>
    /// WPF does not refresh a brush property that reads a resource through DynamicResource when that resource changes.
    /// The fix is to publish a rebuilt copy of the brush under the same key, which DynamicResource consumers do pick up.
    /// </summary>
    public static class DependentBrushes
    {
        /// <summary>Returns an editable copy of <paramref name="current"/> with one property replaced. The original is untouched.</summary>
        public static Freezable Rebuild(Freezable current, string property, object value)
        {
            var info = current.GetType().GetProperty(property);
            if (info == null || !info.CanWrite) throw new ArgumentException("'" + current.GetType().Name + "' has no writable property '" + property + "'");
            if (value == null || !info.PropertyType.IsAssignableFrom(value.GetType()))
                throw new ArgumentException("property '" + property + "' expects " + info.PropertyType.Name + " but got " + (value == null ? "null" : value.GetType().Name));
            var copy = current.Clone();   // Clone() is never frozen and keeps the current (resolved) values
            info.SetValue(copy, value, null);
            return copy;
        }
    }
}
