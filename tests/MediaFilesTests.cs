using System.IO;
using System.Linq;
using LayoutStudio.Core;

public static class MediaFilesTests
{
    private static string Temp()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ls-media-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static void Screenshots_are_listed_without_thumbnails_and_limited()
    {
        string root = Temp();
        string game = Path.Combine(root, "g1", "steam");
        Directory.CreateDirectory(game);
        foreach (var name in new[] { "a.jpg", "a_thumb.jpg", "b.png", "c.webp", "notes.txt", "d.JPG" }) File.WriteAllText(Path.Combine(game, name), "x");
        var all = MediaFiles.Screenshots(root, "g1", 10).Select(Path.GetFileName).ToList();
        Assert.Equal(4, all.Count, "a, b, c, d: no thumbnail, no text file");
        Assert.True(!all.Contains("a_thumb.jpg") && !all.Contains("notes.txt"));
        Assert.Equal(2, MediaFiles.Screenshots(root, "g1", 2).Count, "limited");
    }

    public static void A_missing_folder_or_a_bad_argument_gives_an_empty_list()
    {
        string root = Temp();
        Assert.Equal(0, MediaFiles.Screenshots(root, "nope", 5).Count);
        Assert.Equal(0, MediaFiles.Screenshots(null, "g", 5).Count);
        Assert.Equal(0, MediaFiles.Screenshots(root, null, 5).Count);
        Assert.Equal(0, MediaFiles.Screenshots(root, "g", 0).Count);
    }

    public static void The_micro_trailer_is_preferred_to_the_full_trailer()
    {
        string cfg = Temp();
        string dir = Path.Combine(cfg, "ExtraMetadata", "games", "g1");
        Directory.CreateDirectory(dir);
        Assert.Equal(null, MediaFiles.Video(cfg, "g1"));
        File.WriteAllText(Path.Combine(dir, "VideoTrailer.mp4"), "x");
        Assert.Equal("VideoTrailer.mp4", Path.GetFileName(MediaFiles.Video(cfg, "g1")));
        File.WriteAllText(Path.Combine(dir, "VideoMicrotrailer.mp4"), "x");
        Assert.Equal("VideoMicrotrailer.mp4", Path.GetFileName(MediaFiles.Video(cfg, "g1")));
        Assert.Equal(null, MediaFiles.Video(cfg, "other"));
    }
}
