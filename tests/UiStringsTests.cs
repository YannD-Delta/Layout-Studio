using System.Linq;
using LayoutStudio.Core;
using LayoutStudio.Editor;

public static class UiStringsTests
{
    public static void Missing_translations_fall_back_to_english()
    {
        var ui = new UiStrings();
        Assert.Equal("Turn on", ui.Get("LOCLS_X", "Turn on"));
        ui.Lookup = k => null;
        Assert.Equal("Turn on", ui.Get("LOCLS_X", "Turn on"));
    }

    public static void A_translation_replaces_the_english_and_formats_its_numbers()
    {
        var ui = new UiStrings { Lookup = k => k == "LOCLS_Many" ? "{0} choses à corriger." : k == "LOCLS_Odd" ? "{bad" : null };
        Assert.Equal("3 choses à corriger.", ui.Format("LOCLS_Many", "{0} things to fix.", 3));
        Assert.Equal("3 things to fix.", ui.Format("LOCLS_None", "{0} things to fix.", 3));
        Assert.Equal("{bad", ui.Format("LOCLS_Odd", "{0} x", 1), "a broken translation is shown as it is, never an exception");
    }

    private static UiStrings French()
    {
        return new UiStrings { Lookup = k => k == "LOCLS_Adv_playniteDarken_Title" ? "Assombrir l'arrière-plan"
            : k == "LOCLS_Adv_Status_Attention" ? "À corriger" : k == "LOCLS_Adv_Summary_Many" ? "{0} choses à corriger." : null };
    }

    public static void The_advice_page_uses_the_translation_and_keeps_the_numbers()
    {
        var fake = new FakeSettings();   // darkening 0.85 and blur 39
        var vm = new AdviceViewModel(new PlayniteSettingsViewModel(fake), new ConstantStore(new ConstantDef[0]), null, new Profile { Name = "Default" }, null, French());
        var darken = vm.Items.Single(i => i.Id == "playniteDarken");
        Assert.Equal("Assombrir l'arrière-plan (0.85)", darken.Title);
        Assert.Equal("À corriger", darken.StatusText);
        Assert.Equal("Blur the window background (39)", vm.Items.Single(i => i.Id == "playniteBlur").Title, "no translation: english");
        Assert.True(vm.Summary.StartsWith("2 choses"), vm.Summary);
    }

    public static void The_banner_message_uses_the_translation()
    {
        var ui = new UiStrings { Lookup = k => k == "LOCLS_Banner_Layers" ? "Playnite applique {0}." : k == "LOCLS_Banner_PartBlur" ? "flou ({0})" : k == "LOCLS_Banner_PartDark" ? "assombrissement ({0})" : k == "LOCLS_Banner_And" ? " et " : null };
        var vm = new PlayniteSettingsViewModel(new FakeSettings(), null, ui);
        Assert.Equal("Playnite applique flou (39) et assombrissement (0.85).", vm.Findings[0].Message);
    }

    public static void Texts_are_translated_when_the_dictionary_arrives_after_the_view_models()
    {
        var ui = new UiStrings();   // the editor window sets Lookup once it has loaded its dictionary, after the view models exist
        var vm = new AdviceViewModel(new PlayniteSettingsViewModel(new FakeSettings(), null, ui), new ConstantStore(new ConstantDef[0]), null, new Profile { Name = "Default" }, null, ui);
        Assert.Equal("Darken the window background (0.85)", vm.Items.Single(i => i.Id == "playniteDarken").Title);
        ui.Lookup = k => k == "LOCLS_Adv_playniteDarken_Title" ? "Assombrir l'arrière-plan" : null;
        Assert.Equal("Assombrir l'arrière-plan (0.85)", vm.Items.Single(i => i.Id == "playniteDarken").Title);
    }
}
