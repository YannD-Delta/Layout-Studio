using System;
using System.IO;
using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>Where preset XAML files (color profiles...) are merged into or removed from the running theme.</summary>
    public interface IPresetFiles
    {
        void Merge(string groupId, string absolutePath);
        void Remove(string groupId);
    }

    /// <summary>What ApplyProfile could not use: names this theme version lacks, and values that failed validation.</summary>
    public class ApplyReport
    {
        public int Applied;
        public readonly System.Collections.Generic.List<string> Unknown = new System.Collections.Generic.List<string>();
        public readonly System.Collections.Generic.List<string> Invalid = new System.Collections.Generic.List<string>();
        public int Ignored { get { return Unknown.Count + Invalid.Count; } }

        /// <summary>One sentence for the user, or null when nothing was dropped.</summary>
        public string Describe()
        {
            if (Ignored == 0) return null;
            var names = Unknown.Concat(Invalid).Take(6).ToList();
            return "Imported " + Applied + " settings, ignored " + Ignored + " ("
                + string.Join(", ", names) + (Ignored > names.Count ? ", ..." : "")
                + "): unknown on this theme version or out of range.";
        }
    }

    /// <summary>Applies a profile to the store: preset selections first (the base), then explicit overrides on top.</summary>
    public class ProfileApplier
    {
        private readonly Schema schema;
        private readonly ConstantStore store;
        private readonly IPresetFiles files;
        private readonly string themeDir;

        /// <summary>False while the plugin starts (nothing is on screen yet); true once the window exists.</summary>
        public bool Live { get; set; }

        /// <summary>True when a structural preset was chosen or dropped while live: it takes effect at the next start.</summary>
        public bool RestartRequired { get; private set; }

        /// <summary>The Look shared with the other mode (null = this theme has none): its preset choices and tokens apply under the profile's own overrides.</summary>
        public Look Look { get; set; }

        /// <summary>Raised after a preset choice was applied, so the profile can be saved even when no constant changed.</summary>
        public event Action PresetChanged;

        public int SubscriberCount { get { var h = PresetChanged; return h == null ? 0 : h.GetInvocationList().Length; } }

        /// <summary>Remembers the handlers now attached; the returned action detaches every handler added after this call.</summary>
        public Action MarkSubscribers()
        {
            var before = PresetChanged == null ? new Delegate[0] : PresetChanged.GetInvocationList();
            return () =>
            {
                var now = PresetChanged;
                if (now == null) return;
                foreach (var h in now.GetInvocationList())
                    if (Array.IndexOf(before, h) < 0) PresetChanged -= (Action)h;
            };
        }

        /// <summary>Receives a line when a preset file could not be merged; the rest of the profile still applies.</summary>
        public Action<string> Log { get; set; }

        public ProfileApplier(Schema schema, ConstantStore store, IPresetFiles files, string themeDir)
        {
            this.schema = schema;
            this.store = store;
            this.files = files;
            this.themeDir = themeDir;
        }

        public ApplyReport ApplyProfile(Profile profile)
        {
            var report = new ApplyReport();
            LegacyKeys.Migrate(profile);
            // A shared group follows the Look; the profile records what is applied (a group the Look does not have yet is adopted below by ApplyPreset).
            if (Look != null)
                foreach (var kv in Look.Presets.ToList())
                    if (Look.IsShared(kv.Key) && PresetKnown(kv.Key, kv.Value)) profile.Presets[kv.Key] = kv.Value;
            // Iterate over copies: ApplyPreset records the selection back into profile.Presets.
            foreach (var kv in profile.Presets.ToList())
            {
                if (!PresetKnown(kv.Key, kv.Value)) { report.Unknown.Add(kv.Key); continue; }
                ApplyPreset(profile, kv.Key, kv.Value);
                report.Applied++;
            }
            if (Look != null) report.Invalid.AddRange(Look.ApplyTokens(store));
            foreach (var kv in profile.Overrides.ToList())
            {
                string error;
                // unknown keys and invalid values are skipped, never fatal, but counted
                if (store.TrySet(kv.Key, kv.Value, out error)) report.Applied++;
                else if (error != null && error.StartsWith("unknown constant")) report.Unknown.Add(kv.Key);
                else report.Invalid.Add(kv.Key);
            }
            return report;
        }

        public bool HasChoice(string groupId, string choiceId) { return PresetKnown(groupId, choiceId); }

        private bool PresetKnown(string groupId, string choiceId)
        {
            var control = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls)
                .FirstOrDefault(c => c.Kind == "preset" && c.Id == groupId);
            return control != null && control.Choices.Any(c => c.Id == choiceId);
        }

        /// <summary>The choice a group has when the profile does not name one.</summary>
        public string DefaultChoiceOf(string groupId)
        {
            var control = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls)
                .FirstOrDefault(c => c.Kind == "preset" && c.Id == groupId);
            return control == null ? null : (control.DefaultChoice ?? "Default");
        }

        /// <summary>Selects a choice of a preset group: resets what the group's choices touch, then applies the chosen one.</summary>
        public void ApplyPreset(Profile profile, string groupId, string choiceId)
        {
            var control = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls)
                .FirstOrDefault(c => c.Kind == "preset" && c.Id == groupId);
            if (control == null) return;
            var choice = control.Choices.FirstOrDefault(c => c.Id == choiceId);
            if (choice == null) return;

            string previousId;
            var previous = profile.Presets.TryGetValue(groupId, out previousId) ? control.Choices.FirstOrDefault(c => c.Id == previousId) : null;

            foreach (var key in control.Choices.SelectMany(c => c.Constants.Keys).Distinct()) store.Reset(key);
            foreach (var kv in choice.Constants)
            {
                string error;
                store.TrySet(kv.Key, kv.Value, out error);
            }
            if (Live && (choice.Structural || (previous != null && previous.Structural)))
            {
                RestartRequired = true;   // replacing control templates under a running Playnite window can crash it
            }
            else
            {
                try
                {
                    if (choice.Files.Count > 0) files.Merge(groupId, Path.Combine(themeDir, choice.Files[0]));
                    else files.Remove(groupId);
                }
                catch (Exception ex)
                {
                    // a missing or broken preset file must not leave the whole profile half applied
                    if (Log != null) Log("preset " + groupId + "/" + choiceId + " could not be applied: " + ex.Message);
                }
            }
            profile.Presets[groupId] = choiceId;
            if (Look != null) Look.SetPreset(groupId, choiceId);   // ignored unless the group is shared
            var handler = PresetChanged;
            if (handler != null) handler();
        }
    }
}
