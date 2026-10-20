using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LayoutStudio.Editor
{
    /// <summary>What the profile page needs from the running plugin: share codes and a safe reset.</summary>
    public interface IProfileSharing
    {
        /// <summary>Saves the active profile and returns its share code.</summary>
        string Export();

        /// <summary>Replaces the active profile by the one in the code. Returns an error message, or null on success.</summary>
        string Import(string code);

        /// <summary>After a successful Import: what was dropped (null when everything applied).</summary>
        string ImportNote { get; }

        /// <summary>Saves the current profile (kept as a copy), then returns everything to the theme defaults. Returns an error message, or null on success.</summary>
        string ResetAll();
    }

    /// <summary>What the profile page needs to list, switch and create profiles (the plugin implements it next to <see cref="IProfileSharing"/>).</summary>
    public interface IProfileSwitching
    {
        IList<string> ProfileNames();
        string ActiveProfileName { get; }

        /// <summary>Saves the active profile, then loads the named one in its place. Returns an error message, or null on success.</summary>
        string SwitchProfile(string name);

        /// <summary>Copies the active profile under a new name and switches to it. Returns an error message, or null on success.</summary>
        string NewProfile(string name);
    }

    /// <summary>One line of the profile list.</summary>
    public class ProfileEntryViewModel : ViewModelBase
    {
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public RelayCommand SwitchCommand { get; set; }
    }

    /// <summary>The profile page: export/import a share code and reset everything after a confirmation.</summary>
    public class SharingViewModel : ViewModelBase
    {
        private readonly IProfileSharing service;
        private readonly Action<string> copy;
        private readonly IProfileSwitching switching;
        private string newName = "";
        private string code = "", message;
        private bool isError, resetPending;

        public SharingViewModel(IProfileSharing service, Action<string> copyToClipboard)
        {
            this.service = service;
            copy = copyToClipboard;
            switching = service as IProfileSwitching;
            Profiles = new ObservableCollection<ProfileEntryViewModel>();
            NewProfileCommand = new RelayCommand(NewProfile, () => !string.IsNullOrWhiteSpace(newName));
            RefreshProfiles();
            ExportCommand = new RelayCommand(Export);
            ImportCommand = new RelayCommand(Import, () => !string.IsNullOrWhiteSpace(code));
            AskResetCommand = new RelayCommand(() => ResetPending = true);
            CancelResetCommand = new RelayCommand(() => ResetPending = false);
            ConfirmResetCommand = new RelayCommand(Reset, () => resetPending);
        }

        /// <summary>False when the plugin cannot switch profiles: the page then shows nothing about it.</summary>
        public bool HasSwitcher { get { return switching != null; } }

        public ObservableCollection<ProfileEntryViewModel> Profiles { get; private set; }
        public RelayCommand NewProfileCommand { get; private set; }

        /// <summary>The name typed for a new profile (a copy of the one in use).</summary>
        public string NewProfileName
        {
            get { return newName; }
            set { newName = value ?? ""; Raise("NewProfileName"); NewProfileCommand.RaiseCanExecuteChanged(); }
        }

        public void RefreshProfiles()
        {
            Profiles.Clear();
            if (switching == null) return;
            string active = switching.ActiveProfileName;
            foreach (var name in switching.ProfileNames())
            {
                string captured = name;
                Profiles.Add(new ProfileEntryViewModel { Name = name, IsActive = string.Equals(name, active, StringComparison.OrdinalIgnoreCase), SwitchCommand = new RelayCommand(() => SwitchTo(captured)) });
            }
        }

        private void SwitchTo(string name)
        {
            string error;
            try { error = switching.SwitchProfile(name); }
            catch (Exception ex) { Fail(ex); return; }
            IsError = error != null;
            Message = error ?? ("Profile '" + name + "' is on. The one you had is saved and one click away in this list.");
            RefreshProfiles();
        }

        private void NewProfile()
        {
            string name = newName.Trim();
            string error;
            try { error = switching.NewProfile(name); }
            catch (Exception ex) { Fail(ex); return; }
            IsError = error != null;
            Message = error ?? ("Profile '" + name + "' created from the one you had and now in use.");
            if (error == null) NewProfileName = "";
            RefreshProfiles();
        }

        public RelayCommand ExportCommand { get; private set; }
        public RelayCommand ImportCommand { get; private set; }
        public RelayCommand AskResetCommand { get; private set; }
        public RelayCommand CancelResetCommand { get; private set; }
        public RelayCommand ConfirmResetCommand { get; private set; }

        public string Code
        {
            get { return code; }
            set
            {
                code = value ?? "";
                Raise("Code");
                ImportCommand.RaiseCanExecuteChanged();
            }
        }

        public string Message
        {
            get { return message; }
            private set { message = value; Raise("Message"); }
        }

        public bool IsError
        {
            get { return isError; }
            private set { isError = value; Raise("IsError"); }
        }

        public bool ResetPending
        {
            get { return resetPending; }
            private set
            {
                resetPending = value;
                Raise("ResetPending");
                ConfirmResetCommand.RaiseCanExecuteChanged();
            }
        }

        private void Export()
        {
            try { Code = service.Export(); }
            catch (Exception ex) { Fail(ex); return; }
            if (copy != null) copy(Code);
            IsError = false;
            Message = "Share code ready" + (copy != null ? " and copied" : "") + ". Send it to someone; they paste it here and press Import.";
        }

        private void Import()
        {
            string error;
            try { error = service.Import(code.Trim()); }
            catch (Exception ex) { Fail(ex); return; }
            IsError = error != null;
            Message = error ?? ("Profile imported. " + (service.ImportNote == null ? "" : service.ImportNote + " ")
                + "Undo history was cleared; your previous profile is kept as a .bak file.");
        }

        private void Reset()
        {
            ResetPending = false;
            string error;
            try { error = service.ResetAll(); }
            catch (Exception ex) { Fail(ex); return; }
            IsError = error != null;
            Message = error ?? "Everything is back to the theme defaults. Your previous profile is kept as a copy next to it.";
        }

        private void Fail(Exception ex)
        {
            IsError = true;
            Message = "That did not work: " + ex.Message;
        }
    }
}
