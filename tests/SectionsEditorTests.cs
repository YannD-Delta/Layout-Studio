using System.IO;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

public static class SectionsEditorTests
{
    private class Env
    {
        public Schema Schema;
        public ConstantStore Store;
        public History History;
        public SectionsEditorViewModel Editor;
    }

    private static Env Make(params string[] fixedSections)
    {
        var e = new Env();
        e.Schema = fixedSections.Length == 0 ? Schema.Load(File.ReadAllText(TestPaths.SchemaPath())) : Schema.Load(TestSections.SchemaJsonWithFixed(fixedSections));
        e.Store = new ConstantStore(e.Schema.AllConstants());
        e.History = new History(e.Store);
        e.Editor = new SectionsEditorViewModel(e.Store, e.Schema, e.History);
        return e;
    }

    public static void Both_columns_are_filled_from_the_store()
    {
        var e = Make();
        Assert.Equal(11, e.Editor.Left.Count);
        Assert.Equal(7, e.Editor.Right.Count);
        Assert.Equal("Notes", e.Editor.Left[0].Id);
    }

    public static void Drop_moves_a_block_between_columns()
    {
        var e = Make();
        e.Editor.Drop("Genres", 1, 0);
        Assert.Equal("Genres", e.Editor.Right[0].Id);
        Assert.False(e.Editor.Left.Any(b => b.Id == "Genres"));
        Assert.Equal("True", e.Store.Get("DetailsViewGenresInRightColumn"));
    }

    public static void A_drop_is_one_undo_step_and_the_lists_follow_the_undo()
    {
        var e = Make();
        e.Editor.Drop("Genres", 1, 0);
        e.History.Undo();
        Assert.Equal(11, e.Editor.Left.Count, "the left column is back");
        Assert.Equal(7, e.Editor.Right.Count);
        Assert.False(e.History.CanUndo, "one move was one step");
        e.History.Redo();
        Assert.Equal("Genres", e.Editor.Right[0].Id);
    }

    public static void Drop_of_a_fixed_section_on_the_other_column_is_ignored()
    {
        var e = Make("Notes");
        e.Editor.Drop("Notes", 1, 0);
        Assert.Equal("Notes", e.Editor.Left[0].Id);
        Assert.False(e.History.CanUndo);
    }

    public static void Drop_of_an_unknown_id_is_ignored()
    {
        var e = Make();
        e.Editor.Drop("Nope", 0, 0);
        e.Editor.Drop(null, 0, 0);
        Assert.False(e.History.CanUndo);
    }

    public static void Toggling_visibility_updates_the_block_and_the_store()
    {
        var e = Make();
        var video = e.Editor.Left.Single(b => b.Id == "Video");
        Assert.True(video.Visible);
        e.Editor.ToggleVisible(video);
        Assert.False(video.Visible);
        Assert.Equal("False", e.Store.Get("DetailsViewShowVideoPanel"));
        e.History.Undo();
        Assert.True(e.Editor.Left.Single(b => b.Id == "Video").Visible);
    }

    public static void Sections_that_cannot_be_hidden_ignore_the_toggle()
    {
        var e = Make();
        var genres = e.Editor.Left.Single(b => b.Id == "Genres");
        Assert.False(genres.CanHide);
        e.Editor.ToggleVisible(genres);
        Assert.True(genres.Visible);
        Assert.False(e.History.CanUndo);
    }

    public static void Toggling_open_by_default_writes_the_expand_constant()
    {
        var e = Make();
        var notes = e.Editor.Left.Single(b => b.Id == "Notes");
        bool was = notes.OpenByDefault;
        e.Editor.ToggleOpen(notes);
        Assert.Equal(!was, e.Editor.Left.Single(b => b.Id == "Notes").OpenByDefault);
    }

    public static void Insertion_index_is_computed_from_block_midpoints()
    {
        // heights 40 each, 3 blocks: [0-40) [40-80) [80-120); pointer above a block's middle inserts before it
        Assert.Equal(0, SectionsEditorViewModel.InsertionIndex(new[] { 40.0, 40.0, 40.0 }, 5));
        Assert.Equal(1, SectionsEditorViewModel.InsertionIndex(new[] { 40.0, 40.0, 40.0 }, 25));
        Assert.Equal(1, SectionsEditorViewModel.InsertionIndex(new[] { 40.0, 40.0, 40.0 }, 45));
        Assert.Equal(2, SectionsEditorViewModel.InsertionIndex(new[] { 40.0, 40.0, 40.0 }, 65));
        Assert.Equal(3, SectionsEditorViewModel.InsertionIndex(new[] { 40.0, 40.0, 40.0 }, 500));
        Assert.Equal(0, SectionsEditorViewModel.InsertionIndex(new double[0], 10), "an empty column inserts at 0");
    }

    public static void Blocks_know_their_column()
    {
        var e = Make();
        Assert.Equal(0, e.Editor.Left.Single(b => b.Id == "Notes").Column);
        Assert.Equal(1, e.Editor.Right.Single(b => b.Id == "HowLongToBeat").Column);
        e.Editor.Drop("Genres", 1, 0);
        Assert.Equal(1, e.Editor.Right.Single(b => b.Id == "Genres").Column);
    }

