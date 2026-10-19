using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using LayoutStudio.Core;
using LayoutStudio.Engine;

public static class OptionsBridgeTests
{
    private static ConstantStore NewStore()
    {
        return new ConstantStore(Schema.Load(TestSchema.Json).AllConstants());
    }

    public static void Indexer_returns_the_typed_current_value()
    {
        var store = NewStore();
        var bridge = new OptionsBridge(store);
        Assert.Equal(30.0, (double)bridge["WindowGlassBlurRadius"], "double");
        Assert.Equal(true, (bool)bridge["WindowGlassEnabled"], "boolean");
        Assert.Equal(4, (int)bridge["SlotCount"], "int");
        Assert.Equal(new Thickness(20, 0, 20, 0), (Thickness)bridge["PageSideMargin"], "thickness");
        Assert.Equal(Visibility.Collapsed, (Visibility)bridge["CoverSubtitleVisibility"], "visibility");
    }

    public static void Indexer_follows_an_override()
    {
        var store = NewStore();
        var bridge = new OptionsBridge(store);
        string error;
        Assert.True(store.TrySet("WindowGlassBlurRadius", "55", out error));
        Assert.Equal(55.0, (double)bridge["WindowGlassBlurRadius"]);
    }

    public static void Unknown_key_gives_null_so_the_theme_fallback_applies()
    {
        var bridge = new OptionsBridge(NewStore());
        Assert.True(bridge["NoSuchConstant"] == null, "TargetNullValue/FallbackValue take over");
    }

    public static void A_change_raises_the_indexer_notification_bindings_listen_to()
    {
        var store = NewStore();
        var bridge = new OptionsBridge(store);
        var raised = new List<string>();
        ((INotifyPropertyChanged)bridge).PropertyChanged += (s, e) => raised.Add(e.PropertyName);
        string error;
        store.TrySet("SlotCount", "6", out error);
        Assert.True(raised.Contains("Item[]"), "'Item[]' makes every Options[...] binding re-read");
        Assert.True(raised.Contains("Item[SlotCount]"), "and the one for this key");
    }

    public static void A_value_that_did_not_change_raises_nothing()
    {
        var store = NewStore();
        var bridge = new OptionsBridge(store);
        int count = 0;
        ((INotifyPropertyChanged)bridge).PropertyChanged += (s, e) => count++;
        string error;
        store.TrySet("SlotCount", "4", out error);   // same as the default
        Assert.Equal(0, count);
    }

    public static void Reset_raises_too()
    {
        var store = NewStore();
        var bridge = new OptionsBridge(store);
        string error;
        store.TrySet("SlotCount", "9", out error);
        int count = 0;
        ((INotifyPropertyChanged)bridge).PropertyChanged += (s, e) => count++;
        store.Reset("SlotCount");
        Assert.True(count > 0, "reset is a change");
        Assert.Equal(4, (int)bridge["SlotCount"]);
    }
}
