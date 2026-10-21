using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>One header card in the flat order of the row: the tile type it shows and whether it starts a new glass box.</summary>
    public class CardEntry
    {
        public int Tile;        // value of the tile type (the Int32 of a HeaderTileSlot constant, 0 = nothing)
        public bool NewBox;     // starts a new box (cell); false = joins the box of the card before it
    }

    /// <summary>
    /// The header cards as one flat list. The theme has 12 boxes (cells) of up to 4 cards side by side, each card a tile type held by a constant
    /// (HeaderTileSlot&lt;n&gt; for the first card of a box, then B, C, D). Reading walks the boxes in order and skips holes; writing packs the list back:
    /// a card with <see cref="CardEntry.NewBox"/> starts the next box, a box takes at most 4 cards, the rest of the constants are emptied.
    /// An empty box leaves no gap in the theme, so packing never changes how the cards look.
    /// </summary>
    public static class CardLayout
    {
        public const int Boxes = 12;
        public const int CardsPerBox = 4;

        /// <summary>The constant of a card: position 0 is the box's own key, 1 to 3 are its extra keys (B, C, D).</summary>
        public static string Key(IList<string> slotKeys, IList<string> extraKeys, int box, int position)
        {
            return position == 0 ? slotKeys[box] : extraKeys[box * (CardsPerBox - 1) + position - 1];
        }

        /// <summary>The cards in the order of the row.</summary>
        public static List<CardEntry> Read(Func<string, string> get, IList<string> slotKeys, IList<string> extraKeys)
        {
            var list = new List<CardEntry>();
            for (int box = 0; box < Math.Min(Boxes, slotKeys.Count); box++)
            {
                bool first = true;
                for (int p = 0; p < CardsPerBox; p++)
                {
                    int tile;
                    if (!int.TryParse(get(Key(slotKeys, extraKeys, box, p)), NumberStyles.Integer, CultureInfo.InvariantCulture, out tile) || tile == 0) continue;
                    list.Add(new CardEntry { Tile = tile, NewBox = first });
                    first = false;
                }
            }
            return list;
        }

        /// <summary>How many boxes a list needs once packed.</summary>
        public static int BoxesNeeded(IList<CardEntry> cards)
        {
            int boxes = 0, inBox = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                if (i == 0 || cards[i].NewBox || inBox == CardsPerBox) { boxes++; inBox = 0; }
                inBox++;
            }
            return boxes;
        }

        /// <summary>The value of every card constant for a list, or null when the list needs more boxes than the theme has.</summary>
        public static Dictionary<string, string> Pack(IList<CardEntry> cards, IList<string> slotKeys, IList<string> extraKeys)
        {
            if (BoxesNeeded(cards) > Math.Min(Boxes, slotKeys.Count)) return null;
            var values = new Dictionary<string, string>();
            for (int box = 0; box < Math.Min(Boxes, slotKeys.Count); box++)
                for (int p = 0; p < CardsPerBox; p++) values[Key(slotKeys, extraKeys, box, p)] = "0";
            int cell = -1, inBox = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                if (i == 0 || cards[i].NewBox || inBox == CardsPerBox) { cell++; inBox = 0; }
                values[Key(slotKeys, extraKeys, cell, inBox)] = cards[i].Tile.ToString(CultureInfo.InvariantCulture);
                inBox++;
            }
            return values;
        }

        /// <summary>Moves a card to a place of the list (0 = first). The box it starts or joins is kept: a card that moves keeps its own "new box" flag,
        /// and the card that used to follow the first card of a box starts that box when the first card leaves.</summary>
        public static List<CardEntry> Move(IList<CardEntry> cards, int from, int to)
        {
            var list = cards.Select(c => new CardEntry { Tile = c.Tile, NewBox = c.NewBox }).ToList();
            if (from < 0 || from >= list.Count) return list;
            if (to < 0) to = 0;
            if (to >= list.Count) to = list.Count - 1;
            if (from == to) return list;
            var moved = list[from];
            // the flags stay with the places of the row, not with the cards: moving a card never regroups the boxes
            var flags = list.Select(c => c.NewBox).ToList();
            list.RemoveAt(from);
            list.Insert(to, moved);
            for (int i = 0; i < list.Count; i++) list[i].NewBox = flags[i];
            return list;
        }

        /// <summary>Takes a card out of the list. The card that follows keeps the box structure: it starts a box when the removed card did.</summary>
        public static List<CardEntry> Remove(IList<CardEntry> cards, int index)
        {
            var list = cards.Select(c => new CardEntry { Tile = c.Tile, NewBox = c.NewBox }).ToList();
            if (index < 0 || index >= list.Count) return list;
            bool startedBox = list[index].NewBox;
            list.RemoveAt(index);
            if (startedBox && index < list.Count) list[index].NewBox = true;
            return list;
        }
    }
}
