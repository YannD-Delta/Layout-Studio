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

    private static Env Make()
    {
        var e = new Env();
        e.Schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
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
        var e = Make();
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

    public static void Blocks_follow_live_through_the_options_bridge()
    {
        var e = Make();
        Assert.False(e.Editor.Left.Single(b => b.Id == "Genres").NeedsRestart, "its column constant follows live through the Options bridge");
    }
}
