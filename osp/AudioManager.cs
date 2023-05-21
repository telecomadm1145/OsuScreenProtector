using osp.Performance;
using ManagedBass;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace osp.Audio
{
    public interface ISample : IDisposable
    {
        int Id { get; }
        LargeArray<float> GetSamples();
    }
    public interface IChannel : IDisposable
    {
        int Id { get; }
        double Volume { get; set; }
        bool Play();
        bool Stop();
        bool Pause(bool pause = true);
        double PlaybackRate { get; set; }
        TimeSpan Current { get; set; }
        bool Stopped { get; }
        bool Paused { get; }
        bool Buffering { get; }
        bool Playing { get; }
        TimeSpan Duration { get; }
        int GetAvaliable();
        LargeArray<float> GetCurrentSamples(); // use LargeArray to lower GC pressure

    }
    public interface IAudioStream : IChannel
    {
        // no audio writing required
    }
    public interface IAudioDevice
    {
        string Name { get; }
        string Type { get; }
        int Id { get; }
        bool Default { get; }
        bool Enabled { get; }
        bool Initiated { get; }
        bool LoopbackDevice { get; }
    }
    public interface IAudioManager
    {
        IAudioStream Load(Stream stream);
        IAudioStream Load(byte[] data);
        ISample LoadSample(byte[] data);
        IAudioDevice[] GetAudioDevices();
        IAudioDevice Current { get; }
        bool DeviceOpened { get; }
        void OpenDevice(IAudioDevice device);
        void Close();
    }
    public static class AudioManagerExtensions
    {
        public static IAudioDevice GetDefaultDevice(this IAudioManager am)
        {
            return am.GetAudioDevices().First(x => x.Default);
        }
    }
    public class BassAudioManager : IAudioManager
    {
        public IAudioDevice Current => DeviceOpened ? CreateBassDevice(Bass.CurrentDevice) : null;

        public bool DeviceOpened { get; private set; } = false;

        public BassAudioManager()
        {

        }
        private class BassException : Exception
        {
            public BassException(Errors err) : base($"Bass returned a error code:{err}({(int)err})")
            {
            }
        }
        private class BassAudioDevice : IAudioDevice
        {
            public string Name { get; set; } = "";

            public string Type { get; set; } = "";

            public int Id { get; set; }

            public bool Default { get; set; }

            public bool Enabled { get; set; }

            public bool Initiated { get; set; }

            public bool LoopbackDevice { get; set; }
            public override string ToString()
            {
                return $"{Name}{(Default ? "(Default)" : "")}({Type})";
            }
        }
        public IAudioDevice[] GetAudioDevices()
        {
            List<IAudioDevice> list = new List<IAudioDevice>();
            for (int i = 0; i < Bass.DeviceCount; i++)
            {
                list.Add(CreateBassDevice(i));
            }
            return list.ToArray();
        }
        private class BassAudioStream : IAudioStream
        {
            public int Id { get; set; }
            public double Volume
            {
                get
                {
                    return Bass.ChannelGetAttribute(Id, ChannelAttribute.Volume);
                }
                set
                {
                    if (!Bass.ChannelSetAttribute(Id, ChannelAttribute.Volume, value))
                        throw new BassException(Bass.LastError);
                }
            }

            public TimeSpan Duration => new TimeSpan((long)(Bass.ChannelBytes2Seconds(Id, Bass.ChannelGetLength(Id)) * 10000000));

            public TimeSpan Current
            {
                get
                {
                    return new TimeSpan((long)(Bass.ChannelBytes2Seconds(Id, Bass.ChannelGetPosition(Id)) * 10000000));
                }
                set
                {
                    if (!Bass.ChannelSetPosition(Id, Bass.ChannelSeconds2Bytes(Id, value.TotalSeconds)))
                        throw new BassException(Bass.LastError);
                }
            }
            public double PlaybackRate
            {
                get
                {
                    return Bass.ChannelGetAttribute(Id, ChannelAttribute.Frequency) / GetInfo().Frequency;
                }
                set
                {
                    if (!Bass.ChannelSetAttribute(Id, ChannelAttribute.Frequency, value * GetInfo().Frequency))
                        throw new BassException(Bass.LastError);
                }
            }

            public bool Stopped => Bass.ChannelIsActive(Id) == PlaybackState.Stopped;

            public bool Paused => Bass.ChannelIsActive(Id) == PlaybackState.Paused;

            public bool Buffering => Bass.ChannelIsActive(Id) == PlaybackState.Stalled;

            public bool Playing => Bass.ChannelIsActive(Id) == PlaybackState.Playing;

            private ChannelInfo GetInfo()
            {
                ChannelInfo inf = new ChannelInfo();
                if (!Bass.ChannelGetInfo(Id, out inf))
                    throw new BassException(Bass.LastError);
                return inf;
            }

            public bool Pause(bool pause = true)
            {
                if (pause)
                    return Bass.ChannelPause(Id);
                else
                    return Bass.ChannelPlay(Id);
            }

            public bool Play()
            {
                return Bass.ChannelPlay(Id);
            }

            public bool Stop()
            {
                return Bass.ChannelStop(Id);
            }

            public unsafe LargeArray<float> GetCurrentSamples()
            {
                var len = Bass.ChannelGetData(Id, (IntPtr)0, (int)DataFlags.Available);
                if (len == -1)
                    throw new BassException(Bass.LastError);
                var buf = new LargeArray<float>(len / 4 - 1);
                int real_len = 0;
                if (((real_len = Bass.ChannelGetData(Id, (IntPtr)buf.GetPointer(), len | (int)DataFlags.Float)) == -1))
                    throw new BassException(Bass.LastError);
                return buf;
            }

            public void Dispose()
            {
                Bass.StreamFree(Id);
            }

            public int GetAvaliable()
            {
                var len = Bass.ChannelGetData(Id, (IntPtr)0, (int)DataFlags.Available);
                if (len == -1)
                    throw new BassException(Bass.LastError);
                return len / 4;
            }
        }
        private static BassAudioDevice CreateBassDevice(int i)
        {
            DeviceInfo info = new DeviceInfo();
            BassAudioDevice dev = null;
            if (Bass.GetDeviceInfo(i, out info))
            {
                dev = new BassAudioDevice()
                {
                    Id = i,
                    Name = info.Name,
                    Type = info.Type.ToString(),
                    LoopbackDevice = info.IsLoopback,
                    Initiated = info.IsInitialized,
                    Enabled = info.IsEnabled,
                    Default = info.IsDefault
                };
            }
            else throw new BassException(Bass.LastError);
            return dev;
        }

        public IAudioStream Load(Stream stream)
        {
            int sid;
            if ((sid = Bass.CreateStream(StreamSystem.NoBuffer, 0, CreateFileProceduresByStream(stream))) == 0)
                throw new BassException(Bass.LastError); 
            return new BassAudioStream() { Id = sid };
        }
        public IAudioStream Load(byte[] data)
        {
            int sid;
            if ((sid = Bass.CreateStream(data, 0, data.Length, 0)) == 0)
                throw new BassException(Bass.LastError);
            return new BassAudioStream() { Id = sid };
        }


        private static FileProcedures CreateFileProceduresByStream(Stream stream)
        {
            return new FileProcedures()
            {
                Close = (_) => { stream.Close(); },
                Length = (_) => { return stream.Length; },
                Read = (buff, len, _) =>
                {
                    byte[] buff2 = new byte[len];
                    var len_read = stream.Read(buff2, 0, len);
                    Marshal.Copy(buff2, 0, buff, len_read);
                    return len_read;
                },
                Seek = (x, _) =>
                {
                    stream.Position = x;
                    return true;
                }
            };
        }

        public void OpenDevice(IAudioDevice device)
        {
            if (!Bass.Init(device.Id, 44100, DeviceInitFlags.Stereo | DeviceInitFlags.Hog))
                throw new BassException(Bass.LastError);
            DeviceOpened = true;
        }

        public void Close()
        {
            if (!Bass.Free())
                throw new BassException(Bass.LastError);
            DeviceOpened = false;
        }
        private class BassAudioSample : ISample
        {
            public int Id { get; set; }

            public void Dispose()
            {
                Bass.SampleFree(Id);
            }

            public unsafe LargeArray<float> GetSamples()
            {
                SampleInfo si = new SampleInfo();
                if (!Bass.SampleGetInfo(Id, si))
                    throw new BassException(Bass.LastError);
                LargeArray<float> buf = new LargeArray<float>(si.Length/4);
                
                if (!Bass.SampleGetData(Id,(IntPtr)buf.GetPointer()))
                    throw new BassException(Bass.LastError);
                return buf;
            }
        }

        public ISample LoadSample(byte[] data)
        {
            int sid = 0;
            if ((sid = Bass.SampleLoad(data, 0, data.Length, 65535, BassFlags.Float)) == 0)
                throw new BassException(Bass.LastError);
            return new BassAudioSample() { Id = sid};
        }
    }
}
