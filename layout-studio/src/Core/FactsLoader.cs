using System;
using System.Collections.Generic;
using System.IO;

namespace LayoutStudio.Core
{
    /// <summary>
    /// Reads a game's GameActivity file and summarizes it, remembering the answer while the file does not change: a big file (a thousand
    /// hours logged is several megabytes) is parsed once, not on every selection.
    /// </summary>
    public class FactsLoader
    {
        private struct Key
        {
            public string Path; public long Written; public long Length; public long Since;
        }

        private readonly Func<string, DateTime?, GameFacts> parse;
        private readonly Dictionary<string, KeyValuePair<Key, GameFacts>> cache = new Dictionary<string, KeyValuePair<Key, GameFacts>>();
        private readonly object gate = new object();

        public FactsLoader() : this((json, since) => ActivityStats.FromJson(json, since)) { }

        public FactsLoader(Func<string, DateTime?, GameFacts> parse) { this.parse = parse; }

        /// <summary>How many files were actually parsed (not served from the cache).</summary>
        public int Parses { get; private set; }

        /// <summary>The facts of the file at <paramref name="path"/>; empty facts when there is no file. Safe to call from any thread.</summary>
        public GameFacts Load(string path, DateTime? since)
        {
            var info = new FileInfo(path);
            if (!info.Exists) return new GameFacts();
            var key = new Key { Path = path, Written = info.LastWriteTimeUtc.Ticks, Length = info.Length, Since = since.HasValue ? since.Value.ToUniversalTime().Ticks : 0 };
            lock (gate)
            {
                KeyValuePair<Key, GameFacts> hit;
                if (cache.TryGetValue(path, out hit) && hit.Key.Written == key.Written && hit.Key.Length == key.Length && hit.Key.Since == key.Since) return hit.Value;
            }
            string json;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream)) json = reader.ReadToEnd();
            var facts = parse(json, since);
            lock (gate)
            {
                Parses++;
                if (cache.Count > 200) cache.Clear();   // a library of thousands of games never grows this without bound
                cache[path] = new KeyValuePair<Key, GameFacts>(key, facts);
            }
            return facts;
        }
    }
}
