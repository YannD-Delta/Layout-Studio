using System.ComponentModel;
using System.Globalization;
using LayoutStudio.Core;
using LayoutStudio.Editor;

public static class RestartBannerTests
{
    private const string Json = @"{""version"":1,
      ""constants"":[{""key"":""Blur"",""type"":""Double"",""default"":""30""}],
      ""pages"":[{""id"":""p"",""title"":""P"",""groups"":[{""id"":""g"",""title"":""G"",""controls"":[
        {""kind"":""preset"",""id"":""Buttons"",""keys"":[],""title"":""Buttons"",""defaultChoice"":""Default"",""choices"":[
          {""id"":""Default"",""name"":""Default"",""constants"":{}},
          {""id"":""Pill"",""name"":""Pill"",""constants"":{},""files"":[""Presets/Buttons/Pill.xaml""],""structural"":true}]}]}]}]}";

    public static void Choosing_a_structural_preset_raises_the_restart_banner()
    {
        var schema = Schema.Load(Json);
        var store = new ConstantStore(schema.AllConstants());
        var history = new History(store);
        var profile = new Profile { Name = "p" };
        var applier = new ProfileApplier(schema, store, new FakePresetFiles(), "theme") { Live = true };
        var shell = new ShellViewModel(schema, store, history, applier, profile, k => null, new CultureInfo("en-US"));
        bool raised = false;
        ((INotifyPropertyChanged)shell).PropertyChanged += (s, e) => { if (e.PropertyName == "RestartRequired") raised = true; };
        applier.ApplyPreset(profile, "Buttons", "Pill");
        Assert.True(applier.RestartRequired);
        Assert.True(raised, "no constant changed, so only the preset event can tell the window to show the banner");
    }
}
