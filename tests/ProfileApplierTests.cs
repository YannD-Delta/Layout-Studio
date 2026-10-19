using System.Collections.Generic;
using System.IO;
using System.Linq;
using LayoutStudio.Core;

public static class ProfileApplierTests
{
    private class FakeFiles : IPresetFiles
    {
        public List<string> Calls = new List<string>();
        public void Merge(string groupId, string absolutePath) { Calls.Add("merge " + groupId + " " + Path.GetFileName(absolutePath)); }
        public void Remove(string groupId) { Calls.Add("remove " + groupId); }
    }

    private const string SchemaJson = @"{
      ""version"": 1,
      ""constants"": [
        {""key"":""Accent"",""type"":""Color"",""default"":""#FFFF0000""},
        {""key"":""Radius"",""type"":""Double"",""default"":""14""},
        {""key"":""Blur"",""type"":""Double"",""default"":""30""}
      ],
      ""pages"": [{""id"":""p"",""title"":""P"",""groups"":[{""id"":""g"",""title"":""G"",""controls"":[
        {""kind"":""preset"",""id"":""ColorProfile"",""keys"":[],""title"":""Colors"",""defaultChoice"":""Default"",""choices"":[
          {""id"":""Default"",""name"":""Default"",""constants"":{}},
          {""id"":""Midnight"",""name"":""Midnight"",""constants"":{""Accent"":""#FF0000FF""},""files"":[""Presets/Colors/Midnight.xaml""]}]},
        {""kind"":""preset"",""id"":""Corners"",""keys"":[],""title"":""Corners"",""defaultChoice"":""Default"",""choices"":[
          {""id"":""Default"",""name"":""Default"",""constants"":{}},
          {""id"":""Sharp"",""name"":""Sharp"",""constants"":{""Radius"":""0""}}]},
        {""kind"":""preset"",""id"":""TopPanel"",""keys"":[],""title"":""Top panel"",""defaultChoice"":""Default"",""choices"":[
          {""id"":""Default"",""name"":""Default"",""constants"":{}},
          {""id"":""Dune"",""name"":""Dune"",""constants"":{""Blur"":""10""},""files"":[""Presets/TopPanel/Dune.xaml""],""structural"":true}]}
      ]}]}]
    }";

    private static ProfileApplier Make(out ConstantStore store, out FakeFiles files)
    {
        var schema = Schema.Load(SchemaJson);
        store = new ConstantStore(schema.AllConstants());
        files = new FakeFiles();
        return new ProfileApplier(schema, store, files, @"C:\theme");
    }

    public static void Profile_with_presets_applies_without_modifying_the_collection_while_enumerating()
    {
        ConstantStore store; FakeFiles files;
        var applier = Make(out store, out files);
        var profile = new Profile { Name = "p" };
        profile.Overrides["Blur"] = "12";
        profile.Presets["ColorProfile"] = "Midnight";
        profile.Presets["Corners"] = "Sharp";
        applier.ApplyProfile(profile);
        Assert.Equal("12", store.Get("Blur"));
        Assert.Equal("#FF0000FF", store.Get("Accent"));
        Assert.Equal("0", store.Get("Radius"));
        Assert.Equal(2, profile.Presets.Count, "the profile keeps its selections");
    }

    public static void Applying_a_preset_raises_PresetChanged_so_the_choice_is_saved()
    {
        ConstantStore store; FakeFiles files;
        var applier = Make(out store, out files);
        int raised = 0;
        applier.PresetChanged += () => raised++;
        applier.ApplyPreset(new Profile { Name = "p" }, "ColorProfile", "Midnight");
        Assert.Equal(1, raised);
        applier.ApplyPreset(new Profile { Name = "p" }, "NoSuchGroup", "x");
        Assert.Equal(1, raised, "an unknown group changes nothing");
    }

    private class ThrowingFiles : IPresetFiles
    {
        public void Merge(string groupId, string absolutePath) { throw new System.IO.FileNotFoundException("missing " + absolutePath); }
        public void Remove(string groupId) { }
    }

    public static void A_preset_file_that_fails_does_not_stop_the_rest_of_the_profile()
    {
        var schema = Schema.Load(SchemaJson);
        var store = new ConstantStore(schema.AllConstants());
        var logs = new List<string>();
        var applier = new ProfileApplier(schema, store, new ThrowingFiles(), @"C:	heme") { Log = logs.Add };
        var profile = new Profile { Name = "p" };
        profile.Presets["ColorProfile"] = "Midnight";   // its file cannot be merged
        profile.Presets["Corners"] = "Sharp";
        profile.Overrides["Blur"] = "12";
        applier.ApplyProfile(profile);
        Assert.Equal("12", store.Get("Blur"), "the override still applies");
        Assert.Equal("0", store.Get("Radius"), "the other preset still applies");
        Assert.Equal(1, logs.Count);
        Assert.True(logs[0].Contains("ColorProfile"), "the log names the preset that failed");
    }

    public static void Choice_with_files_merges_the_first_file()
    {
        ConstantStore store; FakeFiles files;
        var applier = Make(out store, out files);
        applier.ApplyPreset(new Profile { Name = "p" }, "ColorProfile", "Midnight");
        Assert.Equal("merge ColorProfile Midnight.xaml", files.Calls[files.Calls.Count - 1]);
    }

    public static void Default_choice_resets_what_other_choices_touched_and_removes_files()
    {
        ConstantStore store; FakeFiles files;
        var applier = Make(out store, out files);
        var profile = new Profile { Name = "p" };
        applier.ApplyPreset(profile, "ColorProfile", "Midnight");
        applier.ApplyPreset(profile, "ColorProfile", "Default");
        Assert.Equal("#FFFF0000", store.Get("Accent"), "back to the default accent");
        Assert.Equal("remove ColorProfile", files.Calls[files.Calls.Count - 1]);
        Assert.Equal("Default", profile.Presets["ColorProfile"]);
    }

    public static void Unknown_group_or_choice_is_ignored()
    {
        ConstantStore store; FakeFiles files;
        var applier = Make(out store, out files);
        var profile = new Profile { Name = "p" };
        applier.ApplyPreset(profile, "NoSuchGroup", "x");
        applier.ApplyPreset(profile, "Corners", "NoSuchChoice");
        Assert.Equal("14", store.Get("Radius"));
        Assert.Equal(0, files.Calls.Count);
    }

    public static void Invalid_override_in_a_profile_is_skipped_not_fatal()
    {
        ConstantStore store; FakeFiles files;
        var applier = Make(out store, out files);
        var profile = new Profile { Name = "p" };
        profile.Overrides["Blur"] = "abc";
        profile.Overrides["Ghost"] = "1";
        profile.Overrides["Radius"] = "5";
        applier.ApplyProfile(profile);
        Assert.Equal("30", store.Get("Blur"));
        Assert.Equal("5", store.Get("Radius"));
    }

    public static void ApplyProfile_reports_unknown_and_invalid_entries()
    {
        ConstantStore store; FakeFiles files;
        var applier = Make(out store, out files);
        var profile = new Profile { Name = "p" };
        profile.Overrides["Radius"] = "5";
        profile.Overrides["Ghost"] = "1";
        profile.Overrides["Blur"] = "abc";
        profile.Presets["NoSuchGroup"] = "X";
        var report = applier.ApplyProfile(profile);
        Assert.Equal(1, report.Applied);
        Assert.Equal("Ghost", report.Unknown.Single(k => k == "Ghost"));
        Assert.Equal("Blur", report.Invalid.Single());
        Assert.True(report.Unknown.Contains("NoSuchGroup"), "a preset group this theme lacks is reported too");
        Assert.True(report.Ignored == 3);
    }

    public static void Structural_choice_is_merged_at_startup()
    {
        ConstantStore store; FakeFiles files;
        var applier = Make(out store, out files);
        var profile = new Profile { Name = "p" };
        profile.Presets["TopPanel"] = "Dune";
        applier.ApplyProfile(profile);
        Assert.Equal("merge TopPanel Dune.xaml", files.Calls[files.Calls.Count - 1], "before the window exists nothing can crash");
        Assert.False(applier.RestartRequired);
    }

    public static void Structural_choice_is_not_merged_while_live_but_is_remembered()
    {
        ConstantStore store; FakeFiles files;
        var applier = Make(out store, out files);
        applier.Live = true;
        var profile = new Profile { Name = "p" };
        applier.ApplyPreset(profile, "TopPanel", "Dune");
        Assert.Equal(0, files.Calls.Count, "replacing a control template under a running Playnite window can crash it");
        Assert.Equal("Dune", profile.Presets["TopPanel"], "the choice is saved for the next start");
        Assert.Equal("10", store.Get("Blur"), "constants are still applied live");
        Assert.True(applier.RestartRequired);
    }

    public static void Switching_away_from_a_structural_choice_live_defers_the_removal()
    {
        ConstantStore store; FakeFiles files;
        var applier = Make(out store, out files);
        var profile = new Profile { Name = "p" };
        profile.Presets["TopPanel"] = "Dune";
        applier.ApplyProfile(profile);
        files.Calls.Clear();
        applier.Live = true;
        applier.ApplyPreset(profile, "TopPanel", "Default");
        Assert.Equal(0, files.Calls.Count, "the merged file stays until the next start");
        Assert.True(applier.RestartRequired);
        Assert.Equal("Default", profile.Presets["TopPanel"]);
    }

    public static void Plain_choice_still_merges_live()
    {
        ConstantStore store; FakeFiles files;
        var applier = Make(out store, out files);
        applier.Live = true;
        applier.ApplyPreset(new Profile { Name = "p" }, "ColorProfile", "Midnight");
        Assert.Equal("merge ColorProfile Midnight.xaml", files.Calls[files.Calls.Count - 1]);
        Assert.False(applier.RestartRequired);
    }
}
