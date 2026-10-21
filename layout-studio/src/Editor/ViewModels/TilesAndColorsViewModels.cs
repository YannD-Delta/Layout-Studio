using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    /// <summary>A tile type of the palette (Time played, Last played, ...).</summary>
    public class TileViewModel : IDragSource
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string Preview { get; set; }
        public string PreviewPath { get { return PreviewImages.PathOf(Preview); } }
        public bool HasPreview { get { return PreviewPath != null; } }
        public string DragFormat { get { return DragFormats.Tile; } }
        public string DragPayload { get { return Id; } }
    }

    /// <summary>One list of a cell: the card shown at this position. The next list appears once this one holds a card.</summary>
    public class CardPickViewModel : ViewModelBase
    {
        private readonly TilesEditorViewModel owner;
        private readonly int slot;

        public CardPickViewModel(TilesEditorViewModel owner, int slot, int position)
        {
            this.owner = owner; this.slot = slot; Position = position;
        }

        public int Position { get; private set; }
        public IList<TileViewModel> Palette { get { return owner.Palette; } }

        public TileViewModel Selected
        {
            get { return owner.TileForValue(owner.ValueOf(owner.CardKey(slot, Position))); }
            set { if (value != null) owner.AssignCard(slot, Position, value.Id); }
        }

        /// <summary>The first list is always there; each next one shows once the one before it holds a card.</summary>
        public bool IsVisible { get { return Position == 0 || owner.ValueOf(owner.CardKey(slot, Position - 1)) != "0"; } }

        public void Refresh() { RaiseAll(); }
    }

    /// <summary>One of the header cells: up to four cards side by side. Accepts a palette tile or another cell.</summary>
    public class TileSlotViewModel : ViewModelBase, IDragSource
    {
        private readonly TilesEditorViewModel owner;
        private readonly string key;

        public TileSlotViewModel(TilesEditorViewModel owner, int index, string key)
        {
            this.owner = owner;
            Index = index;
            this.key = key;
            Cards = Enumerable.Range(0, TilesEditorViewModel.CardsPerCell).Select(p => new CardPickViewModel(owner, index, p)).ToList();
        }

        public IList<CardPickViewModel> Cards { get; private set; }

        public int Index { get; private set; }
        public int Number { get { return Index + 1; } }
        public string DragFormat { get { return DragFormats.Slot; } }
        public string DragPayload { get { return Index.ToString(CultureInfo.InvariantCulture); } }
        public TileViewModel Tile { get { return owner.TileForValue(owner.ValueOf(key)); } }
        public void Refresh() { RaiseAll(); foreach (var c in Cards) c.Refresh(); }

        /// <summary>A drag ended on this slot: a palette tile assigns it, another slot swaps with it, anything else is ignored.</summary>
        public void Drop(string format, string payload)
        {
            if (format == DragFormats.Tile) owner.Assign(Index, payload);
            else if (format == DragFormats.Slot)
            {
                int other;
                if (int.TryParse(payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out other)) owner.Swap(other, Index);
            }
        }
    }

    /// <summary>Header tiles: a palette of tile types and the slots they are dragged into.</summary>
    public class TilesEditorViewModel : ViewModelBase
    {
        private readonly ConstantStore store;
        private readonly History history;
        private readonly Control control;

        public const int CardsPerCell = 4;

        public TilesEditorViewModel(ConstantStore store, Schema schema, History history)
        {
            this.store = store;
            this.history = history;
            control = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).First(c => c.Kind == "tileSlots");
            Palette = control.Choices.Select(c => new TileViewModel { Id = c.Id, Name = c.Name ?? c.Id, Value = c.Value, Preview = c.Preview }).ToList();
            Slots = control.Keys.Select((k, i) => new TileSlotViewModel(this, i, k)).ToList();
            var keys = new HashSet<string>(control.Keys.Concat(control.ExtraKeys));
            store.ValueChanged += key => { if (keys.Contains(key)) foreach (var s in Slots) s.Refresh(); };
        }

        public IList<TileViewModel> Palette { get; private set; }
        public IList<TileSlotViewModel> Slots { get; private set; }

        public string ValueOf(string key) { return store.Get(key); }

        /// <summary>Constant of one card of a cell: position 0 is the slot's own key, 1 to 3 are its extra keys.</summary>
        public string CardKey(int slot, int position)
        {
            return position == 0 ? control.Keys[slot] : control.ExtraKeys[slot * (CardsPerCell - 1) + position - 1];
        }

        /// <summary>Puts a card at one position of a cell. Choosing "Empty" also empties the positions after it, so a cell has no hole.</summary>
        public void AssignCard(int slot, int position, string tileId)
        {
            var tile = Palette.FirstOrDefault(t => t.Id == tileId);
            if (tile == null || slot < 0 || slot >= Slots.Count || position < 0 || position >= CardsPerCell) return;
            string error;
            history.BeginBatch();
            try
            {
                store.TrySet(CardKey(slot, position), tile.Value, out error);
                if (tile.Value == "0")
                    for (int p = position + 1; p < CardsPerCell; p++) store.TrySet(CardKey(slot, p), "0", out error);
            }
            finally { history.EndBatch(); }
        }

        public TileViewModel TileForValue(string value) { return Palette.FirstOrDefault(t => t.Value == value); }

        public void Assign(int slotIndex, string tileId)
        {
            var tile = Palette.FirstOrDefault(t => t.Id == tileId);
            if (tile == null || slotIndex < 0 || slotIndex >= Slots.Count) return;
            string error;
            history.BeginBatch();
            try { store.TrySet(control.Keys[slotIndex], tile.Value, out error); }
            finally { history.EndBatch(); }
        }

        public void Clear(int slotIndex)
        {
            var empty = Palette.FirstOrDefault(t => t.Id == "Empty");
            if (empty != null) Assign(slotIndex, empty.Id);
        }

        public void Swap(int a, int b)
        {
            if (a == b || a < 0 || b < 0 || a >= Slots.Count || b >= Slots.Count) return;
            string error;
            history.BeginBatch();
            try
            {
                for (int p = 0; p < CardsPerCell; p++)
                {
                    string ka = CardKey(a, p), kb = CardKey(b, p), va = store.Get(ka), vb = store.Get(kb);
                    store.TrySet(ka, vb, out error);
                    store.TrySet(kb, va, out error);
                }
            }
            finally { history.EndBatch(); }
        }
    }

    /// <summary>A color profile or accent shown as a small swatch.</summary>
    public class SwatchViewModel : ViewModelBase
    {
        private bool selected;

        public string Id { get; set; }
        public string Name { get; set; }
        public string Background1 { get; set; }
        public string Background2 { get; set; }
        public string Text { get; set; }
        public string Accent { get; set; }
        public RelayCommand SelectCommand { get; set; }

        public bool IsSelected
        {
            get { return selected; }
            set { if (selected != value) { selected = value; Raise("IsSelected"); } }
        }
    }

    /// <summary>Color profile and accent as swatches to click, plus a custom accent color.</summary>
    public class ColorsEditorViewModel : ViewModelBase
    {
        private readonly ConstantStore store;
        private readonly History history;
        private readonly ProfileApplier applier;
        private readonly Profile profile;
        private string error;

        public ColorsEditorViewModel(ConstantStore store, Schema schema, History history, ProfileApplier applier, Profile profile)
        {
            this.store = store;
            this.history = history;
            this.applier = applier;
            this.profile = profile;
            var controls = schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Where(c => c.Kind == "preset").ToList();
            Profiles = Swatches(controls.First(c => c.Id == "ColorProfile"));
            Accents = Swatches(controls.First(c => c.Id == "Accent"));
            foreach (var sw in Profiles) { var id = sw.Id; sw.SelectCommand = new RelayCommand(() => SelectProfile(id)); }
            foreach (var sw in Accents) { var id = sw.Id; sw.SelectCommand = new RelayCommand(() => SelectAccent(id)); }
            RefreshSelection();
            store.ValueChanged += key => { if (key == "GlyphColor") { Raise("CustomAccent"); Raise("AccentR"); Raise("AccentG"); Raise("AccentB"); } };
            history.Changed += () => { RefreshSelection(); Raise("CustomAccent"); Raise("AccentR"); Raise("AccentG"); Raise("AccentB"); };   // an undo or redo can change the selected profile/accent
        }

        public IList<SwatchViewModel> Profiles { get; private set; }
        public IList<SwatchViewModel> Accents { get; private set; }

        public string Error
        {
            get { return error; }
            private set { if (error != value) { error = value; Raise("Error"); } }
        }

        public void SelectProfile(string id) { Select("ColorProfile", id); }

        public void SelectAccent(string id) { Select("Accent", id); }

        /// <summary>The accent color as #AARRGGBB; typing a hex value sets the accent (and its highlight) directly.</summary>
        public string CustomAccent
        {
            get { return store.Get("GlyphColor"); }
            set
            {
                string message;
                history.BeginBatch();
                try
                {
                    if (!store.TrySet("GlyphColor", value, out message)) { Error = message; return; }
                    string rgb = store.Get("GlyphColor").Substring(3);
                    store.TrySet("GlyphSubColor", "#FF" + rgb, out message);
                    store.TrySet("HighlightGlyphColor", "#88" + rgb, out message);
                    Error = null;
                }
                finally { history.EndBatch(); Raise("CustomAccent"); }
            }
        }

        private bool pickerOpen;
        public bool PickerOpen { get { return pickerOpen; } set { if (pickerOpen != value) { pickerOpen = value; Raise("PickerOpen"); } } }

        private int Channel(int index)
        {
            try { string t = CustomAccent; if (t != null && t.Length == 9) return Convert.ToInt32(t.Substring(1 + index * 2, 2), 16); } catch (FormatException) { }
            return 0;
        }

        private void SetChannel(int index, double value)
        {
            var c = new[] { Channel(0), Channel(1), Channel(2), Channel(3) };
            c[index] = (int)Math.Max(0, Math.Min(255, Math.Round(value)));
            CustomAccent = "#" + c[0].ToString("X2") + c[1].ToString("X2") + c[2].ToString("X2") + c[3].ToString("X2");
            Raise("AccentR"); Raise("AccentG"); Raise("AccentB");
        }

        /// <summary>Red, green and blue of the custom accent for the picker sliders (0 to 255).</summary>
        public double AccentR { get { return Channel(1); } set { SetChannel(1, value); } }
        public double AccentG { get { return Channel(2); } set { SetChannel(2, value); } }
        public double AccentB { get { return Channel(3); } set { SetChannel(3, value); } }

        /// <summary>A quick swatch ("#RRGGBB").</summary>
        public RelayCommand SetAccentCommand
        {
            get { return new RelayCommand(p => { string t = p as string; if (!string.IsNullOrEmpty(t) && t.Length == 7 && t[0] == '#') { CustomAccent = "#FF" + t.Substring(1).ToUpperInvariant(); Raise("AccentR"); Raise("AccentG"); Raise("AccentB"); } }); }
        }

        private void Select(string group, string id)
        {
            PresetHistory.Apply(history, applier, profile, group, id);
            RefreshSelection();
            Raise("CustomAccent");
        }

        private void RefreshSelection()
        {
            Mark(Profiles, "ColorProfile");
            Mark(Accents, "Accent");
        }

        private void Mark(IList<SwatchViewModel> swatches, string group)
        {
            string chosen;
            if (!profile.Presets.TryGetValue(group, out chosen)) chosen = "Default";
            foreach (var s in swatches) s.IsSelected = s.Id == chosen;
        }

        private static IList<SwatchViewModel> Swatches(Control control)
        {
            Func<Choice, string, string> pick = (c, k) => { string v; return c.Swatch.TryGetValue(k, out v) ? v : null; };
            return control.Choices.Select(c => new SwatchViewModel
            {
                Id = c.Id, Name = c.Name ?? c.Id, Background1 = pick(c, "bg1"), Background2 = pick(c, "bg2"), Text = pick(c, "text"), Accent = pick(c, "accent")
            }).ToList();
        }
    }
}
