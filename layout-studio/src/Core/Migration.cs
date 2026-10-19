using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;

namespace LayoutStudio.Core
{
    /// <summary>Imports a ThemeOptions saved configuration (read-only) into a Layout Studio profile.</summary>
    public static class Migration
    {
        public const string ProfileName = "Migrated from ThemeOptions";

        /// <summary>
        /// ThemeOptions stores, per theme id, the selected presets ("Group.Choice") and the user settings
        /// ({Key: {Type, Value}}). Unknown groups, choices and keys, help rows, invalid values and values equal to
        /// the default are dropped; out-of-range numbers are clamped. Never throws: bad input gives an empty profile.
        /// </summary>
        public static Profile FromThemeOptions(string configJson, string themeId, Schema schema)
        {
            var profile = new Profile { Name = ProfileName };
            IDictionary<string, object> root;
            try
            {
                root = string.IsNullOrWhiteSpace(configJson) ? null : new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(configJson) as IDictionary<string, object>;
            }
            catch (Exception)
            {
                return profile;
            }
            if (root == null) return profile;

            var presets = Section(root, "SelectedPresets", themeId) as IEnumerable;
            if (presets != null && !(presets is string))
            {
                foreach (var item in presets)
                {
                    string text = item as string;
                    int dot = text == null ? -1 : text.IndexOf('.');
                    if (dot <= 0) continue;
                    string group = text.Substring(0, dot), choice = text.Substring(dot + 1);
                    var control = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).FirstOrDefault(c => c.Kind == "preset" && c.Id == group);
                    if (control != null && control.Choices.Any(c => c.Id == choice)) profile.Presets[group] = choice;
                }
            }

            var settings = Section(root, "UserSettings", themeId) as IDictionary<string, object>;
            if (settings != null)
            {
                foreach (var kv in settings)
                {
                    if (kv.Key.StartsWith("Help", StringComparison.Ordinal)) continue;
                    var def = schema.Find(kv.Key);
                    var entry = kv.Value as IDictionary<string, object>;
                    object raw;
                    if (def == null || entry == null || !entry.TryGetValue("Value", out raw)) continue;
                    string normalized, normalizedDefault, error;
                    if (!ConstantValue.TryNormalize(def, Convert.ToString(raw, CultureInfo.InvariantCulture), out normalized, out error)) continue;
                    if (ConstantValue.TryNormalize(def, def.Default, out normalizedDefault, out error) && normalized == normalizedDefault) continue;
                    profile.Overrides[kv.Key] = normalized;
                }
            }
            return profile;
        }

        private static object Section(IDictionary<string, object> root, string name, string themeId)
        {
            object section, value;
            var map = root.TryGetValue(name, out section) ? section as IDictionary<string, object> : null;
            return map != null && map.TryGetValue(themeId, out value) ? value : null;
        }
    }
}
