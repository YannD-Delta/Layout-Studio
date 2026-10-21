using System.Globalization;
using System.IO;
using System.Linq;
using LayoutStudio.Core;

public static class SectionOrderMigrationTests
{
    // Links took row 0 of both columns and every other default moved down by one: a profile saved before (Version 1) keeps its own numbers
    // one row further so its order stays what it was, and is stamped Version 2 so that happens once.
    private static ConstantStore Apply(Profile profile, out Schema schema)
    {
        schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        var store = new ConstantStore(schema.AllConstants());
        new ProfileApplier(schema, store, new FakePresetFiles(), "theme").ApplyProfile(profile);
        return store;
    }

    public static void A_version_1_profile_has_its_section_orders_moved_down_by_one()
    {
        var p = new Profile { Name = "old", Version = 1 };
        p.Overrides["DetailsViewSectionOrderGameActivity"] = "1";
        p.Overrides["DetailsViewSectionOrderRightGenres"] = "3";
        p.Overrides["DetailsViewSectionOrderLeftHowLongToBeat"] = "8";
        Schema schema;
        var store = Apply(p, out schema);
        Assert.Equal("2", store.Get("DetailsViewSectionOrderGameActivity"));
        Assert.Equal("4", store.Get("DetailsViewSectionOrderRightGenres"));
        Assert.Equal("9", store.Get("DetailsViewSectionOrderLeftHowLongToBeat"));
        Assert.Equal(2, p.Version, "stamped so it is not shifted again");
        Assert.Equal("2", p.Overrides["DetailsViewSectionOrderGameActivity"]);
    }

    public static void A_version_2_profile_is_left_as_it_is()
    {
        var p = new Profile { Name = "new", Version = 2 };
        p.Overrides["DetailsViewSectionOrderGameActivity"] = "1";
        Schema schema;
        var store = Apply(p, out schema);
        Assert.Equal("1", store.Get("DetailsViewSectionOrderGameActivity"));
        Assert.Equal(2, p.Version);
    }

    public static void Applying_a_migrated_profile_twice_does_not_shift_it_again()
    {
        var p = new Profile { Name = "old", Version = 1 };
        p.Overrides["DetailsViewSectionOrderVideo"] = "2";
        Schema schema;
        Apply(p, out schema);
        var store = Apply(p, out schema);
        Assert.Equal("3", store.Get("DetailsViewSectionOrderVideo"));
    }

    public static void Other_overrides_are_not_touched_by_the_migration()
    {
        var p = new Profile { Name = "old", Version = 1 };
        p.Overrides["WindowGlassBlurRadius"] = "50";
        p.Overrides["HeaderTileSlot1"] = "3";
        Schema schema;
        var store = Apply(p, out schema);
        Assert.Equal("50", store.Get("WindowGlassBlurRadius"));
        Assert.Equal("3", store.Get("HeaderTileSlot1"));
    }

    public static void A_profile_for_a_theme_without_links_is_not_migrated()
    {
        // the Fullscreen theme (or an older Modula) has no Links section: nothing moves and the version stays
        var schema = Schema.Load(TestSchema.Json);
        var p = new Profile { Name = "other", Version = 1 };
        p.Overrides["OrderNotes"] = "1";
        var store = new ConstantStore(schema.AllConstants());
        new ProfileApplier(schema, store, new FakePresetFiles(), "theme").ApplyProfile(p);
        Assert.Equal(1, p.Version);
        Assert.Equal("1", store.Get("OrderNotes"));
    }
}
