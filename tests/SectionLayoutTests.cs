using System;
using System.IO;
using System.Linq;
using LayoutStudio.Core;

public static class SectionLayoutTests
{
    private static Schema Schema_()
    {
        return Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
    }

    private static SectionPlacement Of(System.Collections.Generic.IList<SectionPlacement> all, string id)
    {
        return all.Single(p => p.Id == id);
    }

    public static void Defaults_put_every_section_where_the_theme_puts_it()
    {
        var s = Schema_();
        var all = SectionLayout.Compute(new ConstantStore(s.AllConstants()), s);
        Assert.Equal(18, all.Count);
        Assert.Equal(0, Of(all, "Notes").Column);
        Assert.Equal(0, Of(all, "Notes").Row);
        Assert.Equal(0, Of(all, "NewsViewer").Column);
        Assert.Equal(5, Of(all, "NewsViewer").Row, "its order constant in the left column");
        Assert.Equal(1, Of(all, "HowLongToBeat").Column);
        Assert.Equal(0, Of(all, "HowLongToBeat").Row);
        Assert.Equal(1, Of(all, "ReviewViewer").Column);
        Assert.Equal(5, Of(all, "ReviewViewer").Row);
        Assert.Equal(11, all.Count(p => p.Column == 0));
        Assert.Equal(7, all.Count(p => p.Column == 1));
    }

    public static void A_section_moved_to_the_other_column_takes_the_row_of_that_column()
    {
        var s = Schema_();
        var store = new ConstantStore(s.AllConstants());
        Assert.True(WireframeModel.Move(store, s, "NewsViewer", 1, 0));
        var all = SectionLayout.Compute(store, s);
        Assert.Equal(1, Of(all, "NewsViewer").Column);
        Assert.Equal(0, Of(all, "NewsViewer").Row);
        Assert.Equal(1, Of(all, "HowLongToBeat").Row, "the sections already there shift down");
        var left = all.Where(p => p.Column == 0).Select(p => p.Row).OrderBy(r => r).ToArray();
        Assert.Equal("0,1,2,3,4,5,6,7,8,9", string.Join(",", left), "the left column closed the gap");
        var right = all.Where(p => p.Column == 1).Select(p => p.Row).OrderBy(r => r).ToArray();
        Assert.Equal("0,1,2,3,4,5,6,7", string.Join(",", right));
    }

    public static void A_right_section_moved_left_comes_before_the_ones_it_was_dropped_on()
    {
        var s = Schema_();
        var store = new ConstantStore(s.AllConstants());
        Assert.True(WireframeModel.Move(store, s, "HowLongToBeat", 0, 0));
        var all = SectionLayout.Compute(store, s);
        Assert.Equal(0, Of(all, "HowLongToBeat").Column);
        Assert.Equal(0, Of(all, "HowLongToBeat").Row);
        Assert.Equal(1, Of(all, "Notes").Row);
        Assert.Equal(0, Of(all, "Achievements").Row, "the right column renumbered without it");
    }

    public static void Rows_stay_unique_in_each_column_after_many_moves()
    {
        var s = Schema_();
        var store = new ConstantStore(s.AllConstants());
        var rng = new Random(42);
        var ids = s.Sections.Select(d => d.Id).ToArray();
        for (int step = 0; step < 300; step++)
        {
            WireframeModel.Move(store, s, ids[rng.Next(ids.Length)], rng.Next(2), rng.Next(20));
            var all = SectionLayout.Compute(store, s);
            Assert.Equal(18, all.Count, "no section is lost");
            foreach (var column in new[] { 0, 1 })
            {
                var rows = all.Where(p => p.Column == column).Select(p => p.Row).ToList();
                Assert.Equal(rows.Count, rows.Distinct().Count(), "rows are unique in column " + column + " at step " + step);
                Assert.True(rows.All(r => r >= 0 && r <= 17), "rows fit the 32 row definitions");
            }
        }
    }

    public static void The_default_columns_are_the_ones_the_flags_say()
    {
        var s = Schema_();
        var store = new ConstantStore(s.AllConstants());
        var all = SectionLayout.Compute(store, s);
        foreach (var d in s.Sections)
            Assert.Equal(store.Get(d.ColumnKey) == "True" ? 1 : 0, Of(all, d.Id).Column, d.Id + " follows its column flag");
    }
}
