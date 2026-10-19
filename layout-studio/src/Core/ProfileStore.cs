using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace LayoutStudio.Core
{
    /// <summary>Profiles as JSON files in one folder, the active profile name, and text share codes.</summary>
    public class ProfileStore
    {
        private const string CodePrefix = "LS1:";
        private readonly string dir;
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();

        public ProfileStore(string directory)
        {
            dir = directory;
            Directory.CreateDirectory(dir);
        }

        public string Active
        {
            get
            {
                var path = Path.Combine(dir, "active.txt");
                return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8).Trim() : null;
            }
            set { File.WriteAllText(Path.Combine(dir, "active.txt"), value ?? "", Encoding.UTF8); }
        }

        public IList<string> Names()
        {
            var names = new List<string>();
            // The file name is the identity (Load opens by it), so a copied file lists under its own name.
            foreach (var file in Directory.GetFiles(dir, "*.profile.json"))
                names.Add(Path.GetFileName(file).Substring(0, Path.GetFileName(file).Length - ".profile.json".Length));
            return names;
        }

        public Profile Load(string name)
        {
            var path = PathFor(name);
            if (!File.Exists(path)) return new Profile { Name = name };
            string text;
            try
            {
                text = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (IOException ex)
            {
                // Locked by another program (antivirus, sync client): not corruption. Nothing may be saved over a file we could not read.
                return new Profile { Name = name, ReadOnly = true, Warning = "profile '" + name + "' could not be opened (" + ex.Message + "); it is left untouched and nothing is saved this session" };
            }
            try
            {
                return Parsed(text, name);
            }
            catch (Exception ex)
            {
                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
                var kept = Path.Combine(dir, Stem(name) + ".corrupt-" + stamp + ".bak");
                // A profile that stays corrupt is met at every start: keep one copy of identical content, not one per start.
                var same = SameContentCopy(path, Stem(name) + ".corrupt-*.bak");
                if (same != null) kept = same;
                else
                {
                    try { File.Copy(path, kept, false); } catch (IOException) { }
                    Prune(Stem(name) + ".corrupt-*.bak");
                }
                var previous = Path.Combine(dir, Stem(name) + ".bak");
                if (File.Exists(previous))
                {
                    try
                    {
                        var recovered = Parsed(File.ReadAllText(previous, Encoding.UTF8), name);
                        recovered.Warning = "profile '" + name + "' could not be read (" + ex.Message + "); the last good copy " + Path.GetFileName(previous) + " is used and the broken file was kept as " + Path.GetFileName(kept);
                        return recovered;
                    }
                    catch (Exception) { }
                }
                return new Profile { Name = name, Warning = "profile '" + name + "' could not be read (" + ex.Message + "); defaults are used and the file was kept as " + Path.GetFileName(kept) };
            }
        }

        private Profile Parsed(string text, string name)
        {
            var profile = FromJson(text);
            profile.Name = name;   // the file name decides, so a copied file never saves back into the original
            if (profile.Version > Profile.CurrentVersion)
            {
                profile.ReadOnly = true;
                profile.Warning = "profile '" + name + "' was written by a newer Layout Studio (version " + profile.Version + "); it is read-only";
            }
            return profile;
        }

        public void Save(Profile profile)
        {
            if (profile.ReadOnly) throw new InvalidOperationException("profile '" + profile.Name + "' is read-only (newer version)");
            var path = PathFor(profile.Name);
            string text = ToJson(profile);
            if (File.Exists(path))
            {
                // Autosave saves again and again: only a real change moves the previous file to the .bak.
                if (File.ReadAllText(path, Encoding.UTF8).TrimStart('﻿') == text) return;
                // Write beside, then swap: a power cut leaves either the old or the new file, never half of one. File.Replace also keeps the old as .bak.
                var temp = path + ".tmp";
                File.WriteAllText(temp, text, Encoding.UTF8);
                File.Replace(temp, path, Path.Combine(dir, Stem(profile.Name) + ".bak"));
                Prune(Stem(profile.Name) + ".before-*.bak");
                return;
            }
            var first = path + ".tmp";
            File.WriteAllText(first, text, Encoding.UTF8);
            File.Move(first, path);   // same atomic rule as later saves: never a half-written first file
        }

        private const int KeptBackups = 10;

        private string SameContentCopy(string path, string pattern)
        {
            var length = new FileInfo(path).Length;
            byte[] bytes = null;
            foreach (var candidate in Directory.GetFiles(dir, pattern))
            {
                if (new FileInfo(candidate).Length != length) continue;
                if (bytes == null) bytes = File.ReadAllBytes(path);
                if (File.ReadAllBytes(candidate).SequenceEqual(bytes)) return candidate;
            }
            return null;
        }

        /// <summary>Keeps the newest backups of one kind (the time stamp in the name sorts oldest first).</summary>
        private void Prune(string pattern)
        {
            var files = Directory.GetFiles(dir, pattern).OrderBy(f => f, StringComparer.Ordinal).ToList();
            foreach (var old in files.Take(Math.Max(0, files.Count - KeptBackups)))
            {
                try { File.Delete(old); } catch (IOException) { }
            }
        }

        /// <summary>Copies the saved profile to "&lt;name&gt;.before-&lt;reason&gt;-&lt;time&gt;.bak", which no later save touches. Null when there is no saved profile.</summary>
        public string Backup(string name, string reason)
        {
            var path = PathFor(name);
            if (!File.Exists(path)) return null;
            var copy = Path.Combine(dir, Stem(name) + ".before-" + reason + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".bak");
            File.Copy(path, copy, false);
            Prune(Stem(name) + ".before-*.bak");
            return copy;
        }

        public string Export(Profile profile)
        {
            return CodePrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(ToJson(profile)));
        }

        public Profile Import(string code, out string error)
        {
            error = null;
            if (code == null || !code.Trim().StartsWith(CodePrefix, StringComparison.Ordinal))
            {
                error = "not a Layout Studio share code (it should start with " + CodePrefix + ")";
                return null;
            }
            try
            {
                var bytes = Convert.FromBase64String(code.Trim().Substring(CodePrefix.Length));
                var profile = FromJson(Encoding.UTF8.GetString(bytes));
                if (profile.Version > Profile.CurrentVersion)
                {
                    error = "this share code comes from a newer Layout Studio";
                    return null;
                }
                return profile;
            }
            catch (Exception ex)
            {
                error = "the share code is damaged (" + ex.Message + ")";
                return null;
            }
        }

        private string ToJson(Profile p)
        {
            return json.Serialize(new Dictionary<string, object>
            {
                { "Name", p.Name }, { "Version", p.Version }, { "Overrides", p.Overrides }, { "Presets", p.Presets }, { "Editor", p.Editor }
            });
        }

        private Profile FromJson(string text)
        {
            var d = json.DeserializeObject(text) as IDictionary<string, object>;
            if (d == null || !d.ContainsKey("Name")) throw new FormatException("missing profile name");
            var p = new Profile { Name = Convert.ToString(d["Name"], CultureInfo.InvariantCulture) };
            if (d.ContainsKey("Version")) p.Version = Convert.ToInt32(d["Version"], CultureInfo.InvariantCulture);
            Fill(d, "Overrides", p.Overrides);
            Fill(d, "Presets", p.Presets);
            Fill(d, "Editor", p.Editor);
            return p;
        }

        private static void Fill(IDictionary<string, object> d, string key, Dictionary<string, string> target)
        {
            object value;
            if (!d.TryGetValue(key, out value) || value == null) return;
            var map = value as IDictionary<string, object>;
            if (map == null) throw new FormatException(key + " must be an object");
            foreach (var kv in map) target[kv.Key] = Convert.ToString(kv.Value, CultureInfo.InvariantCulture);
        }

        private string PathFor(string name)
        {
            return Path.Combine(dir, Stem(name) + ".profile.json");
        }

        private static string Stem(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Default";
            var bad = Path.GetInvalidFileNameChars();
            var clean = new string(name.Select(c => bad.Contains(c) ? '_' : c).ToArray());
            if (clean == name) return name;
            // "a:b" and "a?b" must not share a file: a short stable hash of the original tells them apart.
            uint h = 2166136261;
            foreach (var c in name) h = (h ^ c) * 16777619;
            return clean + "-" + (h & 0xFFFF).ToString("x4", CultureInfo.InvariantCulture);
        }
    }
}
