using System;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using LayoutStudio.Core;
using LayoutStudio.Editor;

namespace LayoutStudio.Engine
{
    /// <summary>Wires the Core pieces to the running theme: schema, store, history, profiles and the resource applier.</summary>
    public class StudioService : IProfileSharing, IProfileSwitching
    {
        public string ThemeDir { get; private set; }
        public Schema Schema { get; private set; }

        /// <summary>Inconsistencies of the theme's schema (a visual editor may misbehave); empty when it is sound.</summary>
        public System.Collections.Generic.List<string> SchemaProblems { get { return Schema == null ? new System.Collections.Generic.List<string>() : Schema.Validate(); } }
        public ConstantStore Store { get; private set; }
        public History History { get; private set; }
        public ProfileStore Profiles { get; private set; }
        public ResourceApplier Applier { get; private set; }
        public Profile Active { get; private set; }
        public ProfileApplier ProfileApplier { get; private set; }

        /// <summary>The Look shared with the other mode (null when this theme has no look-tokens.json or no path was given).</summary>
        public Look Look { get; private set; }

        /// <summary>Applies preset files nowhere: the offline service edits a profile that no running window shows.</summary>
        private class NoPresetFiles : IPresetFiles
        {
            public void Merge(string groupId, string absolutePath) { }
            public void Remove(string groupId) { }
        }

        /// <summary>
        /// A service for a theme that is NOT the running one (the Fullscreen theme, edited from the desktop editor): the same schema, store, history,
        /// profiles and autosave, but nothing is written to the running window (no resource applier, preset files are not merged).
        /// </summary>
        public static StudioService CreateOffline(string themeDir, string dataDir, Dispatcher ui, string lookPath = null)
        {
            var schema = Schema.Load(File.ReadAllText(Path.Combine(themeDir, "layout-studio.schema.json")));
            var service = new StudioService { ThemeDir = themeDir, Schema = schema, Store = new ConstantStore(schema.AllConstants()) };
            service.History = new History(service.Store);
            service.Profiles = new ProfileStore(Path.Combine(dataDir, "profiles"));
            service.ProfileApplier = new ProfileApplier(schema, service.Store, new NoPresetFiles(), themeDir);
            string table = Path.Combine(themeDir, "look-tokens.json");
            if (lookPath != null && File.Exists(table))
            {
                try
                {
                    service.Look = Look.Load(lookPath, LookTable.Parse(File.ReadAllText(table)));
                    service.ProfileApplier.Look = service.Look;
                }
                catch (Exception) { service.Look = null; }
            }
            service.EnableAutoSave(ui);
            return service;
        }

        public static StudioService Create(string themeDir, string dataDir, Dispatcher ui, string lookPath = null)
        {
            PreviewImages.Root = themeDir;
            var schema = Schema.Load(File.ReadAllText(Path.Combine(themeDir, "layout-studio.schema.json")));
            var service = new StudioService { ThemeDir = themeDir, Schema = schema, Store = new ConstantStore(schema.AllConstants()) };
            service.History = new History(service.Store);
            service.Profiles = new ProfileStore(Path.Combine(dataDir, "profiles"));
            service.Applier = new ResourceApplier(service.Store, schema, ui);
            service.ProfileApplier = new ProfileApplier(schema, service.Store, service.Applier, themeDir);
            string table = Path.Combine(themeDir, "look-tokens.json");
            if (lookPath != null && File.Exists(table))
            {
                try
                {
                    service.Look = Look.Load(lookPath, LookTable.Parse(File.ReadAllText(table)));
                    service.ProfileApplier.Look = service.Look;
                }
                catch (Exception) { service.Look = null; }   // a broken table in the theme: the modes simply stay separate
            }
            service.EnableAutoSave(ui);
            return service;
        }

        /// <summary>Saves the active profile shortly after the last change, so closing Playnite never loses an edit.</summary>
        private void EnableAutoSave(Dispatcher ui)
        {
            var timer = new DispatcherTimer(DispatcherPriority.Background, ui) { Interval = TimeSpan.FromMilliseconds(800) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                TrySave();
            };
            Store.ValueChanged += key => { timer.Stop(); timer.Start(); };
            ProfileApplier.PresetChanged += () => { timer.Stop(); timer.Start(); };
            History.Changed += () => { timer.Stop(); timer.Start(); };   // a ratio choice is an editor setting, not a constant
        }

