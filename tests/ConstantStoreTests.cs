using System.Globalization;
using LayoutStudio.Core;

public static class ConstantStoreTests
{
    private static ConstantStore Make()
    {
        return new ConstantStore(new[]
        {
            new ConstantDef { Key = "blur", Type = ConstantType.Double, Default = "30", Min = 0, Max = 80 },
            new ConstantDef { Key = "scrim", Type = ConstantType.Double, Default = "0.55", Min = 0, Max = 1 },
            new ConstantDef { Key = "slot", Type = ConstantType.Int, Default = "3", Min = 0, Max = 31 },
            new ConstantDef { Key = "margin", Type = ConstantType.Thickness, Default = "20,0,20,0" },
            new ConstantDef { Key = "glass", Type = ConstantType.Boolean, Default = "True" },
            new ConstantDef { Key = "accent", Type = ConstantType.Color, Default = "#FF1E90FF" },
            new ConstantDef { Key = "font", Type = ConstantType.String, Default = "Segoe UI" },
            new ConstantDef { Key = "radius", Type = ConstantType.CornerRadius, Default = "14" },
            new ConstantDef { Key = "vis", Type = ConstantType.Visibility, Default = "Collapsed" },
        });
    }

    public static void Double_clamps_to_max()
    {
        var s = Make();
        string error;
        Assert.True(s.TrySet("blur", "999", out error), "clamping is not an error");
        Assert.Equal("80", s.Get("blur"));
        Assert.True(s.TrySet("blur", "-5", out error));
        Assert.Equal("0", s.Get("blur"));
    }

    public static void Double_rejects_text()
    {
        var s = Make();
        string error;
        Assert.False(s.TrySet("blur", "abc", out error));
        Assert.True(error != null && error.Length > 0, "an error message is returned");
        Assert.Equal("30", s.Get("blur"));
    }

    public static void Double_accepts_french_comma_via_ui_parser()
    {
        var fr = new CultureInfo("fr-FR");
        Assert.Equal(0.55, ConstantValue.ParseUi("0,55", fr));
        Assert.Equal("0,55", ConstantValue.FormatUi(0.55, fr));
        var s = Make();
        string error;
        Assert.True(s.TrySet("scrim", ConstantValue.ToInvariant(ConstantValue.ParseUi("0,55", fr)), out error));
        Assert.Equal("0.55", s.Get("scrim"));
    }

    public static void Thickness_requires_1_2_or_4_numbers()
    {
        var s = Make();
        string error;
        Assert.True(s.TrySet("margin", "10", out error));
        Assert.Equal("10", s.Get("margin"));
        Assert.True(s.TrySet("margin", "10, 5", out error));
        Assert.Equal("10,5", s.Get("margin"));
        Assert.True(s.TrySet("margin", "1,2,3,4", out error));
        Assert.Equal("1,2,3,4", s.Get("margin"));
        Assert.False(s.TrySet("margin", "1,2,3", out error), "three numbers is not a Thickness");
        Assert.False(s.TrySet("margin", "a,b", out error));
        Assert.Equal("1,2,3,4", s.Get("margin"));
    }

    public static void Boolean_normalizes_True_false()
    {
        var s = Make();
        string error;
        Assert.True(s.TrySet("glass", "false", out error));
        Assert.Equal("False", s.Get("glass"));
        Assert.True(s.TrySet("glass", "TRUE", out error));
        Assert.Equal("True", s.Get("glass"));
        Assert.False(s.TrySet("glass", "yes", out error));
    }

    public static void Int_truncation_is_rejected_and_range_clamped()
    {
        var s = Make();
        string error;
        Assert.False(s.TrySet("slot", "2.5", out error), "an Int constant does not take decimals");
        Assert.True(s.TrySet("slot", "99", out error));
        Assert.Equal("31", s.Get("slot"));
    }

