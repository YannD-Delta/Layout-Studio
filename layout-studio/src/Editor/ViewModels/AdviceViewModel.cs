using System;
using System.Collections.ObjectModel;
using System.Linq;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    /// <summary>One row of the "Graphics and performance" page.</summary>
    public class AdviceItemViewModel : ViewModelBase
    {
        public AdviceItemViewModel(AdviceItem item, UiStrings ui, Action fix, Action<string> openPage)
        {
            ui = ui ?? new UiStrings();
            Id = item.Id;
            Title = ui.Get("LOCLS_Adv_" + item.Id + "_Title", item.Title) + item.TitleSuffix;
            What = ui.Get("LOCLS_Adv_" + item.Id + "_What", item.What);
            Measured = ui.Get("LOCLS_Adv_" + item.Id + "_Measured" + item.MeasuredKey, item.Measured);
            StatusName = item.Status == AdviceStatus.Ok ? "ok" : item.Status == AdviceStatus.Attention ? "attention" : "tip";
            StatusText = item.Status == AdviceStatus.Ok ? ui.Get("LOCLS_Adv_Status_Ok", "OK") : item.Status == AdviceStatus.Attention ? ui.Get("LOCLS_Adv_Status_Attention", "To fix") : ui.Get("LOCLS_Adv_Status_Tip", "Optional");
            HasGraphics = (item.Impact & AdviceImpact.Graphics) != 0;
            HasPerformance = (item.Impact & AdviceImpact.Performance) != 0;
            ImpactText = HasGraphics && HasPerformance ? ui.Get("LOCLS_Adv_Impact_Both", "Graphics and performance") : HasPerformance ? ui.Get("LOCLS_Adv_Impact_Performance", "Performance") : ui.Get("LOCLS_Adv_Impact_Graphics", "Graphics");
            FixLabel = item.FixLabel == null ? null : ui.Get("LOCLS_Adv_Fix_" + item.FixLabel.Replace(" ", ""), item.FixLabel);
            HasFix = item.Changes.Count > 0;
            PageLabel = item.PageLabel == null ? null : ui.Format("LOCLS_Adv_Open", "Open {0}", item.PageLabel);
            HasPage = !string.IsNullOrEmpty(item.PageId);
            FixCommand = new RelayCommand(() => { if (fix != null) fix(); }, () => HasFix);
            string page = item.PageId;
            OpenPageCommand = new RelayCommand(() => { if (openPage != null && page != null) openPage(page); }, () => HasPage);
        }

        public string Id { get; private set; }
        public string Title { get; private set; }
        public string What { get; private set; }
        public string Measured { get; private set; }
        public string StatusName { get; private set; }     // ok | attention | tip
        public string StatusText { get; private set; }
        public string ImpactText { get; private set; }
        public bool HasGraphics { get; private set; }
        public bool HasPerformance { get; private set; }
        public string FixLabel { get; private set; }
        public bool HasFix { get; private set; }
        public string PageLabel { get; private set; }
        public bool HasPage { get; private set; }
        public RelayCommand FixCommand { get; private set; }
        public RelayCommand OpenPageCommand { get; private set; }
    }

    /// <summary>
    /// The first page of the editor: checks Playnite's own settings and a few theme choices, says what each one touches (graphics, performance
    /// or both) with measured numbers, and fixes the Playnite ones with a click. It is read again whenever a setting changes.
    /// </summary>
    public class AdviceViewModel : ViewModelBase
    {
        private readonly PlayniteSettingsViewModel playnite;
        private readonly ConstantStore store;
        private readonly Profile profile;
        private readonly Action<string> openPage;
        private readonly UiStrings ui;
        private string summary = "";

        /// <param name="playnite">Access to Playnite's live settings, or null when they cannot be read (only the theme checks remain).</param>
        /// <param name="openPage">Opens an editor page by id (the theme settings a tip points to).</param>
        public AdviceViewModel(PlayniteSettingsViewModel playnite, ConstantStore store, History history, Profile profile, Action<string> openPage, UiStrings ui = null)
        {
            this.ui = ui ?? new UiStrings();
            this.playnite = playnite;
            this.store = store;
            this.profile = profile;
            this.openPage = openPage;
            Items = new ObservableCollection<AdviceItemViewModel>();
            this.ui.Changed += Refresh;
            store.ValueChanged += key => Refresh();
            if (history != null) history.Changed += Refresh;
            Refresh();
        }

        public ObservableCollection<AdviceItemViewModel> Items { get; private set; }
        public int AttentionCount { get; private set; }
        public string MeasuredOn { get { return ui.Get("LOCLS_Adv_MeasuredOn", Advice.MeasuredOn); } }

        /// <summary>"2 things to fix" or "Nothing to fix".</summary>
        public string Summary
        {
            get { return summary; }
            private set { summary = value; Raise("Summary"); }
        }

        public void Refresh()
        {
            var playniteSettings = playnite == null ? null : playnite.Snapshot();
            bool glassOn = store.Find("WindowGlassEnabled") == null || store.Get("WindowGlassEnabled") != "False";
            Func<string, string> theme = key => store.Find(key) == null ? null : store.Get(key);
            var found = Advice.Evaluate(playniteSettings, theme, profile.Presets, glassOn);
            Items.Clear();
            foreach (var item in found)
            {
                var captured = item;
                Items.Add(new AdviceItemViewModel(item, ui, () => { playnite.ApplyChanges(captured.Changes); Refresh(); }, openPage));
            }
            AttentionCount = Advice.AttentionCount(found);
            Raise("AttentionCount");
            Summary = AttentionCount == 0 ? ui.Get("LOCLS_Adv_Summary_None", "Nothing to fix.") : AttentionCount == 1 ? ui.Get("LOCLS_Adv_Summary_One", "1 thing to fix.") : ui.Format("LOCLS_Adv_Summary_Many", "{0} things to fix.", AttentionCount);
        }
    }
}
