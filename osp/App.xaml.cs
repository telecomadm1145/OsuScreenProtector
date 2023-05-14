using OsuScreenProtector;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
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
            Config.Load();
            Logger.Instance.Setup();
            Config.Instance.RebuildImageCache();
            new Power().EnableConstantDisplayAndPower(true);
            Extensions.SetAutostart(Config.Instance.Autostart);
            base.OnStartup(e);
        }
    }
}
