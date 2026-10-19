using System;

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

    /// <summary>The profile page: export/import a share code and reset everything after a confirmation.</summary>
    public class SharingViewModel : ViewModelBase
    {
        private readonly IProfileSharing service;
        private readonly Action<string> copy;
        private string code = "", message;
        private bool isError, resetPending;

        public SharingViewModel(IProfileSharing service, Action<string> copyToClipboard)
        {
            this.service = service;
            copy = copyToClipboard;
            ExportCommand = new RelayCommand(Export);
            ImportCommand = new RelayCommand(Import, () => !string.IsNullOrWhiteSpace(code));
            AskResetCommand = new RelayCommand(() => ResetPending = true);
            CancelResetCommand = new RelayCommand(() => ResetPending = false);
            ConfirmResetCommand = new RelayCommand(Reset, () => resetPending);
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
