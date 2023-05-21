using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace osp
{
    class AudioMixerHelper
    {
        private static IMMDevice GetDevice()
        {
            // get the speakers (1st render + multimedia) device
            IMMDeviceEnumerator deviceEnumerator = (IMMDeviceEnumerator)(new MMDeviceEnumerator());
            IMMDevice speakers;
            deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out speakers);
            return speakers;
        }
        private static IAudioEndpointVolume GetVolumeController()
        {
            var dev = GetDevice();
            var guid = typeof(IAudioEndpointVolume).GUID;
            object res;
            dev.Activate(ref guid, 0, IntPtr.Zero, out res);
            return (IAudioEndpointVolume)res;
        }
        private static ISimpleAudioVolume GetAppVolumeController()
        {
            var dev = GetDevice();
            var guid = typeof(IAudioSessionManager).GUID;
            object res;
            dev.Activate(ref guid,0, IntPtr.Zero, out res);
            object res2;
            var nullguid = default(Guid);
            ((IAudioSessionManager)res).GetSimpleAudioVolume(ref nullguid,0,out res2);
            return (ISimpleAudioVolume)res2;
        }
        private static IAudioEndpointVolume volume;
        private static ISimpleAudioVolume a_volume;
        public static void SetAppMasterVolume(double vol)
        {
            EnsureAppVolumeController();
            a_volume.SetMute(false, IntPtr.Zero);
            a_volume.SetMasterVolume((float)vol, IntPtr.Zero);
        }
        public static double GetAppMasterVolume()
        {
            EnsureAppVolumeController();
            float t;
            a_volume.GetMasterVolume(out t);
            return t;
        }
        public static void SetMasterVolume(double vol)
        {
            EnsureVolumeController();
            volume.SetMute(false,IntPtr.Zero);
            volume.SetMasterVolumeLevelScalar((float)vol, IntPtr.Zero);
        }
        public static double GetMasterVolume()
        {
            EnsureVolumeController();
            float t;
            volume.GetMasterVolumeLevelScalar(out t);
            return t;
        }
        private static void EnsureAppVolumeController()
        {
            a_volume = a_volume ?? GetAppVolumeController();
        }
        private static void EnsureVolumeController()
        {
            volume = volume ?? GetVolumeController();
        }

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        internal class MMDeviceEnumerator
        {
        }

        internal enum EDataFlow
        {
            eRender,
            eCapture,
            eAll,
            EDataFlow_enum_count
        }

        internal enum ERole
        {
            eConsole,
            eMultimedia,
            eCommunications,
            ERole_enum_count
        }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IMMDeviceEnumerator
        {
            int NotImpl1();

            [PreserveSig]
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);

            // the rest is not implemented
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);

            // the rest is not implemented
        }
        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioEndpointVolume
        {
            int NotImpl1();
            int NotImpl2();
            int NotImpl3();
            [PreserveSig]
            int SetMasterVolumeLevel(float fLevelDB, IntPtr notimpl);
            [PreserveSig]
            int SetMasterVolumeLevelScalar(float fLevel, IntPtr notimpl);
            [PreserveSig]
            int GetMasterVolumeLevel(out float fLevelDB);
            [PreserveSig]
            int GetMasterVolumeLevelScalar(out float fLevel);
            int NotImpl4();
            int NotImpl5();
            int NotImpl6();
            int NotImpl7();
            [PreserveSig]
            int SetMute(bool bMute,IntPtr notimpl);
            [PreserveSig]
            int GetMute(out bool bMute);
        }
        [Guid("BFA971F1-4D5E-40BB-935E-967039BFBEE4"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioSessionManager
        {
            int NotImpl();
            int GetSimpleAudioVolume(ref Guid sessionguid, int flags, [MarshalAs(UnmanagedType.IUnknown)] out object obj);
        }
        [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface ISimpleAudioVolume
        {
            [PreserveSig]
            int SetMasterVolume(float fLevel,IntPtr notimpl);
            [PreserveSig]
            int GetMasterVolume(out float pfLevel);
            [PreserveSig]
            int SetMute(bool bMute,IntPtr notimpl);
            [PreserveSig]
            int GetMute(out bool bMute);
        }
    }
}
