using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using LayoutStudio.Core;

namespace LayoutStudio.Engine
{
    /// <summary>
    /// Writes constant overrides into Application.Resources so the theme's DynamicResource consumers refresh live.
    /// Resource lookup tries a dictionary's own entries before its merged dictionaries, so an entry set here wins over the theme.
    /// Only keys this class set are ever removed: the theme's own definitions are never touched.
    /// </summary>
    public class ResourceApplier : IPresetFiles
    {
        private readonly ConstantStore store;
        private readonly Schema schema;
        private readonly Dispatcher ui;
        private readonly HashSet<string> applied = new HashSet<string>();
        private readonly HashSet<string> clones = new HashSet<string>();
        private readonly Dictionary<string, ResourceDictionary> presets = new Dictionary<string, ResourceDictionary>();

        public ResourceApplier(ConstantStore store, Schema schema, Dispatcher ui)
        {
            this.store = store;
            this.schema = schema;
            this.ui = ui;
            store.ValueChanged += Apply;
        }

        public void ApplyAll()
        {
            foreach (var key in store.Changed()) Apply(key);
        }

        /// <summary>Receives a line when WPF refuses a value; the constant then goes back to its default.</summary>
        public Action<string> Log { get; set; }

        public void Apply(string key)
        {
            var def = store.Find(key);
            if (def == null) return;
            string value = store.Get(key);
            bool isDefault = value == store.GetDefault(key);
            Run(() =>
            {
                var resources = Application.Current.Resources;
                try
                {
                    if (isDefault)
                    {
                        if (applied.Remove(key)) resources.Remove(key);
                    }
                    else
                    {
                        resources[key] = WpfValues.ToWpf(def.Type, value);   // WPF can refuse a value that validates as text (a negative border)
                        applied.Add(key);
                    }
                    RefreshBrushes(key);
                }
                catch (Exception ex)
                {
                    if (Log != null) Log("WPF refused " + key + " = " + value + " (" + ex.Message + "); the default is used");
                    if (applied.Remove(key)) resources.Remove(key);
                    if (!isDefault) store.Reset(key);
                }
            });
        }

        // resource key of a brush -> the constants (and brush property) that change it
        private Dictionary<string, List<KeyValuePair<string, string>>> brushDependencies;

        private Dictionary<string, List<KeyValuePair<string, string>>> BrushDependencies()
        {
            if (brushDependencies != null) return brushDependencies;
            brushDependencies = new Dictionary<string, List<KeyValuePair<string, string>>>();
            // every key a brush reads, not only the editable constants: a color profile also changes keys such as TextColorDarker
            foreach (var key in schema.DependentKeys())
            {
                foreach (var dep in schema.DependentsOf(key))
                {
                    List<KeyValuePair<string, string>> list;
                    if (!brushDependencies.TryGetValue(dep.Resource, out list)) brushDependencies[dep.Resource] = list = new List<KeyValuePair<string, string>>();
                    list.Add(new KeyValuePair<string, string>(key, dep.Property));
                }
            }
            return brushDependencies;
        }

        private void RefreshBrushes(string key)
        {
            foreach (var dep in schema.DependentsOf(key)) RefreshBrush(dep.Resource);
        }

        /// <summary>
        /// Rebuilds the clone of one brush from the theme's (or the active preset's) own brush plus the live value of every resource that feeds it
        /// (a changed constant, a color profile or any other merged preset: the first dictionary that defines the key wins, as for DynamicResource).
        /// A brush that already holds those values gets no clone, so a color preset's brush is never hidden by a stale copy.
        /// </summary>
        private void RefreshBrush(string resource)
        {
            var resources = Application.Current.Resources;
            if (clones.Remove(resource)) resources.Remove(resource);
            var original = Application.Current.TryFindResource(resource) as Freezable;
            if (original == null) return;
            List<KeyValuePair<string, string>> feeds;
            if (!BrushDependencies().TryGetValue(resource, out feeds)) return;
            var live = new List<KeyValuePair<string, object>>();
            foreach (var feed in feeds)
            {
                var value = Application.Current.TryFindResource(feed.Key);
                if (value != null) live.Add(new KeyValuePair<string, object>(feed.Value, value));
            }
            var result = DependentBrushes.Sync(original, live);
            if (ReferenceEquals(result, original)) return;
            resources[resource] = result;
            clones.Add(resource);
        }

        private void RefreshAllBrushes()
        {
            foreach (var resource in BrushDependencies().Keys.ToList()) RefreshBrush(resource);
        }

        public void Merge(string groupId, string absolutePath) { MergePreset(groupId, absolutePath); }

        public void Remove(string groupId) { RemovePreset(groupId); }

        /// <summary>Merges an extra resource dictionary (for example a color profile) on top of the theme. Replaces a previous one with the same id.</summary>
        public void MergePreset(string id, string xamlPath)
        {
            Run(() =>
            {
                DropClones();
                try
                {
                    RemovePresetCore(id);
                    using (var stream = File.OpenRead(xamlPath))
                    {
                        var dictionary = (ResourceDictionary)XamlReader.Load(stream);
                        // merged in the order of the groups in the schema, not in the order they were applied: the later group wins where two files
                        // define the same resource (the Typography group over the fonts of a Material)
                        var merged = Application.Current.Resources.MergedDictionaries;
                        int at = merged.Count, rank = schema.PresetRank(id);
                        foreach (var other in presets)
                        {
                            if (schema.PresetRank(other.Key) <= rank) continue;
                            int index = merged.IndexOf(other.Value);
                            if (index >= 0 && index < at) at = index;
                        }
                        merged.Insert(at, dictionary);
                        presets[id] = dictionary;
                    }
                }
                finally { RefreshAllBrushes(); }
            });
        }

        public void RemovePreset(string id)
        {
            Run(() =>
            {
                DropClones();
                try { RemovePresetCore(id); }
                finally { RefreshAllBrushes(); }
            });
        }

        private void DropClones()
        {
            foreach (var resource in clones) Application.Current.Resources.Remove(resource);
            clones.Clear();
        }

        private void RemovePresetCore(string id)
        {
            ResourceDictionary existing;
            if (presets.TryGetValue(id, out existing))
            {
                Application.Current.Resources.MergedDictionaries.Remove(existing);
                presets.Remove(id);
            }
        }

        private void Run(Action action)
        {
            if (ui.CheckAccess()) action();
            else ui.Invoke(action);
        }
    }
}
