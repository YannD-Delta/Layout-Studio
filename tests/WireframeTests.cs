using System.IO;
using System.Linq;
using LayoutStudio.Core;

public static class WireframeTests
{
    private static Schema Schema_()
    {
        return Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
    }

    private static ConstantStore Store(Schema s)
    {
        return new ConstantStore(s.AllConstants());
    }

    private static string[] Ids(System.Collections.Generic.IEnumerable<SectionSlot> slots, int column)
    {
        return slots.Where(x => x.Column == column).OrderBy(x => x.Index).Select(x => x.Id).ToArray();
    }

    private static string[] OrderValues(ConstantStore store, Schema sc, int column)
    {
        return sc.Sections.Select(d => column == 0 ? d.LeftKey : d.RightKey).Where(k => k != null).Select(k => store.Get(k)).OrderBy(v => int.Parse(v)).ToArray();
    }

    public static void Sections_load_from_the_schema()
    {
        var s = Schema_();
        Assert.Equal(18, s.Sections.Count);
        Assert.Equal(4, s.Sections.Count(d => d.ColumnKey != null));
        Assert.True(s.Sections.All(d => d.LeftKey != null || d.RightKey != null), "every section has a place");
        Assert.True(s.Sections.All(d => (d.LeftKey == null || s.Find(d.LeftKey) != null) && (d.ShowKey == null || s.Find(d.ShowKey) != null)), "all keys are known constants");
    }

    public static void Columns_follow_the_order_constants()
    {
        var s = Schema_();
        var store = Store(s);
        var slots = WireframeModel.FromStore(store, s);
        Assert.Equal(18, slots.Count);
        var left = Ids(slots, 0);
        Assert.Equal("Notes", left[0]);
        Assert.Equal(11, left.Length, "seven fixed + four movable sections by default in the left column");
        var right = Ids(slots, 1);
        Assert.Equal(7, right.Length);
        Assert.True(right.Contains("HowLongToBeat") && right.Contains("Achievements"));
        for (int i = 1; i < left.Length; i++)
            Assert.True(int.Parse(store.Get(s.Sections.Single(d => d.Id == left[i - 1]).LeftKey)) < int.Parse(store.Get(s.Sections.Single(d => d.Id == left[i]).LeftKey)), "left column is sorted by its order constants");
    }

    public static void A_movable_section_in_the_right_column_uses_the_right_order_key()
    {
        var s = Schema_();
        var store = Store(s);
        string error;
        store.TrySet("DetailsViewGenresInRightColumn", "True", out error);
        var slots = WireframeModel.FromStore(store, s);
        Assert.True(Ids(slots, 1).Contains("Genres"));
        Assert.False(Ids(slots, 0).Contains("Genres"));
        Assert.True(slots.Single(x => x.Id == "Genres").CanMove);
        Assert.False(slots.Single(x => x.Id == "Notes").CanMove);
    }

    public static void Move_between_columns_keeps_unique_orders()
    {
        var s = Schema_();
        var store = Store(s);
        Assert.True(WireframeModel.Move(store, s, "Genres", 1, 0));
        Assert.Equal("True", store.Get("DetailsViewGenresInRightColumn"));
        var slots = WireframeModel.FromStore(store, s);
        Assert.Equal("Genres", Ids(slots, 1)[0]);
        var rightValues = OrderValues(store, s, 1);
        Assert.Equal(rightValues.Length, rightValues.Distinct().Count(), "right order numbers are unique");
        var leftValues = OrderValues(store, s, 0);
        Assert.Equal(leftValues.Length, leftValues.Distinct().Count(), "left order numbers are unique");
    }

    public static void Move_within_a_column_reorders_it()
    {
        var s = Schema_();
        var store = Store(s);
        Assert.True(WireframeModel.Move(store, s, "Tags", 0, 0));
        var left = Ids(WireframeModel.FromStore(store, s), 0);
        Assert.Equal("Tags", left[0]);
        Assert.Equal("Notes", left[1]);
        Assert.Equal("False", store.Get("DetailsViewTagsInRightColumn"));
    }

    public static void Move_to_the_same_position_changes_nothing()
    {
        var s = Schema_();
        var store = Store(s);
        Assert.False(WireframeModel.Move(store, s, "Notes", 0, 0));
        Assert.Equal(0, store.Changed().Count);
    }

    public static void A_fixed_section_cannot_change_column()
    {
        var s = Schema_();
        var store = Store(s);
        Assert.False(WireframeModel.Move(store, s, "Notes", 1, 0));
        Assert.False(WireframeModel.Move(store, s, "HowLongToBeat", 0, 0));
        Assert.Equal(0, store.Changed().Count);
    }

    public static void Hidden_sections_keep_their_slot()
    {
        var s = Schema_();
        var store = Store(s);
        var before = WireframeModel.FromStore(store, s).Single(x => x.Id == "Video");
        Assert.True(WireframeModel.SetVisible(store, s, "Video", false));
        var after = WireframeModel.FromStore(store, s).Single(x => x.Id == "Video");
        Assert.False(after.Visible);
        Assert.Equal(before.Index, after.Index);
        Assert.Equal(before.Column, after.Column);
    }

    public static void Sections_without_a_show_constant_cannot_be_hidden()
    {
        var s = Schema_();
        var store = Store(s);
        Assert.False(WireframeModel.SetVisible(store, s, "Genres", false));
        Assert.True(WireframeModel.FromStore(store, s).Single(x => x.Id == "Genres").Visible);
        Assert.False(WireframeModel.FromStore(store, s).Single(x => x.Id == "Genres").CanHide);
    }

    public static void Open_by_default_is_read_and_written()
    {
        var s = Schema_();
        var store = Store(s);
        Assert.True(WireframeModel.SetOpen(store, s, "Notes", true));
        Assert.True(WireframeModel.FromStore(store, s).Single(x => x.Id == "Notes").OpenByDefault);
        Assert.True(WireframeModel.SetOpen(store, s, "Notes", false));
        Assert.False(WireframeModel.FromStore(store, s).Single(x => x.Id == "Notes").OpenByDefault);
    }

    public static void Unknown_section_returns_false_without_changes()
    {
        var s = Schema_();
        var store = Store(s);
        Assert.False(WireframeModel.Move(store, s, "Nope", 0, 0));
        Assert.False(WireframeModel.SetVisible(store, s, "Nope", true));
        Assert.Equal(0, store.Changed().Count);
    }

    public static void Index_beyond_the_end_is_clamped()
    {
        var s = Schema_();
        var store = Store(s);
        Assert.True(WireframeModel.Move(store, s, "Notes", 0, 99));
        var left = Ids(WireframeModel.FromStore(store, s), 0);
        Assert.Equal("Notes", left[left.Length - 1]);
    }

    public static void Roundtrip_after_several_moves_matches_the_planned_layout()
    {
        var s = Schema_();
        var store = Store(s);
        WireframeModel.Move(store, s, "Genres", 1, 2);
        WireframeModel.Move(store, s, "Video", 0, 0);
        WireframeModel.Move(store, s, "Tags", 1, 0);
        var slots = WireframeModel.FromStore(store, s);
        var left = Ids(slots, 0);
        var right = Ids(slots, 1);
        Assert.Equal("Video", left[0]);
        Assert.Equal("Tags", right[0]);
        Assert.Equal("Genres", right[3], "Tags was inserted before it, so Genres moved from index 2 to 3");
        Assert.Equal(18, left.Length + right.Length);
    }
}
