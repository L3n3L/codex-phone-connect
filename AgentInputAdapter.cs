using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace CodexPhoneConnect;

public sealed class AgentInputAdapter
{
    private const int ShowRestore = 9;
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventWheel = 0x0800;
    private const uint KeyboardEventKeyUp = 0x0002;
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyV = 0x56;
    private readonly Dictionary<AgentId, nint> _boundWindows = new();

    public void BindWindow(AgentId agent, nint window)
    {
        if (window == 0 || !IsAgentWindow(agent, window))
        {
            BridgeLog.Warning("Input", $"拒绝绑定非 {AgentCatalog.Get(agent).ProductName} 窗口");
            return;
        }

        _boundWindows[agent] = window;
        BridgeLog.Info("Input", $"已绑定 {AgentCatalog.Get(agent).DisplayName} 窗口，window=0x{window.ToInt64():X}");
    }

    public async Task SendAsync(
        AgentId agent,
        string text,
        AgentProfileSettings profile,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("发送内容为空", nameof(text));
        }

        if (!profile.IsCalibrated)
        {
            throw new InvalidOperationException($"尚未完成 {AgentCatalog.Get(agent).DisplayName} 的输入框和发送按钮校准");
        }

        var window = FindAgentWindow(agent);
        if (window == 0)
        {
            throw new InvalidOperationException($"未找到 {AgentCatalog.Get(agent).ProductName} 窗口");
        }

