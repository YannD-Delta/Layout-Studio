using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    /// <summary>What the editor needs from the plugin to list and apply the global presets (the whole look at once).</summary>
    public interface IGlobalPresetSwitching
    {
        IList<GlobalPreset> GlobalPresetList { get; }
        string DefaultGlobalPresetId { get; }
        /// <summary>The preset the profile was started from, or null.</summary>
        string ActiveGlobalPresetId { get; }
        /// <summary>True when the profile holds exactly that preset (nothing edited since).</summary>
        bool IsExactly(GlobalPreset preset);
        /// <summary>Saves the profile (kept as a backup), then replaces everything by the preset. Returns an error message, or null.</summary>
        string ApplyGlobalPreset(string id);
    }

    /// <summary>One card of the chooser: the preview, the name and the one-line intent of a global preset.</summary>
    public class GlobalPresetCardViewModel : ViewModelBase
    {
        private bool isCurrent, isExact, isPending;

        public string Id { get; set; }
        public string Name { get; set; }
        public string Intent { get; set; }
        public string PreviewPath { get; set; }
        public bool IsDefault { get; set; }
        public RelayCommand ChooseCommand { get; set; }

        /// <summary>The profile was started from this preset.</summary>
        public bool IsCurrent { get { return isCurrent; } set { if (isCurrent != value) { isCurrent = value; Raise("IsCurrent"); Raise("IsCustomised"); } } }

        /// <summary>The profile still holds exactly this preset.</summary>
        public bool IsExact { get { return isExact; } set { if (isExact != value) { isExact = value; Raise("IsExact"); Raise("IsCustomised"); } } }

        /// <summary>Started from this preset and edited since.</summary>
        public bool IsCustomised { get { return isCurrent && !isExact; } }

        /// <summary>First click done: the next click applies it.</summary>
        public bool IsPending { get { return isPending; } set { if (isPending != value) { isPending = value; Raise("IsPending"); } } }
    }

    /// <summary>The "Global preset" page: the cards of the presets, one click to ask and a second to apply (the whole profile changes).</summary>
    public class GlobalPresetsViewModel : ViewModelBase
    {
        private readonly IGlobalPresetSwitching service;
        private readonly string language;
        private string message;
        private bool isError;

        public GlobalPresetsViewModel(IGlobalPresetSwitching service, string language, ConstantStore store)
        {
            this.service = service;
            this.language = language;
            Cards = new ObservableCollection<GlobalPresetCardViewModel>();
            foreach (var preset in service.GlobalPresetList)
            {
                var captured = preset;
                var card = new GlobalPresetCardViewModel
                {
                    Id = preset.Id, Name = preset.NameFor(language), Intent = preset.IntentFor(language),
                    PreviewPath = PreviewImages.PathOf(preset.Preview), IsDefault = string.Equals(preset.Id, service.DefaultGlobalPresetId, StringComparison.OrdinalIgnoreCase)
                };
                card.ChooseCommand = new RelayCommand(() => Choose(card, captured), () => !card.IsExact);
                Cards.Add(card);
            }
            Refresh();
            if (store != null) store.ValueChanged += key => Refresh();
        }

        public ObservableCollection<GlobalPresetCardViewModel> Cards { get; private set; }

        public bool HasPresets { get { return Cards.Count > 0; } }

        public string Message { get { return message; } private set { message = value; Raise("Message"); Raise("HasMessage"); } }
        public bool HasMessage { get { return !string.IsNullOrEmpty(message); } }
        public bool IsError { get { return isError; } private set { isError = value; Raise("IsError"); } }

        /// <summary>Re-reads which card is current and which one still holds exactly (after any edit, preset or reset).</summary>
        public void Refresh()
        {
            string active = service.ActiveGlobalPresetId;
            foreach (var card in Cards)
            {
                var preset = service.GlobalPresetList.First(p => p.Id == card.Id);
                card.IsCurrent = string.Equals(card.Id, active, StringComparison.OrdinalIgnoreCase);
                card.IsExact = card.IsCurrent && service.IsExactly(preset);
                card.ChooseCommand.RaiseCanExecuteChanged();
            }
        }

        private void Choose(GlobalPresetCardViewModel card, GlobalPreset preset)
        {
            if (!card.IsPending)
            {
                foreach (var other in Cards) other.IsPending = false;
                card.IsPending = true;
                return;
            }
            card.IsPending = false;
            string error;
            try { error = service.ApplyGlobalPreset(preset.Id); }
            catch (Exception ex) { error = ex.Message; }
            IsError = error != null;
            Message = error;
            Refresh();
        }
    }
}
