using System.ComponentModel;
using LayoutStudio.Core;

namespace LayoutStudio.Engine
{
    /// <summary>
    /// What the theme reads through {PluginSettings Plugin=LayoutStudio, Path=Options[Key]}: the typed current value of a constant.
    /// Unlike a resource, a binding follows changes, so constants that XAML can only reach through a binding (trigger conditions,
    /// StaticResource fallbacks) change live. An unknown key gives null, so the binding's FallbackValue/TargetNullValue apply.
    /// </summary>
    public class OptionsBridge : INotifyPropertyChanged
    {
        private readonly ConstantStore store;

        public OptionsBridge(ConstantStore store)
        {
            this.store = store;
            store.ValueChanged += OnValueChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public object this[string key]
        {
            get
            {
                var def = store.Find(key);
                return def == null ? null : WpfValues.ToWpf(def.Type, store.Get(key));
            }
        }

        private void OnValueChanged(string key)
        {
            var handler = PropertyChanged;
            if (handler == null) return;
            handler(this, new PropertyChangedEventArgs("Item[" + key + "]"));
            handler(this, new PropertyChangedEventArgs("Item[]"));
        }
    }
}
