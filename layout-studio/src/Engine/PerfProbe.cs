using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace LayoutStudio.Engine
{
    /// <summary>
    /// Testbed measurement, never active on a real install: when a marker file (layout-studio-perf.flag) sits in the Playnite configuration
    /// folder, a few seconds after start-up the game list is scrolled and the selection is moved by code, and the time the UI thread needs for each
    /// step (template instantiation, bindings, layout, Loaded handlers) goes to the log. No mouse, no screen needed, so the numbers repeat well.
    /// Driven by tools\perf-probe.ps1.
    /// </summary>
    public static class PerfProbe
    {
        public const string FlagName = "layout-studio-perf.flag";

        /// <summary>Time spent in the plugin's own OnGameSelected handler (Plugin.cs), so a probe run can tell how much of a game switch is ours.</summary>
        public static long HandlerTicks, HandlerCalls;

        public static void Arm(string flagPath, Action<string> log, int delaySeconds = 25)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(delaySeconds) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                try { System.IO.File.Delete(flagPath); } catch (Exception) { }
                try { Run(log); }
                catch (Exception ex) { log("PERFPROBE failed: " + ex); }
            };
            timer.Start();
        }

        private static void Run(Action<string> log)
        {
            var window = Application.Current.MainWindow;
            var list = Find<ListBox>(window, "PART_ListGames");
            if (list == null) { log("PERFPROBE no game list found"); return; }
            var scroll = Descendants<ScrollViewer>(list).FirstOrDefault();
            if (scroll == null) { log("PERFPROBE no scroll viewer in the list"); return; }
            var ui = Application.Current.Dispatcher;
            Action settle = () => { ui.Invoke(new Action(() => { }), DispatcherPriority.Loaded); ui.Invoke(new Action(() => { }), DispatcherPriority.Render); };
            log("PERFPROBE start: " + list.Items.Count + " games, viewport " + scroll.ViewportHeight + ", extent " + scroll.ExtentHeight + ", logical scrolling " + scroll.CanContentScroll);

            // 1. scrolling the list: steps of 3 rows, the way a wheel notch does
            scroll.ScrollToVerticalOffset(0); settle();
            double step = scroll.CanContentScroll ? 3 : 3 * 40;
            var times = new List<double>();
            for (int i = 0; i < 260; i++)
            {
                var sw = Stopwatch.StartNew();
                scroll.ScrollToVerticalOffset(20 + i * step);
                list.UpdateLayout();
                settle();
                sw.Stop();
                if (i >= 20) times.Add(sw.Elapsed.TotalMilliseconds);
            }
            Report(log, "list scroll step (3 rows)", times);

            // 2. whole page of fresh rows at once (a page-down)
            times = new List<double>();
            double page = Math.Max(1, scroll.ViewportHeight) * (scroll.CanContentScroll ? 1 : 1);
            for (int i = 0; i < 40; i++)
            {
                var sw = Stopwatch.StartNew();
                scroll.ScrollToVerticalOffset(2000 + i * page);
                list.UpdateLayout();
                settle();
                sw.Stop();
                times.Add(sw.Elapsed.TotalMilliseconds);
            }
            Report(log, "list page jump (one viewport)", times);

            // 3. moving the selection: hero artwork, tiles, sections. The phases are timed apart: the selection itself (Playnite's own work),
            // the bindings (DataBind), the layout pass, then Loaded handlers and the render pass.
            scroll.ScrollToVerticalOffset(0); settle();
            long handlerTicks0 = HandlerTicks, handlerCalls0 = HandlerCalls;
            var total = new List<double>(); var tSelect = new List<double>(); var tBind = new List<double>(); var tLayout = new List<double>(); var tRender = new List<double>();
            int count = Math.Min(list.Items.Count, 80);
            for (int i = 0; i < count; i++)
            {
                var sw = Stopwatch.StartNew();
                list.SelectedIndex = i;
                double a = sw.Elapsed.TotalMilliseconds;
                ui.Invoke(new Action(() => { }), DispatcherPriority.DataBind);
                double b = sw.Elapsed.TotalMilliseconds;
                window.UpdateLayout();
                double c = sw.Elapsed.TotalMilliseconds;
                settle();
                double d = sw.Elapsed.TotalMilliseconds;
                if (i >= 5) { total.Add(d); tSelect.Add(a); tBind.Add(b - a); tLayout.Add(c - b); tRender.Add(d - c); }
            }
            Report(log, "game switch (selection by code)", total);
            Report(log, "game switch: selection itself", tSelect);
            Report(log, "game switch: bindings (DataBind)", tBind);
            Report(log, "game switch: layout pass", tLayout);
            Report(log, "game switch: loaded + render", tRender);
            long calls = HandlerCalls - handlerCalls0;
            if (calls > 0) log(string.Format(CultureInfo.InvariantCulture, "PERFPROBE game switch: Layout Studio OnGameSelected handler: calls={0} mean={1:N2} ms", calls, (HandlerTicks - handlerTicks0) * 1000.0 / Stopwatch.Frequency / calls));
            log("PERFPROBE done");
        }

        private static void Report(Action<string> log, string what, List<double> ms)
        {
            if (ms.Count == 0) { log("PERFPROBE " + what + ": no sample"); return; }
            var sorted = ms.OrderBy(x => x).ToList();
            Func<double, double> pct = p => sorted[Math.Min(sorted.Count - 1, (int)(p * sorted.Count))];
            log(string.Format(CultureInfo.InvariantCulture, "PERFPROBE {0}: n={1} mean={2:N2} ms p50={3:N2} p95={4:N2} max={5:N2} total={6:N0} ms",
                what, ms.Count, ms.Average(), pct(0.5), pct(0.95), sorted[sorted.Count - 1], ms.Sum()));
        }

        private static T Find<T>(DependencyObject root, string name) where T : FrameworkElement
        {
            return Descendants<T>(root).FirstOrDefault(e => e.Name == name);
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            var stack = new Stack<DependencyObject>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (node is T && !ReferenceEquals(node, root)) yield return (T)node;
                int n = node is Visual || node is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(node) : 0;
                for (int i = n - 1; i >= 0; i--) stack.Push(VisualTreeHelper.GetChild(node, i));
            }
        }
    }
}
