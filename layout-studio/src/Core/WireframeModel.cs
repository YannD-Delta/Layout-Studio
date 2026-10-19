using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>One Details-view section as the schema describes it: which constants place, show and fold it.</summary>
    public class SectionDef
    {
        public string Id;
        public string Title;
        public string LeftKey;     // order constant while the section is in the left column (null: never there)
        public string RightKey;    // order constant while the section is in the right column (null: never there)
        public string ShowKey;     // Boolean: section shown (null: always shown)
        public string ExpandKey;   // Boolean: section open by default
        public string ColumnKey;   // Boolean "in the right column" (null: the column cannot change)
    }

    /// <summary>A section placed in the wireframe.</summary>
    public class SectionSlot
    {
        public string Id;
        public string Title;
        public int Column;          // 0 = left, 1 = right
        public int Index;           // position inside the column
        public bool Visible;
        public bool OpenByDefault;
        public bool CanMove;        // can change column
        public bool CanHide;
        public bool NeedsRestart;   // one of its constants is read with StaticResource: the window changes after a restart
    }

    /// <summary>Reads and writes the Details-view section layout through the constants. Pure logic: callers add history batches.</summary>
    public static class WireframeModel
    {
        public static IList<SectionSlot> FromStore(ConstantStore store, Schema schema)
        {
            var slots = new List<SectionSlot>();
            foreach (var column in new[] { 0, 1 })
            {
                var members = Members(store, schema, column);
                for (int i = 0; i < members.Count; i++)
                {
                    var d = members[i];
                    slots.Add(new SectionSlot
                    {
                        Id = d.Id, Title = d.Title, Column = column, Index = i,
                        Visible = d.ShowKey == null || store.Get(d.ShowKey) == "True",
                        OpenByDefault = d.ExpandKey != null && store.Get(d.ExpandKey) == "True",
                        CanMove = d.ColumnKey != null, CanHide = d.ShowKey != null,
                        NeedsRestart = new[] { d.LeftKey, d.RightKey, d.ShowKey, d.ExpandKey, d.ColumnKey }.Any(k => k != null && schema.NeedsRestart(k))
                    });
                }
            }
            return slots;
        }

        /// <summary>Moves a section to a column and index. Returns false when nothing changed (unknown or fixed section, same place).</summary>
        public static bool Move(ConstantStore store, Schema schema, string sectionId, int column, int index)
        {
            var section = schema.Sections.FirstOrDefault(d => d.Id == sectionId);
            if (section == null || (column != 0 && column != 1)) return false;
            int current = InRightColumn(store, section) ? 1 : 0;
            if (column != current && section.ColumnKey == null) return false;

            var lists = new[] { Members(store, schema, 0), Members(store, schema, 1) };
            var before = new[] { lists[0].Select(d => d.Id).ToList(), lists[1].Select(d => d.Id).ToList() };
            lists[current].Remove(lists[current].First(d => d.Id == sectionId));
            if (index < 0) index = 0;
            if (index > lists[column].Count) index = lists[column].Count;
            lists[column].Insert(index, section);
            if (before[0].SequenceEqual(lists[0].Select(d => d.Id)) && before[1].SequenceEqual(lists[1].Select(d => d.Id))) return false;

            string error;
            if (section.ColumnKey != null) store.TrySet(section.ColumnKey, column == 1 ? "True" : "False", out error);
            Renumber(store, schema, 0, lists[0]);
            Renumber(store, schema, 1, lists[1]);
            return true;
        }

        public static bool SetVisible(ConstantStore store, Schema schema, string sectionId, bool visible)
        {
            var d = schema.Sections.FirstOrDefault(x => x.Id == sectionId);
            if (d == null || d.ShowKey == null) return false;
            string error;
            return store.TrySet(d.ShowKey, visible ? "True" : "False", out error);
        }

        public static bool SetOpen(ConstantStore store, Schema schema, string sectionId, bool open)
        {
            var d = schema.Sections.FirstOrDefault(x => x.Id == sectionId);
            if (d == null || d.ExpandKey == null) return false;
            string error;
            return store.TrySet(d.ExpandKey, open ? "True" : "False", out error);
        }

        private static bool InRightColumn(ConstantStore store, SectionDef d)
        {
            if (d.ColumnKey != null) return store.Get(d.ColumnKey) == "True";
            return d.LeftKey == null;   // fixed sections: the column they have an order constant for
        }

        private static string OrderKey(SectionDef d, int column)
        {
            return column == 0 ? d.LeftKey : d.RightKey;
        }

        private static int Order(ConstantStore store, string key)
        {
            return int.Parse(store.Get(key), CultureInfo.InvariantCulture);
        }

        /// <summary>The sections of a column, ordered by their order constant for that column.</summary>
        private static List<SectionDef> Members(ConstantStore store, Schema schema, int column)
        {
            return schema.Sections
                .Where(d => (InRightColumn(store, d) ? 1 : 0) == column && OrderKey(d, column) != null)
                .OrderBy(d => Order(store, OrderKey(d, column)))
                .ToList();
        }

        /// <summary>
        /// Writes 0..n-1 into the column's order constants: the sections shown in the column first, in list order,
        /// then the sections parked in the other column (they keep a number so each constant stays unique).
        /// </summary>
        private static void Renumber(ConstantStore store, Schema schema, int column, List<SectionDef> inColumn)
        {
            var parked = schema.Sections
                .Where(d => OrderKey(d, column) != null && !inColumn.Contains(d))
                .OrderBy(d => Order(store, OrderKey(d, column))).ToList();
            int position = 0;
            string error;
            foreach (var d in inColumn.Concat(parked))
                store.TrySet(OrderKey(d, column), (position++).ToString(CultureInfo.InvariantCulture), out error);
        }
    }
}
