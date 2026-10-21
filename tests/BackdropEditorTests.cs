using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LayoutStudio.Core;
using LayoutStudio.Editor;

/// <summary>The generated backdrop: the editor at the top of its block, the constants it writes, undo, and the theme files that carry it.</summary>
public static class BackdropEditorTests
{
    private class Env
    {
        public Schema Schema;
        public ConstantStore Store;
        public History History;
        public Profile Profile;
        public BackdropEditorViewModel Editor;
    }

    private static Env Make(Func<string> cover = null)
    {
        var e = new Env();
        e.Schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        e.Store = new ConstantStore(e.Schema.AllConstants());
        e.History = new History(e.Store);
        e.Profile = new Profile { Name = "p" };
        e.Editor = new BackdropEditorViewModel(e.Store, e.History, e.Profile, k => null, cover);
        return e;
    }

    private static string ThemeDir()
    {
        string dir = Path.Combine(TestPaths.RepoRoot(), "theme", "Modula");
        return Directory.Exists(dir) ? dir : null;   // the exported Layout Studio repository has no theme folder
    }

    private static List<string> BackdropKeys(Schema schema)
    {
        return schema.AllConstants().Where(c => c.Key.StartsWith("Backdrop", StringComparison.Ordinal)).Select(c => c.Key).ToList();
    }

    // ---- the editor --------------------------------------------------------------------------------------------------------------------

    public static void A_fresh_profile_keeps_the_game_artwork_and_changes_nothing()
    {
        var e = Make();
        Assert.Equal("0", e.Store.Get("BackdropMode"));
        Assert.True(e.Editor.Modes[0].IsSelected && !e.Editor.IsGenerated);
        Assert.Equal(0, e.Store.Changed().Count);
    }

    public static void A_mode_button_sets_the_mode_in_one_undo_step()
    {
        var e = Make();
        e.Editor.Modes[2].SelectCommand.Execute(null);
        Assert.Equal("2", e.Store.Get("BackdropMode"));
        Assert.True(e.Editor.Modes[2].IsSelected && !e.Editor.Modes[0].IsSelected && e.Editor.IsGenerated);
        e.History.Undo();
        Assert.Equal("0", e.Store.Get("BackdropMode"));
        Assert.True(e.Editor.Modes[0].IsSelected, "undo moves the selection back");
    }

    public static void Randomize_writes_the_palette_and_the_circles_and_undoes_in_one_step()
    {
        var e = Make();
        e.Editor.Randomize(1234);
        Assert.Equal("1234", e.Store.Get("BackdropSeed"));
        Assert.Equal("4", e.Store.Get("BackdropMode"), "randomizing the artwork switches to the circles");
        var look = BackdropGenerator.Generate(1234, BackdropHarmony.Analogous, BackdropStyle.Aurora);
        foreach (var kv in look.ToConstants()) Assert.Equal(kv.Value, e.Store.Get(kv.Key), kv.Key);
        Assert.True(e.Store.Changed().Count > 20);
        e.History.Undo();
        Assert.Equal(0, e.Store.Changed().Count, "one undo brings back every constant it wrote");
    }

    public static void Randomize_keeps_the_mode_and_the_count_the_user_chose()
    {
        var e = Make();
        string error;
        e.Store.TrySet("BackdropMode", "2", out error);
        e.Store.TrySet("BackdropBlobCount", "3", out error);
        e.Editor.Randomize(77);
        Assert.Equal("2", e.Store.Get("BackdropMode"));
        Assert.Equal("3", e.Store.Get("BackdropBlobCount"));
    }

