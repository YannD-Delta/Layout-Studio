using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    /// <summary>The options of one item that show in one place (the chips, the game page, the grid...).</summary>
    public class ItemSurfaceViewModel
    {
        public string Title { get; set; }
        public IList<SettingViewModel> Settings { get; set; }
    }

    /// <summary>One item (Genres, Release date, HowLongToBeat...) with every option the theme has for it, instead of a lone checkbox in a long list.</summary>
    public class ItemCardViewModel : ViewModelBase
    {
        private bool expanded, visible = true;

        public string Id { get; set; }
        public string Title { get; set; }
        public IList<ItemSurfaceViewModel> Surfaces { get; set; }

        public IEnumerable<SettingViewModel> Settings { get { return Surfaces.SelectMany(s => s.Settings); } }
        public int OptionCount { get { return Settings.Count(); } }
        /// <summary>Words a count ("{0} options"); set by the editor from the language file.</summary>
        public System.Func<int, string> CountText { get; set; }
        public string OptionsText { get { return CountText != null ? CountText(OptionCount) : OptionCount == 1 ? "1 option" : OptionCount + " options"; } }
        public int ChangedCount { get { return Settings.Count(s => s.IsModified); } }
        public bool HasChanges { get { return ChangedCount > 0; } }

        public bool IsExpanded { get { return expanded; } set { if (expanded != value) { expanded = value; Raise("IsExpanded"); } } }
        public bool Visible { get { return visible; } set { if (visible != value) { visible = value; Raise("Visible"); } } }

        public void RefreshCounts() { Raise("ChangedCount"); Raise("HasChanges"); Raise("ShowsTwice"); }

        /// <summary>Reads the current value of a constant; set by the editor (null: the card never says "shown twice").</summary>
        public System.Func<string, string> Reader { get; set; }

        /// <summary>Folds the section at the bottom (one undo step); set by the editor.</summary>
        public System.Action FoldSection { get; set; }

        /// <summary>The words of the notice ("Shown in the chips and in its own section"); set by the editor from the language file.</summary>
        public string TwiceText { get; set; }
        public string FoldText { get; set; }

        /// <summary>True when the chip and the section both show this item and the section is not folded away at the bottom.</summary>
        public bool ShowsTwice { get { return Reader != null && ItemDuplicates.ShowsTwice(ItemDuplicates.For(Id), Reader); } }

        public RelayCommand FoldSectionCommand { get { return new RelayCommand(() => { if (FoldSection != null) FoldSection(); }); } }
    }

    /// <summary>The Items page: a card per piece of information or section with all its options, built from the schema's own controls.</summary>
    public class ItemsEditorViewModel : ViewModelBase, IResettable
    {
        private readonly History history;
        private readonly ConstantStore store;

        public ItemsEditorViewModel(Schema schema, ConstantStore store, History history, ProfileApplier applier, Profile profile,
            Func<string, string> localize, CultureInfo culture)
        {
            this.history = history;
            this.store = store;
            // the card words come from Core in English; the language file may translate each (key: ItemFamilies.LocKey)
            Func<string, string> Loc = english =>
            {
                string text = null;
                try { text = localize(ItemFamilies.LocKey(english)); } catch (Exception) { }
                return string.IsNullOrEmpty(text) ? english : text;
            };
            var cards = new List<ItemCardViewModel>();
            var controls = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Where(ItemFamilies.IsItemControl).ToList();
            foreach (var family in ItemFamilies.All)
            {
                var mine = controls.Where(c => ReferenceEquals(ItemFamilies.FamilyOf(c.Keys[0]), family)).ToList();
                if (mine.Count == 0) continue;
                var surfaces = mine
                    .Select(c =>
                    {
                        var setting = new SettingViewModel(c, store, history, applier, profile, localize, culture);
                        string title = ItemFamilies.OptionTitle(c.Keys[0], family);
                        if (title != null) setting.Title = Loc(title);
                        return new { Surface = ItemFamilies.SurfaceOf(c.Keys[0]), Setting = setting };
                    })
                    .GroupBy(x => x.Surface)
                    .OrderBy(g => ItemFamilies.SurfaceRank(g.Key))
                    .Select(g => new ItemSurfaceViewModel { Title = Loc(g.Key), Settings = g.Select(x => x.Setting).ToList() })
                    .ToList();
                var rule = ItemDuplicates.For(family.Id);
                var card = new ItemCardViewModel
                {
                    Id = family.Id, Title = Loc(family.Title), Surfaces = surfaces,
                    CountText = n => string.Format(CultureInfo.InvariantCulture, Phrase(localize, n == 1 ? "LOCLS_OptionOne" : "LOCLS_OptionMany", n == 1 ? "{0} option" : "{0} options"), n)
                };
                if (rule != null && ItemDuplicates.AllKeys().All(k => store.Find(k) != null))
                {
                    card.Reader = store.Get;
                    card.TwiceText = Phrase(localize, "LOCLS_Twice", "Shown in the artwork chips and in its own section.");
                    card.FoldText = Phrase(localize, "LOCLS_FoldSection", "Fold the section at the bottom");
                    card.FoldSection = () =>
                    {
                        history.BeginBatch();
                        try { foreach (var key in ItemDuplicates.FoldKeys(rule)) { string error; store.TrySet(key, "True", out error); } }
                        finally { history.EndBatch(); }
                    };
                }
                cards.Add(card);
            }
            Cards = cards;
            Once = new InfoOnceViewModel(schema, store, history, profile.Editor, localize);
            store.ValueChanged += key => { foreach (var c in Cards) c.RefreshCounts(); Raise("ChangedCount"); };
            history.Changed += () => { foreach (var c in Cards) c.RefreshCounts(); Raise("ChangedCount"); };
        }

        private static string Phrase(Func<string, string> localize, string key, string english)
        {
            string text = null;
            try { text = localize(key); } catch (Exception) { }
            return string.IsNullOrEmpty(text) ? english : text;
        }

        public IList<ItemCardViewModel> Cards { get; private set; }

        /// <summary>The "Information shown once" part: items on several surfaces and the buttons that hide the extra copies.</summary>
        public InfoOnceViewModel Once { get; private set; }

        private string focusedId, lastQuery = "";
        private bool lastChangedOnly;

        /// <summary>The card shown alone (opened from a section of the Sections page), or null for all of them.</summary>
        public string FocusedId { get { return focusedId; } }
        public bool IsFocused { get { return focusedId != null; } }
        public string FocusedTitle { get { var c = Cards.FirstOrDefault(x => x.Id == focusedId); return c == null ? "" : c.Title; } }
        public RelayCommand ExpandAllCommand { get { return new RelayCommand(() => { foreach (var c in Cards.Where(x => x.Visible)) c.IsExpanded = true; }); } }
        public RelayCommand CollapseAllCommand { get { return new RelayCommand(() => { foreach (var c in Cards) c.IsExpanded = false; }); } }
        public RelayCommand ShowAllCommand { get { return new RelayCommand(() => ShowOnly(null)); } }

        /// <summary>Shows one card alone and opened (null: all cards again).</summary>
        public void ShowOnly(string id)
        {
            focusedId = string.IsNullOrEmpty(id) || !Cards.Any(c => c.Id == id) ? null : id;
            Filter(lastQuery, lastChangedOnly);
            if (focusedId != null) Cards.First(c => c.Id == focusedId).IsExpanded = true;
            Raise("FocusedId"); Raise("IsFocused"); Raise("FocusedTitle");
        }

        /// <summary>Every option of every card, for searching and counting.</summary>
        public IEnumerable<SettingViewModel> AllSettings { get { return Cards.SelectMany(c => c.Settings); } }

        public int ChangedCount { get { return AllSettings.Count(s => s.IsModified); } }

        public void ResetAll()
        {
            history.BeginBatch();
            try { foreach (var s in AllSettings.Where(s => s.IsModified).ToList()) s.Reset(); }
            finally { history.EndBatch(); }
        }

        /// <summary>
        /// Shows only the cards with an option that matches the search (and, with <paramref name="changedOnly"/>, a changed one), opened so the
        /// matches are in view. Returns how many options match. An empty search shows everything, folded as the user left it.
        /// </summary>
        public int Filter(string query, bool changedOnly)
        {
            lastQuery = query ?? "";
            lastChangedOnly = changedOnly;
            bool searching = !string.IsNullOrWhiteSpace(query);
            int matches = 0;
            foreach (var card in Cards)
            {
                bool titleHit = searching && card.Title.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
                int cardMatches = 0;
                foreach (var s in card.Settings)
                {
                    s.Visible = (titleHit || s.Matches(query)) && (!changedOnly || s.IsModified);
                    if (s.Visible) cardMatches++;
                }
                bool inFocus = focusedId == null || card.Id == focusedId;
                card.Visible = cardMatches > 0 && inFocus;
                if (searching && cardMatches > 0 && inFocus) card.IsExpanded = true;
                if (inFocus) matches += cardMatches;
            }
            return matches;
        }
    }
}
