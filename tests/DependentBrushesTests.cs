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
}