    public static void The_same_seed_gives_the_same_backdrop_and_a_harmony_changes_the_colours_only()
    {
        var a = Make();
        var b = Make();
        a.Editor.Randomize(900);
        b.Editor.Randomize(900);
        foreach (var k in BackdropKeys(a.Schema)) Assert.Equal(a.Store.Get(k), b.Store.Get(k), k);
        string before = a.Store.Get("BackdropColor1");
        a.Editor.Harmonies.First(h => h.Id == "Warm").SelectCommand.Execute(null);
        Assert.Equal("900", a.Store.Get("BackdropSeed"), "a harmony keeps the seed");
        Assert.Equal("Warm", a.Profile.Editor[BackdropEditorViewModel.HarmonyKey]);
        Assert.True(a.Store.Get("BackdropColor1") != before, "the colours follow the harmony");
        Assert.True(a.Editor.Harmonies.First(h => h.Id == "Warm").IsSelected);
        a.History.Undo();
        Assert.Equal(before, a.Store.Get("BackdropColor1"));
    }

    public static void A_style_changes_the_size_of_the_circles()
    {
        var e = Make();
        e.Editor.Randomize(5);
        double aurora = double.Parse(e.Store.Get("BackdropBlob1Size"), CultureInfo.InvariantCulture);
        e.Editor.Styles.First(s => s.Id == "Bokeh").SelectCommand.Execute(null);
        double bokeh = double.Parse(e.Store.Get("BackdropBlob1Size"), CultureInfo.InvariantCulture);
        Assert.True(bokeh < aurora, "bokeh circles are smaller than aurora ones");
    }

    public static void The_accent_shortcut_builds_the_palette_on_the_accent_hue()
    {
        var e = Make();
        string error;
        e.Store.TrySet("GlyphColor", "#FFE6A817", out error);
        e.Editor.Harmonies.First(h => h.Id == "Mono").SelectCommand.Execute(null);
        e.Editor.FromAccentCommand.Execute(null);
        double h0, s, l, h1;
        Rgb.ToHsl(Rgb.Parse("#FFE6A817"), out h0, out s, out l);
        Rgb.ToHsl(Rgb.Parse(e.Store.Get("BackdropColor3")), out h1, out s, out l);
        Assert.True(Math.Abs(h0 - h1) < 14, "the accent colours stay amber: " + h0 + " vs " + h1);
    }

    public static void The_seed_box_makes_that_backdrop_and_refuses_what_is_not_a_number()
    {
        var e = Make();
        e.Editor.Seed = "321";
        Assert.Equal("321", e.Store.Get("BackdropSeed"));
        Assert.Equal(BackdropGenerator.Generate(321, BackdropHarmony.Analogous, BackdropStyle.Aurora).Colors[0], e.Store.Get("BackdropColor1"));
        string colour = e.Store.Get("BackdropColor1");
        e.Editor.Seed = "abc";
        Assert.True(e.Editor.HasSeedError);
        Assert.Equal(colour, e.Store.Get("BackdropColor1"), "nothing changes on a bad seed");
        e.Editor.Seed = "322";
        Assert.False(e.Editor.HasSeedError);
    }

