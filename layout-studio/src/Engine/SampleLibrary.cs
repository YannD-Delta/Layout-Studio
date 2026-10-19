using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace LayoutStudio.Engine
{
    /// <summary>
    /// Testbed helper: fills the library with the fake games of tools\sample-games.json, with the art drawn by tools\make-sample-art.ps1.
    /// It only runs where those files sit in the configuration folder, so it never does anything on a real install.
    /// </summary>
    public static class SampleLibrary
    {
        public static bool Available(string configurationPath)
        {
            return File.Exists(Path.Combine(configurationPath, "sample-games.json")) && Directory.Exists(Path.Combine(configurationPath, "sample-art"));
        }

        /// <summary>Adds or refills every game. Returns how many were created.</summary>
        public static int Fill(IPlayniteAPI api, ILogger logger)
        {
            string dir = api.Paths.ConfigurationPath;
            var specs = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(File.ReadAllText(Path.Combine(dir, "sample-games.json"))) as object[];
            if (specs == null) return 0;
            var db = api.Database;
            int created = 0;
            foreach (var item in specs)
            {
                var spec = (IDictionary<string, object>)item;
                string name = Str(spec, "name");
                var game = db.Games.FirstOrDefault(g => g.Name == name);
                if (game == null)
                {
                    game = new Game(name);
                    db.Games.Add(game);
                    created++;
                    string slug = Slug(name);
                    string bg = Path.Combine(dir, "sample-art", slug + "-bg.jpg");
                    string cover = Path.Combine(dir, "sample-art", slug + "-cover.jpg");
                    string icon = Path.Combine(dir, "sample-art", slug + "-icon.jpg");
                    if (File.Exists(bg)) game.BackgroundImage = db.AddFile(bg, game.Id);
                    if (File.Exists(cover)) game.CoverImage = db.AddFile(cover, game.Id);
                    if (File.Exists(icon)) game.Icon = db.AddFile(icon, game.Id);
                }
                Fill(db, game, spec);
                db.Games.Update(game);
            }
            logger.Info("sample library: " + specs.Length + " games, " + created + " created");
            return created;
        }

        private static void Fill(IGameDatabaseAPI db, Game game, IDictionary<string, object> s)
        {
            string name = Str(s, "name");
            int hash = Math.Abs(name.GetHashCode());
            game.IsInstalled = Bool(s, "installed", true);
            game.Favorite = Bool(s, "favorite", false);
            game.Hidden = Bool(s, "hidden", false);
            game.GenreIds = Ids(db.Genres, List(s, "genres"));
            game.TagIds = Ids(db.Tags, List(s, "tags"));
            game.FeatureIds = Ids(db.Features, List(s, "features"));
            game.PlatformIds = Ids(db.Platforms, List(s, "platforms"));
            game.DeveloperIds = Ids(db.Companies, NonEmpty(Str(s, "developer")));
            game.PublisherIds = Ids(db.Companies, NonEmpty(Str(s, "publisher")));
            game.SeriesIds = Ids(db.Series, NonEmpty(Str(s, "series")));
            var source = Ids(db.Sources, NonEmpty(Str(s, "source")));
            game.SourceId = source.Count > 0 ? source[0] : Guid.Empty;
            var status = Ids(db.CompletionStatuses, NonEmpty(Str(s, "status")));
            game.CompletionStatusId = status.Count > 0 ? status[0] : Guid.Empty;
            int year = (int)Num(s, "year");
            if (year > 0) game.ReleaseDate = new ReleaseDate(year, 1 + (year % 12), 1 + (year % 27));
            game.Playtime = (ulong)(Num(s, "playtimeHours") * 3600);
            game.PlayCount = game.Playtime > 0 ? Math.Max(1UL, game.Playtime / 5400) : 0UL;
            double ago = Num(s, "lastPlayedDaysAgo");
            game.LastActivity = ago >= 0 && game.Playtime > 0 ? (DateTime?)DateTime.Now.AddDays(-ago) : null;
            game.Added = DateTime.Now.AddDays(-60 - (hash % 400));
            game.CriticScore = Score(s, "critic");
            game.CommunityScore = Score(s, "community");
            game.UserScore = Score(s, "user");
            game.Description = Str(s, "description");
            game.InstallSize = game.IsInstalled ? (ulong?)((2UL + (ulong)(hash % 60)) * 1024UL * 1024UL * 1024UL) : null;
            // known sites on purpose: the Extras plugin draws a logo for each one it recognises
            game.Links = new System.Collections.ObjectModel.ObservableCollection<Link>
            {
                new Link("Website", "https://example.com/" + Slug(name)),
                new Link("Steam", "https://store.steampowered.com/app/" + (100000 + hash % 800000)),
                new Link("Wikipedia", "https://en.wikipedia.org/wiki/" + Slug(name)),
                new Link("YouTube", "https://www.youtube.com/results?search_query=" + Slug(name)),
                new Link("Discord", "https://discord.gg/" + Slug(name)),
                new Link("Reddit", "https://www.reddit.com/r/" + Slug(name))
            };
        }

        private static int? Score(IDictionary<string, object> s, string key)
        {
            var v = (int)Num(s, key);
            return v > 0 ? (int?)v : null;
        }

        private static string Str(IDictionary<string, object> s, string key)
        {
            object v;
            return s.TryGetValue(key, out v) && v != null ? Convert.ToString(v) : "";
        }

        private static double Num(IDictionary<string, object> s, string key)
        {
            object v;
            return s.TryGetValue(key, out v) && v != null ? Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture) : 0;
        }

        private static bool Bool(IDictionary<string, object> s, string key, bool fallback)
        {
            object v;
            return s.TryGetValue(key, out v) && v is bool ? (bool)v : fallback;
        }

        private static string[] List(IDictionary<string, object> s, string key)
        {
            object v;
            var arr = s.TryGetValue(key, out v) ? v as object[] : null;
            return arr == null ? new string[0] : arr.Select(x => Convert.ToString(x)).Where(x => !string.IsNullOrEmpty(x)).ToArray();
        }

        private static string[] NonEmpty(string text)
        {
            return string.IsNullOrEmpty(text) ? new string[0] : new[] { text };
        }

        public static string Slug(string name)
        {
            var chars = name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) && c < 128 ? c : '-').ToArray();
            var slug = System.Text.RegularExpressions.Regex.Replace(new string(chars), "-+", "-").Trim('-');
            return slug.Length > 40 ? slug.Substring(0, 40).Trim('-') : slug;
        }

        private static List<Guid> Ids<T>(IItemCollection<T> collection, params string[] names) where T : DatabaseObject
        {
            var ids = new List<Guid>();
            foreach (var n in names)
            {
                var item = collection.FirstOrDefault(i => i.Name == n) ?? collection.Add(n);
                ids.Add(item.Id);
            }
            return ids;
        }
    }
}
