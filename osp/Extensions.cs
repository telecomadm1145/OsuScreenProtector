using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace osp
{
    internal static class Extensions
    {

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        public static bool IsPressed(this Key key)
        {
            return (GetAsyncKeyState((int)key) & 0xff00) != 0;
        }
        public static bool GetCanMouseScroll(DependencyObject obj)
        {
            return (bool)obj.GetValue(CanMouseScrollProperty);
        }

        public static void SetCanMouseScroll(DependencyObject obj, bool value)
        {
            obj.SetValue(CanMouseScrollProperty, value);
        }
        private static List<Window> ListenedWindows = new List<Window>();
        private static Point sworig = default;
        private static Vector swacc = default;
        private static ScrollViewer activedscrollviewer = null;
        private static void OnSwMouseDown(object d, MouseButtonEventArgs e)
        {
            if (activedscrollviewer != null)
                return;
            activedscrollviewer = (ScrollViewer)d;
            sworig = e.GetPosition(activedscrollviewer);
            swacc = default;
        }
        private static void OnSwMouseUp(object d, MouseButtonEventArgs e)
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
                activedscrollviewer = null;
            }
        }
        private static void OnSwMouseMove(object d, MouseEventArgs e)
        {
            if (activedscrollviewer != null)
            {
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
            DependencyProperty.RegisterAttached("CanMouseScroll", typeof(bool), typeof(Extensions), new PropertyMetadata(false, (s, e) =>
            {
                if (e.NewValue != e.OldValue)
                {
                    var bl = (bool)e.NewValue;
                    var sw = (ScrollViewer)s;
                    if (bl)
                    {
                        sw.MouseDown += OnSwMouseDown;
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

        private static readonly Random random = new Random();
        public static T Random<T>(this IList<T> e)
        {
            return e[random.Next(0, e.Count)];
        }
        public static void SetAutostart(bool enabled)
        {
            const string Key = "osp_autostart";
            RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (enabled)
                key.SetValue(Key, System.Reflection.Assembly.GetEntryAssembly().Location,RegistryValueKind.String);
            else
                if (key.GetValueNames().Contains(Key))
                    key.DeleteValue(Key);
            key.Dispose();
        }
    }
}
