using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    /// <summary>One information chip of the artwork card: its Show switch and its "new line before" switch(es), side by side.</summary>
    public class ChipRowViewModel
    {
        public string Title { get; set; }
        public SettingViewModel Show { get; set; }
        public IList<SettingViewModel> Breaks { get; set; }
        public bool HasBreaks { get { return Breaks.Count > 0; } }
        public bool SeveralBreaks { get { return Breaks.Count > 1; } }
    }

    /// <summary>
    /// What the Header cards page shows besides the tile cells: every information chip as one table row (show / new line before) and
    /// every other header setting of the generated Game header page (artwork card, look, buttons, header bar, tile look), so the page holds
    /// all the header parameters. The settings are the same constants as on the generated pages: both views stay in step.
    /// </summary>
    public class HeaderExtrasViewModel
    {
        public HeaderExtrasViewModel(Schema schema, ConstantStore store, History history, ProfileApplier applier, Profile profile,
            Func<string, string> localize, CultureInfo culture)
        {
            var headerPage = schema.Pages.FirstOrDefault(p => p.Id == "header");
            var controls = headerPage == null ? new List<Control>() : headerPage.Groups.SelectMany(g => g.Controls).ToList();
            Func<Control, SettingViewModel> make = c => new SettingViewModel(c, store, history, applier, profile, localize, culture);

            var rows = new List<ChipRowViewModel>();
            var chipControls = new HashSet<string>();
            foreach (var item in InfoOnce.All.Where(i => i.ChipKey != null))
            {
                var show = controls.FirstOrDefault(c => c.Keys.Count == 1 && c.Keys[0] == item.ChipKey && c.Kind == "toggle");
                if (show == null) continue;
                var family = ItemFamilies.All.FirstOrDefault(f => f.Id == item.FamilyId);
                var breaks = controls.Where(c => c.Kind == "toggle" && c.Keys.Count == 1 && c.Keys[0].StartsWith("HeroChipBreak", StringComparison.Ordinal)
                    && family != null && ReferenceEquals(ItemFamilies.FamilyOf(c.Keys[0]), family)).ToList();
                var showVm = make(show);
                showVm.Title = Localized(localize, item.Title);
                rows.Add(new ChipRowViewModel { Title = Localized(localize, item.Title), Show = showVm, Breaks = breaks.Select(make).ToList() });
                chipControls.Add(show.Keys[0]);
                foreach (var b in breaks) chipControls.Add(b.Keys[0]);
            }
            Chips = rows;

            Groups = (headerPage == null ? new List<Group>() : headerPage.Groups)
                .Select(g => new GroupViewModel(SchemaCoverage.ShortTitle(g.Title),
                    g.Controls.Where(c => c.Kind != "tileSlots" && !(c.Keys.Count == 1 && chipControls.Contains(c.Keys[0]))).Select(make).ToList()))
                .Where(g => g.Settings.Count > 0).ToList();
        }

        private static string Localized(Func<string, string> localize, string english)
        {
            string text = null;
            try { text = localize(ItemFamilies.LocKey(english)); } catch (Exception) { }
            return string.IsNullOrEmpty(text) ? english : text;
        }

        public IList<ChipRowViewModel> Chips { get; private set; }
        public IList<GroupViewModel> Groups { get; private set; }
    }
}
