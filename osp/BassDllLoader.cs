using OsuScreenProtector;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace osp
{
    public class BassDllLoader
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LoadLibrary(string lpFileName);
        public static void LoadFromResource()
        {
            if (LoadLibrary("bass.dll")!=(nint)0)
            {
                Logger.Instance.Log("Load bass from local.");
                return;
            }
            Logger.Instance.Log("Try to extract bass and load.");
            var temp = Path.Combine(Path.GetTempPath(),"bass.dll");
            if (Environment.Is64BitProcess)
            {
                File.WriteAllBytes(temp,BassDll.bass_64);
            }
            else
            {
                File.WriteAllBytes(temp, BassDll.bass_32);
            }
            if (LoadLibrary(temp)!=(nint)0)
            {
                Logger.Instance.Log("Loaded bass");
            }
            else
            {
                Logger.Instance.Log("Failed");
            }
        }
    }
}