        /// <summary>Loads the active profile (default name "Default") and applies it to the running theme.</summary>
        public void LoadActive()
        {
            string name = Profiles.Active;
            if (string.IsNullOrEmpty(name)) name = "Default";
            Active = Profiles.Load(name);
            ProfileApplier.ApplyProfile(Active);
            if (Applier != null) Applier.ApplyAll();
            History.Clear();
        }

        /// <summary>Replaces the active profile by the settings found in a ThemeOptions configuration and applies it.</summary>
        public Profile ImportThemeOptions(string configJson)
        {
            var migrated = Migration.FromThemeOptions(configJson, ThemeGuard.ThemeId, Schema);
            migrated.Name = Active != null ? Active.Name : "Default";
            Active = migrated;
            ProfileApplier.ApplyProfile(Active);
            if (Applier != null) Applier.ApplyAll();
            History.Clear();
            SaveActive();
            return Active;
        }

        /// <summary>Called by the autosave timer, the editor and Playnite's shutdown: a failure is logged, never thrown into Playnite.</summary>
        public void TrySave()
        {
            try { SaveActive(); }
            catch (Exception ex) { if (Log != null) Log("could not save the profile: " + ex.Message); }
        }

        /// <summary>Receives lines for the plugin log.</summary>
        public Action<string> Log { get; set; }

        /// <summary>Raised after an import or a reset replaced the whole profile (editor-only state, like the figure colours, must reload).</summary>
        public event Action ProfileReplaced;
        private void RaiseProfileReplaced() { var h = ProfileReplaced; if (h != null) h(); }

        /// <summary>After the shared Look was changed from another service (the Fullscreen profile edited here): read it again and re-apply it.</summary>
        public void ReloadLook()
        {
            if (Look == null || !Look.ReloadIfClean()) return;
            ProfileApplier.ApplyProfile(Active);
            if (Applier != null) Applier.ApplyAll();
        }

        public void SaveActive()
        {
            if (Active == null) Active = new Profile { Name = "Default" };
            if (Active.ReadOnly) return;   // newer version or unreadable file: shown to the user, never overwritten
            OverrideMerge.Rebuild(Active, Store, Look == null ? null : Look.Wanted(Schema, Store));   // keys this theme version does not define are kept; the shared Look's values stay the Look's
            Profiles.Save(Active);   // first: a locked look file must never keep the profile from being saved
            Profiles.Active = Active.Name;
            if (Look != null) Look.TrySaveIfDirty(Log);
        }

        /// <summary>Saves the active profile and returns its share code.</summary>
        public string Export()
        {
            SaveActive();
            return Profiles.Export(Active);
        }

        /// <summary>What the last Import dropped (null when everything applied).</summary>
        public string ImportNote { get; private set; }

        /// <summary>Replaces the active profile (keeping its name) by the one in a share code. Returns an error message or null.</summary>
        public string Import(string code)
        {
            string error;
            ImportNote = null;
            var imported = Profiles.Import(code, out error);
            if (imported == null) return error;
            if (Active != null && Active.ReadOnly) return "the active profile is read-only (written by a newer Layout Studio)";
            SaveActive();
            Profiles.Backup(Active.Name, "import");
            ReturnToDefaults();
            Active.Overrides = imported.Overrides;
            Active.Presets = imported.Presets;
            Active.Editor.Clear();
            foreach (var kv in imported.Editor) Active.Editor[kv.Key] = kv.Value;
            var report = ProfileApplier.ApplyProfile(Active);
            ImportNote = report.Describe();
            RaiseProfileReplaced();
            if (Applier != null) Applier.ApplyAll();
            History.Clear();
            SaveActive();
            RaiseProfileReplaced();
            return null;
        }

