using Microsoft.Web.WebView2.Core;
using ShellNodepad.util;
using System;
using System.IO;
using System.Timers;
using System.Windows;
using System.Windows.Input;
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

#if WINDOWS7_0_OR_GREATER
            AllowsTransparency = false;
            SelfBorder.Margin = new Thickness(0);
#endif
        }
        private void MoveWindow(object sender, MouseButtonEventArgs e)
        {

            Event.Stop = true;
            Event.Clear();
            this.BeginAnimation(Window.TopProperty, null);
            this.BeginAnimation(Window.LeftProperty, null);
            this.ResizeMode = ResizeMode.NoResize;
            this.DragMove();
            this.ResizeMode = ResizeMode.CanResize;
            Event.UpdatePos();
            Event.Stop = false;
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

        public class InnerEvent
        {
            private MainWindow mw;
            private bool dock;
            private bool isHidden;
            private bool isTop;
            private Timer? timer;
            private int triggerWeight = 12;
            private int marginBorder = 2;
            private int amTime = 350;
            private Storyboard? storyboard;

            public bool Stop { get; internal set; }

            public InnerEvent(MainWindow mw)
            {
                this.mw = mw;
                this.mw.MouseEnter += (a, e) => OnMouseEnter();
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
                mw.Dispatcher.Invoke(async () =>
                {
                    this.mw.Focus();
                    this.mw.Activate();
                    await this.mw.WebView.ExecuteScriptAsync("focusEditor()");
                });
                if (Stop) return;
                if (!dock) return;
                mw.Dispatcher.Invoke(() =>
                {
                    if (!isHidden) return;
                    Clear();
                    //Trace.WriteLine("开始进入");
                    isHidden = false;
                    if (isTop) Move("Top", marginBorder);
                    else Move("Left", SystemParameters.PrimaryScreenWidth - mw.Width - marginBorder);
                });
            }
            public void OnMouseLeave(int? timeOut = null)
            {
                if (Stop) return;
                if (!dock) return;
                timer = new Timer();
                timer.AutoReset = false;
                timer.Elapsed += (a, e) =>
                {
                    if (isHidden) return;
                    if (IsInWindow()) return;
                    mw.Dispatcher.Invoke(() =>
                    {
                        isHidden = true;
                        Clear();
                        //Trace.WriteLine("开始离开");
                        if (isTop) Move("Top", triggerWeight - mw.Height);
                        else Move("Left", SystemParameters.PrimaryScreenWidth - triggerWeight);
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
                return false;
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
                        Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath($"(Window.{prop})"));
                        storyboard.Children.Add(doubleAnimation);
                    }
                    //Trace.WriteLine($"{prop} {value} {amTime}");

                    storyboard.Completed += (a, e) =>
                    {
                        onComplete?.Invoke();
                    };
                    storyboard.Begin(mw);

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
                        //Trace.WriteLine("清理前动画");
                        storyboard.Stop();
                        storyboard.Remove();
                        storyboard = null;
                    };
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
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            Event.UpdatePos();
            Event.OnMouseLeave();
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            Event.OnMouseEnter();
        }
    }
}
