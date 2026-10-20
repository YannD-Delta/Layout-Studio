using System.Collections.Generic;
using System.IO;
using System.Linq;
using LayoutStudio.Core;

public static class LookTests
{
    // A Fullscreen-like theme: card corners with a ring that follows (+2), a clamped glass strength, an accent with a light end.
    private const string SchemaJson = @"{
      ""version"": 1,
      ""constants"": [
        {""key"":""FsCardCornerRadius"",""type"":""CornerRadius"",""default"":""16""},
        {""key"":""FsCardRimCornerRadius"",""type"":""CornerRadius"",""default"":""18""},
        {""key"":""FsGlassRimOpacity"",""type"":""Double"",""default"":""1"",""min"":0,""max"":1},
        {""key"":""GlyphColor"",""type"":""Color"",""default"":""#FF4DA3FF""},
        {""key"":""GlyphLightColor"",""type"":""Color"",""default"":""#FF9AD1FF""},
        {""key"":""FsPill"",""type"":""CornerRadius"",""default"":""40""},
        {""key"":""Other"",""type"":""Double"",""default"":""5""}
      ],
      ""pages"": [{""id"":""p"",""title"":""P"",""groups"":[{""id"":""g"",""title"":""G"",""controls"":[
        {""kind"":""preset"",""id"":""Corners"",""keys"":[],""title"":""Corners"",""defaultChoice"":""Default"",""choices"":[
          {""id"":""Default"",""name"":""Default"",""constants"":{}},
          {""id"":""Square"",""name"":""Square"",""constants"":{""FsCardCornerRadius"":""0"",""FsCardRimCornerRadius"":""0""}},
          {""id"":""Round"",""name"":""Round"",""constants"":{""FsCardCornerRadius"":""24"",""FsCardRimCornerRadius"":""26""}}]},
        {""kind"":""preset"",""id"":""Local"",""keys"":[],""title"":""Local"",""defaultChoice"":""Default"",""choices"":[
          {""id"":""Default"",""name"":""Default"",""constants"":{}},
          {""id"":""Big"",""name"":""Big"",""constants"":{""Other"":""9""}}]}
      ]}]}]
    }";

    private const string TableJson = @"{
      ""sharedGroups"": [""Corners""],
      ""tokens"": {
        ""radius_card"": {""type"":""CornerRadius"",""constants"":[""FsCardCornerRadius""],""scale"":0.62,""extra"":{""FsCardRimCornerRadius"":2},""extraScale"":{""FsPill"":3.5}},
        ""glass_rim"": {""type"":""Double"",""constants"":[""FsGlassRimOpacity""],""scale"":1.6667,""max"":1},
        ""accent"": {""type"":""Color"",""constants"":[""GlyphColor""],""light"":{""constant"":""GlyphLightColor"",""mixWhite"":0.47}}
      }
    }";

    private class Rig
    {
        public Schema Schema; public ConstantStore Store; public ProfileApplier Applier; public Look Look; public Profile Profile = new Profile { Name = "p" };
        public string Dir;
    }

