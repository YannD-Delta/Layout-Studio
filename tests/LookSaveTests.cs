using System.Collections.Generic;
using System.IO;
using LayoutStudio.Core;

/// <summary>A locked look.profile.json (sync client, antivirus) must never stop anything else from being saved.</summary>
public static class LookSaveTests
{
    private const string TableJson = @"{ ""sharedGroups"": [""Corners""], ""tokens"": { ""radius_card"": {""type"":""CornerRadius"",""constants"":[""FsCardCornerRadius""],""scale"":0.62} } }";

    private static Look Make(out string path)
    {
        string dir = Path.Combine(Path.GetTempPath(), "ls-looksave-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        path = Path.Combine(dir, "look.profile.json");
        return Look.Load(path, LookTable.Parse(TableJson));
    }

    public static void A_locked_look_file_is_logged_and_retried_never_thrown()
    {
        string path;
        var look = Make(out path);
        look.SetToken("radius_card", "20");
        Assert.True(look.TrySaveIfDirty(null), "the first save creates the file");
        Assert.True(File.Exists(path));
        look.SetToken("radius_card", "30");
        var log = new List<string>();
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))   // another program holds the file
        {
            bool ok = look.TrySaveIfDirty(log.Add);
            Assert.False(ok, "the save failed");
            Assert.True(look.Dirty, "it stays dirty: retried at the next change");
            Assert.Equal(1, log.Count);
            Assert.True(log[0].Contains("could not be saved"));
        }
        Assert.True(look.TrySaveIfDirty(log.Add), "once the file is free it saves");
        Assert.False(look.Dirty);
        Assert.False(File.Exists(path + ".tmp"), "no half-written file is left beside it");
    }

    public static void A_look_changed_by_another_instance_is_read_again_unless_it_has_unsaved_changes()
    {
        string path;
        var mine = Make(out path);
        mine.SetToken("radius_card", "20");
        mine.TrySaveIfDirty(null);
        var other = Look.Load(path, LookTable.Parse(TableJson));   // the Fullscreen profile edited from the desktop
        other.SetToken("radius_card", "40");
        other.TrySaveIfDirty(null);
        Assert.True(mine.ReloadIfClean(), "clean: the new values come in");
        Assert.Equal("40", mine.Tokens["radius_card"]);
        mine.SetToken("radius_card", "50");
        other.SetToken("radius_card", "60");
        other.TrySaveIfDirty(null);
        Assert.False(mine.ReloadIfClean(), "an unsaved change here is never dropped");
        Assert.Equal("50", mine.Tokens["radius_card"]);
    }

    public static void A_clean_look_saves_nothing_and_succeeds()
    {
        string path;
        var look = Make(out path);
        Assert.True(look.TrySaveIfDirty(null));
        Assert.False(File.Exists(path));
    }
}
