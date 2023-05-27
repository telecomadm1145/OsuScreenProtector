using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows;
using System.ComponentModel;
using System.Windows.Media;

namespace osp
{
    public static class ThemeExtension
    {
        private static List<Window> ListenedWindows = new List<Window>();
        private static Point sworig = default;
        private static Vector swacc = default;
        private static ScrollViewer activedscrollviewer = null;
        private static void OnSwMouseDown(object d, MouseButtonEventArgs e)
        {
            if (activedscrollviewer != null)
                return;
            ScrollViewer d1 = (ScrollViewer)d;
            Extensions.RunLater(() =>
            {
                if (d1.IsFocused)
                {
                    activedscrollviewer = d1;
                    sworig = e.GetPosition(activedscrollviewer);
                    swacc = default;
                }
            });
        }
        private static void OnSwMouseUp(object d, MouseEventArgs e)
        {
            if (activedscrollviewer != null)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                DispatcherTimer dt = new DispatcherTimer();
                var rsw = activedscrollviewer;
                dt.Interval = TimeSpan.FromMilliseconds(1);
                var lastms = 0L;
                dt.Tick += (_, __) =>
                {
                    if (swacc.Length < 0.01)
                    {
                        sw.Stop();
                        dt.Stop();
                    }
                    var els = sw.ElapsedMilliseconds;
                    swacc /= 1 + (double)(els - lastms) / 100;
                    lastms = els;
                    rsw.ScrollToVerticalOffset(rsw.ContentVerticalOffset - swacc.Y);
                    rsw.ScrollToHorizontalOffset(rsw.ContentHorizontalOffset - swacc.X);
                };
                dt.Start();
                //FocusManager.SetFocusedElement(FocusManager.GetFocusScope(activedscrollviewer), null);
                //Keyboard.ClearFocus();
                activedscrollviewer = null;
            }
        }
        private static void OnSwMouseMove(object d, MouseEventArgs e)
        {
            if (activedscrollviewer != null)
            {
                if (!activedscrollviewer.IsFocused || (e.LeftButton != MouseButtonState.Pressed && e.RightButton != MouseButtonState.Pressed))
                {
                    OnSwMouseUp(d, e);
                    return;
                }
                Vector offset = (e.GetPosition(activedscrollviewer) - sworig);
                swacc = offset;
                activedscrollviewer.ScrollToVerticalOffset(activedscrollviewer.ContentVerticalOffset - (Math.Pow(Math.Abs(offset.Y), 1.12) * (offset.Y > 0 ? 1 : -1)));
                activedscrollviewer.ScrollToHorizontalOffset(activedscrollviewer.ContentHorizontalOffset - (Math.Pow(Math.Abs(offset.X), 1.12) * (offset.X > 0 ? 1 : -1)));
                sworig = e.GetPosition(activedscrollviewer);
            }
        }
        private static void OnSwMouseWheel(object d, MouseWheelEventArgs e)
        {
            e.Handled = true; // wpf没有控件会处理这个吧(
            ((ScrollViewer)d).ScrollToVerticalOffset(((ScrollViewer)d).ContentVerticalOffset + e.Delta);
        }
        // Using a DependencyProperty as the backing store for CanMouseScroll.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CanMouseScrollProperty =
            DependencyProperty.RegisterAttached("CanMouseScroll", typeof(bool), typeof(ThemeExtension), new PropertyMetadata(false, (s, e) =>
            {
                if (e.NewValue != e.OldValue)
                {
                    var bl = (bool)e.NewValue;
                    var sw = (ScrollViewer)s;
                    if (bl)
                    {
                        sw.PreviewMouseDown += OnSwMouseDown;
                        sw.MouseWheel += OnSwMouseWheel;
                        if (!DesignerProperties.GetIsInDesignMode(sw)) // make designer happy :)
                        {
                            void once(object __, EventArgs ___)
                            {
                                var toplevel = Window.GetWindow(sw);
                                if (toplevel == null) return;
                                if (!ListenedWindows.Contains(toplevel))
                                {
                                    toplevel.MouseMove += OnSwMouseMove;
                                    toplevel.MouseUp += OnSwMouseUp;
                                }
                                ListenedWindows.Add(toplevel);
                                sw.Loaded -= once;
                            };
                            sw.Loaded += once;
                        }
                    }
                    else
                    {
                        sw.MouseDown -= OnSwMouseDown;
                        sw.MouseWheel -= OnSwMouseWheel;
                        if (!DesignerProperties.GetIsInDesignMode(sw)) // make designer happy :)
                        {
                            var toplevel = Window.GetWindow(sw);
                            if (toplevel == null) return;
                            ListenedWindows.Remove(toplevel);
                            if (!ListenedWindows.Contains(toplevel))
                            {
                                toplevel.MouseMove -= OnSwMouseMove;
                                toplevel.MouseUp -= OnSwMouseUp;
                            }
                        }
                    }
                }
            }));

        public static bool GetCanMouseScroll(DependencyObject obj)
        {
            return (bool)obj.GetValue(CanMouseScrollProperty);
        }

        public static void SetCanMouseScroll(DependencyObject obj, bool value)
        {
            obj.SetValue(CanMouseScrollProperty, value);
        }




    }
}
