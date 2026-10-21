using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    public class DiagnosticLineViewModel
    {
        public string Title { get; set; }
        public string Value { get; set; }
        public string Detail { get; set; }
        public bool HasDetail { get { return !string.IsNullOrEmpty(Detail); } }
        public bool IsWarning { get; set; }
        public bool IsOk { get; set; }
    }

    /// <summary>The Diagnostics page: what Layout Studio sees (versions, schema, profile, Playnite settings), to read or to copy into a bug report.</summary>
    public class DiagnosticsViewModel : ViewModelBase
    {
        private readonly Func<IList<DiagnosticLine>> collect;
        private readonly Action<string> copy;
        private string message;

        public DiagnosticsViewModel(Func<IList<DiagnosticLine>> collect, Action<string> copy)
        {
            this.collect = collect;
            this.copy = copy;
            Lines = new ObservableCollection<DiagnosticLineViewModel>();
            RefreshCommand = new RelayCommand(Refresh);
            CopyCommand = new RelayCommand(Copy, () => copy != null);
            Refresh();
        }

        public ObservableCollection<DiagnosticLineViewModel> Lines { get; private set; }
        public RelayCommand RefreshCommand { get; private set; }
        public RelayCommand CopyCommand { get; private set; }
        public string Text { get; private set; }
        public int WarningCount { get { return Lines.Count(l => l.IsWarning); } }
        public string Message { get { return message; } private set { message = value; Raise("Message"); } }

        public void Refresh()
        {
            IList<DiagnosticLine> lines;
            try { lines = collect(); }
            catch (Exception ex) { lines = new List<DiagnosticLine> { new DiagnosticLine("Diagnostics", "could not be collected: " + ex.Message, DiagnosticLevel.Warning) }; }
            Lines.Clear();
            foreach (var l in lines)
                Lines.Add(new DiagnosticLineViewModel { Title = l.Title, Value = l.Value, Detail = l.Detail, IsWarning = l.Level == DiagnosticLevel.Warning, IsOk = l.Level == DiagnosticLevel.Ok });
            Text = Diagnostics.Format(lines);
            Raise("WarningCount");
            Raise("Text");
        }

        private void Copy()
        {
            Refresh();
            copy(Text);
            Message = "Copied: paste it into a bug report.";
        }
    }
}
