using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LayoutStudio.Core;

namespace LayoutStudio.Engine
{
    /// <summary>
    /// Puts the details-view sections in the column and row the profile asks for, on the live theme.
    ///
    /// A section that hosts a plugin control can exist only once in the theme file (the plugin fills the control by name), so it cannot be
    /// duplicated in the other column: the same block instance is re-parented instead, and its plugin control keeps working.
    /// The theme tags what it offers: a section block has Tag="ls:section:Id", a column grid has Tag="ls:column:0|1".
    /// The decision (column and row of every section) is Core\SectionLayout, read from the store; this class only carries it out.
    ///
    /// Pitfalls it handles: a block raises Loaded again after it is moved (its home is the column of its first sighting), and a block that
    /// is collapsed when its view loads never raises Loaded (the visual tree is scanned once when a column loads).
    /// Without the plugin nothing moves: every block stays in the column the theme gives it.
    /// </summary>
    public sealed class SectionMover
    {
        private const string SectionPrefix = "ls:section:";
        private const string ColumnPrefix = "ls:column:";
        private static bool handlerRegistered;
        private static SectionMover current;

        private readonly Schema schema;
        private readonly ConstantStore store;
        private readonly Action<string> log;
        private readonly ISet<string> watched;
        private readonly Dictionary<string, WeakReference> blocks = new Dictionary<string, WeakReference>();
        private readonly Dictionary<int, WeakReference> columns = new Dictionary<int, WeakReference>();
        private readonly Dictionary<string, int> home = new Dictionary<string, int>();
        private readonly HashSet<string> rowOverridden = new HashSet<string>();
        private bool reconcileQueued, scanQueued;

        public SectionMover(Schema schema, ConstantStore store, Action<string> log)
        {
            this.schema = schema;
            this.store = store;
            this.log = log ?? (s => { });
            watched = SectionLayout.WatchedKeys(schema);
        }

        /// <summary>Starts following the theme's elements and the profile. Call on the UI thread.</summary>
        public void Start()
        {
            current = this;
            if (!handlerRegistered)
            {
                // the handler is registered once per process; it forwards to the mover of the moment
                EventManager.RegisterClassHandler(typeof(Grid), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, e) => { if (current != null) current.OnLoaded(s as Grid); }));
                handlerRegistered = true;
            }
            store.ValueChanged += key => { if (watched.Contains(key)) ReconcileSoon(); };
            ScanSoon();
        }

        private void OnLoaded(Grid grid)
        {
            var tag = grid == null ? null : grid.Tag as string;
            if (tag == null) return;
            if (tag.StartsWith(ColumnPrefix, StringComparison.Ordinal))
            {
                int column;
                if (int.TryParse(tag.Substring(ColumnPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out column))
                {
                    columns[column] = new WeakReference(grid);
                    ScanSoon();
                }
            }
            else if (tag.StartsWith(SectionPrefix, StringComparison.Ordinal))
            {
                Register(tag.Substring(SectionPrefix.Length), grid);
                ReconcileSoon();
            }
        }

        private void Register(string id, Grid grid)
        {
            blocks[id] = new WeakReference(grid);
            int at = ColumnOf(grid.Parent as Grid);
            // a block raises Loaded again after it is moved: its home is the column it was first seen in, never the one it was moved to
            if (at >= 0 && !home.ContainsKey(id)) home[id] = at;
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
                catch (Exception ex) { log("sections: scan failed: " + ex.Message); }
            }), DispatcherPriority.ApplicationIdle);
        }

        private void Scan(DependencyObject node, int depth)
        {
            if (depth > 250) return;
            var grid = node as Grid;
            var tag = grid == null ? null : grid.Tag as string;
            if (tag != null)
            {
                if (tag.StartsWith(SectionPrefix, StringComparison.Ordinal))
                {
                    string id = tag.Substring(SectionPrefix.Length);
                    WeakReference known;
                    if (!blocks.TryGetValue(id, out known) || !ReferenceEquals(known.Target, grid)) Register(id, grid);
                }
                else if (tag.StartsWith(ColumnPrefix, StringComparison.Ordinal))
                {
                    int column;
                    if (int.TryParse(tag.Substring(ColumnPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out column)) columns[column] = new WeakReference(grid);
                }
            }
            int count = node is Visual || node is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(node) : 0;
            for (int i = 0; i < count; i++) Scan(VisualTreeHelper.GetChild(node, i), depth + 1);
        }

        private static int ColumnOf(Grid column)
        {
            var tag = column == null ? null : column.Tag as string;
            int n;
            return tag != null && tag.StartsWith(ColumnPrefix, StringComparison.Ordinal)
                && int.TryParse(tag.Substring(ColumnPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : -1;
        }

        private void ReconcileSoon()
        {
            if (reconcileQueued) return;
            reconcileQueued = true;
            Application.Current.Dispatcher.BeginInvoke(new Action(() => { reconcileQueued = false; ReconcileAll(); }), DispatcherPriority.Background);
        }

        /// <summary>Carries out the placement the profile asks for on every block that is currently in the visual tree.</summary>
        public void ReconcileAll()
        {
            IList<SectionPlacement> placements;
            try { placements = SectionLayout.Compute(store, schema); }
            catch (Exception ex) { log("sections: placement could not be computed: " + ex.Message); return; }
            foreach (var p in placements)
            {
                WeakReference w;
                var block = blocks.TryGetValue(p.Id, out w) ? w.Target as Grid : null;
                if (block != null) Place(p, block);
            }
        }

        private void Place(SectionPlacement p, Grid block)
        {
            int homeColumn;
            if (!home.TryGetValue(p.Id, out homeColumn)) return;
            WeakReference cw;
            var target = columns.TryGetValue(p.Column, out cw) ? cw.Target as Grid : null;
            if (target == null) return;
            try
            {
                if (!ReferenceEquals(block.Parent, target))
                {
                    var from = block.Parent as Panel;
                    if (from != null) from.Children.Remove(block);
                    target.Children.Add(block);
                    log("section " + p.Id + " moved to column " + p.Column);
                }
                if (p.Column == homeColumn)
                {
                    // back where the theme puts it: its own order constant gives the row again
                    if (rowOverridden.Remove(p.Id)) block.ClearValue(Grid.RowProperty);
                }
                else
                {
                    Grid.SetRow(block, Math.Min(p.Row, Math.Max(0, target.RowDefinitions.Count - 1)));
                    rowOverridden.Add(p.Id);
                }
            }
            catch (Exception ex)
            {
                log("section " + p.Id + " could not move: " + ex.Message);
            }
        }
    }
}
