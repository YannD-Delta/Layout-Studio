using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>
    /// The order of the information chips in the artwork header. Every chip has an order constant (<c>HeroChipOrder&lt;Id&gt;</c>, each number once);
    /// the theme file lists the chips in the default order and the Layout Studio plugin puts them in the order of the constants (Engine\ChipMover).
    /// Pure logic: callers add history batches.
    /// </summary>
    public static class ChipLayout
    {
        public const string OrderPrefix = "HeroChipOrder";
        public const string ListId = "HeroChipOrder";

        public static string OrderKey(string id) { return OrderPrefix + id; }

        /// <summary>The chips, in the default order (the order of the schema's ordered list).</summary>
        public static IList<string> Ids(Schema schema)
        {
            var list = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).FirstOrDefault(c => c.Kind == "orderedList" && c.Id == ListId);
            if (list == null) return new List<string>();
            return list.Keys.Select(k => k.Substring(OrderPrefix.Length)).ToList();
        }

        /// <summary>The chips in the order of the profile: by order number, equal numbers in the default order.</summary>
        public static IList<string> Order(ConstantStore store, Schema schema)
        {
            var ids = Ids(schema);
            return ids.Select((id, i) => new { Id = id, Default = i, Number = Number(store, id) })
                .OrderBy(x => x.Number).ThenBy(x => x.Default).Select(x => x.Id).ToList();
        }

        private static int Number(ConstantStore store, string id)
        {
            int n;
            return int.TryParse(store.Get(OrderKey(id)), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : int.MaxValue;
        }

        /// <summary>Puts a chip at a place of the line (0 = first; past the end = last) and renumbers the line 0..n-1. False when nothing changed.</summary>
        public static bool Move(ConstantStore store, Schema schema, string id, int index)
        {
            var order = Order(store, schema).ToList();
            int from = order.IndexOf(id);
            if (from < 0) return false;
            order.RemoveAt(from);
            if (index < 0) index = 0;
            if (index > order.Count) index = order.Count;
            order.Insert(index, id);
            bool changed = false;
            for (int i = 0; i < order.Count; i++)
            {
                string text = i.ToString(CultureInfo.InvariantCulture), error;
                if (store.Get(OrderKey(order[i])) == text) continue;
                if (store.TrySet(OrderKey(order[i]), text, out error)) changed = true;
            }
            return changed;
        }

        /// <summary>The constants whose change moves a chip.</summary>
        public static ISet<string> WatchedKeys(Schema schema)
        {
            return new HashSet<string>(Ids(schema).Select(OrderKey));
        }

        /// <summary>
        /// Puts the blocks of a chip line in the given chip order. A chip is several blocks (a line break, then the chip; HowLongToBeat has three of each):
        /// the blocks of one chip keep their relative order. The places of blocks that belong to no chip stay where they are, and blocks of a chip that is
        /// not in <paramref name="order"/> go after the known ones.
        /// </summary>
        public static IList<T> Arrange<T>(IList<T> blocks, Func<T, string> idOf, IList<string> order)
        {
            var result = blocks.ToList();
            var places = new List<int>();
            var tagged = new List<KeyValuePair<int, T>>();
            for (int i = 0; i < blocks.Count; i++)
            {
                if (idOf(blocks[i]) == null) continue;
                places.Add(i);
                tagged.Add(new KeyValuePair<int, T>(i, blocks[i]));
            }
            var sorted = tagged
                .OrderBy(kv => { int at = order.IndexOf(idOf(kv.Value)); return at < 0 ? int.MaxValue : at; })
                .ThenBy(kv => kv.Key).Select(kv => kv.Value).ToList();
            for (int n = 0; n < places.Count; n++) result[places[n]] = sorted[n];
            return result;
        }
    }
}