    public static void Color_is_normalized_to_upper_case_argb()
    {
        var s = Make();
        string error;
        Assert.True(s.TrySet("accent", "#1e90ff", out error));
        Assert.Equal("#FF1E90FF", s.Get("accent"));
        Assert.True(s.TrySet("accent", "#80ff0000", out error));
        Assert.Equal("#80FF0000", s.Get("accent"));
        Assert.False(s.TrySet("accent", "red", out error), "only hex colors are accepted");
    }

    public static void Unknown_key_is_rejected()
    {
        var s = Make();
        string error;
        Assert.False(s.TrySet("nope", "1", out error));
        Assert.Equal(null, s.Get("nope"));
    }

    public static void Reset_restores_default()
    {
        var s = Make();
        string error;
        s.TrySet("blur", "10", out error);
        s.Reset("blur");
        Assert.Equal("30", s.Get("blur"));
        Assert.Equal("30", s.GetDefault("blur"));
    }

    public static void Changed_lists_only_modified()
    {
        var s = Make();
        string error;
        s.TrySet("blur", "10", out error);
        s.TrySet("scrim", "0.55", out error);
        var changed = s.Changed();
        Assert.Equal(1, changed.Count);
        Assert.True(changed.Contains("blur"));
    }

    public static void ValueChanged_fires_once_per_real_change()
    {
        var s = Make();
        int fired = 0;
        s.ValueChanged += key => fired++;
        string error;
        s.TrySet("blur", "10", out error);
        s.TrySet("blur", "10", out error);
        Assert.Equal(1, fired);
    }

    public static void CornerRadius_takes_one_or_four_numbers()
    {
        var s = Make();
        string error;
        Assert.True(s.TrySet("radius", "8", out error));
        Assert.Equal("8", s.Get("radius"));
        Assert.True(s.TrySet("radius", "1, 2, 3, 4", out error));
        Assert.Equal("1,2,3,4", s.Get("radius"));
        Assert.False(s.TrySet("radius", "1,2", out error), "two numbers is not a CornerRadius");
        Assert.False(s.TrySet("radius", "-3", out error), "radii cannot be negative");
        Assert.Equal("1,2,3,4", s.Get("radius"));
    }

    public static void Visibility_accepts_the_three_keywords_case_insensitively()
    {
        var s = Make();
        string error;
        Assert.True(s.TrySet("vis", "visible", out error));
        Assert.Equal("Visible", s.Get("vis"));
        Assert.True(s.TrySet("vis", "HIDDEN", out error));
        Assert.Equal("Hidden", s.Get("vis"));
        Assert.False(s.TrySet("vis", "shown", out error));
    }

    public static void Thickness_and_corner_radius_reject_NaN_and_infinity()
    {
        var s = Make();
        string error;
        foreach (var bad in new[] { "NaN", "Infinity", "-Infinity", "1,NaN", "1,2,3,Infinity" })
        {
            Assert.False(s.TrySet("margin", bad, out error), "'" + bad + "' must be refused for a Thickness");
            Assert.False(s.TrySet("radius", bad, out error), "'" + bad + "' must be refused for a CornerRadius");
        }
        Assert.Equal("20,0,20,0", s.Get("margin"), "the value is untouched");
    }

    public static void Thickness_with_a_minimum_clamps_every_component()
    {
        var s = new ConstantStore(new[] { new ConstantDef { Key = "border", Type = ConstantType.Thickness, Default = "1", Min = 0 } });
        string error;
        Assert.True(s.TrySet("border", "-3,2,-1,5", out error));
        Assert.Equal("0,2,0,5", s.Get("border"), "a border cannot be negative: WPF would throw");
    }

    public static void Thickness_without_a_minimum_allows_negative_margins()
    {
        var s = Make();
        string error;
        Assert.True(s.TrySet("margin", "-4,0,0,0", out error));
        Assert.Equal("-4,0,0,0", s.Get("margin"));
    }
}
