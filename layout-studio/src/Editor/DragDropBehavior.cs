using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LayoutStudio.Editor
{
    /// <summary>
    /// Attached properties for the sections wireframe: a block is a drag source (DragKind="section") and each column is a drop
    /// target (DropColumn="0|1"). Pure WPF drag and drop; the view model decides what a drop means.
    /// </summary>
    public static class DragDropBehavior
    {
        public const string Format = DragFormats.Section;
        private static Point start;
        private static FrameworkElement pressed;

        public static readonly DependencyProperty DragKindProperty =
            DependencyProperty.RegisterAttached("DragKind", typeof(string), typeof(DragDropBehavior), new PropertyMetadata(null, OnDragKindChanged));

        public static readonly DependencyProperty DropColumnProperty =
            DependencyProperty.RegisterAttached("DropColumn", typeof(int), typeof(DragDropBehavior), new PropertyMetadata(-1, OnDropColumnChanged));

        public static readonly DependencyProperty DropSlotProperty =
            DependencyProperty.RegisterAttached("DropSlot", typeof(bool), typeof(DragDropBehavior), new PropertyMetadata(false, OnDropSlotChanged));

        public static bool GetDropSlot(DependencyObject d) { return (bool)d.GetValue(DropSlotProperty); }
        public static void SetDropSlot(DependencyObject d, bool value) { d.SetValue(DropSlotProperty, value); }

        public static string GetDragKind(DependencyObject d) { return (string)d.GetValue(DragKindProperty); }
        public static void SetDragKind(DependencyObject d, string value) { d.SetValue(DragKindProperty, value); }
        public static int GetDropColumn(DependencyObject d) { return (int)d.GetValue(DropColumnProperty); }
        public static void SetDropColumn(DependencyObject d, int value) { d.SetValue(DropColumnProperty, value); }

        private static void OnDragKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as FrameworkElement;
            if (element == null || e.NewValue == null) return;
            element.PreviewMouseLeftButtonDown += (s, a) => { start = a.GetPosition(null); pressed = element; };
            element.PreviewMouseLeftButtonUp += (s, a) => pressed = null;
            element.PreviewMouseMove += (s, a) =>
            {
                // a fast move can cross other drag sources before the threshold: only the element that was pressed starts the drag
                if (a.LeftButton != MouseButtonState.Pressed || pressed != element) return;
                var now = a.GetPosition(null);
                if (System.Math.Abs(now.X - start.X) < SystemParameters.MinimumHorizontalDragDistance
                    && System.Math.Abs(now.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                var source = element.DataContext as IDragSource;
                if (source == null) return;
                pressed = null;
                DragDrop.DoDragDrop(element, new DataObject(source.DragFormat, source.DragPayload), DragDropEffects.Move);
            };
        }

        private static void OnDropSlotChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as FrameworkElement;
            if (element == null || !(bool)e.NewValue) return;
            element.AllowDrop = true;
            element.DragOver += (s, a) =>
            {
                a.Effects = a.Data.GetDataPresent(DragFormats.Tile) || a.Data.GetDataPresent(DragFormats.Slot) ? DragDropEffects.Move : DragDropEffects.None;
                a.Handled = true;
            };
            element.Drop += (s, a) =>
            {
                var slot = element.DataContext as TileSlotViewModel;
                if (slot == null) return;
                foreach (var format in new[] { DragFormats.Tile, DragFormats.Slot })
                {
                    if (!a.Data.GetDataPresent(format)) continue;
                    slot.Drop(format, a.Data.GetData(format) as string);
                    break;
                }
                a.Handled = true;
            };
        }

        private static void OnDropColumnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var items = d as ItemsControl;
            if (items == null || (int)e.NewValue < 0) return;
            items.AllowDrop = true;
            items.DragOver += (s, a) =>
            {
                a.Effects = a.Data.GetDataPresent(Format) ? DragDropEffects.Move : DragDropEffects.None;
                a.Handled = true;
            };
            items.Drop += (s, a) =>
            {
                var editor = items.DataContext as SectionsEditorViewModel;
                var id = a.Data.GetData(Format) as string;
                if (editor == null || id == null) return;
                editor.Drop(id, GetDropColumn(items), IndexAt(items, a.GetPosition(items).Y));
                a.Handled = true;
            };
        }

        /// <summary>Insertion index for a pointer height inside the items control, from the real positions of its blocks.</summary>
        private static int IndexAt(ItemsControl items, double y)
        {
            var heights = new List<double>();
            double firstTop = 0;
            for (int i = 0; i < items.Items.Count; i++)
            {
                var container = items.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                if (container == null) continue;
                if (heights.Count == 0) firstTop = container.TranslatePoint(new Point(0, 0), items).Y;
                heights.Add(container.ActualHeight);
            }
            return SectionsEditorViewModel.InsertionIndex(heights, y - firstTop);
        }
    }
}
