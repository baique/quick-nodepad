using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;

namespace ShellNodepad
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var proc = GetRunningInstance();
            if (proc != null)
            {
                HandleRunningInstance(proc);
                Application.Current.Shutdown();
                return;
            }
            if (InstallWebview2())
            {
                this.StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
            }
        }

        public static bool InstallWebview2()
        {
            if (!IsInstallWebview2())
            {
                if (MessageBox.Show("未检测到WebView2环境，是否自动下载安装？", "提示", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                {
                    Current.Shutdown();
                    return false;
                }
                try
                {
                    using var webClient = new HttpClient();
                    string MicrosoftEdgeWebview2Setup = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");
                    using var stream = webClient.GetByteArrayAsync("https://go.microsoft.com/fwlink/p/?LinkId=2124703");
                    File.WriteAllBytes(MicrosoftEdgeWebview2Setup, stream.Result);
                    Process.Start(MicrosoftEdgeWebview2Setup).WaitForExit();
                    if (IsInstallWebview2())
                    {
                        MessageBox.Show("webview2依赖已成功安装，请重新启动应用");
                        if (File.Exists("MicrosoftEdgeWebview2Setup.exe"))
                        {
                            File.Delete("MicrosoftEdgeWebview2Setup.exe");
                        }
                        System.Windows.Forms.Application.Restart();
                        Current.Shutdown();
                        return false;
                    }
                    else
                    {
                        throw new Exception("依赖不存在");
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show("webview2依赖安装失败:" + e.Message);
                    Current.Shutdown();
                    return false;
                }
            }
            return true;
        }

        public static void DeleteWebView2Folder()
        {
            string webview2Dir = $"{Environment.GetCommandLineArgs()[0]}.WebView2";
            if (Directory.Exists(webview2Dir))
            {
                Directory.Delete(webview2Dir, true);
            }
        }

        public static bool IsInstallWebview2()
        {

            string? res = "";
            try
            {
                res = CoreWebView2Environment.GetAvailableBrowserVersionString();
            }
            catch (System.Exception)
            {
            }
            if (res == "" || res == null)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 获取当前是否具有相同进程。
        /// </summary>
        /// <returns></returns>
        public static Process GetRunningInstance()
        {
            Process current = Process.GetCurrentProcess();
            Process[] processes = Process.GetProcessesByName(current.ProcessName);
            //遍历正在有相同名字运行的例程   
            foreach (Process process in processes)
            {
                //忽略现有的例程   
                if (process.Id != current.Id)
                {
                    //确保例程从EXE文件运行 
                    if (process.ProcessName == process.ProcessName)
                        return process;
                }
            }
            return null!;
        }

        private const int WS_SHOWNORMAL = 1;
        [DllImport("User32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int cmdShow);
        [DllImport("User32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        /// <summary>
        /// 激活原有的进程。
        /// </summary>
        /// <param name="instance"></param>
        public static void HandleRunningInstance(Process instance)
        {
            ShowWindowAsync(instance.MainWindowHandle, WS_SHOWNORMAL);
            SetForegroundWindow(instance.MainWindowHandle);
        }
    }
}
