using LayoutStudio.Core;

public static class HistoryTests
{
    private static ConstantStore Store()
    {
        return new ConstantStore(new[]
        {
            new ConstantDef { Key = "blur", Type = ConstantType.Double, Default = "30", Min = 0, Max = 80 },
            new ConstantDef { Key = "scrim", Type = ConstantType.Double, Default = "0.55", Min = 0, Max = 1 },
        });
    }

    private static void Set(ConstantStore s, string key, string value)
    {
        string error;
        Assert.True(s.TrySet(key, value, out error), error);
    }

    public static void Undo_restores_previous()
    {
        var s = Store();
        var h = new History(s);
        Set(s, "blur", "10");
        Assert.True(h.CanUndo);
        h.Undo();
        Assert.Equal("30", s.Get("blur"));
        Assert.False(h.CanUndo);
    }

    public static void Redo_after_undo()
    {
        var s = Store();
        var h = new History(s);
        Set(s, "blur", "10");
        h.Undo();
        Assert.True(h.CanRedo);
        h.Redo();
        Assert.Equal("10", s.Get("blur"));
        Assert.False(h.CanRedo);
    }

    public static void New_change_clears_redo()
    {
        var s = Store();
        var h = new History(s);
        Set(s, "blur", "10");
        h.Undo();
        Set(s, "blur", "20");
        Assert.False(h.CanRedo);
    }

    public static void Undo_and_redo_do_not_record_themselves()
    {
        var s = Store();
        var h = new History(s);
        Set(s, "blur", "10");
        h.Undo();
        Assert.False(h.CanUndo, "undoing must not push a new step");
        h.Redo();
        h.Undo();
        Assert.Equal("30", s.Get("blur"));
        Assert.False(h.CanUndo);
    }

    public static void Max_depth_drops_oldest()
    {
        var s = Store();
        var h = new History(s, 3);
        Set(s, "blur", "1");
        Set(s, "blur", "2");
        Set(s, "blur", "3");
        Set(s, "blur", "4");
        h.Undo();
        h.Undo();
        h.Undo();
        Assert.False(h.CanUndo, "only three steps are kept");
        Assert.Equal("1", s.Get("blur"), "the first change can no longer be undone");
    }

    public static void Batch_groups_one_drag_into_one_step()
    {
        var s = Store();
        var h = new History(s);
        h.BeginBatch();
        Set(s, "blur", "10");
        Set(s, "blur", "20");
        Set(s, "scrim", "0.3");
        h.EndBatch();
        h.Undo();
        Assert.Equal("30", s.Get("blur"), "the first 'before' wins");
        Assert.Equal("0.55", s.Get("scrim"));
        Assert.False(h.CanUndo, "one drag is one step");
    }

    public static void Unbalanced_begin_is_recovered_by_EndAllBatches()
    {
        var s = Store();
        var h = new History(s);
        h.BeginBatch();
        h.BeginBatch();            // a drag started twice and never completed (focus lost, window closed)
        Set(s, "blur", "10");
        h.EndAllBatches();
        Assert.True(h.CanUndo, "what was done before the window closed is one undo step");
        h.Undo();
        Assert.Equal("30", s.Get("blur"));
        Set(s, "blur", "15");
        Assert.True(h.CanUndo, "later changes are recorded again");
    }

    public static void Empty_batch_records_nothing()
    {
        var s = Store();
        var h = new History(s);
        h.BeginBatch();
        h.EndBatch();
        Assert.False(h.CanUndo);
    }

    public static void Same_value_set_again_is_not_a_step()
    {
        var s = Store();
        var h = new History(s);
        Set(s, "blur", "30");
        Assert.False(h.CanUndo);
    }

    public static void Clear_forgets_everything()
    {
        var s = Store();
        var h = new History(s);
        Set(s, "blur", "10");
        h.Clear();
        Assert.False(h.CanUndo);
        Assert.False(h.CanRedo);
    }

    public static void An_action_step_runs_on_undo_and_redo_in_order()
    {
        var store = new ConstantStore(new[] { new ConstantDef { Key = "a", Type = ConstantType.Double, Default = "1" } });
        var h = new History(store);
        var log = new System.Collections.Generic.List<string>();
        h.BeginBatch();
        string error;
        store.TrySet("a", "5", out error);
        h.RecordAction(() => log.Add("undo"), () => log.Add("redo"));
        h.EndBatch();
        Assert.True(h.CanUndo);
        h.Undo();
        Assert.Equal("1", store.Get("a"));
        Assert.Equal("undo", log[0]);
        h.Redo();
        Assert.Equal("5", store.Get("a"));
        Assert.Equal("redo", log[1]);
    }

    public static void An_action_alone_outside_a_batch_is_its_own_step()
    {
        var store = new ConstantStore(new[] { new ConstantDef { Key = "a", Type = ConstantType.Double, Default = "1" } });
        var h = new History(store);
        int undone = 0;
        h.RecordAction(() => undone++, () => { });
        Assert.True(h.CanUndo);
        h.Undo();
        Assert.Equal(1, undone);
        Assert.False(h.CanUndo);
    }
}
