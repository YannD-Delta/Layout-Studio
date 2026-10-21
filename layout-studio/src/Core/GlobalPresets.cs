using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace LayoutStudio.Core
{
    /// <summary>
    /// A global preset: the whole look at once (every preset group choice and every override), as shipped in the theme's GlobalPresets folder.
    /// One of them is the default: a fresh profile starts as it and "Reset all" goes back to it.
    /// </summary>
    public class GlobalPreset
    {
        public string Id;
        public Dictionary<string, string> Name = new Dictionary<string, string>();
        public Dictionary<string, string> Intent = new Dictionary<string, string>();
        public Dictionary<string, string> Presets = new Dictionary<string, string>();
        public Dictionary<string, string> Overrides = new Dictionary<string, string>();
        public Dictionary<string, string> Editor = new Dictionary<string, string>();

        /// <summary>Picture relative to the theme folder, or null when there is none.</summary>
        public string Preview;

        /// <summary>The colour profile and the accent: what makes two presets look alike at first sight.</summary>
        public string ColorAndAccent()
        {
            string color, accent;
            return (Presets.TryGetValue("ColorProfile", out color) ? color : "Default") + "/" + (Presets.TryGetValue("Accent", out accent) ? accent : "Default");
        }

        public string NameFor(string language) { return Pick(Name, language, Id); }
        public string IntentFor(string language) { return Pick(Intent, language, ""); }

        private static string Pick(Dictionary<string, string> texts, string language, string fallback)
        {
            string text;
            if (!string.IsNullOrEmpty(language) && texts.TryGetValue(language.Substring(0, Math.Min(2, language.Length)).ToLowerInvariant(), out text) && !string.IsNullOrEmpty(text)) return text;
            return texts.TryGetValue("en", out text) && !string.IsNullOrEmpty(text) ? text : fallback;
        }

        /// <summary>A profile that holds exactly this look (the caller gives it a name and, when needed, the version).</summary>
        public Profile ToProfile(string name)
        {
            var profile = new Profile { Name = name };
            foreach (var kv in Presets) profile.Presets[kv.Key] = kv.Value;
            foreach (var kv in Overrides) profile.Overrides[kv.Key] = kv.Value;
            foreach (var kv in Editor) profile.Editor[kv.Key] = kv.Value;
            profile.Editor[GlobalPresets.EditorKey] = Id;
            return profile;
        }

        /// <summary>What the schema does not know about this bundle (unknown group or choice, unknown or invalid constant): empty when it is sound.</summary>
        public List<string> Problems(Schema schema)
        {
            var problems = new List<string>();
            var groups = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Where(c => c.Kind == "preset").ToList();
            foreach (var kv in Presets)
            {
                var group = groups.FirstOrDefault(c => c.Id == kv.Key);
                if (group == null) problems.Add("unknown preset group " + kv.Key);
                else if (!group.Choices.Any(c => c.Id == kv.Value)) problems.Add("unknown choice " + kv.Key + "=" + kv.Value);
            }
            var store = new ConstantStore(schema.AllConstants());
            foreach (var kv in Overrides)
            {
                string error;
                if (!store.TrySet(kv.Key, kv.Value, out error)) problems.Add((error != null && error.StartsWith("unknown constant") ? "unknown constant " : "invalid value for ") + kv.Key);
            }
            if (Name.Count == 0 || string.IsNullOrEmpty(NameFor("en"))) problems.Add("no English name");
            if (!Name.ContainsKey("fr") || string.IsNullOrEmpty(Name["fr"])) problems.Add("no French name");
            if (string.IsNullOrEmpty(IntentFor("en"))) problems.Add("no English intent");
            if (!Intent.ContainsKey("fr") || string.IsNullOrEmpty(Intent["fr"])) problems.Add("no French intent");
            return problems;
        }
    }

    /// <summary>The global presets of a theme folder: <c>GlobalPresets/&lt;Id&gt;.json</c> plus <c>GlobalPresets/index.json</c> (default, order, private ones).</summary>
    public class GlobalPresets
    {
        /// <summary>The profile's Editor entry that remembers which global preset the profile was started from.</summary>
        public const string EditorKey = "GlobalPreset";

        public const string Folder = "GlobalPresets";

        public List<GlobalPreset> Items = new List<GlobalPreset>();
        public string DefaultId;

        /// <summary>Ids that the public export leaves out (the owner's own configuration).</summary>
        public List<string> Private = new List<string>();

        /// <summary>No global presets at all (a theme without the folder): every call is harmless.</summary>
        public static GlobalPresets None() { return new GlobalPresets(); }

        public bool Any { get { return Items.Count > 0; } }

        public GlobalPreset Find(string id)
        {
            return id == null ? null : Items.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The preset a fresh profile starts as and "Reset all" returns to; null when the theme ships none.</summary>
        public GlobalPreset Default { get { return Find(DefaultId) ?? Items.FirstOrDefault(); } }

        public static GlobalPresets Load(string themeDir)
        {
            var result = new GlobalPresets();
            string dir = Path.Combine(themeDir, Folder);
            if (!Directory.Exists(dir)) return result;
            var json = new JavaScriptSerializer();
            var order = new List<string>();
            string indexPath = Path.Combine(dir, "index.json");
            if (File.Exists(indexPath))
            {
                var index = json.DeserializeObject(File.ReadAllText(indexPath, Encoding.UTF8)) as IDictionary<string, object>;
                if (index != null)
                {
                    object v;
                    if (index.TryGetValue("default", out v)) result.DefaultId = Convert.ToString(v, CultureInfo.InvariantCulture);
                    if (index.TryGetValue("order", out v) && v is System.Collections.IEnumerable) foreach (var o in (System.Collections.IEnumerable)v) order.Add(Convert.ToString(o, CultureInfo.InvariantCulture));
                    if (index.TryGetValue("private", out v) && v is System.Collections.IEnumerable) foreach (var o in (System.Collections.IEnumerable)v) result.Private.Add(Convert.ToString(o, CultureInfo.InvariantCulture));
                }
            }
            var loaded = new Dictionary<string, GlobalPreset>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                if (string.Equals(Path.GetFileName(file), "index.json", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var preset = Parse(json.DeserializeObject(File.ReadAllText(file, Encoding.UTF8)) as IDictionary<string, object>, Path.GetFileNameWithoutExtension(file));
                    string picture = Folder + "/Previews/" + preset.Id + ".png";
                    if (File.Exists(Path.Combine(themeDir, picture.Replace('/', Path.DirectorySeparatorChar)))) preset.Preview = picture;
                    loaded[preset.Id] = preset;
                }
                catch (Exception) { /* one broken file never hides the others */ }
            }
            foreach (var id in order) { GlobalPreset p; if (loaded.TryGetValue(id, out p)) { result.Items.Add(p); loaded.Remove(id); } }
            result.Items.AddRange(loaded.Values.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase));
            return result;
        }

        private static GlobalPreset Parse(IDictionary<string, object> d, string fileId)
        {
            if (d == null) throw new FormatException("not an object");
            var p = new GlobalPreset { Id = Text(d, "Id") ?? fileId };
            Fill(p.Name, d, "Name");
            Fill(p.Intent, d, "Intent");
            Fill(p.Presets, d, "Presets");
            Fill(p.Overrides, d, "Overrides");
            Fill(p.Editor, d, "Editor");
            return p;
        }

        private static string Text(IDictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;
        }

        private static void Fill(Dictionary<string, string> target, IDictionary<string, object> d, string key)
        {
            object v;
            var map = d.TryGetValue(key, out v) ? v as IDictionary<string, object> : null;
            if (map == null) return;
            foreach (var kv in map) target[kv.Key] = Convert.ToString(kv.Value, CultureInfo.InvariantCulture);
        }

        /// <summary>The profile that holds a global preset; the shared Look follows it (a global preset is the whole look, in both modes).</summary>
        public static Profile StartFrom(GlobalPreset preset, string name, Look look)
        {
            var profile = preset.ToProfile(name);
            if (look != null) foreach (var kv in preset.Presets) look.SetPreset(kv.Key, kv.Value);
            return profile;
        }

        /// <summary>True when everything the preset sets holds right now and nothing else was changed from the theme's defaults.</summary>
        public static bool IsExactly(GlobalPreset preset, Profile profile, ConstantStore store, Schema schema, ProfileApplier applier)
        {
            if (preset == null || profile == null) return false;
            foreach (var kv in preset.Presets)
            {
                string chosen;
                if (!profile.Presets.TryGetValue(kv.Key, out chosen)) chosen = applier.DefaultChoiceOf(kv.Key);
                if (chosen != kv.Value) return false;
            }
            foreach (var kv in preset.Overrides) if (store.Find(kv.Key) != null && store.Get(kv.Key) != kv.Value) return false;
            return store.Changed().All(key => preset.Overrides.ContainsKey(key) || SetByChoice(preset, schema, key));
        }

        // a constant that a chosen preset group sets is not an edit of the user
        private static bool SetByChoice(GlobalPreset preset, Schema schema, string key)
        {
            foreach (var kv in preset.Presets)
            {
                var control = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).FirstOrDefault(c => c.Kind == "preset" && c.Id == kv.Key);
                var choice = control == null ? null : control.Choices.FirstOrDefault(c => c.Id == kv.Value);
                if (choice != null && choice.Constants.ContainsKey(key)) return true;
            }
            return false;
        }
    }
}
