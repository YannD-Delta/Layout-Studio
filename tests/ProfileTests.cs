using System;
using System.IO;
using System.Linq;
using LayoutStudio.Core;

public static class ProfileTests
{
    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ls-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static Profile Sample()
    {
        var p = new Profile { Name = "Dark glass" };
        p.Overrides["WindowGlassBlurRadius"] = "12";
        p.Overrides["PanelGlassOpacity"] = "0.2";
        p.Presets["PageBackground"] = "Dark";
        return p;
    }

    public static void Roundtrip_keeps_overrides()
    {
        var store = new ProfileStore(TempDir());
        store.Save(Sample());
        var loaded = store.Load("Dark glass");
        Assert.Equal("Dark glass", loaded.Name);
        Assert.Equal("12", loaded.Overrides["WindowGlassBlurRadius"]);
        Assert.Equal("0.2", loaded.Overrides["PanelGlassOpacity"]);
        Assert.Equal("Dark", loaded.Presets["PageBackground"]);
        Assert.False(loaded.ReadOnly);
    }

    public static void Corrupt_file_loads_defaults_and_keeps_bak()
    {
        var dir = TempDir();
        var store = new ProfileStore(dir);
        store.Save(Sample());
        var file = Directory.GetFiles(dir, "*.profile.json").Single();
        File.WriteAllText(file, "{ this is not json");

        var loaded = store.Load("Dark glass");
        Assert.Equal(0, loaded.Overrides.Count, "a corrupt profile falls back to defaults");
        Assert.True(loaded.Warning != null, "the user is told why");
        Assert.True(Directory.GetFiles(dir, "*.bak").Length >= 1, "the bad file is kept as .bak");
    }

    public static void Names_come_from_file_names_for_copied_files()
    {
        var dir = TempDir();
        var store = new ProfileStore(dir);
        store.Save(new Profile { Name = "Default" });
        File.Copy(Path.Combine(dir, "Default.profile.json"), Path.Combine(dir, "Work.profile.json"));
        var names = store.Names().OrderBy(n => n).ToList();
        Assert.Equal("Default,Work", string.Join(",", names), "a copied file is listed under its own file name");
        Assert.Equal("Work", store.Load("Work").Name);
    }

    public static void Two_names_that_sanitize_the_same_do_not_share_a_file()
    {
        var dir = TempDir();
        var store = new ProfileStore(dir);
        var a = new Profile { Name = "a:b" }; a.Overrides["K"] = "1";
        var b = new Profile { Name = "a?b" }; b.Overrides["K"] = "2";
        store.Save(a); store.Save(b);
        Assert.Equal(2, Directory.GetFiles(dir, "*.profile.json").Length);
        Assert.Equal("1", store.Load("a:b").Overrides["K"]);
        Assert.Equal("2", store.Load("a?b").Overrides["K"]);
        foreach (var listed in store.Names()) Assert.True(store.Load(listed).Overrides.Count == 1, "every listed name opens its own file");
    }

    public static void A_file_that_stays_corrupt_gets_one_copy_not_one_per_load()
    {
        var dir = TempDir();
        var store = new ProfileStore(dir);
        store.Save(Sample());
        File.WriteAllText(Directory.GetFiles(dir, "*.profile.json").Single(), "{ not json");
        for (int i = 0; i < 3; i++) store.Load("Dark glass");
        Assert.Equal(1, Directory.GetFiles(dir, "*.corrupt-*.bak").Length);
    }

    public static void Pruning_keeps_the_newest_ten_backups()
    {
        var dir = TempDir();
        var store = new ProfileStore(dir);
        store.Save(Sample());
        for (int i = 0; i < 14; i++) { System.Threading.Thread.Sleep(3); store.Backup("Dark glass", "reset"); }
        var changed = Sample(); changed.Overrides["Extra"] = "1";
        store.Save(changed);   // a save prunes
        Assert.Equal(10, Directory.GetFiles(dir, "*.before-reset-*.bak").Length);
    }

    public static void Missing_profile_loads_empty_default_profile()
    {
        var store = new ProfileStore(TempDir());
        var loaded = store.Load("Nothing here");
        Assert.Equal("Nothing here", loaded.Name);
        Assert.Equal(0, loaded.Overrides.Count);
    }

