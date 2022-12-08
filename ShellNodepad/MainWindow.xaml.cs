using Microsoft.Web.WebView2.Core;
using ShellNodepad.util;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Application = System.Windows.Application;
using Path = System.IO.Path;

namespace ShellNodepad
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private InnerEvent Event;

        public MainWindow()
        {
            InitializeComponent();
            string wt = SettingUtil.GetSetting("wt"), wl = SettingUtil.GetSetting("wl");
            string ww = SettingUtil.GetSetting("ww", "400"), wh = SettingUtil.GetSetting("wh", "650");
            if (!string.IsNullOrWhiteSpace(wt) && !string.IsNullOrWhiteSpace(wl))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Height = double.Parse(wh);
                Width = double.Parse(ww);
                Top = double.Parse(wt);
                Left = double.Parse(wl);
            }
            else
            {
                Left = SystemParameters.PrimaryScreenWidth / 2 - ActualWidth / 2;
                Top = SystemParameters.PrimaryScreenHeight / 2 - ActualHeight / 2;
            }
            Event = new InnerEvent(this);
            MaxWidth = 1199;
            MaxHeight = 799;
        }

        private async void ViewLoaded(object sender, RoutedEventArgs e)
        {
            WebView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
            string dataFolder = Path.Combine(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%"), "quick_nodepad");
            if (!Directory.Exists(dataFolder))
            {
                Directory.CreateDirectory(dataFolder);
            }
            var env = CoreWebView2Environment.CreateAsync(userDataFolder: dataFolder);
            await WebView.EnsureCoreWebView2Async(env.Result);
        }

        private void CustomResourceResponse(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            var uri = e.Request.Uri;
            if (uri.StartsWith("file://"))
            {
                string header = "Content-Type: text/html";
                if (uri.EndsWith(".js"))
                {
                    header = "Content-Type: application/javascript";
                }
                else if (uri.EndsWith(".css"))
                {
                    header = "Content-Type: text/css";
                }
                uri = uri.Substring(7);
                var res = Application.GetResourceStream(new Uri("pack://application:,,,/views" + uri)).Stream;
                CoreWebView2WebResourceResponse response = WebView.CoreWebView2.Environment.CreateWebResourceResponse(res, 200, "OK", header);
                e.Response = response;
            }
        }

        private void WebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                MessageBox.Show("webview2未就绪");
                App.Current.Shutdown();
#if DEBUG
                throw e.InitializationException;
#endif
            }
            else
            {
                WebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
                WebView.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;
                WebView.CoreWebView2.WebResourceRequested += CustomResourceResponse;
                WebView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                WebView.CoreWebView2.AddHostObjectToScript("_PC", Event);
                var url = "file:///index.html";
                WebView.CoreWebView2.Navigate(url);
#if !DEBUG
                WebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                WebView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                WebView.CoreWebView2.Settings.IsGeneralAutofillEnabled = true;
                WebView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                WebView.CoreWebView2.Settings.IsPinchZoomEnabled = false;
                WebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
#endif
            }

        }

        private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            System.Diagnostics.Process.Start("explorer.exe", e.Uri);
            e.Handled = true;
        }

        private async void CoreWebView2_DOMContentLoaded(object? sender, CoreWebView2DOMContentLoadedEventArgs e)
        {
            await WebView.ExecuteScriptAsync("document.onmouseenter = function(){chrome.webview.hostObjects.sync._PC.onMouseEnter();}");
        }

        private void BeforeClose(object sender, System.ComponentModel.CancelEventArgs e)
        { }

        private void OnClosed(object sender, EventArgs e)
        {
            App.Current.Shutdown();
        }


        private void CloseWindow(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            Event.Clear();
            Event.Stop = true;
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            Event.Clear();
            Event.Stop = false;
        }

        public class InnerEvent
        {
            private Graphics currentGraphics = Graphics.FromHwnd(new WindowInteropHelper(Application.Current.MainWindow).Handle);
            private MainWindow mw;
            private bool dock;
            private bool isHidden;
            private bool isTop;
            private Timer? timer;
            private int triggerWeight = 6;
            private int marginBorder = 1;
            private int amTime = 240;
            private int offset = 50;
            private Storyboard? storyboard;

            public bool Stop { get; internal set; }

            public InnerEvent(MainWindow mw)
            {
                this.mw = mw;
                this.mw.MouseLeftButtonDown += MoveWindow;
                this.mw.Loaded += (a, e) => UpdatePos();
                this.mw.SizeChanged += (a, e) =>
                {
                    if (storyboard != null || mw.WindowState == WindowState.Maximized) return;
                    SettingUtil.SetSetting("wh", e.NewSize.Height + "");
                    SettingUtil.SetSetting("ww", e.NewSize.Width + "");
                };
                this.mw.LocationChanged += (a, e) =>
                {
                    if (storyboard != null || mw.WindowState == WindowState.Maximized) return;
                    UpdatePos();
                };
                this.mw.Deactivated += (a, e) => { if (dock) OnLeave(); };
                this.mw.StateChanged += (a, e) =>
                {
                    Debug.WriteLine("状态变更");
                    this.mw.Topmost = true;
                    if (this.mw.WindowState == WindowState.Minimized)
                    {
                        this.mw.WindowState = WindowState.Normal;
                        Clear();
                        if (dock) OnLeave();
                    }
                    else if (this.mw.WindowState == WindowState.Maximized)
                    {
                        return;
                    }
                    else
                    {
                        UpdatePos();
                    }
                };

                this.timer = new Timer();
                this.timer.Interval = 300;
                this.timer.Elapsed += (a, e) =>
                {
                    if (Stop || !dock) return;
                    if (isHidden)
                    {
                        if (IsInWindow()) OnEnter();
                    }
                    else OnLeave();
                };
                this.timer.AutoReset = true;
                this.timer.Start();
            }

            #region 判定和运动
            private void OnLeave()
            {
                mw.Dispatcher.Invoke(() =>
                {
                    if (this.mw.IsActive) return;
                    isHidden = true;
                    Debug.WriteLine("隐藏了");
                    Clear();
                    if (isTop) Move("Top", -1 * mw.Height - offset);
                    else Move("Left", SystemParameters.PrimaryScreenWidth + offset);
                });
            }

            public void OnEnter(bool activte = true)
            {
                if (activte)
                {
                    mw.Dispatcher.Invoke(() =>
                    {
                        this.mw.Activate();
                    });
                }
                if (Stop) return;
                if (!dock) return;
                mw.Dispatcher.Invoke(() =>
                {
                    if (!isHidden) return;
                    isHidden = false;
                    Debug.WriteLine("显示了");
                    Clear();
                    if (isTop) Move("Top", marginBorder);
                    else Move("Left", SystemParameters.PrimaryScreenWidth - mw.Width - marginBorder);
                });
            }

            /// <summary>
            /// 验证当前鼠标是否进入区域
            /// </summary>
            /// <returns></returns>
            private bool IsInWindow()
            {
                return mw.Dispatcher.Invoke(() =>
                {
                    var DpiX = currentGraphics.DpiX / 96;
                    var DpiY = currentGraphics.DpiY / 96;
                    double MousePositionX = System.Windows.Forms.Control.MousePosition.X / DpiX;
                    double MousePositionY = System.Windows.Forms.Control.MousePosition.Y / DpiY;
                    if (MousePositionX + triggerWeight + offset < mw.Left ||
                        MousePositionY + triggerWeight + offset < mw.Top ||
                        MousePositionX - triggerWeight - offset > mw.Left + mw.Width ||
                        MousePositionY - triggerWeight - offset > mw.Top + mw.Height)
                    {
                        return false;
                    }
                    return true;
                });
            }
            private void MoveWindow(object sender, MouseButtonEventArgs e)
            {
                Clear();
                Stop = true;
                mw.ResizeMode = ResizeMode.NoResize;
                mw.DragMove();
                SettingUtil.SetSetting("wt", mw.Top + "");
                SettingUtil.SetSetting("wl", mw.Left + "");
                UpdatePos();
                mw.ResizeMode = ResizeMode.CanResize;
                Stop = false;

            }



            /// <summary>
            /// 移动到指定位置
            /// </summary>
            /// <param name="prop">属性</param>
            /// <param name="value">值</param>
            /// <param name="localAmTime">运动时间</param>
            private void Move(string prop, double value, Action? onComplete = null)
            {
                mw.Dispatcher.Invoke(() =>
                {
                    storyboard = new();
                    {
                        DoubleAnimationUsingKeyFrames doubleAnimation = new DoubleAnimationUsingKeyFrames();
                        doubleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame()
                        {
                            Value = value,
                            KeyTime = TimeSpan.FromMilliseconds(amTime)
                        });
                        doubleAnimation.FillBehavior = FillBehavior.HoldEnd;
                        Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath($"(Window.{prop})"));
                        storyboard.Children.Add(doubleAnimation);
                    }
                    storyboard.Completed += (a, e) =>
                    {
                        Debug.WriteLine($"动画结束{prop},{value}");
                        this.mw.BeginAnimation(Window.LeftProperty, null);
                        this.mw.BeginAnimation(Window.TopProperty, null);
                        this.mw.BeginAnimation(Window.HeightProperty, null);
                        this.mw.BeginAnimation(Window.WidthProperty, null);
                        onComplete?.Invoke();
                        Clear();
                    };
                    storyboard.Begin(mw);
                    Debug.WriteLine($"动画开始{prop},{value}");
                });
            }

            /// <summary>
            /// 验证是否贴靠右侧
            /// </summary>
            /// <returns></returns>
            public bool IsToRight()
            {
                return mw.Dispatcher.Invoke(() =>
                {
                    var ml = SystemParameters.PrimaryScreenWidth - mw.Width - triggerWeight;
                    return mw.Left >= ml;
                });
            }

            /// <summary>
            /// 验证是否贴靠顶部
            /// </summary>
            /// <returns></returns>
            public bool IsToTop()
            {
                return mw.Dispatcher.Invoke(() =>
                {
                    return mw.Top <= triggerWeight;
                });
            }

            /// <summary>
            /// 结束当前运动
            /// </summary>
            public void Clear()
            {
                this.mw.Dispatcher.Invoke(() =>
                {
                    if (storyboard != null)
                    {
                        storyboard.Stop();
                        storyboard.Remove();
                        storyboard = null;
                    };
                });
            }
            #endregion

            #region 更新判定
            /// <summary>
            /// 更新位置并标记贴靠状态
            /// </summary>
            public void UpdatePos()
            {
                mw.Dispatcher.Invoke(() =>
                {
                    if (mw.WindowState == WindowState.Maximized)
                    {
                        dock = false;
                        return;
                    }
                    if (IsToTop())
                    {
                        dock = true;
                        isTop = true;
                        if (!isHidden) mw.Top = marginBorder;
                    }
                    else if (IsToRight())
                    {
                        dock = true;
                        isTop = false;
                        if (!isHidden) mw.Left = SystemParameters.PrimaryScreenWidth - mw.Width - marginBorder;
                    }
                    else
                    {
                        dock = false;
                    }
                });
            }
            #endregion
        }
    }
}
