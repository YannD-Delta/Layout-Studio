using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

public static class BackgroundModeTests
{
    private class Env
    {
        public Schema Schema;
        public ConstantStore Store;
        public History History;
        public BackgroundEditorViewModel Editor;
    }

    private static Env Make()
    {
        var e = new Env();
        e.Schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        e.Store = new ConstantStore(e.Schema.AllConstants());
        e.History = new History(e.Store);
        var profile = new Profile { Name = "p" };
        var applier = new ProfileApplier(e.Schema, e.Store, new FakePresetFiles(), "theme");
        e.Editor = new BackgroundEditorViewModel(e.Store, e.Schema, e.History, applier, profile, k => null, new CultureInfo("en-US"), null);
        return e;
    }

    public static void The_default_look_is_the_artwork_behind_the_whole_window()
    {
        var e = Make();
        Assert.Equal("window", e.Editor.Mode);
        Assert.True(e.Editor.IsModeWindow && !e.Editor.IsModePage && !e.Editor.IsModeOff);
        Assert.True(e.Editor.PreviewEnabled);
    }

    public static void Page_only_turns_the_window_layer_off_and_keeps_the_page_layer()
    {
        var e = Make();
        e.Editor.SetPageCommand.Execute(null);
        Assert.Equal("False", e.Store.Get("WindowGlassEnabled"));
        Assert.Equal("page", e.Editor.Mode);
        Assert.True(double.Parse(e.Store.Get("PageGlassOpacity"), CultureInfo.InvariantCulture) > 0, "the page keeps its own artwork");
        Assert.True(e.Editor.PreviewEnabled);
    }

    public static void Off_removes_the_artwork_from_both_layers()
    {
        var e = Make();
        e.Editor.SetOffCommand.Execute(null);
        Assert.Equal("False", e.Store.Get("WindowGlassEnabled"));
        Assert.Equal("0", e.Store.Get("PageGlassOpacity"));
        Assert.Equal("off", e.Editor.Mode);
        Assert.False(e.Editor.PreviewEnabled, "the preview shows no artwork");
    }

    public static void Coming_back_from_off_to_the_page_restores_its_artwork()
    {
        var e = Make();
        e.Editor.SetOffCommand.Execute(null);
        e.Editor.SetPageCommand.Execute(null);
        Assert.Equal("page", e.Editor.Mode);
        Assert.Equal(e.Store.GetDefault("PageGlassOpacity"), e.Store.Get("PageGlassOpacity"));
        e.Editor.SetWindowCommand.Execute(null);
        Assert.Equal("window", e.Editor.Mode);
    }

    public static void A_mode_switch_is_one_undo_step()
    {
        var e = Make();
        e.Editor.SetOffCommand.Execute(null);
        e.History.Undo();
        Assert.Equal("window", e.Editor.Mode);
        Assert.Equal(e.Store.GetDefault("PageGlassOpacity"), e.Store.Get("PageGlassOpacity"));
        Assert.False(e.History.CanUndo);
    }

    public static void The_sliders_list_no_longer_repeats_the_on_off_switch()
    {
        var e = Make();
        Assert.False(e.Editor.Sliders.Any(s => s.Kind == "toggle"));
        Assert.Equal(7, e.Editor.Sliders.Count, "blur, darkness and the five zones");
    }

    public static void The_sliders_act_on_the_layer_that_is_showing()
    {
        var e = Make();
        e.Editor.SetPageCommand.Execute(null);
        var blur = e.Editor.Sliders.Single(s => s.Id == "PageGlassBlurRadius");
        var dark = e.Editor.Sliders.Single(s => s.Id == "PageGlassScrimOpacity");
        Assert.False(e.Editor.Sliders.Any(s => s.Id == "WindowGlassBlurRadius"), "the window layer is hidden in page mode");
        Assert.True(blur != null && dark != null);
        string error;
        e.Store.TrySet("PageGlassBlurRadius", "0", out error);
        e.Store.TrySet("PageGlassScrimOpacity", "0", out error);
        Assert.Equal(0.0, e.Editor.PreviewBlurRadius);
        Assert.Equal(0.0, e.Editor.PreviewScrimOpacity);
        e.Editor.SetWindowCommand.Execute(null);
        Assert.True(e.Editor.Sliders.Any(s => s.Id == "WindowGlassBlurRadius"), "the window layer is back in window mode");
        Assert.False(e.Editor.Sliders.Any(s => s.Id == "PageGlassBlurRadius"));
    }
}
