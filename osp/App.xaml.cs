using OsuScreenProtector;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace osp
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            Dispatcher.UnhandledException += (_, e2) => {
                Logger.Instance.Log(e2.Exception.ToString());
                e2.Handled = true; 
            };
            Config.Load();
            Logger.Instance.Setup();
            Config.Instance.RebuildImageCache();
            new Power().EnableConstantDisplayAndPower(true);
            Extensions.SetAutostart(Config.Instance.Autostart);
            BassDllLoader.LoadFromResource();
            base.OnStartup(e);
        }
    }
}
