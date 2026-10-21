using System.Collections.Generic;
using System.Linq;
using LayoutStudio.Editor;

public class FakeSwitching : FakeSharing, IProfileSwitching
{
    public List<string> Names = new List<string> { "Default", "Night" };
    public string Active = "Default";
    public string SwitchError;
    public IList<string> ProfileNames() { return Names.ToList(); }
    public string ActiveProfileName { get { return Active; } }
    public string SwitchProfile(string name) { Calls.Add("switch " + name); if (SwitchError == null) Active = name; return SwitchError; }
    public string NewProfile(string name) { Calls.Add("new " + name); if (SwitchError != null) return SwitchError; Names.Add(name); Active = name; return null; }
}

public static class ProfileSwitcherTests
{
    public static void The_page_lists_the_profiles_and_marks_the_one_in_use()
    {
        var vm = new SharingViewModel(new FakeSwitching(), null);
        Assert.True(vm.HasSwitcher);
        Assert.Equal(2, vm.Profiles.Count);
        Assert.True(vm.Profiles.Single(p => p.Name == "Default").IsActive);
        Assert.False(vm.Profiles.Single(p => p.Name == "Night").IsActive);
    }

    public static void Switching_asks_the_plugin_and_moves_the_mark()
    {
        var fake = new FakeSwitching();
        var vm = new SharingViewModel(fake, null);
        vm.Profiles.Single(p => p.Name == "Night").SwitchCommand.Execute(null);
        Assert.Equal("switch Night", fake.Calls.Single());
        Assert.True(vm.Profiles.Single(p => p.Name == "Night").IsActive);
        Assert.False(vm.IsError);
    }

    public static void A_refused_switch_shows_the_reason_and_keeps_the_mark()
    {
        var fake = new FakeSwitching { SwitchError = "the active profile is read-only" };
        var vm = new SharingViewModel(fake, null);
        vm.Profiles.Single(p => p.Name == "Night").SwitchCommand.Execute(null);
        Assert.True(vm.IsError);
        Assert.True(vm.Message.Contains("read-only"));
        Assert.True(vm.Profiles.Single(p => p.Name == "Default").IsActive);
    }

    public static void A_new_profile_needs_a_name_and_is_listed_once_made()
    {
        var fake = new FakeSwitching();
        var vm = new SharingViewModel(fake, null);
        Assert.False(vm.NewProfileCommand.CanExecute(null), "no name, no button");
        vm.NewProfileName = "  Quiet  ";
        Assert.True(vm.NewProfileCommand.CanExecute(null));
        vm.NewProfileCommand.Execute(null);
        Assert.Equal("new Quiet", fake.Calls.Single(), "the name is trimmed");
        Assert.True(vm.Profiles.Single(p => p.Name == "Quiet").IsActive);
        Assert.Equal("", vm.NewProfileName);
    }

    public static void Without_a_switching_service_the_page_has_no_switcher()
    {
        var vm = new SharingViewModel(new FakeSharing(), null);
        Assert.False(vm.HasSwitcher);
        Assert.Equal(0, vm.Profiles.Count);
    }
}
