using System.Windows;
using System.Windows.Media;
using LayoutStudio.Core;
using LayoutStudio.Engine;

public static class WpfValuesTests
{
    public static void Double_and_Int_and_Boolean_convert()
    {
        Assert.Equal(0.55, (double)WpfValues.ToWpf(ConstantType.Double, "0.55"));
        Assert.Equal(7, (int)WpfValues.ToWpf(ConstantType.Int, "7"));
        Assert.Equal(true, (bool)WpfValues.ToWpf(ConstantType.Boolean, "True"));
    }

    public static void Thickness_with_one_two_or_four_numbers()
    {
        Assert.Equal(new Thickness(10), (Thickness)WpfValues.ToWpf(ConstantType.Thickness, "10"));
        Assert.Equal(new Thickness(10, 5, 10, 5), (Thickness)WpfValues.ToWpf(ConstantType.Thickness, "10,5"));
        Assert.Equal(new Thickness(1, 2, 3, 4), (Thickness)WpfValues.ToWpf(ConstantType.Thickness, "1,2,3,4"));
    }

    public static void CornerRadius_with_one_or_four_numbers()
    {
        Assert.Equal(new CornerRadius(14), (CornerRadius)WpfValues.ToWpf(ConstantType.CornerRadius, "14"));
        Assert.Equal(new CornerRadius(1, 2, 3, 4), (CornerRadius)WpfValues.ToWpf(ConstantType.CornerRadius, "1,2,3,4"));
    }

    public static void Visibility_converts_to_the_enum()
    {
        Assert.Equal(Visibility.Collapsed, (Visibility)WpfValues.ToWpf(ConstantType.Visibility, "Collapsed"));
        Assert.Equal(Visibility.Hidden, (Visibility)WpfValues.ToWpf(ConstantType.Visibility, "Hidden"));
    }

    public static void Color_converts_argb()
    {
        var c = (Color)WpfValues.ToWpf(ConstantType.Color, "#801E90FF");
        Assert.Equal((byte)0x80, c.A);
        Assert.Equal((byte)0x1E, c.R);
        Assert.Equal((byte)0x90, c.G);
        Assert.Equal((byte)0xFF, c.B);
    }

    public static void String_passes_through()
    {
        Assert.Equal("Segoe UI", (string)WpfValues.ToWpf(ConstantType.String, "Segoe UI"));
    }

    public static void Unnormalized_input_throws_a_clear_exception()
    {
        Assert.Throws<System.FormatException>(() => WpfValues.ToWpf(ConstantType.Thickness, "1,2,3"));
        Assert.Throws<System.FormatException>(() => WpfValues.ToWpf(ConstantType.Double, "abc"));
    }
}
