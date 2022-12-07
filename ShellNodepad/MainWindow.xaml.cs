using Microsoft.Web.WebView2.Core;
using ShellNodepad.util;
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
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
            Event = new InnerEvent(this);
            string wt = SettingUtil.GetSetting("wt"), wl = SettingUtil.GetSetting("wl");
            string ww = SettingUtil.GetSetting("ww", "400"), wh = SettingUtil.GetSetting("wh", "650");
            if (!string.IsNullOrWhiteSpace(wt) && !string.IsNullOrWhiteSpace(wl))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Top = double.Parse(wt);
                Left = double.Parse(wl);
                Height = double.Parse(wh);
                Width = double.Parse(ww);
            }
            else
            {
                Left = SystemParameters.PrimaryScreenWidth / 2 - ActualWidth / 2;
                Top = SystemParameters.PrimaryScreenHeight / 2 - ActualHeight / 2;
            }
        }
        private void MoveWindow(object sender, MouseButtonEventArgs e)
        {
            Event.Stop = true;
            Event.Clear();
            this.ResizeMode = ResizeMode.NoResize;
            this.DragMove();
            this.ResizeMode = ResizeMode.CanResize;
            Event.UpdatePos();
            Event.Stop = false;
            Event.OnMouseLeave();
            MaxHeight = SystemParameters.WorkArea.Height;
            MaxWidth = SystemParameters.WorkArea.Width;
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
                WebView.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;
                WebView.CoreWebView2.WebResourceRequested += CustomResourceResponse;
                WebView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                WebView.CoreWebView2.AddHostObjectToScript("_PC", Event);
                WebView.CoreWebView2.Navigate("file:///index.html");
#if !DEBUG
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

        private async void CoreWebView2_DOMContentLoaded(object? sender, CoreWebView2DOMContentLoadedEventArgs e)
        {
            await WebView.ExecuteScriptAsync("document.onmouseenter = function(){chrome.webview.hostObjects.sync._PC.onMouseEnter();}");
            await WebView.ExecuteScriptAsync("document.onmouseleave = function(){chrome.webview.hostObjects.sync._PC.onMouseLeave();}");
            Event.UpdatePos();
            Event.OnMouseLeave(2000);
        }

        private void BeforeClose(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SettingUtil.SetSetting("wt", this.Top + "");
            SettingUtil.SetSetting("wl", this.Left + "");
            SettingUtil.SetSetting("wh", this.ActualHeight + "");
            SettingUtil.SetSetting("ww", this.ActualWidth + "");
        }

        private void OnClosed(object sender, EventArgs e)
        {
            App.Current.Shutdown();
        }


        private void CloseWindow(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }

        private void OnLocationChanged(object sender, EventArgs e)
        {
        }

        public class InnerEvent
        {
            private Graphics currentGraphics = Graphics.FromHwnd(new WindowInteropHelper(Application.Current.MainWindow).Handle);
            private MainWindow mw;
            private bool dock;
            private bool isHidden;
            private bool isTop;
            private Timer? timer;
            private int triggerWeight = 15;
            private int marginBorder = 2;
            private int amTime = 350;
            private Storyboard? storyboard;

            public bool Stop { get; internal set; }

            public InnerEvent(MainWindow mw)
            {
                this.mw = mw;
                this.mw.MouseEnter += (a, e) => OnMouseEnter();
                this.mw.MouseLeave += (a, e) => OnMouseLeave();
            }

            private void ToggleVisible()
            {
                mw.Dispatcher.Invoke(() =>
                {
                    if (isHidden)
                    {
                        if (isTop)
                        {
                            mw.MiniMode.Width = 150;
                            mw.MiniMode.Height = mw.WebView.ActualHeight;
                        }
                        else
                        {
                            mw.MiniMode.Height = 150;
                            mw.MiniMode.Width = mw.WebView.ActualWidth;
                        }
                        mw.MiniMode.CornerRadius = new CornerRadius(20);
                        mw.MiniMode.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        mw.MiniMode.Visibility = Visibility.Collapsed;
                    }
                });
            }

            public void OnMouseEnter()
            {
                if (Stop) return;
                if (!dock) return;
                if (!isHidden) return;
                isHidden = false;
                mw.Dispatcher.Invoke(() =>
                {
                    Clear();
                    ToggleVisible();
                    if (isTop) Move("Top", marginBorder);
                    else Move("Left", SystemParameters.PrimaryScreenWidth - mw.Width - marginBorder);
                });
            }

            public void OnMouseLeave(int? timeOut = null)
            {
                if (Stop) return;
                timer = new Timer();
                timer.AutoReset = false;
                timer.Elapsed += (a, e) =>
                {
                    if (!dock) return;
                    if (isHidden) return;
                    if (IsInWindow()) return;
                    isHidden = true;
                    mw.Dispatcher.Invoke(() =>
                    {
                        Clear();
                        if (isTop) Move("Top", triggerWeight - mw.Height, ToggleVisible);
                        else Move("Left", SystemParameters.PrimaryScreenWidth - triggerWeight, ToggleVisible);
                    });
                };
                timer.Interval = timeOut ?? 600;
                timer.Start();
            }

            /// <summary>
            /// 验证当前鼠标是否仍然悬停在窗口范围内
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
                    if (MousePositionX < mw.Left ||
                        MousePositionY < mw.Top ||
                        MousePositionX > mw.Left + mw.Width ||
                        MousePositionY > mw.Top + mw.Height)
                    {
                        return false;
                    }
                    return true;
                });
            }

            /// <summary>
            /// 移动到指定位置
            /// </summary>
            /// <param name="prop">属性</param>
            /// <param name="value">值</param>
            /// <param name="localAmTime">运动时间</param>
            private void Move(string prop, double value, Action onComplete = null)
            {
                mw.Dispatcher.Invoke(() =>
                {
                    if (storyboard != null)
                    {
                        storyboard.Stop();
                    }
                    storyboard = new();
                    {
                        DoubleAnimationUsingKeyFrames doubleAnimation = new DoubleAnimationUsingKeyFrames();
                        doubleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame()
                        {
                            Value = value,
                            KeyTime = TimeSpan.FromMilliseconds(amTime)
                        });
                        Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath($"(Window.{prop})"));
                        Storyboard.SetTarget(doubleAnimation, mw);
                        storyboard.Children.Add(doubleAnimation);
                    }
                    Storyboard.SetTarget(storyboard, mw);
                    storyboard.Completed += (a, e) =>
                    {
                        if (onComplete != null) onComplete();
                    };
                    storyboard.Begin();

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
                    if (storyboard != null) storyboard.Stop();
                    if (timer != null)
                    {
                        timer.Stop();
                        timer.Dispose();
                    }
                });
            }

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
                        mw.Top = marginBorder;
                    }
                    else if (IsToRight())
                    {
                        dock = true;
                        isTop = false;
                        mw.Left = SystemParameters.PrimaryScreenWidth - mw.Width - marginBorder;
                    }
                    else
                    {
                        dock = false;
                    }
                });
            }
        }
    }
}
