using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LayoutStudio.Core;

namespace LayoutStudio.Engine
{
    /// <summary>
    /// Puts the information chips of the artwork header in the order of the profile, on the live theme.
    ///
    /// The theme lists every chip once in a WrapPanel (Tag="ls:chips"); each block of a chip (a line break, then the chip itself) has
    /// Uid="ls:chip:Id". The decision is Core\ChipLayout.Order, read from the HeroChipOrder constants; this class only re-inserts the blocks.
    /// Without the plugin nothing moves: the theme file lists the chips in the default order.
    /// </summary>
    public sealed class ChipMover
    {
        private const string PanelTag = "ls:chips";
        private const string ChipPrefix = "ls:chip:";
        private static bool handlerRegistered;
        private static ChipMover current;

        private readonly Schema schema;
        private readonly ConstantStore store;
        private readonly Action<string> log;
        private readonly ISet<string> watched;
        private readonly List<WeakReference> panels = new List<WeakReference>();
        private bool reconcileQueued, scanQueued;

        public ChipMover(Schema schema, ConstantStore store, Action<string> log)
        {
            this.schema = schema;
            this.store = store;
            this.log = log ?? (s => { });
            watched = ChipLayout.WatchedKeys(schema);
        }

        /// <summary>Starts following the chip line and the profile. Call on the UI thread.</summary>
        public void Start()
        {
            current = this;
            if (!handlerRegistered)
            {
                // the handler is registered once per process; it forwards to the mover of the moment
                EventManager.RegisterClassHandler(typeof(WrapPanel), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, e) => { if (current != null) current.OnLoaded(s as WrapPanel); }));
                handlerRegistered = true;
            }
            store.ValueChanged += key => { if (watched.Contains(key)) ReconcileSoon(); };
            ScanSoon();
        }

        private void OnLoaded(WrapPanel panel)
        {
            if (panel == null || !IsChipLine(panel)) return;
            Remember(panel);
            Arrange(panel);
        }

        private static bool IsChipLine(WrapPanel panel) { return PanelTag.Equals(panel.Tag as string, StringComparison.Ordinal); }

        private void Remember(WrapPanel panel)
        {
            panels.RemoveAll(w => !w.IsAlive);
            if (!panels.Any(w => ReferenceEquals(w.Target, panel))) panels.Add(new WeakReference(panel));
        }

        private void ScanSoon()
        {
            if (scanQueued) return;
            scanQueued = true;
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                scanQueued = false;
                try
                {
                    var window = Application.Current.MainWindow;
                    if (window != null) Scan(window, 0);
                    ReconcileAll();
                }
                catch (Exception ex) { log("chips: scan failed: " + ex.Message); }
            }), DispatcherPriority.ApplicationIdle);
        }

        private void Scan(DependencyObject node, int depth)
        {
            if (depth > 250) return;
            var wrap = node as WrapPanel;
            if (wrap != null && IsChipLine(wrap)) Remember(wrap);
            int count = node is Visual || node is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(node) : 0;
            for (int i = 0; i < count; i++) Scan(VisualTreeHelper.GetChild(node, i), depth + 1);
        }

        private void ReconcileSoon()
        {
            if (reconcileQueued) return;
            reconcileQueued = true;
            Application.Current.Dispatcher.BeginInvoke(new Action(() => { reconcileQueued = false; ReconcileAll(); }), DispatcherPriority.Background);
        }

        /// <summary>Carries out the order of the profile on every chip line that is currently in the visual tree.</summary>
        public void ReconcileAll()
        {
            panels.RemoveAll(w => !w.IsAlive);
            foreach (var w in panels.ToList())
            {
                var panel = w.Target as WrapPanel;
                if (panel != null) Arrange(panel);
            }
        }

        private void Arrange(WrapPanel panel)
        {
            try
            {
                var order = ChipLayout.Order(store, schema);
                var blocks = panel.Children.Cast<UIElement>().ToList();
                var wanted = ChipLayout.Arrange(blocks, IdOf, order);
                if (wanted.SequenceEqual(blocks)) return;
                for (int i = 0; i < wanted.Count; i++)
                {
                    if (ReferenceEquals(panel.Children[i], wanted[i])) continue;
                    panel.Children.Remove(wanted[i]);
                    panel.Children.Insert(i, wanted[i]);
                }
                log("chips: put in order " + string.Join(",", order));
            }
            catch (Exception ex)
            {
                log("chips: could not be ordered: " + ex.Message);
            }
        }

        private static string IdOf(UIElement block)
        {
            var uid = block.Uid;
            return !string.IsNullOrEmpty(uid) && uid.StartsWith(ChipPrefix, StringComparison.Ordinal) ? uid.Substring(ChipPrefix.Length) : null;
        }
    }
}
