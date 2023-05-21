﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OsuScreenProtector
{
    internal class Config
    {
        private const string OsuPathToken = "OsuPath";
        private const string ConfigName = "config.json";
        public static Config Instance { get; private set; }

        public static Config Load()
        {
            if (Instance != null)
                throw new InvalidOperationException();
            var dic = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User);
            if (dic.Contains(OsuPathToken))
            {
                var osupath = (string)dic[OsuPathToken];
                if (Directory.Exists(osupath))
                {
                    var cfgpath = Path.Combine(osupath, ConfigName);
                    if (File.Exists(cfgpath) & File.Exists(Path.Combine(osupath, "osu!.exe")))
                    {
                        try
                        {
                            Instance = System.Text.Json.JsonSerializer.Deserialize<Config>(File.ReadAllText(cfgpath));
                            Instance.OsuPath = osupath;
                            if (!Instance.Collections.Any(x => x.Name == "Sukidesu"))
                                Instance.Collections.Add(new GalleryCollection() { Name = "Sukidesu", Description = "我喜欢的图集" });
                            return Instance;
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                }
            }
            Instance = new Config();
            var ofd = new OpenFileDialog();
            ofd.Filter = "osu|osu!.exe";
            ofd.Multiselect = false;
            ofd.ShowDialog();
            if (!File.Exists(ofd.FileName))
                throw new Exception();
            Environment.SetEnvironmentVariable(OsuPathToken, Instance.OsuPath = Directory.GetParent(ofd.FileName).FullName, EnvironmentVariableTarget.User);
            Instance.Save();
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            return Instance;
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            Instance.Save();
        }
        public bool GetShouldRebuildImageCache()
        {
            var songs = Path.Combine(OsuPath, "Songs");
            if (!Directory.Exists(songs))
                throw new DirectoryNotFoundException();
            var newlast = Directory.GetLastWriteTimeUtc(songs);
            return newlast > LastSongsFolderModifiyTime;
        }
        public void RebuildImageCache()
        {
            var songs = Path.Combine(OsuPath, "Songs");
            if (!Directory.Exists(songs))
                throw new DirectoryNotFoundException();
            var count = 0;
            var newlast = Directory.GetLastWriteTimeUtc(songs);
            if (newlast > LastSongsFolderModifiyTime)
            {
                Logger.Instance.Log("Change detected,rebuilding cache...", "Info");
                // perform update...
                foreach (var dir_ in Directory.EnumerateDirectories(songs))
                {
                    //var dir = Environment.OSVersion.Version.Major >= 10 ? dir_ : "\\\\?\\" + dir_; // long path aware issue
                    var dir = "\\\\?\\" + dir_;
                    if (Directory.GetLastWriteTimeUtc(dir) > LastSongsFolderModifiyTime)
                    {
                        Logger.Instance.Log($"Reading {dir}...", "Info");
                        var cache = new CacheEntry();
                        foreach (var dir2 in Directory.EnumerateFiles(dir))
                        {
                            if (dir2.EndsWith(".osu", StringComparison.CurrentCultureIgnoreCase))
                            {
                                using (var sr = new StreamReader(dir2))
                                {
                                    try
                                    {
                                        var bmp = Beatmap.Parse(sr);
                                        bmp.SongPath = Path.Combine(dir, bmp.SongPath);
                                        bmp.BgPath = Path.Combine(dir, bmp.BgPath);
                                        if (File.Exists(bmp.BgPath) && File.Exists(bmp.SongPath))
                                        {
                                            cache.MapsetId = bmp.MapsetId;
                                            cache.Name = bmp.Title;
                                            cache.Dir = dir;
                                            cache.Beatmaps.Add(bmp);
                                            Logger.Instance.Log($"Successfully added song {bmp.Artist} - {bmp.Title}.", "Info");
                                        }
                                        count++;
                                    }
                                    catch
                                    {
                                        Logger.Instance.Log($"Failed to process {dir2}.", "Info");
                                    }
                                }
                            }
                        }
                        var same = Caches.FirstOrDefault(x => x.Dir == cache.Dir);
                        if (same != null)
                        {
                            Logger.Instance.Log($"Updating {same.Name}({same.MapsetId})");
                            Caches.Remove(same);
                        }
                        Caches.Add(cache);
                    }
                }
                Logger.Instance.Log($"Updated or loaded {count} beatmaps");
                LastSongsFolderModifiyTime = newlast;
                Save();
            }
        }
        public void Save()
        {
            Logger.Instance.Log("Saved!");
            var cfgpath = Path.Combine(OsuPath, ConfigName);
            File.WriteAllText(cfgpath, System.Text.Json.JsonSerializer.Serialize(this));
        }
        public void DeleteConfig()
        {
            Logger.Instance.Log("Delete config file.");
            var cfgpath = Path.Combine(OsuPath, ConfigName);
            File.Delete(cfgpath);
        }
        public string ConfigPath => Path.Combine(OsuPath, ConfigName);
        public string OsuPath { get; set; }
        public string LogPath { get; set; }
        public DateTime LastSongsFolderModifiyTime { get; set; }
        public List<CacheEntry> Caches { get; set; } = new List<CacheEntry>();
        public class CacheEntry
        {
            public string Dir { get; set; }
            public string Name { get; set; }
            public long MapsetId { get; set; }
            public List<Beatmap> Beatmaps { get; set; } = new List<Beatmap>();
        }
        public class Beatmap
        {
            public string Title { get; set; }
            public string Artist { get; set; }
            public string TitleUnicode { get; set; }
            public string ArtistUnicode { get; set; }
            public string SongPath { get; set; }
            public string BgPath { get; set; }
            public long Id { get; set; }
            public long MapsetId { get; set; }
            public double PreviewPoint { get; set; }
            public static Beatmap Parse(string data)
            {
                return Parse(new StringReader(data));
            }
            public static Beatmap Parse(TextReader data)
            {
                Beatmap bmp = new Beatmap();
                var name = "";
                while (data.Peek() != -1)
                {
                    var line = data.ReadLine().Trim();
                    if (line == "")
                        continue;
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        name = line.Substring(1, line.Length - 2).ToLower();
                        continue;
                    }
                    if (name == "events") // spec
                    {
                        if (line.StartsWith("0,")) // bg
                        {
                            bmp.BgPath = line.Split(',')[2].Trim('\"');
                        }
                        continue;
                    }
                    if (name == "timingpoints" || name == "hitobjects")
                    {
                        continue;
                    }
                    if (name == "")
                        continue;
                    var key = line.Substring(0, line.IndexOf(':')).Trim().ToLower();
                    var value = line.Substring(line.IndexOf(':') + 1).Trim();
                    if (key == "audiofilename")
                    {
                        bmp.SongPath = value;
                    }
                    if (key == "beatmapid")
                    {
                        long id;
                        if (long.TryParse(value, out id))
                            bmp.Id = id;
                    }
                    if (key == "previewtime")
                    {
                        double preview;
                        if (double.TryParse(value, out preview))
                            bmp.PreviewPoint = preview;
                    }
                    if (key == "beatmapsetid")
                    {
                        long id;
                        if (long.TryParse(value, out id))
                            bmp.MapsetId = id;
                    }
                    if (key == "title")
                    {
                        bmp.Title = value;
                    }
                    if (key == "artist")
                    {
                        bmp.Artist = value;
                    }
                    if (key == "titleunicode")
                    {
                        bmp.TitleUnicode = value;
                    }
                    if (key == "artistunicode")
                    {
                        bmp.ArtistUnicode = value;
                    }
                }
                return bmp;
            }
        }
        public List<long> DislikedImage { get; set; } = new List<long>();
        public class GalleryCollection
        {
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public List<long> Images { get; set; } = new List<long>();
        }
        public List<GalleryCollection> Collections { get; set; } = new List<GalleryCollection> { };
        public double AutoOpenMin { get; set; } = 5;
        public double AutoNextMin { get; set; } = 3;
        public bool ShowTray { get; set; } = true;
        public string FontFamily { get; set; } = "Microsoft YaHei UI";
        public string TimeFormat { get; set; } = "HH:mm";
        public string DateFormat { get; set; } = "yyyy/MM/dd dddd";
        public double TimeFontSize { get; set; } = 65;
        public double DateFontSize { get; set; } = 35;
        public double TimeDateGap { get; set; } = 0;
        public bool Autostart { get; set; } =
#if DEBUG
            false;
#else
            true;
#endif
        public double Volume { get; set; } = 10;
        public string SettingPasswordHash { get; set; } = "";
    }
}

