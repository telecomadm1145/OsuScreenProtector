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
using Keys = System.Windows.Forms.Keys;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.IO;

namespace osp
{
    internal static class Extensions
    {

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        public static bool IsPressed(this Keys key)
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
        [DllImport("kernel32.dll")]
        private static extern void ExitProcess(int exitcode);
        public static void FastQuit(int exitcode = 0)
        {
ExitProcess(exitcode);
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
        private class DCommand : ICommand
        {
            public Action<object> x;
            public event EventHandler CanExecuteChanged;

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {
                x(parameter);
            }
        }
        public static ICommand MakeCommand(Action<object> x)
        {
            return new DCommand() { x = x };
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
        public static ImageSource LoadImage(this string path)
        {
            using (var fs = File.OpenRead(path))
            {
                var imgsource = new BitmapImage();
                imgsource.BeginInit();
                imgsource.CacheOption = BitmapCacheOption.OnLoad;
                imgsource.StreamSource = fs;
                imgsource.EndInit();
                imgsource.Freeze();
                return imgsource;
            }
        }
        public static string GetImageSourcePath(this ImageSource i)
        {
            if (i is BitmapImage bi)
            {
                if (bi.UriSource != null)
                {
                    return bi.UriSource.LocalPath;
                }
                if (bi.StreamSource != null)
                {
                    if (bi.StreamSource is FileStream fs)
                    {
                        return fs.Name;
                    }
                }
            }
            return null;
        }
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        public static extern IntPtr GetShellWindow();
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        // judges if window is likely reaches maxium
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);
        [DllImport("user32.dll", SetLastError = false)]
        public static extern IntPtr GetDesktopWindow();
        public static bool PixelLikely(this int x, int y)
        {
            return Math.Abs(x - y) < 5;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;        // x position
            public int Y;         // y position
        }
        /// <summary>
        /// Contains information about the placement of a window on the screen.
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        internal struct WINDOWPLACEMENT
        {
            /// <summary>
            /// The length of the structure, in bytes. Before calling the GetWindowPlacement or SetWindowPlacement functions, set this member to sizeof(WINDOWPLACEMENT).
            /// <para>
            /// GetWindowPlacement and SetWindowPlacement fail if this member is not set correctly.
            /// </para>
            /// </summary>
            public int Length;

            /// <summary>
            /// Specifies flags that control the position of the minimized window and the method by which the window is restored.
            /// </summary>
            public int Flags;

            /// <summary>
            /// The current show state of the window.
            /// </summary>
            public int ShowCmd;

            /// <summary>
            /// The coordinates of the window's upper-left corner when the window is minimized.
            /// </summary>
            public POINT MinPosition;

            /// <summary>
            /// The coordinates of the window's upper-left corner when the window is maximized.
            /// </summary>
            public POINT MaxPosition;

            /// <summary>
            /// The window's coordinates when the window is in the restored position.
            /// </summary>
            public RECT NormalPosition;

            /// <summary>
            /// Gets the default (empty) value.
            /// </summary>
            public static WINDOWPLACEMENT Default
            {
                get
                {
                    WINDOWPLACEMENT result = new WINDOWPLACEMENT();
                    result.Length = Marshal.SizeOf(result);
                    return result;
                }
            }
        }
        public static void RunLater(this Action x)
        {
            Dispatcher.CurrentDispatcher.BeginInvoke(x);
        }
        public static string GetSha256(this string text)
        {
            if (String.IsNullOrEmpty(text))
                return null;

            using (var sha = new System.Security.Cryptography.SHA256Managed())
            {
                byte[] textData = System.Text.Encoding.UTF8.GetBytes(text.ToLower() + "_salt_by_telecomadm1145_and_happy_rainbow_table");
                byte[] hash = sha.ComputeHash(textData);
                return BitConverter.ToString(hash.Take(8).ToArray()).Replace("-", "");
            }
        }
    }

}
