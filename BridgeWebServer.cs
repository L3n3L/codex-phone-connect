using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace CodexPhoneConnect;

public sealed class BridgeWebServer : IAsyncDisposable
{
    private static readonly IReadOnlyDictionary<string, (string FileName, string ContentType)> BrandAssets =
        new Dictionary<string, (string FileName, string ContentType)>(StringComparer.OrdinalIgnoreCase)
        {
            ["openai-symbol.svg"] = ("openai-symbol.svg", "image/svg+xml"),
            ["openai-symbol.png"] = ("openai-symbol.png", "image/png"),
            ["claude.svg"] = ("claude.svg", "image/svg+xml"),
            ["claude.png"] = ("claude.png", "image/png"),
        };

    private readonly int _port;
    private WebApplication? _application;
    private int _activeAgent = (int)AgentId.Gpt;

    public BridgeWebServer(int port)
    {
        _port = port;
        Address = $"http://电脑地址:{_port}";
    }

    public event EventHandler<SendJobReceivedEventArgs>? JobReceived;

    public event EventHandler<ScrollCommandReceivedEventArgs>? ScrollCommandReceived;

    public event EventHandler<ActiveAgentChangedEventArgs>? ActiveAgentChanged;

    public string Address { get; private set; }

    public AgentId ActiveAgent => (AgentId)Volatile.Read(ref _activeAgent);

    public void SetAddress(string address)
    {
        Address = address;
    }

    public void SetActiveAgent(AgentId agent)
    {
        var previous = (AgentId)Interlocked.Exchange(ref _activeAgent, (int)agent);
        if (previous == agent)
        {
            return;
        }

        BridgeLog.Info("Agent", $"当前智能体已切换：{AgentCatalog.Get(previous).WireId} -> {AgentCatalog.Get(agent).WireId}");
        ActiveAgentChanged?.Invoke(this, new ActiveAgentChangedEventArgs(agent));
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_application is not null)
        {
            BridgeLog.Warning("Http", "重复请求启动 HTTP 服务，已忽略");
            return;
        }

        BridgeLog.Info("Http", $"准备启动 HTTP 服务，监听端口：{_port}");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(BridgeWebServer).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.WebHost.UseUrls($"http://0.0.0.0:{_port}");
        var application = builder.Build();