    public static void The_cover_shortcut_needs_a_picture_and_uses_its_dominant_colour()
    {
        var none = Make(() => null);
        Assert.False(none.Editor.HasCover);
        string path = Path.Combine(Path.GetTempPath(), "backdrop-test-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            var pixels = new byte[32 * 32 * 4];
            for (int i = 0; i < 32 * 32; i++) { pixels[i * 4] = 200; pixels[i * 4 + 1] = 70; pixels[i * 4 + 2] = 40; pixels[i * 4 + 3] = 255; }   // blue
            var bmp = System.Windows.Media.Imaging.BitmapSource.Create(32, 32, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, pixels, 32 * 4);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
            using (var fs = File.Create(path)) enc.Save(fs);
            var e = Make(() => path);
            Assert.True(e.Editor.HasCover);
            e.Editor.Harmonies.First(h => h.Id == "Mono").SelectCommand.Execute(null);
            e.Editor.FromCoverCommand.Execute(null);
            double h, s, l;
            Rgb.ToHsl(Rgb.Parse(e.Store.Get("BackdropColor3")), out h, out s, out l);
            Assert.True(h > 200 && h < 250, "the palette of a blue picture is blue, hue " + h);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    public static void The_dominant_colour_is_the_vivid_one_and_grey_pictures_give_their_average()
    {
        var px = new byte[100 * 4];
        for (int i = 0; i < 100; i++)
        {
            bool red = i < 20;   // 20 red pixels among 80 grey ones: the red stands out
            px[i * 4] = (byte)(red ? 20 : 120); px[i * 4 + 1] = (byte)(red ? 30 : 120); px[i * 4 + 2] = (byte)(red ? 220 : 120); px[i * 4 + 3] = 255;
        }
        double h, s, l;
        Rgb.ToHsl(Rgb.Parse(DominantColor.Of(px, 100)), out h, out s, out l);
        Assert.True(h < 10 || h > 350, "red wins over grey, hue " + h);
        for (int i = 0; i < 100; i++) { px[i * 4] = 90; px[i * 4 + 1] = 90; px[i * 4 + 2] = 90; }
        Assert.Equal("#FF5A5A5A", DominantColor.Of(px, 100));
        Assert.Equal(null, DominantColor.Of(new byte[0], 0));
    }

    // ---- the options, the block and the presets ----------------------------------------------------------------------------------------

    public static void Every_backdrop_option_has_exactly_one_home_in_the_backdrop_block()
    {
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        var keys = BackdropKeys(schema);
        Assert.True(keys.Count >= 60, "the backdrop has its constants: " + keys.Count);
        foreach (var k in keys) Assert.Equal("window/backdrop", schema.Homes.First(h => h.Key == k).Value, k);
        var block = schema.Pages.First(p => p.Id == "window").Groups.First(g => g.Id == "backdrop");
        Assert.Equal("backdrop", block.Editor);
        Assert.True(block.Controls.Any(c => c.Kind == "preset" && c.Id == "Backdrop"), "the ready-made looks are the presets of the block");
        var pattern = block.Controls.First(c => c.Keys.Contains("BackdropPatternKind"));
        Assert.Equal("choice", pattern.Kind);
        Assert.Equal(4, pattern.Choices.Count);
    }

    public static void The_shell_shows_the_backdrop_editor_on_the_window_page_with_its_regions_and_counts_its_changes()
    {
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        var store = new ConstantStore(schema.AllConstants());
        var history = new History(store);
        var profile = new Profile { Name = "Default" };
        var applier = new ProfileApplier(schema, store, new FakePresetFiles(), @"C:\theme");
        var shell = new ShellViewModel(schema, store, history, applier, profile, k => null, new CultureInfo("en-US"), null, FakeGlobalSharing.FromTheme(), null, null, null, null, null, null, null);
        var block = shell.Pages.First(p => p.Id == "window").Groups.First(g => g.Id == "backdrop");
        Assert.True(block.Editor is BackdropEditorViewModel, "the block shows the backdrop editor");
        Assert.True(block.Regions.Contains("window"), "hovering the block lights the window of the plan");
        Assert.Equal(0, block.ChangedCount);
        string error;
        store.TrySet("BackdropMode", "3", out error);
        Assert.True(block.ChangedCount >= 1, "a change of the mode counts on the block");
    }

    public static void A_look_of_the_backdrop_group_sets_only_backdrop_constants_that_the_store_accepts()
    {
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        var group = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).First(c => c.Kind == "preset" && c.Id == "Backdrop");
        Assert.True(group.Choices.Count >= 11, "at least ten ready-made looks and the artwork: " + group.Choices.Count);
        var store = new ConstantStore(schema.AllConstants());
        foreach (var choice in group.Choices)
            foreach (var kv in choice.Constants)
            {
                string error;
                Assert.True(kv.Key.StartsWith("Backdrop", StringComparison.Ordinal), choice.Id + " sets " + kv.Key + ", which is not a backdrop constant");
                Assert.True(store.TrySet(kv.Key, kv.Value, out error), choice.Id + ": " + kv.Key + " = " + kv.Value + " " + error);
            }
        Assert.Equal(0, group.Choices.First(c => c.Id == "Default").Constants.Count());
        Assert.True(group.Choices.Where(c => c.Id != "Default").All(c => c.Constants.Single(kv => kv.Key == "BackdropMode").Value != "0"), "a look turns the generated backdrop on");
    }

    public static void Choosing_a_look_then_default_gives_the_artwork_back_with_every_constant_at_its_default()
    {
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        var store = new ConstantStore(schema.AllConstants());
        var history = new History(store);
        var profile = new Profile { Name = "Default" };
        var applier = new ProfileApplier(schema, store, new FakePresetFiles(), @"C:\theme");
        PresetHistory.Apply(history, applier, profile, "Backdrop", "Candy");
        Assert.Equal("4", store.Get("BackdropMode"));
        Assert.True(store.Changed().Count > 10);
        PresetHistory.Apply(history, applier, profile, "Backdrop", "Default");
        Assert.Equal(0, store.Changed().Count, "the default look leaves nothing behind: " + string.Join(",", store.Changed()));
    }

    public static void Every_look_has_a_preview_and_a_name_in_both_theme_language_files()
    {
        string theme = ThemeDir();
        if (theme == null) return;
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        var group = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).First(c => c.Kind == "preset" && c.Id == "Backdrop");
        string en = File.ReadAllText(Path.Combine(theme, "Localization", "en_US.xaml")), fr = File.ReadAllText(Path.Combine(theme, "Localization", "fr_FR.xaml"));
        foreach (var choice in group.Choices)
        {
            Assert.True(en.Contains("x:Key=\"" + choice.LocKey + "\""), choice.Id + " has no English name");
            Assert.True(fr.Contains("x:Key=\"" + choice.LocKey + "\""), choice.Id + " has no French name");
            if (choice.Id != "Default") Assert.True(File.Exists(Path.Combine(theme, "Presets", "Previews", "Backdrop_" + choice.Id + ".png")), choice.Id + " has no preview picture");
        }
    }

