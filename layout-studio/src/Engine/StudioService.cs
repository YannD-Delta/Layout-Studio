using System;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using LayoutStudio.Core;
using LayoutStudio.Editor;

namespace LayoutStudio.Engine
{
    /// <summary>Wires the Core pieces to the running theme: schema, store, history, profiles and the resource applier.</summary>
    public class StudioService : IProfileSharing
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

        public static StudioService Create(string themeDir, string dataDir, Dispatcher ui)
        {
            PreviewImages.Root = themeDir;
            var schema = Schema.Load(File.ReadAllText(Path.Combine(themeDir, "layout-studio.schema.json")));
            var service = new StudioService { ThemeDir = themeDir, Schema = schema, Store = new ConstantStore(schema.AllConstants()) };
            service.History = new History(service.Store);
            service.Profiles = new ProfileStore(Path.Combine(dataDir, "profiles"));
            service.Applier = new ResourceApplier(service.Store, schema, ui);
            service.ProfileApplier = new ProfileApplier(schema, service.Store, service.Applier, themeDir);
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
            Applier.ApplyAll();
            History.Clear();
        }

        /// <summary>Replaces the active profile by the settings found in a ThemeOptions configuration and applies it.</summary>
        public Profile ImportThemeOptions(string configJson)
        {
            var migrated = Migration.FromThemeOptions(configJson, ThemeGuard.ThemeId, Schema);
            migrated.Name = Active != null ? Active.Name : "Default";
            Active = migrated;
            ProfileApplier.ApplyProfile(Active);
            Applier.ApplyAll();
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

        public void SaveActive()
        {
            if (Active == null) Active = new Profile { Name = "Default" };
            if (Active.ReadOnly) return;   // newer version or unreadable file: shown to the user, never overwritten
            OverrideMerge.Rebuild(Active, Store);   // keys this theme version does not define are kept
            Profiles.Save(Active);
            Profiles.Active = Active.Name;
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
            Applier.ApplyAll();
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
            Applier.ApplyAll();
            History.Clear();
            SaveActive();
            RaiseProfileReplaced();
            return null;
        }

        private void ReturnToDefaults()
        {
            if (Active == null) Active = new Profile { Name = "Default" };
            foreach (var key in Store.Changed().ToList()) Store.Reset(key);
            foreach (var group in Active.Presets.Keys.ToList())
            {
                var control = Schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).FirstOrDefault(c => c.Kind == "preset" && c.Id == group);
                if (control != null && control.DefaultChoice != null) ProfileApplier.ApplyPreset(Active, group, control.DefaultChoice);
            }
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