    public static void The_column_button_sends_a_movable_section_to_the_end_of_the_other_column()
    {
        var e = Make();
        var genres = e.Editor.Left.Single(b => b.Id == "Genres");
        Assert.True(genres.MoveColumnCommand.CanExecute(null));
        genres.MoveColumnCommand.Execute(null);
        Assert.Equal("Genres", e.Editor.Right[e.Editor.Right.Count - 1].Id);
        Assert.False(e.Editor.Left.Any(b => b.Id == "Genres"));
        Assert.Equal("True", e.Store.Get("DetailsViewGenresInRightColumn"));
        var back = e.Editor.Right.Single(b => b.Id == "Genres");
        back.MoveColumnCommand.Execute(null);
        Assert.Equal("Genres", e.Editor.Left[e.Editor.Left.Count - 1].Id, "and back to the end of the left column");
        e.History.Undo();
        Assert.Equal("Genres", e.Editor.Right[e.Editor.Right.Count - 1].Id, "each button press is one undo step");
    }

    public static void The_column_button_is_off_for_a_fixed_section()
    {
        var e = Make("Notes");
        var notes = e.Editor.Left.Single(b => b.Id == "Notes");
        Assert.False(notes.CanMove);
        Assert.False(notes.MoveColumnCommand.CanExecute(null));
        notes.MoveColumnCommand.Execute(null);
        Assert.False(e.History.CanUndo);
        Assert.Equal("Notes", e.Editor.Left[0].Id);
    }

    public static void CanDrop_refuses_a_fixed_section_on_the_other_column_only()
    {
        var e = Make("Notes", "HowLongToBeat");
        Assert.False(e.Editor.CanDrop("Notes", 1), "Notes is fixed in the left column");
        Assert.True(e.Editor.CanDrop("Notes", 0), "but it can be reordered in its own column");
        Assert.False(e.Editor.CanDrop("HowLongToBeat", 0));
        Assert.True(e.Editor.CanDrop("HowLongToBeat", 1));
        Assert.True(Make().Editor.CanDrop("Notes", 1), "with the real schema every section can change column");
        Assert.True(e.Editor.CanDrop("Genres", 1));
        Assert.True(e.Editor.CanDrop("Genres", 0));
        Assert.False(e.Editor.CanDrop("Nope", 0));
        Assert.False(e.Editor.CanDrop(null, 0));
        Assert.False(e.Editor.CanDrop("Genres", 2));
    }

    public static void The_drop_line_shows_above_the_block_at_the_insertion_index()
    {
        var e = Make();
        e.Editor.SetDropTarget("Genres", 1, 2);
        Assert.True(e.Editor.Right[2].DropLineAbove);
        Assert.Equal(1, e.Editor.Right.Count(b => b.DropLineAbove) + e.Editor.Left.Count(b => b.DropLineAbove) + (e.Editor.RightDropLineAtEnd ? 1 : 0) + (e.Editor.LeftDropLineAtEnd ? 1 : 0), "exactly one line");
        e.Editor.SetDropTarget("Genres", 1, 0);
        Assert.True(e.Editor.Right[0].DropLineAbove);
        Assert.False(e.Editor.Right[2].DropLineAbove, "the previous line is gone");
    }

    public static void The_drop_line_goes_after_the_last_block_when_the_index_is_past_the_end()
    {
        var e = Make();
        e.Editor.SetDropTarget("Genres", 1, e.Editor.Right.Count);
        Assert.True(e.Editor.RightDropLineAtEnd);
        Assert.False(e.Editor.LeftDropLineAtEnd);
        Assert.False(e.Editor.Right.Any(b => b.DropLineAbove));
        e.Editor.SetDropTarget("Notes", 0, 99);
        Assert.True(e.Editor.LeftDropLineAtEnd);
        Assert.False(e.Editor.RightDropLineAtEnd, "one line at a time");
    }

    public static void No_drop_line_where_the_drop_is_refused_and_ClearDropTarget_removes_it()
    {
        var e = Make("Notes");
        e.Editor.SetDropTarget("Notes", 1, 1);
        Assert.False(e.Editor.Right.Any(b => b.DropLineAbove) || e.Editor.RightDropLineAtEnd, "a fixed section cannot go there");
        e.Editor.SetDropTarget("Genres", 1, 1);
        Assert.True(e.Editor.Right[1].DropLineAbove);
        e.Editor.ClearDropTarget();
        Assert.False(e.Editor.Right.Any(b => b.DropLineAbove) || e.Editor.Left.Any(b => b.DropLineAbove) || e.Editor.RightDropLineAtEnd || e.Editor.LeftDropLineAtEnd);
    }

    public static void Dropping_down_inside_a_column_lands_at_the_drop_line_not_one_further()
    {
        // the index is a place among the blocks as they are shown (the dragged block included): before Features means between Categories and Features
        var e = Make();
        Assert.Equal("Features", e.Editor.Left[3].Id);
        e.Editor.Drop("Genres", 0, 3);
        Assert.Equal("Notes,Categories,Genres,Features", string.Join(",", e.Editor.Left.Take(4).Select(b => b.Id)));
        // and moving up is unchanged: before Categories (index 1)
        e.Editor.Drop("Genres", 0, 1);
        Assert.Equal("Notes,Genres,Categories,Features", string.Join(",", e.Editor.Left.Take(4).Select(b => b.Id)));
    }

    public static void Dropping_on_the_place_the_block_already_has_changes_nothing()
    {
        var e = Make();
        e.Editor.Drop("Genres", 0, 1);
        e.Editor.Drop("Genres", 0, 2);
        Assert.False(e.History.CanUndo, "just above itself or just below itself is no move");
    }

    public static void Blocks_follow_live_through_the_options_bridge()
    {
        var e = Make();
        Assert.False(e.Editor.Left.Single(b => b.Id == "Genres").NeedsRestart, "its column constant follows live through the Options bridge");
    }
}
