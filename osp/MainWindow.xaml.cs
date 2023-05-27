using osp.Audio;
using OsuScreenProtector;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using Keys = System.Windows.Forms.Keys;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using static OsuScreenProtector.Config;

namespace osp
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
#if !DEBUG
            Hide();
            Activated+=(s,e) => Topmost = true;
#endif
            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None;
            OsuPathBox.Text = cfg.OsuPath;
            OsuPathBox.MouseDoubleClick += (s, e) => Process.Start(cfg.OsuPath);
            LogPathBox.Text = cfg.LogPath;
            LogPathBox.TextChanged += (s, e) =>
            {
                cfg.LogPath = LogPathBox.Text;
            };
            InputBoxInput.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter && !e.Handled)
                {
                    SubmitButton.Focus();
                }
            };
            PasswordBoxInput.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter && !e.Handled)
                {
                    SubmitButton.Focus();
                }
            };
            var monitor = new Thread(MonitorLoop);
            monitor.Start(new WindowInteropHelper(this).EnsureHandle()); //贺松鼠
            Loaded += (s, e) =>
            {
                RandomImg();
            };
            AutoOpenBox.Text = cfg.AutoOpenMin.ToString();
            AutoOpenBox.TextChanged += (s, e) =>
            {
                if (double.TryParse(AutoOpenBox.Text, out double res))
                {
                    cfg.AutoOpenMin = res;
                }
            };
            AutoNextBox.Text = cfg.AutoNextMin.ToString();
            AutoNextBox.TextChanged += (s, e) =>
            {
                if (double.TryParse(AutoNextBox.Text, out double res))
                {
                    cfg.AutoNextMin = res;
                }
            };
            System.Windows.Forms.NotifyIcon ni = new System.Windows.Forms.NotifyIcon();
            ni.Text = "OSUpdater 护眼程序";
            ni.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetEntryAssembly().Location);
            if (cfg.ShowTray)
                log.Log("Loading tray.");
            else
                log.Log("Tray disabled.");
            ni.Visible = cfg.ShowTray;
            ni.ContextMenu = new System.Windows.Forms.ContextMenu(new System.Windows.Forms.MenuItem[]{
                new System.Windows.Forms.MenuItem("显示",(s,e)=>{
                    Show();
                    Activate();
                }),
                new System.Windows.Forms.MenuItem("设置", (s, e) =>
                {
                    OpenSettings();
                    Show();
                    Activate();
                }),
                new System.Windows.Forms.MenuItem("不显示托盘图标", (s, e) =>
                {
                    cfg.ShowTray = ni.Visible=false;
                    cfg.Save();
                }),
#if DEBUG
                new System.Windows.Forms.MenuItem("退出", (s, e) =>
                {
                    cfg.SaveImmediately();
                    Extensions.FastQuit();
                }),
#endif
            });
            TextBlock current = null;
            var height = .0;
            var ch = .0;
            System.Drawing.FontFamily.Families.ToList().ForEach(x =>
            {
                Border r = new Border() { CornerRadius = new CornerRadius(10), Margin = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left };
                TextBlock element = new TextBlock() { Text = x.GetName(0) };
                element.MouseDown += (s, e) =>
                {
                    cfg.FontFamily = element.Text;
                    foreach (var item in FontList.Children)
                    {
                        if (item is Border b)
                        {
                            b.Background = null;
                        }
                    }
                    r.Background = new SolidColorBrush(Colors.White) { Opacity = 0.1 };
                };
                element.FontFamily = new FontFamily(x.GetName(0));
                element.LineHeight = double.NaN;
                if (element.Text == cfg.FontFamily)
                {
                    r.Background = new SolidColorBrush(Colors.White) { Opacity = 0.1 };
                    current = element;
                    ch = height;
                }
                r.Child = element;
                FontList.Children.Add(r);
                element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                height += element.DesiredSize.Height;
            });
            FontListViewer.ScrollToVerticalOffset(ch);
            Closing += (s, e) => { e.Cancel = true; Hide(); };
            Beatmap onp = null;
            var dt = new DispatcherTimer();
            dt.Interval = TimeSpan.FromMilliseconds(1);
            dt.Tick += (s, e) =>
            {
                var now = DateTime.Now;
                Time.Text = now.ToString(cfg.TimeFormat);
                Date.Text = now.ToString(cfg.DateFormat);
                if (Time.FontFamily.ToString() != cfg.FontFamily)
                {
                    Time.FontFamily = Date.FontFamily = new FontFamily(cfg.FontFamily);
                }
                Time.FontSize = cfg.TimeFontSize;
                Date.FontSize = cfg.DateFontSize;
                Date.Margin = new Thickness(0, cfg.TimeDateGap, 0, 0);

                if (audiostream != null && audiostream.Stopped && cfg.Loop)
                {
                    audiostream.Play();
                }
                if (audiostream != null && !audiostream.Playing && !cfg.Loop)
                {
                    audiostream = null;
                    np = null;
                }
                if (onp != np)
                {
                    Beatmap np_c = np;
                    StopPlayButton.Visibility = np != null && cfg.ShowStopButton ? Visibility.Visible : Visibility.Collapsed;
                    if (np != null)
                    {
                        PushNotification($"Now playing {np.Artist} - {np.Title}", double.MaxValue, (x) => np != np_c);
                    }
                    onp = np;
                }
                stoptimer = DetailInfo.IsVisible;
            };
            dt.Start();
            var trigonce = new Thread(() =>
            {
                while (!cfg.GetShouldRebuildImageCache())
                {
                    Thread.Sleep(100);
                }
                Dispatcher.Invoke(() => PushNotification("检测到缓存过期，请在设置中重建缓存", double.MaxValue, (x) => !cfg.GetShouldRebuildImageCache()));
            });
            trigonce.Start();
            var hotkey = new Thread(() =>
            {
                while (true)
                {
                    if (Keys.ControlKey.IsPressed() && Keys.Tab.IsPressed() && Keys.S.IsPressed())
                    {
                        Dispatcher.Invoke(() => { Show(); Activate(); });
                    }
                    Thread.Sleep(1);
                }
            });
            hotkey.Start();
            var now2 = DateTime.Now;
            Time.Text = now2.ToString(cfg.TimeFormat);
            Date.Text = now2.ToString(cfg.DateFormat);
            RefreshCollections();
            TimeFormat.Text = cfg.TimeFormat;
            DateFormat.Text = cfg.DateFormat;
            Time.FontFamily = Date.FontFamily = new FontFamily(cfg.FontFamily);
            TextChangedEventHandler l = (s, e) =>
            {
                cfg.TimeFormat = TimeFormat.Text;
                cfg.DateFormat = DateFormat.Text;
            };
            TimeFormat.TextChanged += l;
            DateFormat.TextChanged += l;
            TimeFontSizeSlider.Value = cfg.TimeFontSize;
            DateFontSizeSlider.Value = cfg.DateFontSize;
            GapSlider.Value = cfg.TimeDateGap;
            RoutedPropertyChangedEventHandler<double> l2 = (s, e) =>
            {
                cfg.TimeFontSize = TimeFontSizeSlider.Value;
                cfg.DateFontSize = DateFontSizeSlider.Value;
                cfg.TimeDateGap = GapSlider.Value;
            };
            TimeFontSizeSlider.ValueChanged += l2;
            DateFontSizeSlider.ValueChanged += l2;
            GapSlider.ValueChanged += l2;

            AutostartCheckBox.IsChecked = cfg.Autostart;
            RoutedEventHandler l3 = (_, __) =>
            {
                cfg.Autostart = AutostartCheckBox.IsChecked.Value;
                Extensions.SetAutostart(cfg.Autostart);
            };
            AutostartCheckBox.Checked += l3;
            AutostartCheckBox.Unchecked += l3;
            try
            {
                bam.OpenDevice(bam.GetDefaultDevice());
            }
            catch
            {
                log.Log("Cannot init bass", "Error");
                bam = null;
            }
            VolumeSlider.Value = cfg.Volume;
            VolumeSlider.ValueChanged += (_, __) =>
            {
                cfg.Volume = VolumeSlider.Value;
                if (audiostream != null)
                {
                    audiostream.Volume = cfg.Volume / 100;
                }
            };
            RoutedEventHandler t4 = (_, __) =>
            {
                cfg.Loop = AudioLoopCheckbox.IsChecked.Value;
            };
            AudioLoopCheckbox.Checked += t4;
            AudioLoopCheckbox.Unchecked += t4;

            RandomOrderCheckbox.IsChecked = cfg.RandomOrder;
            RoutedEventHandler t5 = (_, __) =>
            {
                cfg.RandomOrder = RandomOrderCheckbox.IsChecked.Value;
            };
            RandomOrderCheckbox.Checked += t5;
            RandomOrderCheckbox.Unchecked += t5;

            ShowStopButtonCheckbox.IsChecked = cfg.ShowStopButton;
            RoutedEventHandler t6 = (_, __) =>
            {
                cfg.ShowStopButton = ShowStopButtonCheckbox.IsChecked.Value;
                if (!cfg.ShowStopButton)
                {
                    StopPlayButton.Visibility = Visibility.Collapsed;
                }
                else if (np != null)
                {
                    StopPlayButton.Visibility = Visibility.Visible;
                }
            };
            ShowStopButtonCheckbox.Checked += t6;
            ShowStopButtonCheckbox.Unchecked += t6;

            BgDimSlider.Value = cfg.BackgroundDim;
            BgDimSlider.ValueChanged += (_, __) =>
            {
                cfg.BackgroundDim = BgDimSlider.Value;
                UpdateBgDim();
            };
            UpdateBgDim();
        }

        private void UpdateBgDim()
        {
            var sb = (SolidColorBrush)BgDimOverlay.Fill;
            sb.Opacity = Math.Abs(cfg.BackgroundDim / 100);
            sb.Color = cfg.BackgroundDim > 0 ? Color.FromArgb(255, 0, 0, 0) : Color.FromArgb(255, 255, 255, 255);
        }
        private bool stoptimer = false;
        private const string FailedMsg = "密码不正确，凭证错误，加密错误，Windows加密服务已损坏";
        private BassAudioManager bam = new BassAudioManager();
        private static bool HasKeyPressed()
        {
            return Enum.GetValues(typeof(Keys)).Cast<Keys>().Where(x => (int)x != 0 && (int)x != 1).Any(x => x.IsPressed());
        }
        [DllImport("user32.dll")]
        static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);
        private void MonitorLoop(object mw) // 松鼠循环
        {
            IntPtr rmw = (IntPtr)mw;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            TimeSpan lastaction = default;
            TimeSpan lastnext = default;
            var mouse = System.Windows.Forms.Control.MousePosition;
            log.Log("Monitor thread on.");
            log.Log($"Shows on every {cfg.AutoOpenMin} min.Switch to next page on every {cfg.AutoNextMin} min");
            while (true)
            {
                TimeSpan elapsed = sw.Elapsed;
                if (breaktimer)
                {
                    lastaction = elapsed + breakduration;
                    lastnext = elapsed + breakduration;
                    breaktimer = false;
                    breakduration = default;
                }
                if (stoptimer)
                {
                    lastnext = elapsed + (elapsed - lastnext > TimeSpan.Zero ? elapsed - lastnext : TimeSpan.Zero);
                }
                if (!IsWin32WindowVisble(rmw))
                {
                    bool key = HasKeyPressed();
                    if (mouse != System.Windows.Forms.Control.MousePosition || key)
                    {
                        lastaction = elapsed;
                        mouse = System.Windows.Forms.Control.MousePosition;
                    }
                    lastnext = elapsed;
                }
                else
                {
                    AudioMixerHelper.SetMasterVolume(1);
                    lastaction = elapsed;
                }
                var hwnd = Extensions.GetForegroundWindow();
                if (hwnd != IntPtr.Zero && hwnd != Extensions.GetShellWindow() && hwnd != Extensions.GetDesktopWindow())
                {
                    Extensions.RECT scrsz;
                    if (Extensions.GetWindowRect(Extensions.GetDesktopWindow(), out scrsz))
                    {
                        Extensions.RECT foreg;
                        if (Extensions.GetWindowRect(Extensions.GetForegroundWindow(), out foreg))
                        {
                            // 多屏幕就等会了（
                            if (foreg.Left.PixelLikely(scrsz.Left) && foreg.Top.PixelLikely(scrsz.Top)
                            && foreg.Bottom.PixelLikely(scrsz.Bottom) && foreg.Right.PixelLikely(scrsz.Right))
                            {
                                lastaction = elapsed;
                            }
                        }
                    }
                    else
                    {
                    }
                }
                if ((elapsed - lastaction).TotalMinutes > cfg.AutoOpenMin)
                {
                    log.Log($"Automatic shows atfer {(elapsed - lastaction).TotalSeconds}s.(config:{cfg.AutoOpenMin})");
                    Dispatcher.Invoke(() => { Show(); Activate(); });
                    lastaction = elapsed;
                }
                if ((elapsed - lastnext).TotalMinutes > cfg.AutoNextMin)
                {
                    log.Log($"Switch to {(cfg.RandomOrder ? "random" : "next")} image after {(elapsed - lastnext).TotalSeconds}s.(config:{cfg.AutoNextMin})");
                    if (cfg.RandomOrder)
                        Dispatcher.Invoke(RandomImg);
                    else
                        Dispatcher.Invoke(NextImg);
                    lastnext = elapsed;
                }
                AudioMixerHelper.SetAppMasterVolume(1);
                log.Flush();
                Thread.Sleep(10);
            }
        }

        private static bool IsWin32WindowVisble(IntPtr rmw)
        {
            return ((long)GetWindowLong(rmw, -16) & 0x10000000) == 0x10000000;
        }

        private static readonly Random random = new Random();
        private Config cfg => Config.Instance;
        private Logger log => Logger.Instance;
        private int cursor;
        private bool breaktimer;
        private TimeSpan breakduration;
        private Config.Beatmap curbmp;
        private IAudioStream audiostream;
        private Config.Beatmap np;
        private bool loading = false;
        private void LoadImg(bool next = true)
        {
            if (loading)
            {
                log.Log("wont load until last call finishes.");
                return;
            }
            loading = true;
            log.Log("loading img.");
            Task.Run(() => {
            reload:
                Config.Beatmap beatmap = null;
                ImageSource imageSource = null;
                var cursor = this.cursor;
                try
                {
                    beatmap = cfg.Caches[cursor].Beatmaps.Random();
                    if (cfg.DislikedImage.Contains(beatmap.MapsetId))
                    {
                        log.Log($"{beatmap.Title} disliked.random to {(next ? "next" : "prev")}.");
                        if (next)
                        {
                            cursor++;
                            if (cursor >= cfg.Caches.Count)
                                cursor = 0;
                        }
                        else
                        {
                            cursor--;
                            if (cursor < 0)
                                cursor = cfg.Caches.Count;
                        }
                        goto reload;
                    }
                    imageSource = beatmap.BgPath.LoadImage();
                    imageSource.Freeze();
                }
                catch
                {
                    log.Log($"Unable to read bmp at {cursor}.Switch to {(next ? "next" : "prev")}.");
                    if (next)
                    {
                        cursor++;
                        if (cursor >= cfg.Caches.Count)
                            cursor = 0;
                    }
                    else
                    {
                        cursor--;
                        if (cursor < 0)
                            cursor = cfg.Caches.Count;
                    }
                    goto reload;
                }
                Dispatcher.BeginInvoke((Action)(() => {
                    Bg.Source = imageSource;
                    DetailInfoBox.Inlines.Clear();
                    var span = new Span();
                    var inline1 = new Run($"{beatmap.Artist} - {beatmap.Title}\n") { FontSize = 18, ToolTip = $"{beatmap.ArtistUnicode} - {beatmap.TitleUnicode}" };

                    ContextMenu ctx = new ContextMenu();
                    MenuItem copy1 = new MenuItem() { Header = "复制全称" };
                    copy1.Click += (s, e) => Clipboard.SetText($"{beatmap.ArtistUnicode} - {beatmap.TitleUnicode}");
                    ctx.Items.Add(copy1);
                    MenuItem copy2 = new MenuItem() { Header = "复制全称(罗马音)" };
                    copy2.Click += (s, e) => Clipboard.SetText($"{beatmap.Artist} - {beatmap.Title}");
                    ctx.Items.Add(copy2);
                    MenuItem copy3 = new MenuItem() { Header = "复制标题" };
                    copy3.Click += (s, e) => Clipboard.SetText(beatmap.TitleUnicode);
                    ctx.Items.Add(copy3);
                    MenuItem copy4 = new MenuItem() { Header = "复制标题(罗马音)" };
                    copy4.Click += (s, e) => Clipboard.SetText(beatmap.Title);
                    ctx.Items.Add(copy4);
                    MenuItem copy5 = new MenuItem() { Header = "复制艺术家" };
                    copy5.Click += (s, e) => Clipboard.SetText(beatmap.ArtistUnicode);
                    ctx.Items.Add(copy5);
                    MenuItem copy6 = new MenuItem() { Header = "复制艺术家(罗马音)" };
                    copy6.Click += (s, e) => Clipboard.SetText(beatmap.Artist);
                    ctx.Items.Add(copy6);
                    if (beatmap.MapsetId != -1 && beatmap.Id != 0)
                    {
                        MenuItem copy7 = new MenuItem() { Header = "复制Id" };
                        copy7.Click += (s, e) => Clipboard.SetText(beatmap.MapsetId.ToString());
                        ctx.Items.Add(copy7);
                        MenuItem open1 = new MenuItem() { Header = "在浏览器打开" };
                        open1.Click += (s, e) => Process.Start($"https://osu.ppy.sh/b/{beatmap.Id}");
                        ctx.Items.Add(open1);
                        MenuItem open2 = new MenuItem() { Header = "在应用内打开" };
                        open2.Click += (s, e) => Process.Start($"osu://b/{beatmap.Id}");
                        ctx.Items.Add(open2);
                    }
                    inline1.ContextMenu = ctx;
                    span.Inlines.Add(inline1);
                    var hl = new Hyperlink(new Run("点我查看图片\n"));
                    hl.Click += (s, e) => { Topmost = false; Process.Start(beatmap.BgPath); };
                    hl.ToolTip = beatmap.BgPath;
                    span.Inlines.Add(hl);
                    if (!string.IsNullOrWhiteSpace(beatmap.SongPath) && bam != null)
                    {
                        var hl2 = new Hyperlink(new Run("点我播放 "));
                        var hl3 = new Hyperlink(new Run("点我停止\n"));
                        var dt = new DispatcherTimer();
                        dt.Interval = TimeSpan.FromMilliseconds(1);
                        var progress = new Slider();
                        progress.Width = 200;
                        progress.Maximum = 0;
                        progress.Value = 0;
                        progress.TickFrequency = 10;
                        progress.Visibility = Visibility.Collapsed;
                        progress.TickPlacement = System.Windows.Controls.Primitives.TickPlacement.BottomRight;
                        progress.ValueChanged += (s, e) =>
                        {
                            if (audiostream != null && Math.Abs(audiostream.Current.TotalSeconds - progress.Value) > 0.1)
                            {
                                var p = TimeSpan.FromSeconds(progress.Value);
                                if (p < audiostream.Duration)
                                    audiostream.Current = p;
                            }
                        };
                        progress.Unloaded += (_, __) => dt.Stop();
                        dt.Tick += (s, e) => { if (audiostream != null) progress.Value = audiostream.Current.TotalSeconds; };
                        dt.Start();
                        TextBlock.SetBaselineOffset(progress, 14);
                        hl2.Click += (s, e) =>
                        {
                            if (audiostream != null)
                            {
                                var as_ = audiostream;
                                audiostream = null;
                                as_.Stop();
                            }
                            np = beatmap;
                            var channel = bam.Load(File.OpenRead(beatmap.SongPath));
                            audiostream = channel;
                            audiostream.Volume = cfg.Volume / 100;
                            audiostream.Play();
                            progress.Maximum = Math.Round(audiostream.Duration.TotalSeconds);
                            audiostream.Current = TimeSpan.FromMilliseconds(beatmap.PreviewPoint == -1 || beatmap.PreviewPoint == 0 ? audiostream.Duration.TotalMilliseconds / 3 : beatmap.PreviewPoint);
                            progress.Visibility = Visibility.Visible;
                            hl3.IsEnabled = true;
                        };
                        hl3.Click += (s, e) =>
                        {
                            var as_ = audiostream;
                            audiostream = null;
                            as_.Stop();
                            np = null;
                        };
                        hl3.IsEnabled = false;
                        span.Inlines.Add(hl2);
                        span.Inlines.Add(hl3);
                        span.Inlines.Add(progress);
                        span.Inlines.Add(new Run("\n"));
                    }
                    var hl4 = new Hyperlink(new Run("点我打开音频\n"));
                    hl4.Click += (s, e) => { Topmost = false; Process.Start(beatmap.SongPath); };
                    hl4.ToolTip = beatmap.SongPath;
                    span.Inlines.Add(hl4);
                    var hl5 = new Hyperlink(new Run("额外保持10分钟"));
                    hl5.Click += (_, __) =>
                    {
                        breaktimer = true;
                        breakduration = TimeSpan.FromMinutes(10);
                    };
                    span.Inlines.Add(hl5);
                    DetailInfoBox.Inlines.Add(span);
                    log.Log($"已加载{beatmap.BgPath}({beatmap.Artist} - {beatmap.Title}(Id:{beatmap.Id}))");
                }));
                curbmp = beatmap;
                breaktimer = true;
                var rank = cfg.Ranks.FirstOrDefault(x => x.MapsetId == beatmap.MapsetId);
                if (rank != null)
                {
                    rank.Rank += 1;
                    cfg.Save();
                }
                loading = false;
            });
        }
        private void NextImg()
        {
            breaktimer = true;
            cursor++;
            if (cursor >= cfg.Caches.Count)
                cursor = 0;
            log.Log($"{cursor}/{cfg.Caches.Count}");
            LoadImg();
        }
        private void PrevImg()
        {
            breaktimer = true;
            cursor--;
            if (cursor < 0)
                cursor = cfg.Caches.Count;
            log.Log($"{cursor}/{cfg.Caches.Count}");
            LoadImg(false);
        }
        private void RandomImg()
        {
            breaktimer = true;
            long targetbmp = 0;
            List<RankEntry> ranks = cfg.Ranks.Where(x => !cfg.DislikedImage.Contains(x.MapsetId)).ToList();
            if (ranks.Count == 0)
                goto failback;
            var totalrank = 0.0;
            ranks.ForEach(x => totalrank += x.GetRelativeRank());
            log.Log($"total rank == {totalrank}");
            var index = random.NextDouble() * totalrank;
            var ranki = 0.0;
            foreach (var x in ranks)
            {
                ranki += x.GetRelativeRank();
                if (ranki >= index)
                {
                    targetbmp = x.MapsetId;
                    break;
                }
            }
        failback:
            if (targetbmp == 0)
            {
                log.Log("Falled to perform a ranked random order,fail back to normal random order.");
            redo:
                cursor = random.Next(0, cfg.Caches.Count);
                if (cfg.DislikedImage.Contains(cfg.Caches[cursor].MapsetId))
                {
                    goto redo;
                }
            }
            else
            {
                cursor = cfg.Caches.IndexOf(cfg.Caches.First(x => x.MapsetId == targetbmp));
            }
            log.Log($"{cursor}/{cfg.Caches.Count}");
            LoadImg();
        }
        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            NextImg();
            //right
        }

        private void Grid_MouseLeftButtonDown_1(object sender, MouseButtonEventArgs e)
        {
            PrevImg();
            //left
        }

        private void Button_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenSettings();
            // settings
        }

        private void OpenSettings()
        {
            if (string.IsNullOrEmpty(cfg.SettingPasswordHash))
            {
                SettingsFlyout.Visibility = Visibility.Visible;
                return;
            }
            InputBoxPassword("输入密码以打开设置", "", null, (x) =>
            {
                if (x.GetSha256() == cfg.SettingPasswordHash)
                {
                    SettingsFlyout.Visibility = Visibility.Visible;
                }
                else
                {
                    PushNotification(FailedMsg);
                }
            });
        }

        private void PushNotification(string msg, double delay = 20000, Func<TimeSpan, bool> CanClose = null)
        {
            if (nofitifactiontolog)
            Logger.Instance.Log(msg);
            var msggrid = new Grid() { Width = 350, MinHeight = 50, Margin = new Thickness(5) };
            var border = new Border()
            {
                Background = new SolidColorBrush(Colors.Black) { Opacity = .8 },
                CornerRadius = new CornerRadius(10),
                BorderBrush = new SolidColorBrush(Colors.Black) { Opacity = .9 },
                BorderThickness = new Thickness(.5),
            };
            msggrid.Children.Add(border);
            msggrid.Children.Add(new TextBlock() { Text = msg, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10), Foreground = new SolidColorBrush(Colors.White), FontSize = 15 });
            Storyboard autohide = new Storyboard();
            autohide.Completed += (s, e) => NotificationPanel.Children.Remove(msggrid);
            var closeani = new DoubleAnimation() { Duration = TimeSpan.FromSeconds(.5), EasingFunction = new SineEase(), To = 0 };
            Storyboard.SetTargetProperty(closeani, new PropertyPath("Opacity"));
            autohide.Children.Add(closeani);
            var closeani2 = new ThicknessAnimation() { To = new Thickness(0, -200, 0, 0), EasingFunction = new SineEase(), Duration = TimeSpan.FromSeconds(.5) };
            Storyboard.SetTargetProperty(closeani2, new PropertyPath("Margin"));
            autohide.Children.Add(closeani2);
            Storyboard slidein = new Storyboard();
            var openani = new DoubleAnimation() { Duration = TimeSpan.FromSeconds(.3), EasingFunction = new SineEase(), To = 1, From = 0 };
            var openani2 = new ThicknessAnimation() { From = new Thickness(-300, 0, 0, 0), To = default, EasingFunction = new SineEase(), Duration = TimeSpan.FromSeconds(.3) };
            Storyboard.SetTargetProperty(openani, new PropertyPath("Opacity"));
            Storyboard.SetTargetProperty(openani2, new PropertyPath("Margin"));
            slidein.Children.Add(openani2);
            slidein.Children.Add(openani);
            msggrid.BeginStoryboard(slidein);
            NotificationPanel.Children.Add(msggrid);
            NotificationScroller.ScrollToBottom();
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Task.Run(() =>
            {
                while (sw.ElapsedMilliseconds <= delay && (CanClose == null || !CanClose(sw.Elapsed)) || sw.ElapsedMilliseconds < 1000)
                {
                    Thread.Sleep(1);
                }
            }).ContinueWith(t => Dispatcher.Invoke(() => msggrid.BeginStoryboard(autohide)));
        }

        private void Button_MouseLeftButtonDown_1(object sender, EventArgs e)
        {
            Hide();
            // hide
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // clean cache and random img
            if (e.RightButton == MouseButtonState.Pressed)
            {
                log.Log("Switch to random image by pressing right button");
                RandomImg();
            }
            else
            {
                NextImg();
            }
        }

        private void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // toggle detail info
            e.Handled = true;
            DetailInfo.Visibility = Visibility.Visible;
        }

        private void Button_MouseLeftButtonDown_2(object sender, EventArgs e)
        {
            DetailInfo.Visibility = Visibility.Collapsed;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var suki = cfg.Collections.Find(x => x.Name.ToLower() == "sukidesu");
            if (suki.Images.Contains(curbmp.MapsetId))
            {
                PushNotification($"{curbmp.Title} 已经在收藏品里面了:(");
                return;
            }
            suki.Images.Add(curbmp.MapsetId);
            cfg.Save();
            PushNotification($"已将 {curbmp.Title} 加入收藏品:)");
            RefreshCollections();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            SettingsFlyout.Visibility = Visibility.Collapsed;
        }

        private void ScrollViewer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var sv = ((ScrollViewer)sender);
            e.Handled = true;
            sv.ScrollToVerticalOffset(sv.ContentVerticalOffset + (double)e.Delta / 10);
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            PushNotification($"通知!现在时间是{DateTime.Now}");
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            bool finished = false;
            PushNotification("正在后台重建缓存...", double.MaxValue, (_) => finished);
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Task.Run(() => cfg.RebuildImageCache()).ContinueWith(t => Dispatcher.Invoke(() => { finished = true; PushNotification($"重建完毕 耗时:{sw.ElapsedMilliseconds}ms 加载了{cfg.Caches.Count}张"); RandomImg(); sw.Stop(); }));
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            cfg.LastSongsFolderModifiyTime = DateTime.MinValue;
            cfg.Caches.Clear();
            stoptimer = true;
            bool finished = false;
            PushNotification("正在后台重建缓存...", double.MaxValue, (_) => finished);
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Task.Run(() => cfg.RebuildImageCache()).ContinueWith(t => Dispatcher.Invoke(() => { finished = true; PushNotification($"重建完毕 耗时:{sw.ElapsedMilliseconds}ms 加载了{cfg.Caches.Count}张"); stoptimer=false; RandomImg(); sw.Stop(); }));
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            cfg.Save();
        }

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            var count = cfg.DislikedImage.Count;
            cfg.DislikedImage.Clear();
            cfg.Save();
            PushNotification($"清除了{count}张不喜欢的图片");
        }

        private void Button_Click_7(object sender, RoutedEventArgs e)
        {
            var count = cfg.Collections.Count;
            cfg.Collections.ForEach(x => x.Images.Clear());
            PushNotification($"清空了{count}个收藏夹");
        }

        private void Button_Click_8(object sender, RoutedEventArgs e)
        {
            cfg.DeleteConfig();
            PushNotification($"删除了配置文件，请重启");
        }

        private void Button_Click_9(object sender, RoutedEventArgs e)
        {
            Process.Start(cfg.ConfigPath);
        }

        private void Button_Click_10(object sender, RoutedEventArgs e)
        {
            cfg.SaveImmediately();
            Extensions.FastQuit();
        }

        private void Button_Click_11(object sender, RoutedEventArgs e)
        {
            var collections = cfg.Collections.Where(x => x.Images.Count == 0 && !(x.Name == "Sukidesu")).ToList();
            var count = collections.Count;
            collections.ForEach(x => cfg.Collections.Remove(x));
            PushNotification($"删除了{count}个空收藏夹");
        }
        private void OpenCollections()
        {
            if (cfg.CollectionPasswordHash != null)
            {
                InputBoxPassword("输入密码打开收藏", "", null, x =>
                {
                    if (cfg.CollectionPasswordHash == x.GetSha256() || cfg.SettingPasswordHash == x.GetSha256())
                    {
                        CollectionFlyout.Visibility = Visibility.Visible;
                        return;
                    }
                    PushNotification(FailedMsg);
                });
                return;
            }
            CollectionFlyout.Visibility = Visibility.Visible;
        }
        private void Button_Click_12(object sender, RoutedEventArgs e)
        {
            OpenCollections();
        }

        private void Button_Click_13(object sender, RoutedEventArgs e)
        {
            CollectionFlyout.Visibility = Visibility.Collapsed;
        }
        private void MakeGridView(IEnumerable<UIElement> elements, Grid grid, int columns)
        {
            Enumerable.Range(0, 10).Select(x => new ColumnDefinition() { Width = new GridLength(0, GridUnitType.Auto) }).ToList().ForEach(x => grid.ColumnDefinitions.Add(x));
            Enumerable.Range(0, elements.Count() / columns + 1).Select(x => new RowDefinition() { Height = new GridLength(0, GridUnitType.Auto) }).ToList().ForEach(x => grid.RowDefinitions.Add(x));
            var i = 0;
            foreach (var item in elements)
            {
                Grid.SetColumn(item, i % columns);
                Grid.SetRow(item, i / columns);
                grid.Children.Add(item);
                i++;
            }
            if (i == 0)
            {
                var tb = new TextBlock();
                tb.Text = "<空>";
                tb.HorizontalAlignment = HorizontalAlignment.Stretch;
                tb.VerticalAlignment = VerticalAlignment.Top;
                grid.Children.Add(tb);
            }
        }
        private void RefreshCollections()
        {
            Collections.Items.Clear();
            Dictionary<Image, string> BackgroundLoadImages = new Dictionary<Image, string>();
            foreach (var collection in cfg.Collections)
            {
                var localizedstr = collection.Name;
                if (localizedstr.ToLower() == "sukidesu")
                    localizedstr = "我喜欢的";
                TabItem colpage = new TabItem() { Header = localizedstr };
                var menu2 = new ContextMenu();
                if (collection.Name.ToLower() != "sukidesu")
                {
                    menu2.Items.Add(new MenuItem()
                    {
                        Header = "移除此收藏夹",
                        Command = Extensions.MakeCommand(_ =>
                        {
                            cfg.Collections.Remove(collection);
                        })
                    });
                    menu2.Items.Add(new MenuItem()
                    {
                        Header = "重命名此收藏夹",
                        Command = Extensions.MakeCommand(_ =>
                        {
                            InputBox("输入新名字", localizedstr, null, y =>
                            {
                                collection.Name = y;
                                colpage.Header = y;
                                cfg.Save();
                            });
                        })
                    });
                }
                menu2.Items.Add(new MenuItem()
                {
                    Header = "清空收藏夹",
                    Command = Extensions.MakeCommand(_ =>
                    {
                        collection.Images.Clear();
                        cfg.Save();
                        RefreshCollections();
                    })
                });
                colpage.ContextMenu = menu2;
                var colpage2 = new ScrollViewer();
                var colpage2_g = new StackPanel();
                colpage2.Content = colpage2_g;
                var colpage2_tb = new TextBox() { MinHeight = 50, HorizontalAlignment = HorizontalAlignment.Stretch };
                colpage2_tb.Text = collection.Description ?? "还没有简介呢，火速写一个";
                colpage2_tb.AcceptsReturn = true;
                colpage2_tb.AcceptsTab = true;
                colpage2_tb.TextChanged += (s, e) => { collection.Description = colpage2_tb.Text; };
                colpage2_g.Children.Add(colpage2_tb);
                var colpage2_gv = new Grid();
                MakeGridView(collection.Images.Select(x => cfg.Caches.Find(y => y.MapsetId == x)).Where(y => y != null).Select(x =>
                {
                    var container = new Grid()
                    {
                        Width = 200,
                        Margin = new Thickness(10),
                    };
                    var img = new Image()
                    {
                        Stretch = Stretch.Uniform,
                    };
                    var cover = new Rectangle()
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Fill = new SolidColorBrush(Colors.Black) { Opacity = .3 },
                        Visibility = Visibility.Collapsed,
                    };
                    var plain = new TextBlock()
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Text = "已删除",
                        Visibility = Visibility.Collapsed,
                    };
                    BackgroundLoadImages.Add(img, x.Beatmaps[0].BgPath);
                    container.Children.Add(img);
                    container.Children.Add(cover);
                    container.Children.Add(plain);
                    var menu = new ContextMenu();
                    img.ContextMenu = menu;
                    menu.Items.Add(new MenuItem()
                    {
                        Header = "移除收藏",
                        Command = Extensions.MakeCommand(__ =>
                        {
                            cover.Visibility = Visibility.Visible;
                            plain.Visibility = Visibility.Visible;
                            collection.Images.Remove(x.MapsetId);
                            log.Log($"Removed {x.Name} from {collection.Name}");
                        })
                    });
                    img.MouseDown += (s, e) =>
                    {
                        if (e.LeftButton != MouseButtonState.Pressed)
                            return;
                        CollectionsBack.Effect = new BlurEffect();
                        Dictionary<Image, string> BackgroundLoadImages2 = new Dictionary<Image, string>();
                        CollectionDetailView.Visibility = Visibility.Visible;
                        CollectionDetailViewImage.Source = x.Beatmaps[0].BgPath.LoadImage();
                        CollectionDetailViewInfo.Inlines.Clear();
                        var span = new Span();
                        span.Inlines.Add($"{x.Beatmaps[0].Artist} - {x.Beatmaps[0].Title}\n");
                        if (!string.IsNullOrWhiteSpace(x.Beatmaps[0].SongPath) && bam != null)
                        {
                            var hl2 = new Hyperlink(new Run("点我播放 "));
                            var hl3 = new Hyperlink(new Run("点我停止\n"));
                            var dt = new DispatcherTimer();
                            dt.Interval = TimeSpan.FromMilliseconds(1);
                            var progress = new Slider();
                            progress.Width = 200;
                            progress.Maximum = 0;
                            progress.Value = 0;
                            progress.TickFrequency = 10;
                            progress.Visibility = Visibility.Collapsed;
                            progress.TickPlacement = System.Windows.Controls.Primitives.TickPlacement.BottomRight;
                            progress.ValueChanged += (_, __) =>
                            {
                                if (audiostream != null && Math.Abs(audiostream.Current.TotalSeconds - progress.Value) > 0.1)
                                {
                                    var p = TimeSpan.FromSeconds(progress.Value);
                                    if (p < audiostream.Duration)
                                        audiostream.Current = p;
                                }
                            };
                            progress.Unloaded += (_, __) => dt.Stop();
                            dt.Tick += (_, __) => { if (audiostream != null) progress.Value = audiostream.Current.TotalSeconds; };
                            dt.Start();
                            TextBlock.SetBaselineOffset(progress, 14);
                            hl2.Click += (_, __) =>
                            {
                                if (audiostream != null)
                                {
                                    var as_ = audiostream;
                                    audiostream = null;
                                    as_.Stop();
                                }
                                np = x.Beatmaps[0];
                                var channel = bam.Load(File.OpenRead(x.Beatmaps[0].SongPath));
                                audiostream = channel;
                                audiostream.Volume = cfg.Volume / 100;
                                audiostream.Play();
                                progress.Maximum = Math.Round(audiostream.Duration.TotalSeconds);
                                audiostream.Current = TimeSpan.FromMilliseconds(x.Beatmaps[0].PreviewPoint == -1 || x.Beatmaps[0].PreviewPoint == 0 ? audiostream.Duration.TotalMilliseconds / 3 : x.Beatmaps[0].PreviewPoint);
                                progress.Visibility = Visibility.Visible;
                                hl3.IsEnabled = true;
                            };
                            hl3.Click += (_, __) =>
                            {
                                var as_ = audiostream;
                                audiostream = null;
                                as_.Stop();
                                np = null;
                            };
                            hl3.IsEnabled = false;
                            span.Inlines.Add(hl2);
                            span.Inlines.Add(hl3);
                            span.Inlines.Add(progress);
                            span.Inlines.Add(new Run("\n"));
                        }
                        var hl4 = new Hyperlink(new Run("切换到这一张\n"));
                        hl4.Click += (_, __) =>
                        {
                            cursor = cfg.Caches.IndexOf(x);
                            LoadImg();
                        };
                        span.Inlines.Add(hl4);
                        var hl5 = new Hyperlink(new Run("切换到这一张并额外保持10分钟"));
                        hl5.Click += (_, __) =>
                        {
                            cursor = cfg.Caches.IndexOf(x);
                            breaktimer = true;
                            breakduration = TimeSpan.FromMinutes(10);
                            LoadImg();
                        };
                        span.Inlines.Add(hl5);
                        CollectionDetailViewInfo.Inlines.Add(span);
                        CollectionDetailViewOtherImages.Children.Clear();
                        RemoveCollectionButton.Command = Extensions.MakeCommand(_ =>
                        {
                            cover.Visibility = Visibility.Visible;
                            plain.Visibility = Visibility.Visible;
                            collection.Images.Remove(x.MapsetId);
                            log.Log($"Removed {x.Name} from {collection.Name}");
                            Button_Click_17(null, null);
                        });
                        OpenCollectionPictureButton.Command = Extensions.MakeCommand(_ =>
                        {
                            var bi = CollectionDetailViewImage.Source as BitmapImage;
                            if (bi != null)
                            {
                                Process.Start(bi.GetImageSourcePath());
                            }
                        });
                        foreach (var bmp in x.Beatmaps)
                        {
                            var img3 = new Image();
                            img3.Height = 80;
                            img3.Margin = new Thickness(3);
                            img3.MouseDown += (_, __) =>
                            {
                                CollectionDetailViewImage.Source = bmp.BgPath.LoadImage();
                            };
                            BackgroundLoadImages2.Add(img3, bmp.BgPath);
                            CollectionDetailViewOtherImages.Children.Add(img3);
                        }
                        Task.Run(() =>
                        {
                            foreach (var img2 in BackgroundLoadImages2)
                            {
                                using (var fs = File.OpenRead(img2.Value))
                                {
                                    var imgsource = new BitmapImage();
                                    imgsource.BeginInit();
                                    imgsource.DecodePixelHeight = 80;
                                    imgsource.CacheOption = BitmapCacheOption.OnLoad;
                                    imgsource.StreamSource = fs;
                                    imgsource.EndInit();
                                    imgsource.Freeze();
                                    img2.Key.Dispatcher.Invoke(() => { img2.Key.Source = imgsource; });
                                }
                            }
                        });
                    };
                    return container;
                }), colpage2_gv, 3);
                colpage2_g.Children.Add(colpage2_gv);
                colpage.Content = colpage2;
                Collections.Items.Add(colpage);
            }
            TabItem dislikepage = new TabItem() { Header = "回收站" };
            var dislikepage2 = new ScrollViewer();
            var dislikepage2_g = new StackPanel();
            dislikepage2.Content = dislikepage2_g;
            dislikepage2_g.Children.Add(new TextBlock() { Text = "欢迎来到回收站\n下面会显示你之前加入过不喜欢的图片,单击还原" });
            var dislikepage2_gv = new Grid();
            MakeGridView(cfg.DislikedImage.Select(x => cfg.Caches.Find(y => y.MapsetId == x)).Where(y => y != null).Select(x =>
            {
                var container = new Grid()
                {
                    Width = 200,
                    Margin = new Thickness(10),
                };
                var img = new Image()
                {
                    Stretch = Stretch.Uniform,
                };
                BackgroundLoadImages.Add(img, x.Beatmaps[0].BgPath);
                var cover = new Rectangle()
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Fill = new SolidColorBrush(Colors.Black) { Opacity = .3 },
                    Visibility = Visibility.Collapsed,
                };
                var plain = new TextBlock()
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = "已删除",
                    Visibility = Visibility.Collapsed,
                };
                container.Children.Add(img);
                container.Children.Add(cover);
                container.Children.Add(plain);
                img.MouseDown += (s, e) =>
                {
                    cfg.DislikedImage.Remove(x.MapsetId);
                    cover.Visibility = Visibility.Visible;
                    plain.Visibility = Visibility.Visible;
                };
                return container;
            }), dislikepage2_gv, 3);
            dislikepage2_g.Children.Add(dislikepage2_gv);
            dislikepage.Content = dislikepage2;
            Collections.Items.Add(dislikepage);
            Collections.SelectedIndex = 0;
            Task.Run(() =>
            {
                foreach (var img in BackgroundLoadImages)
                {
                    using (var fs = File.OpenRead(img.Value))
                    {
                        var imgsource = new BitmapImage();
                        imgsource.BeginInit();
                        imgsource.DecodePixelWidth = 200;
                        imgsource.CacheOption = BitmapCacheOption.OnLoad;
                        imgsource.StreamSource = fs;
                        imgsource.EndInit();
                        imgsource.Freeze();
                        img.Key.Dispatcher.Invoke(() => { img.Key.Source = imgsource; });
                    }
                }
            });
        }

        private void Button_Click_14(object sender, RoutedEventArgs e)
        {
            if (cfg.DislikedImage.Contains(curbmp.MapsetId))
            {
                PushNotification("图片已经不喜欢过了!");
                return;
            }
            PushNotification("将不再显示该图片,切换到下一张图片\n如果误操作,请在收藏撤销");
            log.Log($"Disliked {curbmp.Title}.({curbmp.MapsetId}:{curbmp.Id})");
            cfg.DislikedImage.Add(curbmp.MapsetId);
            NextImg();
            cfg.Save(); // save
            RefreshCollections();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                PrevImg();
            }
            else if (e.Key == Key.Right || e.Key == Key.Space)
            {
                NextImg();
            }
        }

        private void Button_Click_15(object sender, RoutedEventArgs e)
        {
            RefreshCollections();
        }

        private void Button_Click_16(object sender, RoutedEventArgs e)
        {
            InputBox("输入新的收藏夹名", "", null, x =>
            {
                if (!string.IsNullOrWhiteSpace(x))
                {
                    cfg.Collections.Add(new GalleryCollection() { Name = x });
                    RefreshCollections();
                }
            });
        }

        private void Button_Click_17(object sender, RoutedEventArgs e)
        {
            CollectionsBack.Effect = null;
            CollectionDetailView.Visibility = Visibility.Collapsed;
        }

        private void CollectionDetailViewImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            FullViewImg.Source = CollectionDetailViewImage.Source;
            FullViewScrollViewer.Visibility = Visibility.Visible;
        }

        private void FullViewScrollViewer_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            FullViewScrollViewer.Visibility = Visibility.Collapsed;
        }

        private void FullViewScrollViewer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keys.ControlKey.IsPressed())
            {
                e.Handled = true;
                var orig = FullViewImgScale.ScaleX;
                FullViewImgScale.ScaleY = FullViewImgScale.ScaleX = FullViewImgScale.ScaleX + (double)e.Delta / 10000;
                var now = FullViewImgScale.ScaleX;
                // 中间点
                /*
                FullViewScrollViewer.ScrollToVerticalOffset((FullViewScrollViewer.VerticalOffset + FullViewScrollViewer.ActualHeight / 2) / orig * now - FullViewScrollViewer.ActualHeight / 2);
                FullViewScrollViewer.ScrollToHorizontalOffset((FullViewScrollViewer.HorizontalOffset + FullViewScrollViewer.ActualWidth / 2) / orig * now - FullViewScrollViewer.ActualWidth / 2);
                */
                var originp = e.GetPosition(FullViewScrollViewer);
                FullViewScrollViewer.ScrollToVerticalOffset((FullViewScrollViewer.VerticalOffset + originp.Y) / orig * now - originp.Y);
                FullViewScrollViewer.ScrollToHorizontalOffset((FullViewScrollViewer.HorizontalOffset + originp.X) / orig * now - originp.X);
            }
        }

        private void Button_Click_18(object sender, RoutedEventArgs e)
        {
            if (audiostream != null && audiostream.Playing)
            {
                audiostream.Stop();
                audiostream = null;
                np = null;
            }
        }

        private void Button_Click_19(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(cfg.SettingPasswordHash))
            {
                InputBoxPassword("输入原密码以重设密码", "", null, (x) =>
                {
                    if (cfg.SettingPasswordHash != x.GetSha256() && string.IsNullOrWhiteSpace(x))
                    {
                        PushNotification(FailedMsg);
                        return;
                    }
                    Extensions.RunLater(ChangeSettingPassword);
                });
                return;
            }
            ChangeSettingPassword();
        }

        private void ChangeSettingPassword()
        {
            InputBoxPassword("输入新密码", "", null, (x) =>
            {
                log.Log("Settings password reset!");
                cfg.SettingPasswordHash = x.GetSha256();
                cfg.Save();
                PushNotification("设置密码已更改");
            });
        }
        private void ChangeCollectionPassword()
        {
            InputBoxPassword("输入新密码", "", null, (x) =>
            {
                log.Log("Collection password reset!");
                cfg.CollectionPasswordHash = x.GetSha256();
                cfg.Save();
                PushNotification("收藏密码已更改");
            });
        }

        private void Button_Click_20(object sender, RoutedEventArgs e)
        {
            if (cfg.CollectionPasswordHash != null || cfg.SettingPasswordHash != null)
            {
                InputBoxPassword("输入原密码或设置密码以重设密码", "", null, (x) =>
                {
                    if (cfg.SettingPasswordHash != x.GetSha256() && cfg.CollectionPasswordHash != x.GetSha256() && string.IsNullOrWhiteSpace(x))
                    {
                        PushNotification(FailedMsg);
                        return;
                    }
                    Extensions.RunLater(ChangeCollectionPassword);
                });
                return;
            }
            ChangeCollectionPassword();
        }

        private void Button_Click_21(object sender, RoutedEventArgs e)
        {

        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/telecomadm1145");
        }

        private void Hyperlink_Click_1(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/telecomadm1145/osuscreenprotector");
        }
        private void InputBox(string message, string prompt, Action OnCanceled = null, Action<string> OnInputCompleted = null)
        {
            ComboBoxInput.Visibility = Visibility.Collapsed;
            PasswordBoxInput.Visibility = Visibility.Collapsed;
            InputBoxFlyout.Visibility = Visibility.Visible;
            CloseButton.Command = Extensions.MakeCommand(_ =>
            {
                if (OnCanceled != null)
                    OnCanceled();
                InputBoxFlyout.Visibility = Visibility.Collapsed;
            });
            InputBoxDesc.Text = message;
            InputBoxInput.Visibility = Visibility.Visible;
            InputBoxInput.Text = prompt;
            InputBoxInput.Focus();
            SubmitButton.Command = Extensions.MakeCommand(_ =>
            {
                if (OnInputCompleted != null)
                    OnInputCompleted(InputBoxInput.Text);
                InputBoxFlyout.Visibility = Visibility.Collapsed;
            });
        }
        private void InputBoxPassword(string message, string prompt, Action OnCanceled = null, Action<string> OnInputCompleted = null)
        {
            ComboBoxInput.Visibility = Visibility.Collapsed;
            InputBoxInput.Visibility = Visibility.Collapsed;
            InputBoxFlyout.Visibility = Visibility.Visible;
            CloseButton.Command = Extensions.MakeCommand(_ =>
            {
                if (OnCanceled != null)
                    OnCanceled();
                InputBoxFlyout.Visibility = Visibility.Collapsed;
            });
            InputBoxDesc.Text = message;
            PasswordBoxInput.Visibility = Visibility.Visible;
            PasswordBoxInput.Password = prompt;
            PasswordBoxInput.Focus();
            SubmitButton.Command = Extensions.MakeCommand(_ =>
            {
                if (OnInputCompleted != null)
                    OnInputCompleted(PasswordBoxInput.Password);
                InputBoxFlyout.Visibility = Visibility.Collapsed;
            });
        }
        private void MessageBox(string message, Action OnCanceled = null, Action OnInputCompleted = null)
        {
            ComboBoxInput.Visibility = Visibility.Collapsed;
            PasswordBoxInput.Visibility = Visibility.Collapsed;
            InputBoxInput.Visibility = Visibility.Collapsed;
            InputBoxFlyout.Visibility = Visibility.Visible;
            CloseButton.Command = Extensions.MakeCommand(_ =>
            {
                if (OnCanceled != null)
                    OnCanceled();
                InputBoxFlyout.Visibility = Visibility.Collapsed;
            });
            InputBoxDesc.Text = message;
            SubmitButton.Focus();
            SubmitButton.Command = Extensions.MakeCommand(_ =>
            {
                if (OnInputCompleted != null)
                    OnInputCompleted();
                InputBoxFlyout.Visibility = Visibility.Collapsed;
            });
        }
        private void SelectionInput<T>(string message,List<T> selections,T defaultitem = null, Action OnCanceled = null, Action<T> OnInputCompleted = null) where T :class
        {
            PasswordBoxInput.Visibility = Visibility.Collapsed;
            InputBoxInput.Visibility = Visibility.Collapsed;
            InputBoxFlyout.Visibility = Visibility.Visible;
            CloseButton.Command = Extensions.MakeCommand(_ =>
            {
                if (OnCanceled != null)
                    OnCanceled();
                InputBoxFlyout.Visibility = Visibility.Collapsed;
            });
            ComboBoxInput.Visibility = Visibility.Visible;
            InputBoxDesc.Text = message;
            ComboBoxInput.Focus();
            ComboBoxInput.ItemsSource = selections;
            if (selections.Count > 0)
            {
                ComboBoxInput.SelectedItem = defaultitem;
                if (defaultitem == null)
                {
                    ComboBoxInput.SelectedIndex = 0;
                }
            }
            SubmitButton.Command = Extensions.MakeCommand(_ =>
            {
                if (ComboBoxInput.SelectedItem == null)
                    return;
                if (OnInputCompleted != null)
                    OnInputCompleted((T)ComboBoxInput.SelectedItem);
                InputBoxFlyout.Visibility = Visibility.Collapsed;
            });
        }

        private void Button_Click_22(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_23(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_24(object sender, RoutedEventArgs e)
        {
            var rank = cfg.Ranks.FirstOrDefault(x => x.MapsetId == curbmp.MapsetId);
            if (rank == null)
            {
                PushNotification("错误：无法读取权重数据,请在设置中重建缓存");
                return;
            }
            rank.Rank -= 30;
#if DEBUG
            PushNotification($"已提高 {curbmp.Title} 的权重到 {rank.GetRelativeRank()}");
#else
            PushNotification("已喜欢该图片,将增加显示该图片的概率");
#endif
        }

        private void Button_Click_25(object sender, RoutedEventArgs e)
        {
            var rank = cfg.Ranks.FirstOrDefault(x => x.MapsetId == curbmp.MapsetId);
            if (rank == null)
            {
                PushNotification("错误：无法读取权重数据,请在设置中重建缓存");
                return;
            }
            rank.Rank += 20;
#if DEBUG
            PushNotification($"已降低 {curbmp.Title} 的权重到 {rank.GetRelativeRank()}");
#else
            PushNotification("已切换到下一张图片，将减少显示此图片的概率");
            RandomImg();
#endif
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            SelectionInput("选择要加入的收藏夹", cfg.Collections,cfg.Collections.First(x=>string.Compare(x.Name,"sukidesu",true) == 0), null, x => {
                x.Images.Add(curbmp.MapsetId);
                PushNotification($"已将 {curbmp.Title} 加入 {x.Name} 收藏夹");
                RefreshCollections();
            });
        }

        private void Button_Click_26(object sender, RoutedEventArgs e)
        {
            MessageBox($"权重数据:\r\n{string.Join("\r\n",cfg.Ranks.OrderBy(x=>x.GetRelativeRank()).Select(x=> $"{cfg.Caches.Find(y=> y.MapsetId ==x.MapsetId).Name}({x.MapsetId}) : {x.GetRelativeRank()}(Real:{x.Rank})"))}");
        }

        private void Button_Click_27(object sender, RoutedEventArgs e)
        {
            MessageBox($"缓存数据:\r\n最近更新: {cfg.LastSongsFolderModifiyTime}\r\n缓存条数: {cfg.Caches.Count}\r\n{string.Join("\r\n", cfg.Caches.Where(x=>x.Beatmaps.Count >0).Select(x => $"{x.Beatmaps.FirstOrDefault().Artist} - {x.Name} : {x.Dir}"))}");
        }
        private bool nofitifactiontolog = true;
        private void Button_Click_28(object sender, RoutedEventArgs e)
        {
            log.LogHook += (s, e2) => {
                Dispatcher.BeginInvoke((Action)(() => {
                    PushNotification(e2.Trim(), 3000, null);
                }));
            };
            nofitifactiontolog = false; 
            log.Log("已将日志重定向到通知");
        }
    }
}
