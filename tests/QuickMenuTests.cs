using System.Collections.Generic;
using System.Linq;
using LayoutStudio.Core;

public static class QuickMenuTests
{
    private class NoFiles : IPresetFiles
    {
        public void Merge(string groupId, string absolutePath) { }
        public void Remove(string groupId) { }
    }

    private const string SchemaJson = @"{
      ""version"": 1,
      ""constants"": [
        {""key"":""Accent"",""type"":""Color"",""default"":""#FF4DA3FF""},
        {""key"":""Radius"",""type"":""Double"",""default"":""16""}
      ],
      ""pages"": [{""id"":""look"",""title"":""Look"",""groups"":[{""id"":""presets"",""title"":""Presets"",""controls"":[
        {""kind"":""preset"",""id"":""Accent"",""keys"":[],""title"":""Accent colour"",""titleKey"":""LOCAccent"",""defaultChoice"":""Default"",""choices"":[
          {""id"":""Default"",""name"":""Neon blue"",""locKey"":""LOCAccentDefault"",""constants"":{}},
          {""id"":""Violet"",""name"":""Violet"",""locKey"":""LOCAccentViolet"",""constants"":{""Accent"":""#FFA66BFF""}}]},
        {""kind"":""preset"",""id"":""Corners"",""keys"":[],""title"":""Corners"",""defaultChoice"":""Default"",""choices"":[
          {""id"":""Default"",""name"":""Rounded"",""constants"":{}},
          {""id"":""Square"",""name"":""Square"",""constants"":{""Radius"":""0""}}]}
      ]}]}]
    }";

    public static void Each_preset_group_becomes_one_entry_with_its_choices()
    {
        var schema = Schema.Load(SchemaJson);
        var entries = QuickMenu.Entries(schema, new Profile { Name = "p" }, key => key == "LOCAccentViolet" ? "Violette" : null);
        Assert.Equal("Accent,Corners", string.Join(",", entries.Select(e => e.Id)));
        var accent = entries[0];
        Assert.Equal("Accent colour", accent.Title);
        Assert.Equal("Default,Violet", string.Join(",", accent.Choices.Select(c => c.Id)));
        Assert.Equal("Violette", accent.Choices[1].Name, "a localized name wins over the English one");
        Assert.Equal("Neon blue", accent.Choices[0].Name);
    }

    public static void The_current_choice_is_the_profile_choice_or_the_default()
    {
        var schema = Schema.Load(SchemaJson);
        var profile = new Profile { Name = "p" };
        profile.Presets["Accent"] = "Violet";
        var entries = QuickMenu.Entries(schema, profile, null);
        Assert.Equal("Violet", entries.Single(e => e.Id == "Accent").CurrentId);
        Assert.Equal("Default", entries.Single(e => e.Id == "Corners").CurrentId, "an unset group is on its default");
    }

    public static void Choosing_applies_the_constants_and_remembers_the_choice()
    {
        var schema = Schema.Load(SchemaJson);
        var store = new ConstantStore(schema.AllConstants());
        var applier = new ProfileApplier(schema, store, new NoFiles(), @"C:\theme");
        var profile = new Profile { Name = "p" };
        int changed = 0;
        applier.PresetChanged += () => changed++;
        Assert.True(QuickMenu.Choose(applier, profile, "Corners", "Square"));
        Assert.Equal("0", store.Get("Radius"));
        Assert.Equal("Square", profile.Presets["Corners"]);
        Assert.Equal(1, changed, "the profile is saved after a choice");
        Assert.False(QuickMenu.Choose(applier, profile, "Corners", "Nope"), "an unknown choice does nothing");
        Assert.Equal("Square", profile.Presets["Corners"]);
    }
}
