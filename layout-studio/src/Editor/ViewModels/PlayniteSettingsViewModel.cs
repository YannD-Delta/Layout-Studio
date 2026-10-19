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
    /// The banner about Playnite's own settings that conflict with the theme or cost performance: one line per finding, a button to
    /// turn each (or all) off, a restore button, and a confirmation read back from the live settings.
    /// </summary>
    public class PlayniteSettingsViewModel : ViewModelBase
    {
        private readonly IPlayniteSettingsAccess access;
        private readonly Dictionary<string, object> previous = new Dictionary<string, object>();   // values before the first fix
        private string status;
        private bool isError;

        private readonly string backupPath;

        /// <param name="backupPath">Where the original values are kept until restored, so closing the editor does not lose them (null: not kept).</param>
        public PlayniteSettingsViewModel(IPlayniteSettingsAccess access, string backupPath = null)
        {
            this.access = access;
            this.backupPath = backupPath;
            LoadBackup();
            Findings = new ObservableCollection<SettingsFindingViewModel>();
            FixAllCommand = new RelayCommand(FixAll, () => Findings.Count > 0);
            RestoreCommand = new RelayCommand(Restore, () => previous.Count > 0);
            Refresh();
        }

        public ObservableCollection<SettingsFindingViewModel> Findings { get; private set; }
        public RelayCommand FixAllCommand { get; private set; }
        public RelayCommand RestoreCommand { get; private set; }
        public bool HasFindings { get { return Findings.Count > 0; } }
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
            var found = access == null ? new List<SettingsFinding>() : AppearanceCheck.Evaluate(Read()).ToList();
            Findings.Clear();
            foreach (var f in found)
            {
                var captured = f;
                Findings.Add(new SettingsFindingViewModel
                {
                    Id = f.Id, Message = f.Message, IsPerformance = f.IsPerformance,
                    FixCommand = new RelayCommand(() => Apply(new List<SettingsFinding> { captured }))
                });
            }
            Raise("HasFindings");
            FixAllCommand.RaiseCanExecuteChanged();
            RestoreCommand.RaiseCanExecuteChanged();
        }

        private IDictionary<string, object> Read()
        {
            try { return access.Read(); }
            catch (Exception) { return null; }
        }

        private void FixAll()
        {
            Apply(AppearanceCheck.Evaluate(Read()).ToList());
        }

        private void Apply(List<SettingsFinding> findings)
        {
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
                        Status = "Playnite refused to change " + change.Key + " (" + error + "). Turn it off in Playnite's settings (Appearance).";
                        Refresh();
                        return;
                    }
                }
            }
            Refresh();   // read back: what is still on is shown again
            IsError = false;
            Status = HasFindings ? "Changed, but Playnite still reports some options on." : "Done. Playnite now reports those options off.";
        }

        private void Restore()
        {
            foreach (var kv in previous.ToList())
            {
                string error;
                try { access.TrySet(kv.Key, kv.Value, out error); } catch (Exception) { }
            }
            previous.Clear();
            SaveBackup();
            Refresh();
            IsError = false;
            Status = "Playnite's settings are back as they were.";
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
