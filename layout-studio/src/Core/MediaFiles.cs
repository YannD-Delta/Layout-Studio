using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>
    /// Finds the media the Fullscreen details page shows for a game, straight from the folders the add-ons fill (their controls are Desktop-only and
    /// expose no paths to a theme): the screenshots Screenshot Utilities keeps per game, and the trailer Extra Metadata Loader downloads.
    /// </summary>
    public static class MediaFiles
    {
        private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        /// <summary>Up to <paramref name="max"/> screenshot files of a game, newest folders first; thumbnails (name ending in _thumb) are skipped. Empty when the folder is missing.</summary>
        public static List<string> Screenshots(string screenshotsRoot, string gameId, int max)
        {
            var result = new List<string>();
            try
            {
                if (string.IsNullOrEmpty(screenshotsRoot) || string.IsNullOrEmpty(gameId) || max <= 0) return result;
                string dir = Path.Combine(screenshotsRoot, gameId);
                if (!Directory.Exists(dir)) return result;
                result = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                    .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()) && !Path.GetFileNameWithoutExtension(f).EndsWith("_thumb", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .Take(max)
                    .ToList();
            }
            catch (Exception)
            {
                // a folder that cannot be read (antivirus, removed while listing) simply shows nothing
            }
            return result;
        }

        /// <summary>The trailer of a game (the micro trailer first: short, made to loop), or null.</summary>
        public static string Video(string configurationPath, string gameId)
        {
            try
            {
                if (string.IsNullOrEmpty(configurationPath) || string.IsNullOrEmpty(gameId)) return null;
                string dir = Path.Combine(configurationPath, "ExtraMetadata", "games", gameId);
                foreach (var name in new[] { "VideoMicrotrailer.mp4", "VideoTrailer.mp4" })
                {
                    string path = Path.Combine(dir, name);
                    if (File.Exists(path)) return path;
                }
            }
            catch (Exception) { }
            return null;
        }
    }
}