        application.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (Exception exception)
            {
                BridgeLog.Error("Http", $"请求处理失败：{context.Request.Method} {context.Request.Path}", exception);
                throw;
            }
        });

        application.MapGet("/", () => Results.Content(LoadPhonePage(), "text/html; charset=utf-8"));
        application.MapGet("/api/status", () => Results.Ok(new BridgeStatus(
            "running",
            true,
            Address,
            AgentCatalog.ToWireValue(ActiveAgent))));
        application.MapGet("/assets/brand/{assetName}", (string assetName) => ServeBrandAsset(assetName));

        application.MapPost("/api/control/agent", (AgentSelectionRequest request) =>
        {
            if (!AgentCatalog.TryParse(request.AgentId, out var agent))
            {
                BridgeLog.Warning("Http", $"拒绝未知智能体：{request.AgentId ?? "-"}");
                return Results.BadRequest(new { error = "unknown_agent" });
            }

            SetActiveAgent(agent);
            return Results.Ok(new AgentSelectionAccepted(AgentCatalog.ToWireValue(agent), "active"));
        });

        application.MapPost("/api/jobs", (SendJobRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                BridgeLog.Warning("Http", $"拒绝空消息，requestId={request.RequestId ?? "-"}");
                return Results.BadRequest(new { error = "text_required" });
            }

            if (!TryResolveAgent(request.AgentId, out var agent))
            {
                BridgeLog.Warning("Http", $"拒绝未知智能体消息，agent={request.AgentId ?? "-"}");
                return Results.BadRequest(new { error = "unknown_agent" });
            }

            var jobId = Guid.NewGuid().ToString("N");
            BridgeLog.Info(
                "Http",
                $"收到发送任务，jobId={jobId}，agent={AgentCatalog.ToWireValue(agent)}，字符数={request.Text.Length}，requestId={request.RequestId ?? "-"}");
            try
            {
                JobReceived?.Invoke(this, new SendJobReceivedEventArgs(
                    jobId,
                    request with { AgentId = AgentCatalog.ToWireValue(agent) },
                    agent));
            }
            catch (Exception exception)
            {
                BridgeLog.Error("Http", $"任务分发失败，jobId={jobId}", exception);
                return Results.Problem("任务分发失败");
            }

            return Results.Accepted($"/api/jobs/{jobId}", new SendJobAccepted(jobId, "queued"));
        });

        application.MapPost("/api/control/scroll", (ScrollRequest request) =>
        {
            var deltaY = Math.Clamp(request.DeltaY, -120, 120);
            if (deltaY == 0)
            {
                return Results.BadRequest(new { error = "delta_required" });
            }

            if (!TryResolveAgent(request.AgentId, out var agent))
            {
                BridgeLog.Warning("Http", $"拒绝未知智能体滚动，agent={request.AgentId ?? "-"}");
                return Results.BadRequest(new { error = "unknown_agent" });
            }

            var commandId = Guid.NewGuid().ToString("N");
            BridgeLog.Debug(
                "Http",
                $"收到滚动命令，commandId={commandId}，agent={AgentCatalog.ToWireValue(agent)}，deltaY={deltaY}，requestId={request.RequestId ?? "-"}");
            try
            {
                ScrollCommandReceived?.Invoke(
                    this,
                    new ScrollCommandReceivedEventArgs(
                        commandId,
                        request with
                        {
                            DeltaY = deltaY,
                            AgentId = AgentCatalog.ToWireValue(agent),
                        },
                        agent));
            }
            catch (Exception exception)
            {
                BridgeLog.Error("Http", $"滚动命令分发失败，commandId={commandId}", exception);
                return Results.Problem("滚动命令分发失败");
            }

            return Results.Accepted(
                $"/api/control/scroll/{commandId}",
                new ScrollCommandAccepted(commandId, "queued"));
        });

        await application.StartAsync(cancellationToken);
        _application = application;
        BridgeLog.Info("Http", $"HTTP 服务已启动：0.0.0.0:{_port}");
    }

    public async ValueTask DisposeAsync()
    {
        if (_application is null)
        {
            return;
        }

        BridgeLog.Info("Http", "正在停止 HTTP 服务");
        await _application.StopAsync();
        await _application.DisposeAsync();
        _application = null;
        BridgeLog.Info("Http", "HTTP 服务已停止");
    }

    private static IResult ServeBrandAsset(string assetName)
    {
        if (!BrandAssets.TryGetValue(assetName, out var asset))
        {
            return Results.NotFound();
        }

        var path = Path.Combine(AppContext.BaseDirectory, "assets", "brand", asset.FileName);
        return File.Exists(path)
            ? Results.File(path, asset.ContentType)
            : Results.NotFound();
    }

    private bool TryResolveAgent(string? requestedAgent, out AgentId agent)
    {
        if (string.IsNullOrWhiteSpace(requestedAgent))
        {
            agent = ActiveAgent;
            return true;
        }

        return AgentCatalog.TryParse(requestedAgent, out agent);
    }

    private static string LoadPhonePage()
    {
        var pagePath = Path.Combine(AppContext.BaseDirectory, "prototype", "phone-scroll.html");
        if (!File.Exists(pagePath))
        {
            BridgeLog.Error("Http", $"手机页面文件不存在：{pagePath}");
            return "<!doctype html><meta charset=\"utf-8\"><p>手机页面文件缺失，请重新发布程序。</p>";
        }

        var page = File.ReadAllText(pagePath)
            // 原型页保留 demoOnly=true 供静态预览；正式服务运行时切换为真实接口。
            .Replace("const demoOnly = true;", "const demoOnly = false;", StringComparison.Ordinal);

        var closingBody = page.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return closingBody >= 0
            ? page.Insert(closingBody, PhoneAgentShell.Markup)
            : page + PhoneAgentShell.Markup;
    }
}
