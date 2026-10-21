using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace LayoutStudio.Core
{
    /// <summary>
    /// The state of the header card's completion status picker, as the theme binds it (Completion.Choices, Completion.StatusId two ways, Completion.HasChoices).
    /// Choosing a status raises <see cref="Picked"/>; following the selected game (<see cref="Show"/>) never does. While the list is refilled the bound
    /// ComboBox reports "nothing selected": that must not be taken for a choice (it would erase the game's status).
    /// </summary>
    public class CompletionPicker : INotifyPropertyChanged
    {
        private string statusId = "";
        private bool following;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Raised with the chosen status id ("" = none) when the picker chooses.</summary>
        public Action<string> Picked;

        public ObservableCollection<CompletionChoice> Choices { get; } = new ObservableCollection<CompletionChoice>();

        public bool HasChoices { get { return Choices.Count > 0; } }

        public string StatusId
        {
            get { return statusId; }
            set
            {
                if (following) return;
                value = value ?? "";
                if (statusId == value) return;
                statusId = value;
                Raise("StatusId");
                var picked = Picked;
                if (picked != null) picked(value);
            }
        }

        public void Show(IList<CompletionChoice> choices, string id)
        {
            following = true;
            try
            {
                Choices.Clear();
                foreach (var c in choices) Choices.Add(c);
                statusId = id ?? "";
            }
            finally { following = false; }
            Raise("HasChoices");
            Raise("StatusId");
        }

        private void Raise(string name)
        {
            var h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(name));
        }
    }
}
