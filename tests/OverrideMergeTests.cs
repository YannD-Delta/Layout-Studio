using System.IO;
using System.Linq;
using LayoutStudio.Core;

public static class OverrideMergeTests
{
    private static ConstantStore Store()
    {
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        return new ConstantStore(schema.AllConstants());
    }

    public static void Keys_the_theme_does_not_define_survive_a_save()
    {
        var store = Store();
        var profile = new Profile { Name = "Default" };
        profile.Overrides["GoneKey"] = "7";
        string error;
        store.TrySet("WindowGlassBlurRadius", "55", out error);
        int kept = OverrideMerge.Rebuild(profile, store);
        Assert.Equal(1, kept);
        Assert.Equal("7", profile.Overrides["GoneKey"]);
        Assert.Equal("55", profile.Overrides["WindowGlassBlurRadius"]);
    }

    public static void A_value_put_back_to_its_default_leaves_the_profile()
    {
        var store = Store();
        var profile = new Profile { Name = "Default" };
        profile.Overrides["WindowGlassBlurRadius"] = "55";
        OverrideMerge.Rebuild(profile, store);
        Assert.False(profile.Overrides.ContainsKey("WindowGlassBlurRadius"), "the store has the default: nothing to keep");
    }

    public static void Marked_subscribers_are_detached_and_the_older_ones_stay()
    {
        var store = Store();
        int calls = 0;
        store.ValueChanged += key => calls++;
        var release = store.MarkSubscribers();
        store.ValueChanged += key => calls += 100;
        Assert.Equal(2, store.SubscriberCount);
        release();
        Assert.Equal(1, store.SubscriberCount);
        string error;
        store.TrySet("WindowGlassBlurRadius", "40", out error);
        Assert.Equal(1, calls);
    }
}
