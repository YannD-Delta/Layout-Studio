using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>Where one details-view section sits: a column (0 = left, 1 = right) and the Grid row of that column.</summary>
    public class SectionPlacement
    {
        public string Id;
        public int Column;
        public int Row;
    }

    /// <summary>
    /// What the live theme has to show: for every section, its column and its row, read from the same constants the editor writes
    /// (the column flag and the order constant of that column). Pure logic; the runtime side that moves the blocks is Engine\SectionMover.
    /// </summary>
    public static class SectionLayout
    {
        public static IList<SectionPlacement> Compute(ConstantStore store, Schema schema)
        {
            var result = new List<SectionPlacement>();
            foreach (var slot in WireframeModel.FromStore(store, schema))
            {
                var def = schema.Sections.First(d => d.Id == slot.Id);
                string key = slot.Column == 0 ? def.LeftKey : def.RightKey;
                result.Add(new SectionPlacement { Id = slot.Id, Column = slot.Column, Row = int.Parse(store.Get(key), CultureInfo.InvariantCulture) });
            }
            return result;
        }

        /// <summary>The constants whose change can move a section: column flags and order constants.</summary>
        public static ISet<string> WatchedKeys(Schema schema)
        {
            var keys = new HashSet<string>();
            foreach (var d in schema.Sections)
                foreach (var k in new[] { d.LeftKey, d.RightKey, d.ColumnKey })
                    if (!string.IsNullOrEmpty(k)) keys.Add(k);
            return keys;
        }
    }
}
