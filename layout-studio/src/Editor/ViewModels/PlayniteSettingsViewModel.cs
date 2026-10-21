using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    /// <summary>Read and write access to Playnite's live settings (implemented with reflection in the plugin; a fake in the tests).</summary>
    public interface IPlayniteSettingsAccess
    {
        IDictionary<string, object> Read();
        bool TrySet(string name, object value, out string error);
    }

    public class SettingsFindingViewModel : ViewModelBase
    {
        public string Id { get; set; }
        public string Message { get; set; }
        public bool IsPerformance { get; set; }
        public RelayCommand FixCommand { get; set; }
    }

    /// <summary>
    /// The banner about Playnite's own settings that conflict with the theme: one line per finding, a button to
    /// fix each (or all), a restore button, and a confirmation read back from the live settings.
    /// </summary>
    public class PlayniteSettingsViewModel : ViewModelBase
    {
        private readonly IPlayniteSettingsAccess access;
        private readonly Dictionary<string, object> previous = new Dictionary<string, object>();   // values before the first fix
        private string status;
        /// <summary>Whether the theme's window glass is on (the artwork option matters only then); unknown counts as on.</summary>
        public Func<bool> ThemeGlassOn { get; set; }
        private bool isError;

        private readonly string backupPath;

        /// <summary>The editor's texts in the user's language; the English in the code is the fallback.</summary>
        public UiStrings Ui { get; private set; }

        /// <param name="backupPath">Where the original values are kept until restored, so closing the editor does not lose them (null: not kept).</param>
        public PlayniteSettingsViewModel(IPlayniteSettingsAccess access, string backupPath = null, UiStrings ui = null)
        {
            this.access = access;
            Ui = ui ?? new UiStrings();
            Ui.Changed += () => { Refresh(); if (restoreHint) ShowRestoreHint(); };
            this.backupPath = backupPath;
            LoadBackup();
            Findings = new ObservableCollection<SettingsFindingViewModel>();
            FixAllCommand = new RelayCommand(FixAll, () => Findings.Count > 0);
            RestoreCommand = new RelayCommand(Restore, () => previous.Count > 0);
            Refresh();
            // settings changed in an earlier session are still on file: say so, or the Put back button would stay hidden until the next fix
            if (previous.Count > 0) { restoreHint = true; ShowRestoreHint(); }
        }

        private bool restoreHint;

        private void ShowRestoreHint()
        {
            IsError = false;
            Status = Ui.Format("LOCLS_Status_Changed", "Layout Studio changed {0} of Playnite's settings earlier. Put back as before restores them.", previous.Count);
        }

        public ObservableCollection<SettingsFindingViewModel> Findings { get; private set; }
        public RelayCommand FixAllCommand { get; private set; }
        public RelayCommand RestoreCommand { get; private set; }
        public bool HasFindings { get { return Findings.Count > 0; } }

        /// <summary>How many of Playnite's settings Layout Studio changed and has not put back.</summary>
        public int ChangedByUsCount { get { return previous.Count; } }
        public bool HasStatus { get { return !string.IsNullOrEmpty(status); } }

        public string Status
        {
            get { return status; }
            private set { status = value; Raise("Status"); Raise("HasStatus"); }
        }

        public bool IsError
        {
            get { return isError; }
            private set { isError = value; Raise("IsError"); }
        }

        /// <summary>Reads the live settings again and rebuilds the list.</summary>
        public void Refresh()
        {
            var found = access == null ? new List<SettingsFinding>() : AppearanceCheck.Evaluate(Read(), GlassOn()).ToList();
            Findings.Clear();
            foreach (var f in found)
            {
                var captured = f;
                Findings.Add(new SettingsFindingViewModel
                {
                    Id = f.Id, Message = Message(f), IsPerformance = f.IsPerformance,
                    FixCommand = new RelayCommand(() => Apply(new List<SettingsFinding> { captured }))
                });
            }
            Raise("HasFindings");
            FixAllCommand.RaiseCanExecuteChanged();
            RestoreCommand.RaiseCanExecuteChanged();
        }

        /// <summary>The live settings as they are now; null when they cannot be read.</summary>
        public IDictionary<string, object> Snapshot()
        {
            return access == null ? null : Read();
        }

        /// <summary>Writes the given Playnite settings (remembering the previous values for "Put back as before") and refreshes the banner.</summary>
        public void ApplyChanges(IEnumerable<KeyValuePair<string, object>> changes)
        {
            var finding = new SettingsFinding { Id = "advice" };
            finding.Changes.AddRange(changes);
            Apply(new List<SettingsFinding> { finding });
        }

        private string Message(SettingsFinding f)
        {
            if (f.Id == "windowBackgroundOff") return Ui.Get("LOCLS_Banner_Off", f.Message);
            if (f.Id != "windowBackground") return f.Message;
            var parts = new List<string>();
            if (f.Blur > 0) parts.Add(Ui.Format("LOCLS_Banner_PartBlur", "blur ({0})", f.Blur.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)));
            if (f.Dark > 0) parts.Add(Ui.Format("LOCLS_Banner_PartDark", "darkening ({0})", f.Dark.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)));
            return Ui.Format("LOCLS_Banner_Layers", "Playnite applies {0} to the artwork before the theme draws it. The theme's own blur and darkness come on top. Set the amount to 0 to leave it all to the theme.",
                string.Join(Ui.Get("LOCLS_Banner_And", " and "), parts));
        }

        private bool GlassOn()
        {
            try { return ThemeGlassOn == null || ThemeGlassOn(); }
            catch (Exception) { return true; }
        }

        private IDictionary<string, object> Read()
        {
            try { return access.Read(); }
            catch (Exception) { return null; }
        }

        private void FixAll()
        {
            Apply(AppearanceCheck.Evaluate(Read(), GlassOn()).ToList());
        }

        private void Apply(List<SettingsFinding> findings)
        {
            restoreHint = false;
            var before = Read() ?? new Dictionary<string, object>();
            foreach (var finding in findings)
            {
                foreach (var change in finding.Changes)
                {
                    string error;
                    bool ok;
                    object original;
                    if (!previous.ContainsKey(change.Key) && before.TryGetValue(change.Key, out original)) { previous[change.Key] = original; SaveBackup(); }   // on disk before the first write
                    try { ok = access.TrySet(change.Key, change.Value, out error); }
                    catch (Exception ex) { ok = false; error = ex.Message; }
                    if (!ok)
                    {
                        IsError = true;
                        Status = Ui.Format("LOCLS_Status_Refused", "Playnite refused to change {0} ({1}). Change it in Playnite's settings (Appearance).", change.Key, error);
                        Refresh();
                        return;
                    }
                }
            }
            Refresh();   // read back: what is still on is shown again
            IsError = false;
            Status = HasFindings ? Ui.Get("LOCLS_Status_More", "Applied. Playnite reports something else to review.") : Ui.Get("LOCLS_Status_Done", "Done. Playnite now reports the new values.");
        }

        private void Restore()
        {
            restoreHint = false;
            foreach (var kv in previous.ToList())
            {
                string error;
                try { access.TrySet(kv.Key, kv.Value, out error); } catch (Exception) { }
            }
            previous.Clear();
            SaveBackup();
            Refresh();
            IsError = false;
            Status = Ui.Get("LOCLS_Status_Restored", "Playnite's settings are back as they were.");
        }

        private void LoadBackup()
        {
            if (backupPath == null || !File.Exists(backupPath)) return;
            try
            {
                var map = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(backupPath)) as IDictionary<string, object>;
                if (map == null) return;
                foreach (var kv in map) previous[kv.Key] = kv.Value;
            }
            catch (Exception) { previous.Clear(); }   // a damaged backup is ignored, never fatal
        }

        private void SaveBackup()
        {
            if (backupPath == null) return;
            try
            {
                if (previous.Count == 0) { if (File.Exists(backupPath)) File.Delete(backupPath); return; }
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
                File.WriteAllText(backupPath, new JavaScriptSerializer().Serialize(previous));
            }
            catch (Exception) { }
        }
    }
}
