using System;
using System.Windows.Media;
using LayoutStudio.Engine;

public static class DependentBrushesTests
{
    public static void Rebuild_sets_opacity_on_a_copy()
    {
        var original = new SolidColorBrush(Colors.Red) { Opacity = 0.4 };
        var rebuilt = (SolidColorBrush)DependentBrushes.Rebuild(original, "Opacity", 1.0);
        Assert.Equal(1.0, rebuilt.Opacity);
        Assert.Equal(Colors.Red, rebuilt.Color, "other properties are kept");
        Assert.Equal(0.4, original.Opacity, "the original brush is not modified");
        Assert.False(ReferenceEquals(original, rebuilt), "a new instance, so DynamicResource consumers see a new resource");
    }

    public static void Rebuild_sets_color_on_a_copy()
    {
        var original = new SolidColorBrush(Colors.Red) { Opacity = 0.25 };
        var rebuilt = (SolidColorBrush)DependentBrushes.Rebuild(original, "Color", Colors.Blue);
        Assert.Equal(Colors.Blue, rebuilt.Color);
        Assert.Equal(0.25, rebuilt.Opacity);
    }

    public static void Rebuild_works_on_a_frozen_brush()
    {
        var original = new SolidColorBrush(Colors.Green) { Opacity = 0.5 };
        original.Freeze();
        var rebuilt = (SolidColorBrush)DependentBrushes.Rebuild(original, "Opacity", 0.9);
        Assert.Equal(0.9, rebuilt.Opacity);
        Assert.False(rebuilt.IsFrozen, "the copy is editable");
    }

    public static void Rebuild_with_unknown_property_throws()
    {
        Assert.Throws<ArgumentException>(() => DependentBrushes.Rebuild(new SolidColorBrush(Colors.Red), "Nope", 1.0));
    }

    public static void Rebuild_with_wrong_value_type_throws()
    {
        Assert.Throws<ArgumentException>(() => DependentBrushes.Rebuild(new SolidColorBrush(Colors.Red), "Opacity", "high"));
    }

    public static void Rebuild_sets_one_gradient_stop_on_a_copy()
    {
        var original = new LinearGradientBrush(Colors.Red, Colors.Blue, 0);
        original.GradientStops.Add(new GradientStop(Colors.Green, 1));
        var rebuilt = (LinearGradientBrush)DependentBrushes.Rebuild(original, "GradientStops[1].Color", Colors.Yellow);
        Assert.Equal(Colors.Yellow, rebuilt.GradientStops[1].Color);
        Assert.Equal(Colors.Red, rebuilt.GradientStops[0].Color, "the other stops are kept");
        Assert.Equal(Colors.Blue, original.GradientStops[1].Color, "the original brush is not modified");
        Assert.Equal(3, rebuilt.GradientStops.Count);
    }

    public static void Rebuild_gradient_stop_out_of_range_or_on_a_solid_brush_throws()
    {
        var gradient = new LinearGradientBrush(Colors.Red, Colors.Blue, 0);
        Assert.Throws<ArgumentException>(() => DependentBrushes.Rebuild(gradient, "GradientStops[5].Color", Colors.Yellow));
        Assert.Throws<ArgumentException>(() => DependentBrushes.Rebuild(new SolidColorBrush(Colors.Red), "GradientStops[0].Color", Colors.Yellow));
        Assert.Throws<ArgumentException>(() => DependentBrushes.Rebuild(gradient, "GradientStops[0].Color", 0.5));
    }

    public static void Read_returns_properties_and_stops()
    {
        var gradient = new LinearGradientBrush(Colors.Red, Colors.Blue, 0) { Opacity = 0.3 };
        Assert.Equal(Colors.Blue, (Color)DependentBrushes.Read(gradient, "GradientStops[1].Color"));
        Assert.Equal(0.3, (double)DependentBrushes.Read(gradient, "Opacity"));
        Assert.True(DependentBrushes.Read(gradient, "GradientStops[9].Color") == null, "no such stop");
        Assert.True(DependentBrushes.Read(gradient, "Nope") == null, "no such property");
    }

    public static void Sync_copies_only_what_differs_and_keeps_the_original_otherwise()
    {
        var original = new LinearGradientBrush(Colors.Red, Colors.Blue, 0);
        var same = DependentBrushes.Sync(original, new[] { new System.Collections.Generic.KeyValuePair<string, object>("GradientStops[0].Color", Colors.Red) });
        Assert.True(ReferenceEquals(original, same), "nothing differs: no copy");
        var changed = (LinearGradientBrush)DependentBrushes.Sync(original, new[]
        {
            new System.Collections.Generic.KeyValuePair<string, object>("GradientStops[0].Color", Colors.Lime),
            new System.Collections.Generic.KeyValuePair<string, object>("Opacity", 0.5),
            new System.Collections.Generic.KeyValuePair<string, object>("GradientStops[1].Color", "not a color"),
        });
        Assert.False(ReferenceEquals(original, changed), "a copy");
        Assert.Equal(Colors.Lime, changed.GradientStops[0].Color);
        Assert.Equal(0.5, changed.Opacity);
        Assert.Equal(Colors.Blue, changed.GradientStops[1].Color, "a value of the wrong type is skipped");
        Assert.Equal(Colors.Red, original.GradientStops[0].Color, "the original is untouched");
    }
}