        /// <summary>Safe reset: the current profile is saved first (it stays as the .bak), then everything goes back to the theme defaults.</summary>
        public string ResetAll()
        {
            if (Active != null && Active.ReadOnly) return "the active profile is read-only (" + Active.Warning + ")";
            SaveActive();
            Profiles.Backup(Active.Name, "reset");
            ReturnToDefaults();
            if (Applier != null) Applier.ApplyAll();
            History.Clear();
            SaveActive();
            RaiseProfileReplaced();
            return null;
        }

        // ---- profile switching (IProfileSwitching) ---------------------------------------------------------------

        public System.Collections.Generic.IList<string> ProfileNames()
        {
            var names = Profiles.Names().ToList();
            if (Active != null && !names.Contains(Active.Name, StringComparer.OrdinalIgnoreCase)) names.Add(Active.Name);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        public string ActiveProfileName { get { return Active == null ? "Default" : Active.Name; } }

        /// <summary>Saves the profile in use, then loads another one in its place (the same object, like Import, so every open view model stays valid).</summary>
        public string SwitchProfile(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "choose a profile";
            if (Active != null && Active.ReadOnly) return "the active profile is read-only (" + Active.Warning + ")";
            if (string.Equals(name, ActiveProfileName, StringComparison.OrdinalIgnoreCase)) return null;
            var other = Profiles.Load(name);
            if (other.ReadOnly) return other.Warning;
            SaveActive();
            // the Look shared with the other mode belongs to no profile: ApplyPreset would write the default choices into it while the groups go back to defaults
            var look = ProfileApplier.Look;
            ProfileApplier.Look = null;
            try { ReturnToDefaults(clearLook: false); }
            finally { ProfileApplier.Look = look; }
            Active.Name = name;
            Active.Version = other.Version;
            Active.Warning = other.Warning;
            Active.Overrides = other.Overrides;
            Active.Presets = other.Presets;
            Active.Editor.Clear();
            foreach (var kv in other.Editor) Active.Editor[kv.Key] = kv.Value;
            ProfileApplier.ApplyProfile(Active);
            RaiseProfileReplaced();
            if (Applier != null) Applier.ApplyAll();
            History.Clear();
            SaveActive();
            RaiseProfileReplaced();
            return null;
        }

        /// <summary>A copy of the profile in use under a new name, switched to at once.</summary>
        public string NewProfile(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) return "give the profile a name";
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return "a profile name cannot contain \\ / : * ? \" < > |";
            if (ProfileNames().Contains(name, StringComparer.OrdinalIgnoreCase)) return "a profile called '" + name + "' exists already";
            if (Active != null && Active.ReadOnly) return "the active profile is read-only (" + Active.Warning + ")";
            SaveActive();
            var copy = new Profile { Name = name };
            foreach (var kv in Active.Overrides) copy.Overrides[kv.Key] = kv.Value;
            foreach (var kv in Active.Presets) copy.Presets[kv.Key] = kv.Value;
            foreach (var kv in Active.Editor) copy.Editor[kv.Key] = kv.Value;
            try { Profiles.Save(copy); }
            catch (Exception ex) { return "the profile could not be saved (" + ex.Message + ")"; }
            return SwitchProfile(name);
        }

        /// <param name="clearLook">False when switching profiles: the Look shared with the other mode belongs to no profile and stays as it is.</param>
        private void ReturnToDefaults(bool clearLook = true)
        {
            if (Active == null) Active = new Profile { Name = "Default" };
            foreach (var key in Store.Changed().ToList()) Store.Reset(key);
            foreach (var group in Active.Presets.Keys.ToList())
            {
                var control = Schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).FirstOrDefault(c => c.Kind == "preset" && c.Id == group);
                if (control != null && control.DefaultChoice != null) ProfileApplier.ApplyPreset(Active, group, control.DefaultChoice);
            }
            if (Look != null && clearLook) Look.ClearAllTokens();   // a reset is a reset in both modes: the shared presets went back to Default above
            Active.Overrides.Clear();
            Active.Presets.Clear();
            Active.Editor.Clear();
        }

        public void ApplyPreset(string groupId, string choiceId)
        {
            if (Active == null) Active = new Profile { Name = "Default" };
            ProfileApplier.ApplyPreset(Active, groupId, choiceId);
        }
    }
}
