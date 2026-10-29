using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodexPhoneConnect;

public sealed class BridgeSettings
{
    public const int CurrentCoordinateVersion = 4;

    public int CoordinateVersion { get; set; } = CurrentCoordinateVersion;

    public AgentId ActiveAgent { get; set; } = AgentId.Gpt;

    public AgentProfileSettings Gpt { get; set; } = new();

    public AgentProfileSettings Claude { get; set; } = new();

    public AgentProfileSettings WorkBuddy { get; set; } = new();

    public AgentProfileSettings DeepSeekHarness { get; set; } = new();

    public AgentProfileSettings GetProfile(AgentId agent)
    {
        return agent switch
        {
            AgentId.Claude => Claude,
            AgentId.WorkBuddy => WorkBuddy,
            AgentId.DeepSeekHarness => DeepSeekHarness,
            _ => Gpt,
        };
    }
}

public sealed class AgentProfileSettings
{
    public CalibrationPoint? InputPoint { get; set; }

    public CalibrationPoint? SendPoint { get; set; }

    public CalibrationPoint? ScrollPoint { get; set; }

    [JsonIgnore]
    public bool IsCalibrated => InputPoint is not null && SendPoint is not null;

    [JsonIgnore]
    public bool IsScrollCalibrated => ScrollPoint is not null;
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
            : Path.Combine(root, "CodexPhoneConnect");
        FilePath = Path.Combine(RootDirectory, "settings.json");
    }

    public string RootDirectory { get; }

    public string FilePath { get; }

    public BridgeSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            TryMigrateLegacyFile();
        }

        if (!File.Exists(FilePath))
        {
            BridgeLog.Info("Settings", "未找到配置文件，使用空配置");
            return new BridgeSettings();
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            using var document = JsonDocument.Parse(json);
            var version = document.RootElement.TryGetProperty("CoordinateVersion", out var versionElement)
                && versionElement.TryGetInt32(out var parsedVersion)
                ? parsedVersion
                : 0;

            if (version is 0 or 2 or 3)
            {
                var legacy = JsonSerializer.Deserialize<LegacyBridgeSettings>(json, JsonOptions) ?? new();
                if (legacy.InputPoint is not null || legacy.SendPoint is not null || legacy.ScrollPoint is not null)
                {
                    var migrated = new BridgeSettings
                    {
                        CoordinateVersion = BridgeSettings.CurrentCoordinateVersion,
                        Gpt = new AgentProfileSettings
                        {
                            InputPoint = legacy.InputPoint,
                            SendPoint = legacy.SendPoint,
                            ScrollPoint = legacy.ScrollPoint,
                        },
                    };
                    BridgeLog.Info("Settings", $"已将旧版单智能体配置迁移到 GPT/Codex 配置，旧版本={version}");
                    Save(migrated);
                    return migrated;
                }
            }

            if (version != BridgeSettings.CurrentCoordinateVersion)
            {
                BridgeLog.Warning("Settings", $"配置坐标版本过期（{version}），需要重新校准");
                return new BridgeSettings();
            }

            BridgeSettings settings = JsonSerializer.Deserialize<BridgeSettings>(json, JsonOptions) ?? new BridgeSettings();
            settings.Gpt ??= new AgentProfileSettings();
            settings.Claude ??= new AgentProfileSettings();
            settings.WorkBuddy ??= new AgentProfileSettings();
            settings.DeepSeekHarness ??= new AgentProfileSettings();
            BridgeLog.Info(
                "Settings",
                $"已加载多智能体配置，active={AgentCatalog.ToWireValue(settings.ActiveAgent)}，"
                + $"gptInput={settings.Gpt.InputPoint is not null}，claudeInput={settings.Claude.InputPoint is not null}，"
                + $"workbuddyInput={settings.WorkBuddy.InputPoint is not null}，"
                + $"dshInput={settings.DeepSeekHarness.InputPoint is not null}");
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

    private void TryMigrateLegacyFile()
    {
        var legacyRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexBridge");
        var legacyFile = Path.Combine(legacyRoot, "settings.json");
        if (!File.Exists(legacyFile))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(RootDirectory);
            File.Copy(legacyFile, FilePath);
            BridgeLog.Info("Settings", "已迁移旧版 Codex Bridge 配置");
        }
        catch (Exception exception)
        {
            BridgeLog.Warning("Settings", $"迁移旧版配置失败：{exception.Message}");
        }
    }

    private sealed class LegacyBridgeSettings
    {
        public int CoordinateVersion { get; set; }

        public CalibrationPoint? InputPoint { get; set; }

        public CalibrationPoint? SendPoint { get; set; }

        public CalibrationPoint? ScrollPoint { get; set; }
    }
}