    public static void Every_option_and_tip_of_the_backdrop_has_its_words_in_english_and_french()
    {
        string theme = ThemeDir();
        if (theme == null) return;
        string en = File.ReadAllText(Path.Combine(theme, "Localization", "en_US.xaml")), fr = File.ReadAllText(Path.Combine(theme, "Localization", "fr_FR.xaml"));
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        foreach (var control in schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Where(c => c.Keys.Any(k => k.StartsWith("Backdrop", StringComparison.Ordinal))))
        {
            Assert.True(control.TitleKey != null && en.Contains("x:Key=\"" + control.TitleKey + "\"") && fr.Contains("x:Key=\"" + control.TitleKey + "\""), control.Id + " title");
            if (control.TipKey != null) Assert.True(en.Contains("x:Key=\"" + control.TipKey + "\"") && fr.Contains("x:Key=\"" + control.TipKey + "\""), control.Id + " tip");
            if (control.Kind == "choice") foreach (var ch in control.Choices) Assert.True(en.Contains("x:Key=\"" + ch.LocKey + "\"") && fr.Contains("x:Key=\"" + ch.LocKey + "\""), ch.Id + " choice");
        }
    }

    public static void The_layers_of_the_window_read_only_constants_that_exist_and_every_constant_is_used()
    {
        string theme = ThemeDir();
        if (theme == null) return;
        string library = File.ReadAllText(Path.Combine(theme, "Views", "Library.xaml")), constants = File.ReadAllText(Path.Combine(theme, "Constants.xaml"));
        // the generated region only: the theme has other brushes whose name starts with Backdrop (BackdropGlass1)
        int begin = constants.IndexOf("BEGIN generated backdrop constants"), end = constants.IndexOf("END generated backdrop constants");
        Assert.True(begin > 0 && end > begin, "the generated region is in Constants.xaml");
        var declared = new HashSet<string>(Regex.Matches(constants.Substring(begin, end - begin), "x:Key=\"(Backdrop\\w+)\"").Cast<Match>().Select(m => m.Groups[1].Value));
        var used = new HashSet<string>(Regex.Matches(library, "DynamicResource (Backdrop\\w+)").Cast<Match>().Select(m => m.Groups[1].Value));
        foreach (var key in used) Assert.True(declared.Contains(key), "Library.xaml reads " + key + ", which Constants.xaml does not declare");
        foreach (var key in declared.Where(k => k != "BackdropSeed")) Assert.True(used.Contains(key), key + " is declared but no layer reads it");
        Assert.False(Regex.IsMatch(library, "StaticResource Backdrop(?!Mode|\\w*Probe)\\w*Color"), "a backdrop colour read with StaticResource could not change live");
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        Assert.Equal(string.Join(",", declared.OrderBy(k => k)), string.Join(",", BackdropKeys(schema).OrderBy(k => k)), "the theme and the schema declare the same constants");
    }

