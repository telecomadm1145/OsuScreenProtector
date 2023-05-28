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
        [DllImport("kernel32.dll")]
        private static extern void ExitProcess(int exitcode);
        public static void FastQuit(int exitcode = 0)
        {
ExitProcess(exitcode);
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
        public static string GenerateRandomPasswordString(int length)
        {
            List<char> PasswordChars = new();
            PasswordChars.AddRange(Enumerable.Range('A', 26).Select(x=>(char)x));
            PasswordChars.AddRange(Enumerable.Range('a', 26).Select(x => (char)x));
            PasswordChars.AddRange(Enumerable.Range('0', 10).Select(x => (char)x));
            PasswordChars.Add('-');
            return new string(Enumerable.Range(0, length).Select(x => PasswordChars.Random()).ToArray());
        }
    }

}
