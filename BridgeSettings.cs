using System.IO;
using System.Text.Json;

namespace CodexBridge;

public sealed class BridgeSettings
{
    public const int CurrentCoordinateVersion = 2;

    public int CoordinateVersion { get; set; }

    public CalibrationPoint? InputPoint { get; set; }

    public CalibrationPoint? SendPoint { get; set; }

    public bool IsCalibrated => InputPoint is not null && SendPoint is not null;
}

public sealed record CalibrationPoint(double X, double Y);

public sealed class BridgeSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public BridgeSettingsStore()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        RootDirectory = string.IsNullOrWhiteSpace(root)
            ? Path.Combine(AppContext.BaseDirectory, "settings")
            : Path.Combine(root, "CodexBridge");
        FilePath = Path.Combine(RootDirectory, "settings.json");
    }

    public string RootDirectory { get; }

    public string FilePath { get; }

    public BridgeSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            BridgeLog.Info("Settings", "未找到配置文件，使用空配置");
            return new BridgeSettings();
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            BridgeSettings settings = JsonSerializer.Deserialize<BridgeSettings>(json, JsonOptions) ?? new BridgeSettings();
            if (settings.CoordinateVersion != BridgeSettings.CurrentCoordinateVersion)
            {
                BridgeLog.Warning("Settings", $"配置坐标版本过期（{settings.CoordinateVersion}），需要重新校准");
                return new BridgeSettings();
            }

            BridgeLog.Info("Settings", $"已加载配置，输入框={settings.InputPoint is not null}，发送按钮={settings.SendPoint is not null}");
            return settings;
        }
        catch (Exception exception)
        {
            BridgeLog.Error("Settings", "读取配置失败，将使用空配置", exception);
            return new BridgeSettings();
        }
    }

    public void Save(BridgeSettings settings)
    {
        settings.CoordinateVersion = BridgeSettings.CurrentCoordinateVersion;
        Directory.CreateDirectory(RootDirectory);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(FilePath, json);
        BridgeLog.Info("Settings", $"配置已保存：{FilePath}");
    }
}
