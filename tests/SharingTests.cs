using System.Collections.Generic;
using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

public class FakeSharing : IProfileSharing
{
    public string Code = "LS1:abc";
    public string ImportError;
    public string ImportNote { get; set; }
    public List<string> Calls = new List<string>();
    public string Export() { Calls.Add("export"); if (Throw) throw new System.IO.IOException("disk is locked"); return Code; }
    public string Import(string code) { Calls.Add("import " + code); return ImportError; }
    public string ResetError;
    public bool Throw;
    public string ResetAll() { Calls.Add("reset"); return ResetError; }
}

public static class SharingTests
{
    public static void Export_fills_the_code_and_copies_it()
    {
        var fake = new FakeSharing();
        string copied = null;
        var vm = new SharingViewModel(fake, text => copied = text);
        vm.ExportCommand.Execute(null);
        Assert.Equal("LS1:abc", vm.Code);
        Assert.Equal("LS1:abc", copied, "the code is put on the clipboard");
        Assert.False(vm.IsError);
        Assert.True(!string.IsNullOrEmpty(vm.Message));
    }

    public static void Import_passes_the_pasted_text_and_reports_success()
    {
        var fake = new FakeSharing();
        var vm = new SharingViewModel(fake, null);
        vm.Code = "  LS1:xyz  ";
        vm.ImportCommand.Execute(null);
        Assert.Equal("import LS1:xyz", fake.Calls.Single(), "surrounding spaces from a paste are trimmed");
        Assert.False(vm.IsError);
    }

    public static void Import_message_mentions_ignored_entries()
    {
        var fake = new FakeSharing { ImportNote = "Imported 52 settings, ignored 38 (unknown on this theme version)." };
        var vm = new SharingViewModel(fake, null);
        vm.Code = "LS1:xyz";
        vm.ImportCommand.Execute(null);
        Assert.False(vm.IsError, "a partial import is not a failure");
        Assert.True(vm.Message.Contains("ignored 38"), "the user must see what was dropped");
    }

    public static void Import_shows_the_error_of_a_bad_code()
    {
        var fake = new FakeSharing { ImportError = "not a Layout Studio share code" };
        var vm = new SharingViewModel(fake, null);
        vm.Code = "hello";
        vm.ImportCommand.Execute(null);
        Assert.True(vm.IsError);
        Assert.Equal("not a Layout Studio share code", vm.Message);
    }

    public static void Import_with_an_empty_box_does_nothing()
    {
        var fake = new FakeSharing();
        var vm = new SharingViewModel(fake, null);
        Assert.False(vm.ImportCommand.CanExecute(null), "nothing to import");
        vm.Code = "   ";
        Assert.False(vm.ImportCommand.CanExecute(null));
        Assert.Equal(0, fake.Calls.Count);
    }

    public static void Reset_asks_before_acting()
    {
        var fake = new FakeSharing();
        var vm = new SharingViewModel(fake, null);
        vm.AskResetCommand.Execute(null);
        Assert.True(vm.ResetPending);
        Assert.Equal(0, fake.Calls.Count, "asking changes nothing");
        vm.CancelResetCommand.Execute(null);
        Assert.False(vm.ResetPending);
        Assert.Equal(0, fake.Calls.Count);
    }

    public static void Confirmed_reset_runs_once_and_closes_the_question()
    {
        var fake = new FakeSharing();
        var vm = new SharingViewModel(fake, null);
        vm.AskResetCommand.Execute(null);
        vm.ConfirmResetCommand.Execute(null);
        Assert.Equal("reset", fake.Calls.Single());
        Assert.False(vm.ResetPending);
        Assert.False(vm.ConfirmResetCommand.CanExecute(null), "cannot confirm without asking again");
    }

    public static void A_failing_service_shows_a_message_instead_of_throwing()
    {
        var fake = new FakeSharing { Throw = true };
        var vm = new SharingViewModel(fake, null);
        vm.ExportCommand.Execute(null);
        Assert.True(vm.IsError);
        Assert.True(vm.Message.Contains("disk is locked"));
    }

    public static void Reset_that_cannot_run_reports_why()
    {
        var fake = new FakeSharing { ResetError = "the active profile is read-only" };
        var vm = new SharingViewModel(fake, null);
        vm.AskResetCommand.Execute(null);
        vm.ConfirmResetCommand.Execute(null);
        Assert.True(vm.IsError);
        Assert.Equal("the active profile is read-only", vm.Message, "never claim success when nothing was reset");
    }

    private static ShellViewModel NewShell(IProfileSharing sharing)
    {
        var schema = Schema.Load(TestSchema.Json);
        var store = new ConstantStore(schema.AllConstants());
        var history = new History(store);
        var profile = new Profile { Name = "Default" };
        var applier = new ProfileApplier(schema, store, new FakePresetFiles(), "theme");
        return new ShellViewModel(schema, store, history, applier, profile, k => null, new System.Globalization.CultureInfo("en-US"), null, sharing);
    }

    public static void Shell_has_a_profile_page_only_when_sharing_is_available()
    {
        Assert.True(NewShell(null).Pages.All(p => p.Kind != "share"), "no sharing service: no page");
        var page = NewShell(new FakeSharing()).Pages.Single(p => p.Kind == "share");
        Assert.True(page.Editor is SharingViewModel);
    }
}
