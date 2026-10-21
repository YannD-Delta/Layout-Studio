using System.Collections.Generic;
using System.Linq;
using LayoutStudio.Core;

/// <summary>The header cards as one flat list: 12 boxes of up to 4 cards, read and packed back without changing how they look.</summary>
public static class CardLayoutTests
{
    private static readonly string[] Slots = Enumerable.Range(1, 12).Select(n => "HeaderTileSlot" + n).ToArray();
    private static readonly string[] Extras = Enumerable.Range(1, 12).SelectMany(n => new[] { "B", "C", "D" }.Select(s => "HeaderTileSlot" + n + s)).ToArray();

    private static Dictionary<string, string> Values(params string[] set)
    {
        var d = Slots.Concat(Extras).ToDictionary(k => k, k => "0");
        for (int i = 0; i + 1 < set.Length; i += 2) d[set[i]] = set[i + 1];
        return d;
    }

    private static string Show(IEnumerable<CardEntry> cards) { return string.Join(" ", cards.Select(c => (c.NewBox ? "|" : "") + c.Tile)); }

    public static void Reading_walks_the_boxes_in_order_and_skips_holes()
    {
        var v = Values("HeaderTileSlot1", "1", "HeaderTileSlot1C", "9", "HeaderTileSlot3", "5", "HeaderTileSlot12", "7");
        var cards = CardLayout.Read(k => v[k], Slots, Extras);
        Assert.Equal("|1 9 |5 |7", Show(cards), "a hole in a box leaves no gap; the first card of each box starts it");
    }

    public static void Packing_gives_back_what_was_read_for_a_layout_without_holes()
    {
        var v = Values("HeaderTileSlot1", "1", "HeaderTileSlot1B", "2", "HeaderTileSlot2", "3", "HeaderTileSlot3", "4", "HeaderTileSlot3B", "5", "HeaderTileSlot3C", "6", "HeaderTileSlot3D", "7");
        var cards = CardLayout.Read(k => v[k], Slots, Extras);
        var packed = CardLayout.Pack(cards, Slots, Extras);
        foreach (var kv in v) Assert.Equal(kv.Value, packed[kv.Key], kv.Key);
    }

    public static void Packing_moves_a_lone_card_of_a_late_box_to_the_first_free_box()
    {
        var cards = new List<CardEntry> { new CardEntry { Tile = 4, NewBox = true }, new CardEntry { Tile = 9, NewBox = true } };
        var packed = CardLayout.Pack(cards, Slots, Extras);
        Assert.Equal("4", packed["HeaderTileSlot1"]);
        Assert.Equal("9", packed["HeaderTileSlot2"]);
        Assert.Equal("0", packed["HeaderTileSlot3"]);
    }

    public static void A_fifth_card_of_a_box_starts_the_next_one()
    {
        var cards = Enumerable.Range(1, 5).Select(i => new CardEntry { Tile = i, NewBox = i == 1 }).ToList();
        Assert.Equal(2, CardLayout.BoxesNeeded(cards));
        var packed = CardLayout.Pack(cards, Slots, Extras);
        Assert.Equal("4", packed["HeaderTileSlot1D"]);
        Assert.Equal("5", packed["HeaderTileSlot2"]);
    }

    public static void More_cards_than_the_theme_can_hold_are_refused()
    {
        var cards = Enumerable.Range(1, 13).Select(i => new CardEntry { Tile = i, NewBox = true }).ToList();
        Assert.Equal(13, CardLayout.BoxesNeeded(cards));
        Assert.Equal(null, CardLayout.Pack(cards, Slots, Extras));
        Assert.True(CardLayout.Pack(cards.Take(12).ToList(), Slots, Extras) != null);
    }

    public static void Moving_a_card_keeps_the_shape_of_the_boxes()
    {
        var cards = new List<CardEntry>
        {
            new CardEntry { Tile = 1, NewBox = true }, new CardEntry { Tile = 2 }, new CardEntry { Tile = 3, NewBox = true }
        };
        Assert.Equal("|3 1 |2", Show(CardLayout.Move(cards, 2, 0)), "the card goes first, the boxes keep their sizes");
        Assert.Equal("|2 1 |3", Show(CardLayout.Move(cards, 1, 0)), "swapping inside a box keeps the box");
        Assert.Equal("|1 2 |3", Show(CardLayout.Move(cards, 1, 1)), "same place: nothing changes");
        Assert.Equal("|1 2 |3", Show(cards), "the original list is not touched");
    }

    public static void Removing_a_card_that_starts_a_box_hands_the_start_to_the_next_card_of_the_box()
    {
        var cards = new List<CardEntry>
        {
            new CardEntry { Tile = 1, NewBox = true }, new CardEntry { Tile = 2 }, new CardEntry { Tile = 3, NewBox = true }
        };
        Assert.Equal("|2 |3", Show(CardLayout.Remove(cards, 0)));
        Assert.Equal("|1 |3", Show(CardLayout.Remove(cards, 1)));
        Assert.Equal("|1 2", Show(CardLayout.Remove(cards, 2)));
    }
}
