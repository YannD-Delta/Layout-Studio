using System.IO;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

public static class TileCellTests
{
    private class Env
    {
        public Schema Schema;
        public ConstantStore Store;
        public History History;
        public TilesEditorViewModel Tiles;
    }

    private static Env Make()
    {
        var e = new Env();
        e.Schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        e.Store = new ConstantStore(e.Schema.AllConstants());
        e.History = new History(e.Store);
        e.Tiles = new TilesEditorViewModel(e.Store, e.Schema, e.History);
        return e;
    }

    public static void Every_cell_has_four_card_constants_that_start_empty()
    {
        var e = Make();
        foreach (var slot in e.Tiles.Slots)
        {
            Assert.Equal(4, slot.Cards.Count);
            for (int p = 1; p < 4; p++) Assert.Equal("0", e.Store.Get(e.Tiles.CardKey(slot.Index, p)), "cell " + slot.Number + " card " + (p + 1));
        }
    }

    public static void The_next_list_appears_once_the_one_before_holds_a_card()
    {
        var e = Make();
        var cell = e.Tiles.Slots[0];
        Assert.True(cell.Cards[0].IsVisible);
        Assert.True(cell.Cards[1].IsVisible, "the second list shows as soon as the first holds a card");
        Assert.False(cell.Cards[2].IsVisible, "the third waits for the second");
        e.Tiles.AssignCard(0, 1, "AvgFps");
        Assert.True(cell.Cards[2].IsVisible, "and shows once the second holds a card");
        Assert.False(cell.Cards[3].IsVisible);
        Assert.Equal("AvgFps", cell.Cards[1].Selected.Id);
    }

    public static void Choosing_Empty_removes_that_card_and_the_ones_after_it()
    {
        var e = Make();
        e.Tiles.AssignCard(0, 1, "AvgFps");
        e.Tiles.AssignCard(0, 2, "Cpu");
        e.Tiles.AssignCard(0, 3, "Ram");
        e.Tiles.AssignCard(0, 1, "Empty");
        Assert.Equal("0", e.Store.Get(e.Tiles.CardKey(0, 1)));
        Assert.Equal("0", e.Store.Get(e.Tiles.CardKey(0, 2)));
        Assert.Equal("0", e.Store.Get(e.Tiles.CardKey(0, 3)));
        e.History.Undo();
        Assert.Equal("Ram", e.Tiles.Slots[0].Cards[3].Selected.Id, "one undo step brings the whole cell back");
    }

    public static void A_cell_can_hold_FPS_RAM_CPU_and_GPU_in_the_order_the_user_picks()
    {
        var e = Make();
        e.Tiles.AssignCard(0, 0, "AvgFps");
        e.Tiles.AssignCard(0, 1, "Ram");
        e.Tiles.AssignCard(0, 2, "Cpu");
        e.Tiles.AssignCard(0, 3, "Gpu");
        Assert.Equal("AvgFps,Ram,Cpu,Gpu", string.Join(",", e.Tiles.Slots[0].Cards.Select(c => c.Selected.Id)));
    }

    public static void Swapping_two_cells_swaps_all_their_cards()
    {
        var e = Make();
        e.Tiles.AssignCard(0, 1, "Ram");
        e.Tiles.AssignCard(1, 1, "Gpu");
        e.Tiles.Swap(0, 1);
        Assert.Equal("Gpu", e.Tiles.Slots[0].Cards[1].Selected.Id);
        Assert.Equal("Ram", e.Tiles.Slots[1].Cards[1].Selected.Id);
    }

    public static void Every_card_type_has_its_own_number()
    {
        var e = Make();
        var tiles = e.Schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).First(c => c.Kind == "tileSlots");
        var dup = tiles.Choices.GroupBy(c => c.Value).Where(g => g.Count() > 1).Select(g => g.Key + ": " + string.Join(", ", g.Select(c => c.Id)));
        Assert.Equal("", string.Join(" | ", dup));
    }

    public static void The_theme_reads_the_extra_cards_from_the_constants_the_schema_lists()
    {
        var e = Make();
        if (!Directory.Exists(Path.Combine(TestPaths.RepoRoot(), @"theme\Modula"))) return;   // the exported Layout Studio repository has no theme
        string xaml = File.ReadAllText(Path.Combine(TestPaths.RepoRoot(), @"theme\Modula\Views\DetailsViewGameOverview.xaml"));
        string constants = File.ReadAllText(Path.Combine(TestPaths.RepoRoot(), @"theme\Modula\Constants.xaml"));
        for (int n = 1; n <= 12; n++)
            foreach (string s in new[] { "", "B", "C", "D" })
            {
                Assert.True(xaml.Contains("{DynamicResource HeaderTileSlot" + n + s + "}"), "the theme binds HeaderTileSlot" + n + s);
                Assert.True(constants.Contains("x:Key=\"HeaderTileSlot" + n + s + "\""), "Constants.xaml declares HeaderTileSlot" + n + s);
            }
    }
}
