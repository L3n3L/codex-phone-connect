using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodexPhoneConnect;

public partial class MainWindow : Window
{
    private const int Port = 8787;
    private const int WhMouseLl = 14;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const uint GaRoot = 2;

    private readonly BridgeWebServer _server = new(Port);
    private readonly BridgeSettingsStore _settingsStore = new();
    private readonly AgentInputAdapter _inputAdapter = new();
    private readonly BridgeJobQueue _jobQueue = new();
    private readonly NetworkConnectionMonitor _connectionMonitor = new(Port);
    private readonly Dictionary<string, ConnectionEntryViewModel> _connectionEntries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BitmapSource> _brandIcons = new(StringComparer.OrdinalIgnoreCase);
    private BridgeSettings _settings = new();
    private AgentId _selectedAgent;
    private LowLevelMouseProc? _mouseProc;
    private nint _mouseHook;
    private CalibrationTarget? _pendingTarget;
    private AgentId _pendingAgent;
    private CapturedCalibrationClick? _capturedCalibrationClick;
    private bool _settingsDirty;
    private bool _isClosing;

    public MainWindow()
    {
        InitializeComponent();
        MainScrollViewer.MaxHeight = Math.Max(240, SystemParameters.WorkArea.Height - 72);
        _settings = _settingsStore.Load();
        _selectedAgent = _settings.ActiveAgent;
        LoadBrandIcons();
        _server.SetActiveAgent(_selectedAgent);
        SelectAgent(_selectedAgent, markDirty: false);
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        BridgeLog.Info("UI", $"主窗口已创建，active={AgentCatalog.ToWireValue(_selectedAgent)}");
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _server.JobReceived += Server_JobReceived;
        _server.ScrollCommandReceived += Server_ScrollCommandReceived;
        _server.ActiveAgentChanged += Server_ActiveAgentChanged;
        BridgeLog.Info("UI", "主窗口已加载，开始启动桥接服务");
        try
        {
            await _server.StartAsync();
            if (_isClosing)
            {
                await _server.DisposeAsync();
                return;
            }

            _connectionMonitor.AddressesChanged += ConnectionMonitor_AddressesChanged;
            _connectionMonitor.Start();
            ServerStateText.Text = "服务运行中";
            _jobQueue.Start(ProcessCommandAsync);
            BridgeLog.Info("UI", "桥接服务已就绪，自动发现所有可用连接地址");
        }
        catch (Exception exception)
        {
            ServerStateText.Text = $"服务启动失败：{exception.Message}";
            ConnectionEmptyText.Text = "服务未启动，暂无连接二维码";
            BridgeLog.Error("UI", "桥接服务启动失败", exception);
        }
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        BridgeLog.Info("UI", "开始关闭主窗口");
        _isClosing = true;
        _connectionMonitor.AddressesChanged -= ConnectionMonitor_AddressesChanged;
        _connectionMonitor.Dispose();
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

    private void AgentButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: AgentId agent })
        {
            SelectAgent(agent, markDirty: true);
        }
    }

    private void InputPointButton_Click(object sender, RoutedEventArgs e)
    {
        BridgeLog.Info("Calibration", $"开始设置输入框位置，agent={AgentCatalog.ToWireValue(_selectedAgent)}");
        BeginCalibration(CalibrationTarget.Input);
    }

    private void SendPointButton_Click(object sender, RoutedEventArgs e)
    {
        BridgeLog.Info("Calibration", $"开始设置发送按钮位置，agent={AgentCatalog.ToWireValue(_selectedAgent)}");
        BeginCalibration(CalibrationTarget.Send);
    }

    private void ScrollPointButton_Click(object sender, RoutedEventArgs e)
    {
        BridgeLog.Info("Calibration", $"开始设置滚动区域位置，agent={AgentCatalog.ToWireValue(_selectedAgent)}");
        BeginCalibration(CalibrationTarget.Scroll);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings.ActiveAgent = _selectedAgent;
            _settingsStore.Save(_settings);
            _settingsDirty = false;
            HintText.Text = "设置已保存";
            UpdateCalibrationUi();
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
        BridgeLog.Info(
            "Queue",
            $"任务已交给 UI 层，jobId={e.JobId}，agent={AgentCatalog.ToWireValue(e.Agent)}，字符数={e.Request.Text.Length}");
        var accepted = _jobQueue.Enqueue(new BridgeJob(e.JobId, e.Request.Text, e.Agent));
        Dispatcher.BeginInvoke(() => HintText.Text = accepted
            ? $"已收到 {AgentCatalog.Get(e.Agent).DisplayName} 请求 {e.JobId[..8]}"
            : "发送队列不可用");
    }

    private void Server_ScrollCommandReceived(object? sender, ScrollCommandReceivedEventArgs e)
    {
        var deltaY = Math.Clamp(e.Request.DeltaY, -120, 120);
        var accepted = _jobQueue.Enqueue(new BridgeScrollCommand(e.CommandId, deltaY, e.Agent));
        BridgeLog.Debug(
            "Queue",
            $"滚动命令已入队，commandId={e.CommandId}，agent={AgentCatalog.ToWireValue(e.Agent)}，deltaY={deltaY}");
        Dispatcher.BeginInvoke(() => HintText.Text = accepted
            ? $"正在控制 {AgentCatalog.Get(e.Agent).DisplayName} 滚动"
            : "滚动队列不可用");
    }

    private void Server_ActiveAgentChanged(object? sender, ActiveAgentChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() => SelectAgent(e.Agent, markDirty: true));
    }

    private async Task ProcessCommandAsync(BridgeInputCommand command, CancellationToken cancellationToken)
    {
        switch (command)
        {
            case BridgeJob job:
                await ProcessTextJobAsync(job, cancellationToken);
                break;
            case BridgeScrollCommand scroll:
                await ProcessScrollCommandAsync(scroll, cancellationToken);
                break;
        }
    }

    private async Task ProcessTextJobAsync(BridgeJob job, CancellationToken cancellationToken)
    {
        var definition = AgentCatalog.Get(job.Agent);
        if (!_settings.GetProfile(job.Agent).IsCalibrated)
        {
            BridgeLog.Warning("Input", $"任务因未完成校准而跳过，agent={definition.WireId}，jobId={job.JobId}");
            await Dispatcher.BeginInvoke(() => HintText.Text = $"请先设置 {definition.DisplayName} 的输入框和发送按钮");
            return;
        }

        try
        {
            await Dispatcher.InvokeAsync(
                    () => _inputAdapter.SendAsync(
                        job.Agent,
                        job.Text,
                        _settings.GetProfile(job.Agent),
                        cancellationToken))
                .Task
                .Unwrap();
            await Dispatcher.BeginInvoke(() => HintText.Text = $"已发送 {job.JobId[..8]}");
            BridgeLog.Info("Queue", $"任务发送完成，agent={definition.WireId}，jobId={job.JobId}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            BridgeLog.Warning("Queue", $"任务因程序关闭而取消，agent={definition.WireId}，jobId={job.JobId}");
        }
        catch (Exception exception)
        {
            await Dispatcher.BeginInvoke(() => HintText.Text = $"发送失败 {job.JobId[..8]}");
            BridgeLog.Error("Queue", $"任务发送失败，agent={definition.WireId}，jobId={job.JobId}", exception);
        }
    }

    private async Task ProcessScrollCommandAsync(BridgeScrollCommand command, CancellationToken cancellationToken)
    {
        var definition = AgentCatalog.Get(command.Agent);
        if (!_settings.GetProfile(command.Agent).IsScrollCalibrated)
        {
            BridgeLog.Warning("Input", $"滚动命令因未完成校准而跳过，agent={definition.WireId}，commandId={command.CommandId}");
            return;
        }

        try
        {
            await Dispatcher.InvokeAsync(
                    () => _inputAdapter.Scroll(
                        command.Agent,
                        command.DeltaY,
                        _settings.GetProfile(command.Agent),
                        cancellationToken))
                .Task;
            BridgeLog.Debug("Queue", $"滚动命令执行完成，agent={definition.WireId}，commandId={command.CommandId}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            BridgeLog.Warning("Queue", $"滚动命令因程序关闭而取消，agent={definition.WireId}，commandId={command.CommandId}");
        }
        catch (Exception exception)
        {
            BridgeLog.Error("Queue", $"滚动命令执行失败，agent={definition.WireId}，commandId={command.CommandId}", exception);
        }
    }

    private void SelectAgent(AgentId agent, bool markDirty)
    {
        _selectedAgent = agent;
        _settings.ActiveAgent = agent;
        _server.SetActiveAgent(agent);
        RenderAgentButtons();
        if (markDirty)
        {
            _settingsDirty = true;
        }

        UpdateCalibrationUi();
        BridgeLog.Info("UI", $"界面选择智能体：{AgentCatalog.ToWireValue(agent)}，dirty={_settingsDirty}");
    }

    private void RenderAgentButtons()
    {
        var neutralBorder = (Brush)FindResource("BorderBrush");
        var neutralForeground = (Brush)FindResource("PrimaryTextBrush");
        AgentButtonsPanel.ItemsSource = AgentCatalog.All.Select(definition =>
        {
            var selected = definition.Id == _selectedAgent;
            return new AgentButtonOption(
                definition,
                _brandIcons[definition.IconDesktopAsset],
                selected ? CreateSelectedBackground(definition) : Brushes.White,
                selected ? CreateBrush(definition.SelectedBorder) : neutralBorder,
                selected ? CreateBrush(definition.SelectedForeground) : neutralForeground);
        }).ToArray();
    }

    private void LoadBrandIcons()
    {
        foreach (var definition in AgentCatalog.All)
        {
            _brandIcons[definition.IconDesktopAsset] = LoadBrandIcon(definition.IconDesktopAsset);
        }
    }

    private static Brush CreateBrush(string color)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
        brush.Freeze();
        return brush;
    }

    private static Brush CreateSelectedBackground(AgentDefinition definition)
    {
        if (string.Equals(definition.SelectedBackground, definition.SelectedBackgroundEnd, StringComparison.OrdinalIgnoreCase))
        {
            return CreateBrush(definition.SelectedBackground);
        }

        var brush = new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString(definition.SelectedBackground)!,
            (Color)ColorConverter.ConvertFromString(definition.SelectedBackgroundEnd)!,
            35);
        brush.Freeze();
        return brush;
    }

    private static BitmapImage LoadBrandIcon(string assetName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "assets", "brand", assetName);
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void ConnectionMonitor_AddressesChanged(object? sender, ConnectionAddressesChangedEventArgs e)
    {
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_isClosing)
            {
                UpdateConnectionEntries(e.Connections);
            }
        }));
    }

    private void UpdateConnectionEntries(IReadOnlyList<NetworkConnection> connections)
    {
        try
        {
            // Reuse unchanged QR images when another adapter connects or disconnects.
            var entries = connections.Select(connection =>
                _connectionEntries.TryGetValue(connection.Url, out var existing) && existing.Connection == connection
                    ? existing
                    : ConnectionEntryViewModel.Create(connection)).ToArray();

            _connectionEntries.Clear();
            foreach (var entry in entries)
            {
                _connectionEntries.Add(entry.Connection.Url, entry);
            }

            _server.SetAddresses(connections.Select(connection => connection.Url));
            ConnectionEntries.ItemsSource = entries;
            NoConnectionsPanel.Visibility = entries.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            ConnectionEmptyText.Text = "暂无可用连接地址";
        }
        catch (Exception exception)
        {
            BridgeLog.Error("UI", "更新连接地址和二维码失败", exception);
        }
    }

    private void BeginCalibration(CalibrationTarget target)
    {
        RemoveMouseHook();
        _pendingTarget = target;
        _pendingAgent = _selectedAgent;
        var definition = AgentCatalog.Get(_pendingAgent);
        HintText.Text = target switch
        {
            CalibrationTarget.Input => $"请点击 {definition.ProductName} 输入框",
            CalibrationTarget.Send => $"请点击 {definition.ProductName} 发送按钮",
            _ => $"请点击 {definition.ProductName} 主聊天内容区域",
        };

        _mouseProc = MouseHookCallback;
        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, GetModuleHandle(null), 0);
        if (_mouseHook == 0)
        {
            HintText.Text = "无法开始设置";
            BridgeLog.Error("Calibration", $"安装全局鼠标钩子失败，target={target}，win32Error={Marshal.GetLastWin32Error()}");
            _pendingTarget = null;
            return;
        }

        BridgeLog.Info("Calibration", $"已安装全局鼠标钩子，agent={definition.WireId}，target={target}");
        Hide();
    }

    private nint MouseHookCallback(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && wParam == WmLButtonUp && _capturedCalibrationClick is { } capturedClick)
        {
            _capturedCalibrationClick = null;
            BridgeLog.Info(
                "Calibration",
                $"已屏蔽校准点击释放，agent={AgentCatalog.ToWireValue(capturedClick.Agent)}，target={capturedClick.Target}");
            Dispatcher.BeginInvoke(() => CompleteCalibration(
                capturedClick.Agent,
                capturedClick.Target,
                capturedClick.Point,
                capturedClick.TargetWindow));
            return 1;
        }

        if (code >= 0 && wParam == WmLButtonDown && _pendingTarget is not null)
        {
            var mouseData = Marshal.PtrToStructure<MsllHookStruct>(lParam);
            var childWindow = WindowFromPoint(mouseData.Point);
            var targetWindow = GetAncestor(childWindow, GaRoot);
            GetWindowThreadProcessId(targetWindow, out var processId);
            var definition = AgentCatalog.Get(_pendingAgent);

            try
            {
                using var process = Process.GetProcessById((int)processId);
                if (definition.MatchesProcess(process.ProcessName))
                {
                    var clientPoint = mouseData.Point;
                    if (!ScreenToClient(targetWindow, ref clientPoint)
                        || !GetClientRect(targetWindow, out var clientRect))
                    {
                        BridgeLog.Warning("Calibration", $"无法将屏幕坐标转换为 {definition.DisplayName} 客户区坐标");
                        return CallNextHookEx(_mouseHook, code, wParam, lParam);
                    }

                    var width = Math.Max(1, clientRect.Right - clientRect.Left);
                    var height = Math.Max(1, clientRect.Bottom - clientRect.Top);
                    var point = new CalibrationPoint(
                        (double)clientPoint.X / width,
                        (double)clientPoint.Y / height);
                    var target = _pendingTarget.Value;
                    var agent = _pendingAgent;
                    BridgeLog.Info(
                        "Calibration",
                        $"捕获 {definition.DisplayName} 点击，target={target}，x={point.X:F4}，y={point.Y:F4}");
                    _capturedCalibrationClick = new CapturedCalibrationClick(agent, target, point, targetWindow);
                    BridgeLog.Info(
                        "Calibration",
                        $"已屏蔽校准点击按下，等待释放后完成，agent={definition.WireId}，target={target}");
                    return 1;
                }

                BridgeLog.Debug(
                    "Calibration",
                    $"忽略非目标窗口点击，expected={definition.WireId}，process={process.ProcessName}");
            }
            catch (ArgumentException)
            {
                BridgeLog.Warning("Calibration", "获取点击窗口进程失败，窗口可能已退出");
            }
        }

        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private void CompleteCalibration(AgentId agent, CalibrationTarget target, CalibrationPoint point, nint targetWindow)
    {
        RemoveMouseHook();
        _pendingTarget = null;
        Show();
        Activate();
        _inputAdapter.BindWindow(agent, targetWindow);

        var profile = _settings.GetProfile(agent);
        if (target == CalibrationTarget.Input)
        {
            profile.InputPoint = point;
        }
        else if (target == CalibrationTarget.Send)
        {
            profile.SendPoint = point;
        }
        else
        {
            profile.ScrollPoint = point;
        }

        SelectAgent(agent, markDirty: true);
        HintText.Text = $"{AgentCatalog.Get(agent).DisplayName} 已设置";
        BridgeLog.Info(
            "Calibration",
            $"校准完成，agent={AgentCatalog.ToWireValue(agent)}，target={target}，x={point.X:F4}，y={point.Y:F4}");
    }

    private void UpdateCalibrationUi()
    {
        var profile = _settings.GetProfile(_selectedAgent);
        InputPointText.Text = profile.InputPoint is null ? "未设置" : "已设置";
        SendPointText.Text = profile.SendPoint is null ? "未设置" : "已设置";
        ScrollPointText.Text = profile.ScrollPoint is null ? "未设置" : "已设置";
        SaveButton.IsEnabled = _settingsDirty;
    }

    private void RemoveMouseHook()
    {
        if (_mouseHook != 0)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = 0;
        }

        _mouseProc = null;
        _capturedCalibrationClick = null;
    }

    private delegate nint LowLevelMouseProc(int code, nint wParam, nint lParam);

    private enum CalibrationTarget
    {
        Input,
        Send,
        Scroll,
    }

    private sealed record CapturedCalibrationClick(
        AgentId Agent,
        CalibrationTarget Target,
        CalibrationPoint Point,
        nint TargetWindow);

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
    private static extern bool ScreenToClient(nint handle, ref PointStruct point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint handle, out RectStruct rectangle);
}