    public static void Newer_version_is_read_only_with_warning()
    {
        var dir = TempDir();
        var store = new ProfileStore(dir);
        store.Save(Sample());
        var file = Directory.GetFiles(dir, "*.profile.json").Single();
        File.WriteAllText(file, File.ReadAllText(file).Replace("\"Version\":" + Profile.CurrentVersion, "\"Version\":99"));

        var loaded = store.Load("Dark glass");
        Assert.True(loaded.ReadOnly, "a profile from a newer version is not overwritten");
        Assert.True(loaded.Warning != null);
        Assert.Equal("12", loaded.Overrides["WindowGlassBlurRadius"], "values are still readable");
        Assert.Throws<InvalidOperationException>(() => store.Save(loaded));
    }

    public static void Export_import_roundtrip()
    {
        var store = new ProfileStore(TempDir());
        string code = store.Export(Sample());
        Assert.True(code.StartsWith("LS1:"));
        string error;
        var imported = store.Import(code, out error);
        Assert.Equal(null, error);
        Assert.Equal("Dark glass", imported.Name);
        Assert.Equal("0.2", imported.Overrides["PanelGlassOpacity"]);
        Assert.Equal("Dark", imported.Presets["PageBackground"]);
    }

    public static void Import_garbage_returns_error_not_exception()
    {
        var store = new ProfileStore(TempDir());
        string error;
        Assert.Equal(null, store.Import("hello", out error));
        Assert.True(error != null);
        Assert.Equal(null, store.Import("LS1:@@@not-base64@@@", out error));
        Assert.True(error != null);
        Assert.Equal(null, store.Import("LS1:" + Convert.ToBase64String(new byte[] { 1, 2, 3 }), out error));
        Assert.True(error != null);
    }

    public static void Save_writes_backup_of_previous_file()
    {
        var dir = TempDir();
        var store = new ProfileStore(dir);
        var p = Sample();
        store.Save(p);
        p.Overrides["WindowGlassBlurRadius"] = "40";
        store.Save(p);
        var bak = Directory.GetFiles(dir, "*.bak");
        Assert.Equal(1, bak.Length);
        Assert.True(File.ReadAllText(bak[0]).Contains("\"12\""), "the backup holds the previous content");
        Assert.Equal("40", store.Load("Dark glass").Overrides["WindowGlassBlurRadius"]);
    }

    public static void Names_and_active_profile_persist()
    {
        var dir = TempDir();
        var store = new ProfileStore(dir);
        store.Save(Sample());
        store.Save(new Profile { Name = "Plain" });
        store.Active = "Plain";
        var again = new ProfileStore(dir);
        Assert.Equal("Plain", again.Active);
        var names = again.Names().OrderBy(n => n).ToList();
        Assert.Equal(2, names.Count);
        Assert.Equal("Dark glass", names[0]);
    }

    public static void Names_with_invalid_file_characters_are_safe()
    {
        var dir = TempDir();
        var store = new ProfileStore(dir);
        store.Save(new Profile { Name = "a/b:c*?" });
        Assert.Equal("a/b:c*?", store.Load("a/b:c*?").Name);
        Assert.Equal(1, Directory.GetFiles(dir, "*.profile.json").Length);
    }

    public static void Saving_unchanged_content_keeps_the_previous_backup()
    {
        var dir = TempDir();
        var store = new ProfileStore(dir);
        var a = new Profile { Name = "p" }; a.Overrides["X"] = "1";
        var b = new Profile { Name = "p" }; b.Overrides["X"] = "2";
        store.Save(a);
        store.Save(b);
        store.Save(b);   // an autosave with nothing new
        string bak = File.ReadAllText(Path.Combine(dir, "p.bak"));
        Assert.True(bak.Contains("\"1\""), "the backup still holds the state before the last real change");
    }

    public static void Backup_is_a_separate_copy_that_later_saves_do_not_touch()
    {
        var dir = TempDir();
        var store = new ProfileStore(dir);
        var a = new Profile { Name = "p" }; a.Overrides["X"] = "1";
        store.Save(a);
        string copy = store.Backup("p", "reset");
        Assert.True(File.Exists(copy), "the copy exists");
        Assert.True(Path.GetFileName(copy).StartsWith("p.before-reset-"), "named after what is about to happen");
        var empty = new Profile { Name = "p" };
        store.Save(empty); store.Save(empty); store.Save(empty);
        Assert.True(File.ReadAllText(copy).Contains("\"1\""), "autosaves never overwrite it");
    }