    private static Rig Make(string lookText = null)
    {
        var rig = new Rig();
        rig.Dir = Path.Combine(Path.GetTempPath(), "ls-look-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rig.Dir);
        rig.Schema = Schema.Load(SchemaJson);
        rig.Store = new ConstantStore(rig.Schema.AllConstants());
        rig.Applier = new ProfileApplier(rig.Schema, rig.Store, new FakePresetFiles(), @"C:\theme");
        string path = Path.Combine(rig.Dir, "look.profile.json");
        if (lookText != null) File.WriteAllText(path, lookText);
        rig.Look = Look.Load(path, LookTable.Parse(TableJson));
        rig.Applier.Look = rig.Look;
        return rig;
    }

    public static void A_token_sets_every_mapped_constant_with_the_scale_and_the_extra_offset()
    {
        var rig = Make();
        Assert.True(rig.Look.SetToken("radius_card", "26"));
        rig.Applier.ApplyProfile(rig.Profile);
        Assert.Equal("16", rig.Store.Get("FsCardCornerRadius"), "26 x 0.62 = 16.1, rounded");
        Assert.Equal("18", rig.Store.Get("FsCardRimCornerRadius"), "the ring follows with +2");
        Assert.Equal("56", rig.Store.Get("FsPill"), "the pill follows as a multiple (16 x 3.5)");
    }

    public static void A_zero_token_keeps_the_ring_at_zero_and_a_strength_is_clamped()
    {
        var rig = Make();
        rig.Look.SetToken("radius_card", "0");
        rig.Look.SetToken("glass_rim", "0.9");
        rig.Applier.ApplyProfile(rig.Profile);
        Assert.Equal("0", rig.Store.Get("FsCardCornerRadius"));
        Assert.Equal("0", rig.Store.Get("FsCardRimCornerRadius"), "0 + 2 would not be square");
        Assert.Equal("1", rig.Store.Get("FsGlassRimOpacity"), "0.9 x 1.6667 = 1.5 is clamped to the max of 1");
    }

    public static void The_accent_token_also_sets_the_light_end()
    {
        var rig = Make();
        rig.Look.SetToken("accent", "#FF000000");
        rig.Applier.ApplyProfile(rig.Profile);
        Assert.Equal("#FF000000", rig.Store.Get("GlyphColor"));
        Assert.Equal("#FF787878", rig.Store.Get("GlyphLightColor"), "47 % of the way to white");
    }

    public static void An_unknown_token_or_a_bad_value_is_refused_and_changes_nothing()
    {
        var rig = Make();
        Assert.False(rig.Look.SetToken("nope", "1"));
        Assert.False(rig.Look.SetToken("radius_card", "wide"));
        rig.Applier.ApplyProfile(rig.Profile);
        Assert.Equal("16", rig.Store.Get("FsCardCornerRadius"));
    }

    public static void A_shared_group_follows_the_Look_over_the_profiles_own_choice()
    {
        var rig = Make(@"{""Version"":1,""Presets"":{""Corners"":""Round""}}");
        rig.Profile.Presets["Corners"] = "Square";
        rig.Applier.ApplyProfile(rig.Profile);
        Assert.Equal("24", rig.Store.Get("FsCardCornerRadius"));
        Assert.Equal("Round", rig.Profile.Presets["Corners"], "the profile records what is applied");
    }

    public static void A_group_the_Look_does_not_share_stays_with_the_profile()
    {
        var rig = Make(@"{""Version"":1,""Presets"":{""Local"":""Default""}}");
        rig.Profile.Presets["Local"] = "Big";
        rig.Applier.ApplyProfile(rig.Profile);
        Assert.Equal("9", rig.Store.Get("Other"));
        Assert.False(rig.Look.Presets.ContainsKey("Local") && rig.Look.Presets["Local"] == "Big", "a local choice never reaches the Look");
    }

    public static void Choosing_a_shared_preset_records_it_in_the_Look_and_a_local_one_does_not()
    {
        var rig = Make();
        rig.Applier.ApplyPreset(rig.Profile, "Corners", "Square");
        rig.Applier.ApplyPreset(rig.Profile, "Local", "Big");
        Assert.Equal("Square", rig.Look.Presets["Corners"]);
        Assert.False(rig.Look.Presets.ContainsKey("Local"));
        Assert.True(rig.Look.Dirty, "the Look must be saved");
    }

    public static void A_profile_choice_with_no_Look_entry_is_adopted_by_the_Look()
    {
        var rig = Make();
        rig.Profile.Presets["Corners"] = "Square";
        rig.Applier.ApplyProfile(rig.Profile);
        Assert.Equal("Square", rig.Look.Presets["Corners"], "the first mode to start shares its choice");
    }

    public static void Order_of_authority_is_preset_then_token_then_the_modes_own_override()
    {
        var rig = Make(@"{""Version"":1,""Presets"":{""Corners"":""Round""},""Tokens"":{""radius_card"":""26""}}");
        rig.Applier.ApplyProfile(rig.Profile);
        Assert.Equal("16", rig.Store.Get("FsCardCornerRadius"), "the token beats the preset");
        Assert.Equal("18", rig.Store.Get("FsCardRimCornerRadius"), "the ring follows the token, not the preset");
        rig.Profile.Overrides["FsCardCornerRadius"] = "5";
        rig.Applier.ApplyProfile(rig.Profile);
        Assert.Equal("5", rig.Store.Get("FsCardCornerRadius"), "this mode's own override wins");
    }

    public static void Look_owned_values_are_not_saved_as_overrides_so_a_later_Look_change_reaches_the_mode()
    {
        var rig = Make(@"{""Version"":1,""Presets"":{""Corners"":""Round""}}");
        rig.Applier.ApplyProfile(rig.Profile);
        OverrideMerge.Rebuild(rig.Profile, rig.Store, rig.Look.Wanted(rig.Schema, rig.Store));
        Assert.False(rig.Profile.Overrides.ContainsKey("FsCardCornerRadius"), "24 is the Look's value, not the mode's choice");
        rig.Store.TrySet("FsCardCornerRadius", "7", out var error);
        OverrideMerge.Rebuild(rig.Profile, rig.Store, rig.Look.Wanted(rig.Schema, rig.Store));
        Assert.Equal("7", rig.Profile.Overrides["FsCardCornerRadius"], "a value the user moved away from the Look is kept");
    }

    public static void A_damaged_Look_file_is_ignored_and_never_overwritten()
    {
        var rig = Make("{ this is not json");
        Assert.True(rig.Look.Warning != null, "a warning is raised");
        rig.Applier.ApplyProfile(rig.Profile);
        Assert.Equal("16", rig.Store.Get("FsCardCornerRadius"));
        rig.Look.SetToken("radius_card", "30");
        rig.Look.SaveIfDirty();
        Assert.Equal("{ this is not json", File.ReadAllText(Path.Combine(rig.Dir, "look.profile.json")), "the damaged file is left alone");
    }

    public static void A_Look_from_a_newer_version_is_read_only()
    {
        var rig = Make(@"{""Version"":99,""Presets"":{""Corners"":""Round""}}");
        Assert.True(rig.Look.ReadOnly);
        rig.Look.SetToken("radius_card", "30");
        rig.Look.SaveIfDirty();
        Assert.False(File.ReadAllText(Path.Combine(rig.Dir, "look.profile.json")).Contains("radius_card"));
    }

    public static void Clearing_the_tokens_forgets_them_so_a_reset_reaches_both_modes()
    {
        var rig = Make();
        rig.Look.SetToken("radius_card", "30");
        rig.Look.SaveIfDirty();
        rig.Look.ClearAllTokens();
        Assert.True(rig.Look.Dirty);
        rig.Look.SaveIfDirty();
        var again = Look.Load(Path.Combine(rig.Dir, "look.profile.json"), LookTable.Parse(TableJson));
        Assert.Equal(0, again.Tokens.Count);
    }

    public static void The_Look_saves_and_loads_back()
    {
        var rig = Make();
        rig.Look.SetPreset("Corners", "Square");
        rig.Look.SetToken("radius_card", "30");
        rig.Look.SaveIfDirty();
        Assert.False(rig.Look.Dirty);
        var again = Look.Load(Path.Combine(rig.Dir, "look.profile.json"), LookTable.Parse(TableJson));
        Assert.Equal("Square", again.Presets["Corners"]);
        Assert.Equal("30", again.Tokens["radius_card"]);
    }

    public static void The_generated_tables_of_both_themes_only_name_constants_their_schema_has()
    {
        string root = TestPaths.RepoRoot();
        foreach (var theme in new[] { "Modula", "Modula-Fullscreen" })
        {
            string dir = Path.Combine(root, "theme", theme);
            string table = Path.Combine(dir, "look-tokens.json");
            if (!File.Exists(table)) continue;   // the exported Layout Studio repository has no theme
            var schema = Schema.Load(File.ReadAllText(Path.Combine(dir, "layout-studio.schema.json")));
            var keys = new HashSet<string>(schema.AllConstants().Select(c => c.Key));
            var parsed = LookTable.Parse(File.ReadAllText(table));
            Assert.True(parsed.Tokens.Count > 5, theme + ": the table has tokens");
            foreach (var token in parsed.Tokens)
                foreach (var key in token.Value.Constants.Concat(token.Value.Extra.Keys))
                    Assert.True(keys.Contains(key), theme + ": " + token.Key + " maps " + key + ", which the schema must define");
            foreach (var group in parsed.SharedGroups)
                Assert.True(schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Any(c => c.Kind == "preset" && c.Id == group), theme + ": the shared group " + group + " exists");
        }
    }
}