    public static void The_window_background_gradient_constants_are_part_of_the_system()
    {
        string theme = ThemeDir();
        if (theme == null) return;
        string library = File.ReadAllText(Path.Combine(theme, "Views", "Library.xaml"));
        Assert.True(library.Contains("WindowBackgroundGradientColor1") && library.Contains("WindowBackgroundGradientColor2"),
            "the two gradient colours of the window are drawn by the layers of the window, so they have a visible effect");
    }

    public static void A_global_preset_bundle_can_choose_a_look_and_set_every_backdrop_constant()
    {
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        var bundle = new GlobalPreset { Id = "Test" };
        bundle.Name["en"] = "Test"; bundle.Name["fr"] = "Test"; bundle.Intent["en"] = "Test"; bundle.Intent["fr"] = "Test";
        bundle.Presets["Backdrop"] = "Sunset";
        foreach (var kv in BackdropGenerator.Generate(42, BackdropHarmony.Cold, BackdropStyle.Mixed).ToConstants()) bundle.Overrides[kv.Key] = kv.Value;
        bundle.Overrides["BackdropMode"] = "3";
        foreach (var key in BackdropKeys(schema)) if (!bundle.Overrides.ContainsKey(key)) bundle.Overrides[key] = schema.AllConstants().First(c => c.Key == key).Default;
        Assert.Equal(string.Join("; ", BackdropKeys(schema).OrderBy(k => k)), string.Join("; ", bundle.Overrides.Keys.Where(k => k.StartsWith("Backdrop", StringComparison.Ordinal)).OrderBy(k => k)), "every backdrop constant is set");
        Assert.Equal(0, bundle.Problems(schema).Count, string.Join(", ", bundle.Problems(schema)));
        var store = new ConstantStore(schema.AllConstants());
        var applier = new ProfileApplier(schema, store, new FakePresetFiles(), @"C:\theme");
        applier.ApplyProfile(bundle.ToProfile("Default"));
        Assert.Equal("3", store.Get("BackdropMode"), "an override wins over the look of the bundle");
        Assert.Equal(BackdropGenerator.Generate(42, BackdropHarmony.Cold, BackdropStyle.Mixed).Colors[0], store.Get("BackdropColor1"), "the generated colours are set too");
    }

    public static void Hovering_any_backdrop_setting_lights_the_window_of_the_plan()
    {
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        foreach (var key in BackdropKeys(schema))
        {
            var regions = SettingRegions.For(key);
            Assert.True(regions != null && regions.Length == 1 && regions[0] == "window", key + " lights the window");
        }
    }

    public static void The_artwork_stays_the_default_so_nothing_changes_for_anyone_who_does_not_use_it()
    {
        var schema = Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
        Assert.Equal("0", schema.AllConstants().First(c => c.Key == "BackdropMode").Default);
        Assert.Equal("False", schema.AllConstants().First(c => c.Key == "BackdropFallbackOnly").Default);
        Assert.Equal("True", schema.AllConstants().First(c => c.Key == "WindowGlassEnabled").Default);
    }
}
