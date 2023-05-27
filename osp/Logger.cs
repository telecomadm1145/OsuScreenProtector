using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OsuScreenProtector
{
    public class Logger
    {
        private Stream fs;
        private StreamWriter sw;
        public static Logger Instance = new Logger();
        public void Setup()
        {
            if (Config.Instance == null)
                throw new InvalidOperationException();
            var cfg = Config.Instance;
            cfg.LogPath = cfg.LogPath ?? Path.Combine(cfg.OsuPath, "Logs", "osuscreenprotector.log");
            if (!File.Exists(cfg.LogPath))
            {
                fs = File.Create(cfg.LogPath);
            }
            else
            {
                fs = new FileStream(cfg.LogPath, FileMode.Append, FileAccess.Write);
            }
            sw = new StreamWriter(fs, Encoding.UTF8, 16384);
            sw.WriteLine();
            sw.WriteLine("---------------osp log start---------------");
            Log("logger started.");
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        }

        private void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            sw.Dispose();
            fs.Dispose();
        }

        private void CurrentDomain_FirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            Log($"Exception was thrown:{e.Exception}", "Warn");
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log($"Unhandled exception:{e.ExceptionObject}", "Error");
            if (e.IsTerminating)
                Log("Quitting...", "Error");
            sw.Dispose();
            fs.Dispose();
        }
        public event EventHandler<string> LogHook;
        public void Log(string message, string category = "Debug", [CallerMemberName] string caller = null, [CallerLineNumber] int linenum = 0)
        {
            var msg = $"[{DateTime.Now}][{category}]({caller}:{linenum}){message}\n";
            if (sw != null && sw.BaseStream != null && sw.BaseStream.CanWrite)
                sw.Write(msg);
            if (LogHook != null)
            {
                LogHook(this, msg);
            }
        }

        public void Flush()
        {
            sw.Flush();
            fs.Flush();
        }
    }
}
