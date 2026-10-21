using System.Collections.Generic;
using System.IO;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

public class FakeGlobalSharing : FakeSharing, IGlobalPresetSwitching
{
    public List<GlobalPreset> Presets = new List<GlobalPreset>();
    public string Default, Active, ApplyError;
    public HashSet<string> Exact = new HashSet<string>();
    public IList<GlobalPreset> GlobalPresetList { get { return Presets; } }
    public string DefaultGlobalPresetId { get { return Default; } }
    public string ActiveGlobalPresetId { get { return Active; } }
    public bool IsExactly(GlobalPreset preset) { return Exact.Contains(preset.Id); }
    public string ApplyGlobalPreset(string id)
    {
        Calls.Add("apply " + id);
        if (ApplyError != null) return ApplyError;
        Active = id; Exact.Clear(); Exact.Add(id);
        return null;
    }

    public static FakeGlobalSharing FromTheme()
    {
        var themeDir = Path.GetDirectoryName(TestPaths.SchemaPath());
        var gp = GlobalPresets.Load(themeDir);
        return new FakeGlobalSharing { Presets = gp.Items, Default = gp.DefaultId, Active = gp.DefaultId, Exact = new HashSet<string>(new[] { gp.DefaultId }) };
    }
}

public static class GlobalPresetsViewModelTests
{
    private static FakeGlobalSharing Two()
    {
        var a = new GlobalPreset { Id = "A" }; a.Name["en"] = "Alpha"; a.Intent["en"] = "First";
        var b = new GlobalPreset { Id = "B" }; b.Name["en"] = "Beta"; b.Name["fr"] = "Bêta"; b.Intent["en"] = "Second";
        return new FakeGlobalSharing { Presets = new List<GlobalPreset> { a, b }, Default = "A", Active = "A", Exact = new HashSet<string> { "A" } };
    }

    public static void One_card_per_preset_with_the_names_of_the_language_and_the_default_flagged()
    {
        var vm = new GlobalPresetsViewModel(Two(), "fr", null);
        Assert.Equal("Alpha,Bêta", string.Join(",", vm.Cards.Select(c => c.Name)));
        Assert.True(vm.Cards[0].IsDefault && !vm.Cards[1].IsDefault);
        Assert.True(vm.Cards[0].IsCurrent && vm.Cards[0].IsExact && !vm.Cards[0].IsCustomised);
        Assert.True(!vm.Cards[1].IsCurrent);
    }

    public static void An_edited_current_preset_reads_as_customised()
    {
        var fake = Two();
        fake.Exact.Clear();
        var vm = new GlobalPresetsViewModel(fake, "en", null);
        Assert.True(vm.Cards[0].IsCustomised && !vm.Cards[0].IsExact);
        Assert.True(vm.Cards[0].ChooseCommand.CanExecute(null), "a customised preset can be applied again to undo the edits");
    }

    public static void The_card_in_use_cannot_be_chosen_again()
    {
        var vm = new GlobalPresetsViewModel(Two(), "en", null);
        Assert.False(vm.Cards[0].ChooseCommand.CanExecute(null));
    }

    public static void The_first_click_asks_and_the_second_applies()
    {
        var fake = Two();
        var vm = new GlobalPresetsViewModel(fake, "en", null);
        vm.Cards[1].ChooseCommand.Execute(null);
        Assert.True(vm.Cards[1].IsPending);
        Assert.Equal(0, fake.Calls.Count, "nothing changed yet");
        vm.Cards[1].ChooseCommand.Execute(null);
        Assert.Equal("apply B", string.Join(",", fake.Calls));
        Assert.True(vm.Cards[1].IsCurrent && vm.Cards[1].IsExact && !vm.Cards[1].IsPending);
        Assert.True(!vm.Cards[0].IsCurrent);
    }

    public static void An_error_of_the_service_is_shown_and_nothing_is_marked_current()
    {
        var fake = Two();
        fake.ApplyError = "the active profile is read-only";
        var vm = new GlobalPresetsViewModel(fake, "en", null);
        vm.Cards[1].ChooseCommand.Execute(null);
        vm.Cards[1].ChooseCommand.Execute(null);
        Assert.True(vm.IsError && vm.HasMessage);
        Assert.True(!vm.Cards[1].IsCurrent);
    }

    public static void An_edit_in_the_store_refreshes_the_cards()
    {
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        var store = new ConstantStore(schema.AllConstants());
        var fake = Two();
        var vm = new GlobalPresetsViewModel(fake, "en", store);
        fake.Exact.Clear();
        string error;
        store.TrySet("ChipGlassOpacity", "0.5", out error);
        Assert.True(vm.Cards[0].IsCustomised, "the card notices the edit without being asked");
    }

    public static void The_editor_shows_the_presets_page_for_the_real_theme()
    {
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        var store = new ConstantStore(schema.AllConstants());
        var history = new History(store);
        var applier = new ProfileApplier(schema, store, new FakePresetFiles(), @"C:\theme");
        var shell = new ShellViewModel(schema, store, history, applier, new Profile { Name = "Default" }, k => null, new System.Globalization.CultureInfo("en-US"), null, FakeGlobalSharing.FromTheme());
        var page = shell.Pages.Single(p => p.Id == "presets");
        Assert.True(page.Groups.Single().Editor is GlobalPresetsViewModel);
        Assert.Equal(GlobalPresets.Load(System.IO.Path.GetDirectoryName(TestPaths.SchemaPath())).Items[0].Id, ((GlobalPresetsViewModel)page.Groups.Single().Editor).Cards[0].Id);
        Assert.Equal("window", shell.SelectedPage.Id);
    }
}
