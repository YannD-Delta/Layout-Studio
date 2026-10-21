using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace LayoutStudio.Editor
{
    /// <summary>
    /// A number typed in a box next to a slider counts when Enter is pressed (the boxes are bound on lost focus, so Enter alone left the old value in place).
    /// One class handler for every text box of the editor window; text boxes of other windows and multi-line boxes are not touched.
    /// </summary>
    public static class TextCommit
    {
        private static bool installed;

        public static void Install()
        {
            if (installed) return;
            installed = true;
            EventManager.RegisterClassHandler(typeof(TextBox), UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnKeyDown));
        }

        private static void OnKeyDown(object sender, KeyEventArgs e)
        {
            var box = sender as TextBox;
            if (box == null || (e.Key != Key.Enter && e.Key != Key.Return) || box.AcceptsReturn) return;
            var window = Window.GetWindow(box);
            if (window == null || !IsEditor(window)) return;
            var binding = BindingOperations.GetBindingExpression(box, TextBox.TextProperty);
            if (binding == null || binding.ParentBinding.UpdateSourceTrigger != UpdateSourceTrigger.LostFocus) return;
            binding.UpdateSource();
            box.SelectAll();
            e.Handled = true;
        }

        private static bool IsEditor(Window window)
        {
            return window.Title != null && window.Title.IndexOf("Layout Studio", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
