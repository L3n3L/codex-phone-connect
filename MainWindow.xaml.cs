using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using QRCoder;

namespace CodexBridge;

public partial class MainWindow : Window
{
    private const int Port = 8787;
    private const int WhMouseLl = 14;
    private const int WmLButtonDown = 0x0201;
    private const uint GaRoot = 2;

    private readonly BridgeWebServer _server = new(Port);
    private readonly BridgeSettingsStore _settingsStore = new();
    private readonly CodexInputAdapter _inputAdapter = new();
    private readonly BridgeJobQueue _jobQueue = new();
    private BridgeSettings _settings = new();
    private LowLevelMouseProc? _mouseProc;
    private nint _mouseHook;
    private CalibrationTarget? _pendingTarget;

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsStore.Load();
        UpdateCalibrationUi();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        BridgeLog.Info("UI", "主窗口已创建");
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _server.JobReceived += Server_JobReceived;
        BridgeLog.Info("UI", "主窗口已加载，开始启动桥接服务");
        try
        {
            await _server.StartAsync();
            var lanAddress = GetLanAddress();
            var address = $"http://{lanAddress ?? "电脑地址"}:{Port}";
            _server.SetAddress(address);
            AddressText.Text = address;
            UpdateConnectionQr(address, lanAddress is not null);
            ServerStateText.Text = "服务运行中";
            _jobQueue.Start(ProcessJobAsync);
            BridgeLog.Info("UI", $"桥接服务已就绪，局域网地址：{address}");
        }
        catch (Exception exception)
        {
            ServerStateText.Text = $"服务启动失败：{exception.Message}";
            BridgeLog.Error("UI", "桥接服务启动失败", exception);
        }
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        BridgeLog.Info("UI", "开始关闭主窗口");
        RemoveMouseHook();
        try
        {
            await _jobQueue.DisposeAsync();
            await _server.DisposeAsync();
        }
        catch (Exception exception)
        {
            BridgeLog.Error("UI", "关闭桥接服务失败", exception);
        }
    }

    private void InputPointButton_Click(object sender, RoutedEventArgs e)
    {
        BridgeLog.Info("Calibration", "开始设置输入框位置");
        BeginCalibration(CalibrationTarget.Input);
    }

    private void SendPointButton_Click(object sender, RoutedEventArgs e)
    {
        BridgeLog.Info("Calibration", "开始设置发送按钮位置");
        BeginCalibration(CalibrationTarget.Send);
    }

    private void TestButton_Click(object sender, RoutedEventArgs e)
    {
        var job = new BridgeJob(Guid.NewGuid().ToString("N"), "Codex Bridge 测试消息");
        if (_jobQueue.Enqueue(job))
        {
            HintText.Text = "测试消息已排队";
            BridgeLog.Info("UI", $"测试消息已排队，jobId={job.JobId}");
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settingsStore.Save(_settings);
            HintText.Text = "设置已保存";
            SaveButton.IsEnabled = false;
            BridgeLog.Info("UI", "点击保存按钮");
        }
        catch (Exception exception)
        {
            HintText.Text = "设置保存失败";
            BridgeLog.Error("Settings", "保存配置失败", exception);
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            return;
        }

        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Server_JobReceived(object? sender, SendJobReceivedEventArgs e)
    {
        BridgeLog.Info("Queue", $"任务已交给 UI 层，jobId={e.JobId}，字符数={e.Request.Text.Length}");
        var accepted = _jobQueue.Enqueue(new BridgeJob(e.JobId, e.Request.Text));
        Dispatcher.BeginInvoke(() => HintText.Text = accepted
            ? $"已收到请求 {e.JobId[..8]}"
            : "发送队列不可用");
    }

    private async Task ProcessJobAsync(BridgeJob job, CancellationToken cancellationToken)
    {
        if (!_settings.IsCalibrated)
        {
            BridgeLog.Warning("Input", $"任务因未完成校准而跳过，jobId={job.JobId}");
            await Dispatcher.BeginInvoke(() => HintText.Text = "请先设置输入框和发送按钮");
            return;
        }

        try
        {
            await Dispatcher.InvokeAsync(
                    () => _inputAdapter.SendAsync(job.Text, _settings, cancellationToken))
                .Task
                .Unwrap();
            await Dispatcher.BeginInvoke(() => HintText.Text = $"已发送 {job.JobId[..8]}");
            BridgeLog.Info("Queue", $"任务发送完成，jobId={job.JobId}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            BridgeLog.Warning("Queue", $"任务因程序关闭而取消，jobId={job.JobId}");
        }
        catch (Exception exception)
        {
            await Dispatcher.BeginInvoke(() => HintText.Text = $"发送失败 {job.JobId[..8]}");
            BridgeLog.Error("Queue", $"任务发送失败，jobId={job.JobId}", exception);
        }
    }

    private void UpdateConnectionQr(string address, bool isReachableAddress)
    {
        if (!isReachableAddress)
        {
            ConnectionQr.Visibility = Visibility.Collapsed;
            BridgeLog.Warning("UI", "没有找到可用于二维码的局域网地址");
            return;
        }

        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(address, QRCodeGenerator.ECCLevel.M);
        var pngBytes = new PngByteQRCode(qrData).GetGraphic(8);

        using var stream = new MemoryStream(pngBytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();

        ConnectionQr.Source = image;
        ConnectionQr.Visibility = Visibility.Visible;
        BridgeLog.Info("UI", $"二维码已生成，地址={address}");
    }

    private void BeginCalibration(CalibrationTarget target)
    {
        RemoveMouseHook();
        _pendingTarget = target;
        HintText.Text = target == CalibrationTarget.Input
            ? "请点击 Codex 输入框"
            : "请点击 Codex 发送按钮";

        _mouseProc = MouseHookCallback;
        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, GetModuleHandle(null), 0);
        if (_mouseHook == 0)
        {
            HintText.Text = "无法开始设置";
            BridgeLog.Error("Calibration", $"安装全局鼠标钩子失败，target={target}，win32Error={Marshal.GetLastWin32Error()}");
            _pendingTarget = null;
            return;
        }

        BridgeLog.Info("Calibration", $"已安装全局鼠标钩子，等待点击，target={target}");
        Hide();
    }

    private nint MouseHookCallback(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && wParam == WmLButtonDown && _pendingTarget is not null)
        {
            var mouseData = Marshal.PtrToStructure<MsllHookStruct>(lParam);
            var childWindow = WindowFromPoint(mouseData.Point);
            var targetWindow = GetAncestor(childWindow, GaRoot);
            GetWindowThreadProcessId(targetWindow, out var processId);

            try
            {
                using var process = Process.GetProcessById((int)processId);
                if (string.Equals(process.ProcessName, "ChatGPT", StringComparison.OrdinalIgnoreCase))
                {
                    var clientPoint = mouseData.Point;
                    if (!ScreenToClient(targetWindow, ref clientPoint)
                        || !GetClientRect(targetWindow, out var clientRect))
                    {
                        BridgeLog.Warning("Calibration", "无法将屏幕坐标转换为 Codex 客户区坐标");
                        return CallNextHookEx(_mouseHook, code, wParam, lParam);
                    }

                    var width = Math.Max(1, clientRect.Right - clientRect.Left);
                    var height = Math.Max(1, clientRect.Bottom - clientRect.Top);
                    var point = new CalibrationPoint(
                        (double)clientPoint.X / width,
                        (double)clientPoint.Y / height);
                    var target = _pendingTarget.Value;
                    BridgeLog.Info("Calibration", $"捕获 Codex 点击，target={target}，x={point.X:F4}，y={point.Y:F4}");
                    Dispatcher.BeginInvoke(() => CompleteCalibration(target, point));
                    return 1;
                }
                else
                {
                    BridgeLog.Debug("Calibration", $"忽略非 Codex 窗口点击，process={process.ProcessName}");
                }
            }
            catch (ArgumentException)
            {
                // The clicked process can exit while the hook is processing the click.
                BridgeLog.Warning("Calibration", "获取点击窗口进程失败，窗口可能已退出");
            }
        }

        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private void CompleteCalibration(CalibrationTarget target, CalibrationPoint point)
    {
        RemoveMouseHook();
        _pendingTarget = null;
        Show();
        Activate();

        if (target == CalibrationTarget.Input)
        {
            _settings.InputPoint = point;
        }
        else
        {
            _settings.SendPoint = point;
        }

        UpdateCalibrationUi();
        HintText.Text = "";
        BridgeLog.Info("Calibration", $"校准完成，target={target}，x={point.X:F4}，y={point.Y:F4}");
    }

    private void UpdateCalibrationUi()
    {
        InputPointText.Text = _settings.InputPoint is null ? "未设置" : "已设置";
        SendPointText.Text = _settings.SendPoint is null ? "未设置" : "已设置";
        TestButton.IsEnabled = _settings.IsCalibrated;
        SaveButton.IsEnabled = _settings.InputPoint is not null || _settings.SendPoint is not null;
    }

    private void RemoveMouseHook()
    {
        if (_mouseHook != 0)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = 0;
        }

        _mouseProc = null;
    }

    private static string? GetLanAddress()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up
                              && network.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(network =>
            {
                var properties = network.GetIPProperties();
                var address = properties.UnicastAddresses
                    .Select(unicast => unicast.Address)
                    .FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork
                                                 && !IPAddress.IsLoopback(candidate)
                                                 && candidate.GetAddressBytes()[0] != 169);
                var hasDefaultGateway = properties.GatewayAddresses.Any(gateway =>
                    gateway.Address.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(gateway.Address));

                return new
                {
                    Address = address,
                    HasDefaultGateway = hasDefaultGateway,
                    IsWireless = network.NetworkInterfaceType == NetworkInterfaceType.Wireless80211,
                };
            })
            .Where(candidate => candidate.Address is not null)
            .OrderByDescending(candidate => candidate.HasDefaultGateway)
            .ThenByDescending(candidate => candidate.IsWireless)
            .Select(candidate => candidate.Address!.ToString())
            .FirstOrDefault();
    }

    private delegate nint LowLevelMouseProc(int code, nint wParam, nint lParam);

    private enum CalibrationTarget
    {
        Input,
        Send,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointStruct
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectStruct
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MsllHookStruct
    {
        public PointStruct Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelMouseProc callback, nint moduleHandle, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hookHandle);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hookHandle, int code, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(PointStruct point);

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint handle, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint handle, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(nint handle, ref PointStruct point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ScreenToClient(nint handle, ref PointStruct point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint handle, out RectStruct rectangle);
}
