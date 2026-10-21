using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    public class SurfaceChoiceViewModel
    {
        public string Id { get; set; }
        public string Title { get; set; }
    }

    /// <summary>One information item that shows on several surfaces: where, which one to keep, and the button that hides the others.</summary>
    public class OnceRowViewModel : ViewModelBase
    {
        private readonly InfoOnceViewModel owner;
        private readonly InfoItem item;
        private List<string> visible = new List<string>();

        public OnceRowViewModel(InfoOnceViewModel owner, InfoItem item)
        {
            this.owner = owner;
            this.item = item;
            ShowOnceCommand = new RelayCommand(() => owner.ShowOnce(item, Keep));
        }

        public string Id { get { return item.Id; } }
        public string Title { get; set; }
        public RelayCommand ShowOnceCommand { get; private set; }

        public IList<string> Visible { get { return visible; } }
        public bool IsConflict { get { return visible.Count > 1 && !Forced; } }

        /// <summary>"Header tile, Artwork chips, Information panel".</summary>
        public string WhereText { get { return string.Join(", ", visible.Select(owner.SurfaceTitle)); } }

        public IList<SurfaceChoiceViewModel> KeepChoices
        {
            get { return visible.Select(s => new SurfaceChoiceViewModel { Id = s, Title = owner.SurfaceTitle(s) }).ToList(); }
        }

        /// <summary>The surface that keeps the information: the one assigned, else the first in priority among those that show it.</summary>
        public string Keep
        {
            get
            {
                string assigned = owner.AssignedOf(item.Id);
                if (assigned != null && assigned != InfoOnceViewModel.ForceMark && visible.Contains(assigned)) return assigned;
                return InfoOnce.DefaultKeep(visible);
            }
            set { owner.Assign(item.Id, value); Raise("Keep"); Raise("BlockedText"); }
        }

        /// <summary>The exception: this item may show on several surfaces, it is not reported.</summary>
        public bool Forced
        {
            get { return owner.AssignedOf(item.Id) == InfoOnceViewModel.ForceMark; }
            set { owner.Force(item.Id, value); }
        }

        /// <summary>What hiding the other copies cannot do (a surface with no switch in the theme).</summary>
        public string BlockedText { get { return owner.BlockedText(item, Keep); } }
        public bool HasBlocked { get { return !string.IsNullOrEmpty(BlockedText); } }

        public void Update(List<string> now)
        {
            visible = now;
            RaiseAll();
        }
    }

    /// <summary>
    /// The "Information shown once" part of the Items page: finds the items shown on more than one surface (artwork chips, header tile, their own
    /// section, the information panel), lets each be assigned to one surface or forced, and hides the other copies in one undo step.
    /// </summary>
    public class InfoOnceViewModel : ViewModelBase
    {
        public const string ForceMark = "force";
        private readonly ConstantStore store;
        private readonly History history;
        private readonly IDictionary<string, string> editor;
        private readonly IList<string> tileSlots;
        private readonly Func<string, string> localize;
        private readonly Dictionary<string, OnceRowViewModel> all = new Dictionary<string, OnceRowViewModel>();
        private bool updating;

        public InfoOnceViewModel(Schema schema, ConstantStore store, History history, IDictionary<string, string> editor, Func<string, string> localize)
        {
            this.store = store;
            this.history = history;
            this.editor = editor ?? new Dictionary<string, string>();
            this.localize = localize ?? (k => null);
            var tiles = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).FirstOrDefault(c => c.Kind == "tileSlots");
            tileSlots = tiles == null ? new List<string>() : tiles.Keys;
            Rows = new ObservableCollection<OnceRowViewModel>();
            // only the items whose constants this theme has
            foreach (var item in InfoOnce.All)
            {
                if (item.ChipKey != null && store.Find(item.ChipKey) == null) continue;
                var row = new OnceRowViewModel(this, item) { Title = Word("LOCLS_Item_" + System.Text.RegularExpressions.Regex.Replace(item.Title, "[^A-Za-z]", ""), item.Title) };
                all[item.Id] = row;
            }
            ShowAllCommand = new RelayCommand(ShowAllOnce, () => Rows.Any(r => r.IsConflict));
            store.ValueChanged += key => Refresh();
            history.Changed += () => Refresh();
            Refresh();
        }

        /// <summary>The items shown on more than one surface and not forced.</summary>
        public ObservableCollection<OnceRowViewModel> Rows { get; private set; }

        /// <summary>Every item the table knows, conflict or not (the Forced switch of one that is fine is kept too).</summary>
        public IEnumerable<OnceRowViewModel> AllRows { get { return all.Values; } }

        public RelayCommand ShowAllCommand { get; private set; }

        private bool listOpen;
        /// <summary>The list of items shown twice is folded until you ask for it: the page leads with the places, not with the conflicts.</summary>
        public bool IsListOpen { get { return listOpen; } set { if (listOpen != value) { listOpen = value; Raise("IsListOpen"); } } }
        public int ConflictCount { get { return Rows.Count; } }
        public bool HasConflicts { get { return Rows.Count > 0; } }

        public string Summary
        {
            get
            {
                return Rows.Count == 0 ? Word("LOCLS_Once_None", "Every piece of information is shown once.")
                    : string.Format(System.Globalization.CultureInfo.InvariantCulture, Word("LOCLS_Once_Some", "{0} shown in more than one place."), Rows.Count);
            }
        }

        private string Word(string key, string english)
        {
            string text = null;
            try { text = localize(key); } catch (Exception) { }
            return string.IsNullOrEmpty(text) ? english : text;
        }

        public string SurfaceTitle(string surface)
        {
            switch (surface)
            {
                case InfoOnce.Tile: return Word("LOCLS_Surface_Tile", "Header tile");
                case InfoOnce.Chips: return Word("LOCLS_Surface_Chips", "Artwork chips");
                case InfoOnce.Section: return Word("LOCLS_Surface_Section", "Its own section");
                default: return Word("LOCLS_Surface_Panel", "Information panel");
            }
        }

        private Func<string, string> Get { get { return store.Get; } }
        private Func<string, bool> Has { get { return k => store.Find(k) != null; } }

        public string AssignedOf(string id)
        {
            string value;
            return editor.TryGetValue("once." + id, out value) ? value : null;
        }

        private void SetEditor(string id, string value)
        {
            string key = "once." + id, before = AssignedOf(id);
            if (before == value) return;
            history.BeginBatch();
            try
            {
                Action<string> set = v => { if (v == null) editor.Remove(key); else editor[key] = v; };
                set(value);
                history.RecordAction(() => { set(before); Refresh(); }, () => { set(value); Refresh(); });
            }
            finally { history.EndBatch(); }
            Refresh();
        }

        /// <summary>Assigns the information to one surface (a profile setting; the other copies are hidden by Show once).</summary>
        public void Assign(string id, string surface) { SetEditor(id, surface); }

        public void Force(string id, bool on) { SetEditor(id, on ? ForceMark : null); }

        public string BlockedText(InfoItem item, string keep)
        {
            if (keep == null) return null;
            var plan = InfoOnce.Plan(item, keep, Get, Has, tileSlots);
            if (plan.Blocked.Count == 0) return null;
            var names = plan.Blocked.Select(SurfaceTitle).ToList();
            if (plan.Blocked.Contains(InfoOnce.Panel))
                return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    Word("LOCLS_Once_Blocked", "{0}: the theme has no switch to hide it ({1} is missing)."), string.Join(", ", names), InfoOnce.PanelKey(item));
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, Word("LOCLS_Once_BlockedOther", "{0}: no switch to hide it."), string.Join(", ", names));
        }

        /// <summary>Hides every copy of the item but the one on <paramref name="keep"/>, in one undo step. Returns the number of constants changed.</summary>
        public int ShowOnce(InfoItem item, string keep)
        {
            if (keep == null) return 0;
            var plan = InfoOnce.Plan(item, keep, Get, Has, tileSlots);
            history.BeginBatch();
            try
            {
                foreach (var change in plan.Changes) { string error; store.TrySet(change.Key, change.Value, out error); }
                SetEditor(item.Id, keep);
            }
            finally { history.EndBatch(); }
            return plan.Changes.Count;
        }

        public void ShowAllOnce()
        {
            history.BeginBatch();
            try { foreach (var row in Rows.ToList()) ShowOnce(InfoOnce.Find(row.Id), row.Keep); }
            finally { history.EndBatch(); }
        }

        public void Refresh()
        {
            if (updating) return;
            updating = true;
            try
            {
                var conflicts = new List<OnceRowViewModel>();
                foreach (var kv in all)
                {
                    var visible = InfoOnce.Visible(InfoOnce.Find(kv.Key), Get, Has, tileSlots);
                    kv.Value.Update(visible);
                    if (kv.Value.IsConflict) conflicts.Add(kv.Value);
                }
                bool same = conflicts.Count == Rows.Count && conflicts.Zip(Rows, (a, b) => ReferenceEquals(a, b)).All(x => x);
                if (!same)
                {
                    Rows.Clear();
                    foreach (var c in conflicts) Rows.Add(c);
                }
                Raise("ConflictCount"); Raise("HasConflicts"); Raise("Summary");
                ShowAllCommand.RaiseCanExecuteChanged();
            }
            finally { updating = false; }
        }
    }
}
