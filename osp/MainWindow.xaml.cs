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
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
                    SettingsFlyout.Visibility = Visibility.Visible;
                    Show();
                    Activate();
                }),
                new System.Windows.Forms.MenuItem("不显示托盘图标", (s, e) =>
                {
                    cfg.ShowTray = ni.Visible=false;
                    cfg.Save();
                })
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
            var hotkey = new Thread(() => { 
                while(true)
                {
                    if ((Key.LeftCtrl.IsPressed() || Key.RightCtrl.IsPressed()) && Key.Tab.IsPressed() && Key.S.IsPressed())
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
        }
        private static bool HasKeyPressed()
        {
            return Enum.GetValues(typeof(Key)).Cast<Key>().Where(x => (int)x != 0 && (int)x != 1).Any(x =>x.IsPressed());
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
                    lastaction = elapsed;
                }

                if ((elapsed - lastaction).TotalMinutes > cfg.AutoOpenMin)
                {
                    log.Log($"Automatic shows atfer {(elapsed - lastaction).TotalSeconds}s.(config:{cfg.AutoOpenMin})");
                    Dispatcher.Invoke(() => { Show(); Activate(); });
                    lastaction = elapsed;
                }
                if ((elapsed - lastnext).TotalMinutes > cfg.AutoNextMin)
                {
                    log.Log($"Switch to next image after {(elapsed - lastnext).TotalSeconds}s.(config:{cfg.AutoNextMin})");
                    Dispatcher.Invoke(NextImg);
                    lastnext = elapsed;
                }
                log.Flush();
                Thread.Sleep(1000);
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
        private Config.Beatmap curbmp;
        private void LoadImg(bool next = true)
        {
        reload:
            log.Log("loading img.");
            try
            {
                Config.Beatmap beatmap = cfg.Caches[cursor].Beatmaps.Random();
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
                Bg.Source = new BitmapImage(new Uri(beatmap.BgPath));
                DetailInfoBox.Inlines.Clear();
                var span = new Span();
                span.Inlines.Add(new Run($"{beatmap.Artist} - {beatmap.Title}") { FontSize = 18 });
                span.Inlines.Add(new Run($" (Mapset {beatmap.MapsetId};Map {beatmap.Id})\n"));
                var hl = new Hyperlink(new Run("点我查看图片\n"));
                hl.Click += (s, e) => { Topmost = false; Process.Start(beatmap.BgPath); };
                hl.ToolTip = beatmap.BgPath;
                span.Inlines.Add(hl);
                var hl2 = new Hyperlink(new Run("点我播放(小声)\n"));
                var hl3 = new Hyperlink(new Run("点我停止\n"));
                hl2.Click += (s, e) =>
                {

                };
                var hl4 = new Hyperlink(new Run("点我打开音频\n"));
                hl4.Click += (s, e) => { Topmost = false; Process.Start(beatmap.SongPath); };
                hl4.ToolTip = beatmap.SongPath;
                span.Inlines.Add(hl4);
                DetailInfoBox.Inlines.Add(span);
                log.Log($"已加载{beatmap.BgPath}({beatmap.Artist} - {beatmap.Title}(Id:{beatmap.Id}))");
                curbmp = beatmap;
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
        }
        private void NextImg()
        {
            log.Log("");
            cursor++;
            if (cursor >= cfg.Caches.Count)
                cursor = 0;
            LoadImg();
        }
        private void PrevImg()
        {
            log.Log("");
            cursor--;
            if (cursor < 0)
                cursor = cfg.Caches.Count;
            LoadImg(false);
        }
        private void RandomImg()
        {
            cursor = random.Next(0, cfg.Caches.Count);
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
            SettingsFlyout.Visibility = Visibility.Visible;
            // settings
        }
        private void PushNotification(string msg, double delay = 5000, Func<TimeSpan, bool> CanClose = null)
        {
            Logger.Instance.Log(msg);
            var msggrid = new Grid() { Width = 300, MinHeight = 50, Margin = new Thickness(5) };
            var border = new Border()
            {
                Background = new SolidColorBrush(Colors.Black) { Opacity = .5 },
                CornerRadius = new CornerRadius(10),
                BorderBrush = new SolidColorBrush(Colors.Black) { Opacity = .7 },
                BorderThickness = new Thickness(.5),
            };
            msggrid.Children.Add(border);
            msggrid.Children.Add(new TextBlock() { Text = msg, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10), Foreground = new SolidColorBrush(Colors.White) { Opacity = .7 } });
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
            if (e.LeftButton == MouseButtonState.Pressed && e.RightButton == MouseButtonState.Pressed)
            {
                log.Log("Switch to random image by pressing left and right");
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
            PushNotification($"已将{curbmp.Title}加入收藏品:)");
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
            bool finished = false;
            PushNotification("正在后台重建缓存...", double.MaxValue, (_) => finished);
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Task.Run(() => cfg.RebuildImageCache()).ContinueWith(t => Dispatcher.Invoke(() => { finished = true; PushNotification($"重建完毕 耗时:{sw.ElapsedMilliseconds}ms 加载了{cfg.Caches.Count}张"); RandomImg(); sw.Stop(); }));
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
            cfg.Save();
            Environment.Exit(0);
        }

        private void Button_Click_11(object sender, RoutedEventArgs e)
        {
            var collections = cfg.Collections.Where(x => x.Images.Count == 0 && !(x.Name == "Sukidesu")).ToList();
            var count = collections.Count;
            collections.ForEach(x => cfg.Collections.Remove(x));
            PushNotification($"删除了{count}个空收藏夹");
        }

        private void Button_Click_12(object sender, RoutedEventArgs e)
        {
            CollectionFlyout.Visibility = Visibility.Visible;
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
        }
        private void RefreshCollections()
        {
            Collections.Items.Clear();
            Dictionary<Image, string> BackgroundLoadImages = new Dictionary<Image, string>();
            foreach (var collection in cfg.Collections)
            {
                
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
            Task.Run(() => {
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
    }
}
