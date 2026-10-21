using System.Collections.Generic;
using System.IO;
using System.Linq;
using LayoutStudio.Core;

public static class GlobalPresetsTests
{
    private static string ThemeDir() { return Path.GetDirectoryName(TestPaths.SchemaPath()); }
    private static Schema Schema_() { return Schema.Load(File.ReadAllText(TestPaths.SchemaPath())); }

    private static GlobalPresets Real() { return GlobalPresets.Load(ThemeDir()); }

    private static string Scratch()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ls-gp-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, GlobalPresets.Folder));
        return dir;
    }

    public static void A_theme_without_the_folder_has_no_global_preset()
    {
        var gp = GlobalPresets.Load(Scratch());
        Assert.True(!gp.Any && gp.Default == null);
    }

    public static void The_index_gives_the_default_and_the_order_and_a_broken_file_hides_nothing()
    {
        var dir = Scratch();
        var folder = Path.Combine(dir, GlobalPresets.Folder);
        File.WriteAllText(Path.Combine(folder, "index.json"), "{\"default\":\"B\",\"order\":[\"B\",\"A\"],\"private\":[\"A\"]}");
        File.WriteAllText(Path.Combine(folder, "A.json"), "{\"Id\":\"A\",\"Name\":{\"en\":\"Alpha\",\"fr\":\"Alpha\"},\"Presets\":{\"Corners\":\"Sharp\"},\"Overrides\":{\"Radius\":\"2\"}}");
        File.WriteAllText(Path.Combine(folder, "B.json"), "{\"Id\":\"B\",\"Name\":{\"en\":\"Beta\"}}");
        File.WriteAllText(Path.Combine(folder, "C.json"), "this is not json");
        var gp = GlobalPresets.Load(dir);
        Assert.Equal("B,A", string.Join(",", gp.Items.Select(p => p.Id)));
        Assert.Equal("B", gp.Default.Id);
        Assert.Equal("A", string.Join(",", gp.Private));
        Assert.Equal("Alpha", gp.Find("a").NameFor("fr"));
        Assert.Equal("Beta", gp.Find("B").NameFor("fr"));   // no French text: English
    }

    public static void The_default_falls_back_to_the_first_preset_when_the_index_names_none()
    {
        var dir = Scratch();
        File.WriteAllText(Path.Combine(dir, GlobalPresets.Folder, "Z.json"), "{\"Id\":\"Z\",\"Name\":{\"en\":\"Z\"}}");
        Assert.Equal("Z", GlobalPresets.Load(dir).Default.Id);
    }

    // DELTA is the owner's own configuration: the working repository has it, the public export does not (the tests then skip what is about it)
    public static void DELTA_is_the_first_and_the_default_and_private()
    {
        var gp = Real();
        if (gp.Find("DELTA") == null) { Assert.True(gp.Default != null && !gp.Private.Contains(gp.Default.Id), "the public default is a public preset"); return; }
        Assert.Equal("DELTA", gp.Default.Id);
        Assert.Equal("DELTA", gp.Items[0].Id);
        Assert.True(gp.Private.Contains("DELTA"));
    }

    public static void DELTA_holds_the_owner_look()
    {
        var delta = Real().Find("DELTA");
        if (delta == null) return;
        Assert.Equal(17, delta.Presets.Count);
        Assert.Equal("Violet", delta.Presets["ColorProfile"]);
        Assert.Equal("Gold", delta.Presets["Accent"]);
        Assert.Equal("FrostedMono", delta.Presets["Material"]);
        Assert.Equal("Borderless", delta.Presets["Glass"]);
        Assert.Equal(211, delta.Overrides.Count);
    }

    public static void Every_bundle_is_sound_against_the_schema()
    {
        var schema = Schema_();
        foreach (var preset in Real().Items)
        {
            var problems = preset.Problems(schema);
            Assert.True(problems.Count == 0, preset.Id + ": " + string.Join("; ", problems));
        }
    }

    public static void Applying_a_bundle_drops_nothing()
    {
        var schema = Schema_();
        foreach (var preset in Real().Items)
        {
            var store = new ConstantStore(schema.AllConstants());
            var applier = new ProfileApplier(schema, store, new FakePresetFiles(), ThemeDir());
            var report = applier.ApplyProfile(preset.ToProfile("x"));
            Assert.True(report.Ignored == 0, preset.Id + " dropped " + string.Join(", ", report.Unknown.Concat(report.Invalid)));
            foreach (var kv in preset.Overrides) Assert.Equal(kv.Value, store.Get(kv.Key));
        }
    }

    public static void A_profile_started_from_a_bundle_is_exactly_that_bundle()
    {
        var schema = Schema_();
        var delta = Real().Default;
        var store = new ConstantStore(schema.AllConstants());
        var applier = new ProfileApplier(schema, store, new FakePresetFiles(), ThemeDir());
        var profile = GlobalPresets.StartFrom(delta, "Default", null);
        applier.ApplyProfile(profile);
        Assert.Equal(delta.Id, profile.Editor[GlobalPresets.EditorKey]);
        Assert.True(GlobalPresets.IsExactly(delta, profile, store, schema, applier));
        string error;
        store.TrySet("ChipGlassOpacity", "0.9", out error);
        Assert.True(!GlobalPresets.IsExactly(delta, profile, store, schema, applier), "an edit makes it customised");
    }

    public static void Starting_from_a_bundle_hands_its_shared_choices_to_the_look()
    {
        var look = new Look { Table = LookTable.Parse("{\"sharedGroups\":[\"Glass\",\"Accent\"],\"tokens\":{}}") };
        var preset = Real().Default;
        GlobalPresets.StartFrom(preset, "Default", look);
        foreach (var group in new[] { "Glass", "Accent" })
            if (preset.Presets.ContainsKey(group)) Assert.Equal(preset.Presets[group], look.Presets[group]);
        Assert.True(!look.Presets.ContainsKey("Material"), "only the shared groups");
    }

    public static void The_thirty_art_directions_are_complete()
    {
        var items = Real().Items;
        Assert.Equal(Real().Find("DELTA") == null ? 30 : 31, items.Count);
        var seen = new HashSet<string>();
        foreach (var p in items)
        {
            Assert.True(seen.Add(p.ColorAndAccent()), p.Id + " has the same colours and accent as another preset");
            Assert.True(p.IntentFor("en").Length > 20 && p.IntentFor("fr").Length > 20, p.Id + " needs an intent in both languages");
            Assert.True(p.Overrides.Count >= 20, p.Id + " is not a direction yet (" + p.Overrides.Count + " overrides)");
        }
    }

    // the look groups that make two presets look alike or not; absent means the group's default
    private static readonly string[] LookGroups =
    {
        "ColorProfile", "Accent", "Material", "Style", "Corners", "Density", "Glass", "Typography", "Backdrop", "HeroCard", "HeaderBar", "Chips", "TopPanel", "Buttons"
    };

    public static void Any_two_art_directions_differ_in_at_least_six_look_groups()
    {
        var items = Real().Items.Where(p => p.Id != "DELTA").ToList();
        for (int i = 0; i < items.Count; i++)
            for (int j = i + 1; j < items.Count; j++)
            {
                int differ = LookGroups.Count(g =>
                {
                    string a, b;
                    if (!items[i].Presets.TryGetValue(g, out a)) a = "Default";
                    if (!items[j].Presets.TryGetValue(g, out b)) b = "Default";
                    return a != b;
                });
                Assert.True(differ >= 6, items[i].Id + " and " + items[j].Id + " differ in only " + differ + " of the look groups: they are near copies");
            }
    }

    public static void The_section_layout_is_not_the_same_for_every_art_direction()
    {
        // the placement of the sections (column and order constants) and the information shown are part of a direction
        var items = Real().Items.Where(p => p.Id != "DELTA").ToList();
        var signatures = items.Select(p => string.Join(";", p.Overrides.Where(kv => kv.Key.StartsWith("DetailsView") && (kv.Key.EndsWith("InRightColumn") || kv.Key.Contains("SectionOrder") || kv.Key.EndsWith("Header") || kv.Key == "DetailsViewShowSectionHeaders")).OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value))).Distinct().Count();
        Assert.True(signatures >= 12, "only " + signatures + " distinct section layouts among " + items.Count + " art directions: placements must vary too");
    }

    public static void Every_preset_has_a_small_real_preview()
    {
        if (!Directory.Exists(Path.Combine(ThemeDir(), GlobalPresets.Folder, "Previews"))) return;   // the public test fixtures carry the bundles without the pictures
        foreach (var p in Real().Items)
        {
            Assert.True(p.Preview != null, p.Id + " has no preview picture");
            var info = new FileInfo(Path.Combine(ThemeDir(), p.Preview.Replace('/', Path.DirectorySeparatorChar)));
            Assert.True(info.Length > 5000 && info.Length < 400000, p.Id + " preview size " + info.Length);
        }
    }
}