        var definition = AgentCatalog.Get(agent);
        BridgeLog.Info("Input", $"开始发送，agent={definition.WireId}，字符数={text.Length}，window=0x{window.ToInt64():X}");
        var originalClipboard = Clipboard.GetDataObject();
        try
        {
            ActivateWindow(window, definition.DisplayName);
            await Task.Delay(120, cancellationToken);

            ClickClientPoint(window, profile.InputPoint!);
            await Task.Delay(80, cancellationToken);

            Clipboard.SetText(text, TextDataFormat.UnicodeText);
            SendPasteShortcut();
            await Task.Delay(220, cancellationToken);

            ClickClientPoint(window, profile.SendPoint!);
            await Task.Delay(120, cancellationToken);
            BridgeLog.Info("Input", $"已完成 {definition.DisplayName} 输入框粘贴和发送按钮点击");
        }
        finally
        {
            RestoreClipboard(originalClipboard);
        }
    }

    public void Scroll(
        AgentId agent,
        int deltaY,
        AgentProfileSettings profile,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!profile.IsScrollCalibrated)
        {
            throw new InvalidOperationException($"尚未完成 {AgentCatalog.Get(agent).DisplayName} 的滚动区域校准");
        }

        var window = FindAgentWindow(agent);
        if (window == 0)
        {
            throw new InvalidOperationException($"未找到已绑定的 {AgentCatalog.Get(agent).ProductName} 窗口");
        }

        var boundedDelta = Math.Clamp(deltaY, -120, 120);
        if (boundedDelta == 0)
        {
            return;
        }

        if (GetForegroundWindow() != window)
        {
            ActivateWindow(window, AgentCatalog.Get(agent).DisplayName);
        }

        MoveClientPoint(window, profile.ScrollPoint!);

        // 跟随手机直觉：手指上滑（deltaY<0）时，内容向下；手指下滑时，内容向上。
        var wheelAmount = Math.Clamp(boundedDelta * 8, -960, 960);
        var inputs = new[] { CreateMouseWheelInput(wheelAmount) };
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length)
        {
            throw new InvalidOperationException($"无法向 {AgentCatalog.Get(agent).DisplayName} 注入鼠标滚轮");
        }

        BridgeLog.Debug(
            "Input",
            $"已注入桌面窗口滚轮，agent={AgentCatalog.Get(agent).WireId}，deltaY={boundedDelta}，wheel={wheelAmount}，window=0x{window.ToInt64():X}");
    }

    public bool IsAgentWindow(AgentId agent, nint window)
    {
        if (window == 0 || !IsWindowVisible(window))
        {
            return false;
        }

        GetWindowThreadProcessId(window, out var processId);
        if (processId == 0)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return AgentCatalog.Get(agent).MatchesProcess(process.ProcessName);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private nint FindAgentWindow(AgentId agent)
    {
        if (_boundWindows.TryGetValue(agent, out var boundWindow) && IsAgentWindow(agent, boundWindow))
        {
            return boundWindow;
        }

        var foreground = GetForegroundWindow();
        if (IsAgentWindow(agent, foreground))
        {
            return foreground;
        }

        foreach (var processName in AgentCatalog.Get(agent).ProcessNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (IsAgentWindow(agent, process.MainWindowHandle))
                    {
                        return process.MainWindowHandle;
                    }
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        return 0;
    }

    private static void ActivateWindow(nint window, string displayName)
    {
        var wasMinimized = IsIconic(window);
        var wasMaximized = IsZoomed(window);
        if (wasMinimized)
        {
            // 只恢复最小化窗口。最大化窗口不调用 SW_RESTORE，避免被还原成普通窗口尺寸。
            ShowWindow(window, ShowRestore);
        }

        BringWindowToTop(window);
        if (!SetForegroundWindow(window))
        {
            throw new InvalidOperationException($"无法激活 {displayName} 窗口");
        }

        BridgeLog.Debug(
            "Input",
            $"已激活 {displayName} 窗口，window=0x{window.ToInt64():X}，最小化前={wasMinimized}，最大化前={wasMaximized}，最大化后={IsZoomed(window)}");
    }

    private static void ClickClientPoint(nint window, CalibrationPoint point)
    {
        MoveClientPoint(window, point);

        var inputs = new[]
        {
            CreateMouseInput(MouseEventLeftDown),
            CreateMouseInput(MouseEventLeftUp),
        };
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length)
        {
            throw new InvalidOperationException("无法点击桌面目标位置");
        }

        BridgeLog.Debug("Input", $"已点击客户区比例坐标 x={point.X:F4}，y={point.Y:F4}");
    }

    private static void MoveClientPoint(nint window, CalibrationPoint point)
    {
        if (!GetClientRect(window, out var clientRect))
        {
            throw new InvalidOperationException("无法读取桌面窗口客户区位置");
        }

        var width = Math.Max(1, clientRect.Right - clientRect.Left);
        var height = Math.Max(1, clientRect.Bottom - clientRect.Top);
        var clientPoint = new PointStruct
        {
            X = (int)Math.Round(Math.Clamp(point.X, 0, 1) * width),
            Y = (int)Math.Round(Math.Clamp(point.Y, 0, 1) * height),
        };
        if (!ClientToScreen(window, ref clientPoint))
        {
            throw new InvalidOperationException("无法将客户区坐标转换为屏幕坐标");
        }

        if (!SetCursorPos(clientPoint.X, clientPoint.Y))
        {
            throw new InvalidOperationException("无法移动鼠标到桌面目标位置");
        }
    }

    private static Input CreateMouseWheelInput(int wheelAmount)
    {
        return new Input
        {
            Type = InputMouse,
            Data = new InputUnion
            {
                Mouse = new MouseInput
                {
                    MouseData = unchecked((uint)wheelAmount),
                    Flags = MouseEventWheel,
                },
            },
        };
    }

    private static void SendPasteShortcut()
    {
        var inputs = new[]
        {
            CreateKeyboardInput(VirtualKeyControl, 0),
            CreateKeyboardInput(VirtualKeyV, 0),
            CreateKeyboardInput(VirtualKeyV, KeyboardEventKeyUp),
            CreateKeyboardInput(VirtualKeyControl, KeyboardEventKeyUp),
        };

        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length)
        {
            throw new InvalidOperationException("无法发送粘贴快捷键");
        }

        BridgeLog.Debug("Input", "已发送 Ctrl+V");
    }

    private static void RestoreClipboard(IDataObject? originalClipboard)
    {
        try
        {
            if (originalClipboard is null)
            {
                Clipboard.Clear();
            }
            else
            {
                Clipboard.SetDataObject(originalClipboard, true);
            }

            BridgeLog.Debug("Input", "已尝试恢复原剪贴板");
        }
        catch (Exception exception)
        {
            BridgeLog.Warning("Input", $"恢复剪贴板失败：{exception.Message}");
        }
    }

    private static Input CreateMouseInput(uint flags)
    {
        return new Input
        {
            Type = InputMouse,
            Data = new InputUnion
            {
                Mouse = new MouseInput { Flags = flags },
            },
        };
    }

    private static Input CreateKeyboardInput(ushort key, uint flags)
    {
        return new Input
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput { VirtualKey = key, Flags = flags },
            },
        };
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
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(nint window, ref PointStruct point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint window, out RectStruct rectangle);
}