    public static void Backup_of_a_missing_profile_returns_null()
    {
        Assert.True(new ProfileStore(TempDir()).Backup("nothing", "reset") == null);
    }

    public static void Corrupt_file_falls_back_to_the_last_good_bak_and_keeps_a_copy()
    {
        var dir = TempDir();
        var store = new ProfileStore(dir);
        var a = new Profile { Name = "p" }; a.Overrides["X"] = "1";
        var b = new Profile { Name = "p" }; b.Overrides["X"] = "2";
        store.Save(a); store.Save(b);   // p.bak now holds a
        File.WriteAllText(Path.Combine(dir, "p.profile.json"), "{ truncated by a power cut");
        var loaded = store.Load("p");
        Assert.Equal("1", loaded.Overrides["X"], "the last good copy is used instead of the defaults");
        Assert.True(loaded.Warning != null && loaded.Warning.Contains("p.bak"), "the user is told which copy was used");
        Assert.True(Directory.GetFiles(dir, "p.corrupt-*.bak").Length == 1, "the broken file is kept for inspection");
    }

    public static void A_locked_file_is_not_treated_as_corruption()
    {
        var dir = TempDir();
        var store = new ProfileStore(dir);
        store.Save(Sample());
        var path = Directory.GetFiles(dir, "*.profile.json").Single();
        Profile loaded;
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            loaded = store.Load("Dark glass");
        }
        Assert.True(loaded.ReadOnly, "nothing is saved over a file we could not read");
        Assert.True(loaded.Warning != null);
        Assert.True(File.Exists(path), "the file stays where it is");
        Assert.Equal(0, Directory.GetFiles(dir, "*.corrupt-*").Length);
    }

    public static void Save_leaves_no_temporary_file()
    {
        var dir = TempDir();
        var store = new ProfileStore(dir);
        store.Save(Sample());
        var changed = Sample(); changed.Overrides["WindowGlassBlurRadius"] = "13";
        store.Save(changed);
        Assert.Equal(0, Directory.GetFiles(dir, "*.tmp").Length);
        Assert.Equal("13", store.Load("Dark glass").Overrides["WindowGlassBlurRadius"]);
    }

    public static void Load_uses_the_requested_name_even_if_the_file_says_otherwise()
    {
        var dir = TempDir();
        var store = new ProfileStore(dir);
        store.Save(new Profile { Name = "a" });
        File.Copy(Path.Combine(dir, "a.profile.json"), Path.Combine(dir, "b.profile.json"));
        Assert.Equal("b", store.Load("b").Name, "a copied file must not save back into the original");
    }

    public static void Editor_choices_round_trip_and_older_files_without_them_still_load()
    {
        var dir = TempDir();
        var store = new ProfileStore(dir);
        var p = new Profile { Name = "e" };
        p.Editor["ratio.cover"] = "3:4";
        store.Save(p);
        Assert.Equal("3:4", store.Load("e").Editor["ratio.cover"]);
        File.WriteAllText(Path.Combine(dir, "old.profile.json"), "{\"Name\":\"old\",\"Version\":1,\"Overrides\":{},\"Presets\":{}}");
        Assert.Equal(0, store.Load("old").Editor.Count);
        string code = store.Export(p);
        string error;
        Assert.Equal("3:4", store.Import(code, out error).Editor["ratio.cover"]);
    }

    public static void An_old_panel_opacity_becomes_one_value_per_zone_without_overriding_a_zone_already_set()
    {
        var p = new Profile { Name = "Old" };
        p.Overrides["PanelGlassOpacity"] = "0";
        p.Overrides["SidebarGlassOpacity"] = "0.5";
        LegacyKeys.Migrate(p);
        Assert.False(p.Overrides.ContainsKey("PanelGlassOpacity"));
        Assert.Equal("0", p.Overrides["TopPanelGlassOpacity"]);
        Assert.Equal("0", p.Overrides["LibraryPanelGlassOpacity"]);
        Assert.Equal("0", p.Overrides["FilterPanelGlassOpacity"]);
        Assert.Equal("0.5", p.Overrides["SidebarGlassOpacity"], "a zone the user already set stays");
        LegacyKeys.Migrate(p);
        Assert.Equal(4, p.Overrides.Count);
    }}
