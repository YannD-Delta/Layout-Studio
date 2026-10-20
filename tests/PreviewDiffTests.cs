using System.Collections.Generic;
using LayoutStudio.Core;

public static class PreviewDiffTests
{
    private static byte[] Frame(int w, int h, byte value)
    {
        var f = new byte[w * h * 4];
        for (int i = 0; i < f.Length; i++) f[i] = value;
        return f;
    }

    private static void Paint(byte[] frame, int w, int x0, int y0, int x1, int y1, byte value)
    {
        for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) for (int c = 0; c < 3; c++) frame[(y * w + x) * 4 + c] = value;
    }

    public static void Identical_pictures_have_no_difference()
    {
        var box = PreviewDiff.Box(new List<byte[]> { Frame(100, 50, 10), Frame(100, 50, 10), Frame(100, 50, 10) }, 100, 50);
        Assert.True(box.IsEmpty, "nothing differs: " + box);
    }

    public static void A_single_picture_has_nothing_to_compare()
    {
        Assert.True(PreviewDiff.Box(new List<byte[]> { Frame(100, 50, 10) }, 100, 50).IsEmpty);
    }

    public static void The_box_holds_the_part_that_changes_with_a_margin()
    {
        var a = Frame(400, 140, 20);
        var b = Frame(400, 140, 20);
        Paint(b, 400, 200, 60, 260, 100, 200);
        var box = PreviewDiff.Box(new List<byte[]> { a, b }, 400, 140, pad: 8, minWidth: 10, minHeight: 10);
        Assert.Equal(192, box.X);
        Assert.Equal(52, box.Y);
        Assert.Equal(76, box.Width);
        Assert.Equal(56, box.Height);
    }

    public static void Differences_between_any_two_pictures_count()
    {
        var a = Frame(200, 100, 20);
        var b = Frame(200, 100, 20);
        var c = Frame(200, 100, 20);
        Paint(b, 200, 10, 10, 20, 20, 200);
        Paint(c, 200, 150, 70, 160, 80, 200);
        var box = PreviewDiff.Box(new List<byte[]> { a, b, c }, 200, 100, pad: 0, minWidth: 1, minHeight: 1);
        Assert.Equal(10, box.X);
        Assert.Equal(10, box.Y);
        Assert.Equal(150, box.Width);
        Assert.Equal(70, box.Height);
    }

    public static void A_tiny_change_is_shown_in_a_box_big_enough_to_see_and_inside_the_picture()
    {
        var a = Frame(400, 140, 20);
        var b = Frame(400, 140, 20);
        Paint(b, 400, 396, 136, 400, 140, 200);   // the bottom right corner
        var box = PreviewDiff.Box(new List<byte[]> { a, b }, 400, 140);
        Assert.True(box.Width >= 120 && box.Height >= 56, "at least the minimum size: " + box);
        Assert.True(box.X >= 0 && box.Y >= 0 && box.X + box.Width <= 400 && box.Y + box.Height <= 140, "inside the picture: " + box);
        Assert.True(box.X + box.Width == 400 && box.Y + box.Height == 140, "the corner is in the box: " + box);
    }

    public static void A_subtle_but_real_change_of_five_levels_per_channel_is_found()
    {
        var a = Frame(50, 50, 100);
        var b = Frame(50, 50, 100);
        Paint(b, 50, 10, 10, 20, 20, 105);
        Assert.False(PreviewDiff.Box(new List<byte[]> { a, b }, 50, 50, minWidth: 1, minHeight: 1).IsEmpty);
    }

    public static void Faint_noise_below_the_threshold_is_ignored()
    {
        var a = Frame(50, 50, 100);
        var b = Frame(50, 50, 103);
        Assert.True(PreviewDiff.Box(new List<byte[]> { a, b }, 50, 50).IsEmpty);
    }
}
