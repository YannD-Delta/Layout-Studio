using System.Globalization;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

/// <summary>A preset tile lists the settings it changes, with their titles and values, so the user knows what a click does.</summary>
public static class PresetTilesTests
{
    private const string Json = @"{
  ""version"": 1,
  ""constants"": [
    {""key"":""BlurAmount"",""type"":""Double"",""default"":""10"",""min"":0,""max"":50},
    {""key"":""FrostOn"",""type"":""Boolean"",""default"":""False""},
    {""key"":""Hidden"",""type"":""Double"",""default"":""1""}
  ],
  ""pages"": [{""id"":""p"",""title"":""P"",""groups"":[{""id"":""g"",""title"":""G"",""controls"":[
    {""kind"":""slider"",""id"":""BlurAmount"",""keys"":[""BlurAmount""],""title"":""Blur strength"",""choices"":[]},
    {""kind"":""preset"",""id"":""Look"",""keys"":[],""title"":""Look"",""defaultChoice"":""Plain"",""choices"":[
      {""id"":""Plain"",""name"":""Plain"",""constants"":{}},
      {""id"":""Frosted"",""name"":""Frosted"",""constants"":{""BlurAmount"":""30"",""FrostOn"":""True"",""Hidden"":""2""}}]}]}]}]
}";

    private static ShellViewModel Shell()
    {
        var schema = Schema.Load(Json);
        var store = new ConstantStore(schema.AllConstants());
        var history = new History(store);
        var applier = new ProfileApplier(schema, store, new FakePresetFiles(), @"C:\theme");
        return new ShellViewModel(schema, store, history, applier, new Profile { Name = "Default" }, k => null, new CultureInfo("en-US"));
    }

    public static void A_tile_lists_what_it_sets_in_words()
    {
        var look = Shell().Pages.SelectMany(p => p.Settings).Single(s => s.Id == "Look");
        var frosted = look.Choices.Single(c => c.Id == "Frosted");
        Assert.True(frosted.HasVariables);
        Assert.Equal("3 settings", frosted.VariablesHeader);
        var v = frosted.Variables;
        Assert.Equal("Blur strength", v[0].Name);
        Assert.Equal("30", v[0].Value);
        Assert.Equal("FrostOn", v[1].Name, "a constant without a control of its own shows its name");
        Assert.Equal("on", v[1].Value);
    }

    public static void A_choice_that_sets_nothing_has_no_list()
    {
        var look = Shell().Pages.SelectMany(p => p.Settings).Single(s => s.Id == "Look");
        var plain = look.Choices.Single(c => c.Id == "Plain");
        Assert.False(plain.HasVariables);
        Assert.Equal(0, plain.Variables.Count);
    }

    public static void Every_choice_knows_its_siblings_for_the_picture_comparison()
    {
        var look = Shell().Pages.SelectMany(p => p.Settings).Single(s => s.Id == "Look");
        Assert.True(look.Choices.All(c => c.Siblings != null && c.Siblings.Count == 2));
    }
}
