using LayoutStudio.Core;

public static class SmokeTests
{
    public static void Add_adds_two_numbers()
    {
        Assert.Equal(1, Smoke.Add(1, 0));
        Assert.Equal(5, Smoke.Add(2, 3));
        Assert.Equal(-1, Smoke.Add(2, -3));
    }
}
