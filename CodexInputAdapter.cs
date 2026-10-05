using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace CodexPhoneConnect;

public sealed class CodexInputAdapter
{
    private const string CodexProcessName = "ChatGPT";
    private const int ShowNormal = 9;
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint KeyboardEventKeyUp = 0x0002;
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyV = 0x56;

    public async Task SendAsync(string text, BridgeSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("发送内容为空", nameof(text));
        }

        if (!settings.IsCalibrated)
        {
            throw new InvalidOperationException("尚未完成输入框和发送按钮校准");
        }

        var window = FindCodexWindow();
        if (window == 0)
        {
            throw new InvalidOperationException("未找到 Codex 窗口");
        }

        BridgeLog.Info("Input", $"开始发送，字符数={text.Length}，window=0x{window.ToInt64():X}");
        var originalClipboard = Clipboard.GetDataObject();
        try
        {
            ActivateWindow(window);
            await Task.Delay(120, cancellationToken);

            ClickClientPoint(window, settings.InputPoint!);
            await Task.Delay(80, cancellationToken);

            Clipboard.SetText(text, TextDataFormat.UnicodeText);
            SendPasteShortcut();
            await Task.Delay(220, cancellationToken);

            ClickClientPoint(window, settings.SendPoint!);
            await Task.Delay(120, cancellationToken);
            BridgeLog.Info("Input", "已完成输入框粘贴和发送按钮点击");
        }
        finally
        {
            RestoreClipboard(originalClipboard);
        }
    }

    private static nint FindCodexWindow()
    {
        var foreground = GetForegroundWindow();
        GetWindowThreadProcessId(foreground, out var foregroundProcessId);
        try
        {
            using var foregroundProcess = Process.GetProcessById((int)foregroundProcessId);
            if (string.Equals(foregroundProcess.ProcessName, CodexProcessName, StringComparison.OrdinalIgnoreCase)
                && IsWindowVisible(foreground)
                && foreground != 0)
            {
                return foreground;
            }
        }
        catch (ArgumentException)
        {
            // The foreground process can exit while it is being inspected.
        }

        foreach (var process in Process.GetProcessesByName(CodexProcessName))
        {
            try
            {
                if (process.MainWindowHandle != 0 && IsWindowVisible(process.MainWindowHandle))
                {
                    return process.MainWindowHandle;
                }
            }
            finally
            {
                process.Dispose();
            }
        }

        return 0;
    }

    private static void ActivateWindow(nint window)
    {
        ShowWindow(window, ShowNormal);
        BringWindowToTop(window);
        if (!SetForegroundWindow(window))
        {
            throw new InvalidOperationException("无法激活 Codex 窗口");
        }

        BridgeLog.Debug("Input", $"已激活 Codex 窗口，window=0x{window.ToInt64():X}");
    }

    private static void ClickClientPoint(nint window, CalibrationPoint point)
    {
        if (!GetClientRect(window, out var clientRect))
        {
            throw new InvalidOperationException("无法读取 Codex 客户区位置");
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
            throw new InvalidOperationException("无法将 Codex 客户区坐标转换为屏幕坐标");
        }

        if (!SetCursorPos(clientPoint.X, clientPoint.Y))
        {
            throw new InvalidOperationException("无法移动鼠标到 Codex 目标位置");
        }

        var inputs = new[]
        {
            CreateMouseInput(MouseEventLeftDown),
            CreateMouseInput(MouseEventLeftUp),
        };
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length)
        {
            throw new InvalidOperationException("无法点击 Codex 目标位置");
        }

        BridgeLog.Debug("Input", $"已点击 Codex 客户区比例坐标 x={point.X:F4}，y={point.Y:F4}");
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
            throw new InvalidOperationException("无法向 Codex 发送粘贴快捷键");
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
