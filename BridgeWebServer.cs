using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace CodexPhoneConnect;

public sealed class BridgeWebServer : IAsyncDisposable
{
    private readonly int _port;
    private WebApplication? _application;

    public BridgeWebServer(int port)
    {
        _port = port;
        Address = $"http://电脑地址:{_port}";
    }

    public event EventHandler<SendJobReceivedEventArgs>? JobReceived;

    public event EventHandler<ScrollCommandReceivedEventArgs>? ScrollCommandReceived;

    public string Address { get; private set; }

    public void SetAddress(string address)
    {
        Address = address;
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
        application.MapGet("/api/status", () => Results.Ok(new BridgeStatus("running", true, Address)));
        application.MapPost("/api/jobs", (SendJobRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                BridgeLog.Warning("Http", $"拒绝空消息，requestId={request.RequestId ?? "-"}");
                return Results.BadRequest(new { error = "text_required" });
            }

            var jobId = Guid.NewGuid().ToString("N");
            BridgeLog.Info("Http", $"收到发送任务，jobId={jobId}，字符数={request.Text.Length}，requestId={request.RequestId ?? "-"}");
            try
            {
                JobReceived?.Invoke(this, new SendJobReceivedEventArgs(jobId, request));
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

            var commandId = Guid.NewGuid().ToString("N");
            BridgeLog.Debug("Http", $"收到滚动命令，commandId={commandId}，deltaY={deltaY}，requestId={request.RequestId ?? "-"}");
            try
            {
                ScrollCommandReceived?.Invoke(
                    this,
                    new ScrollCommandReceivedEventArgs(
                        commandId,
                        request with { DeltaY = deltaY }));
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

    private static string LoadPhonePage()
    {
        var pagePath = Path.Combine(AppContext.BaseDirectory, "prototype", "phone-scroll.html");
        if (!File.Exists(pagePath))
        {
            BridgeLog.Error("Http", $"手机页面文件不存在：{pagePath}");
            return "<!doctype html><meta charset=\"utf-8\"><p>手机页面文件缺失，请重新发布程序。</p>";
        }

        var page = File.ReadAllText(pagePath);
        // 原型页保留 demoOnly=true 供 5175 静态预览；正式服务运行时切换为真实接口。
        return page.Replace("const demoOnly = true;", "const demoOnly = false;", StringComparison.Ordinal);
    }

    private const string LegacyPhonePage = """
        <!doctype html>
        <html lang="zh-CN">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>Codex Phone Connect</title>
          <style>
            :root { color-scheme: light; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
            * { box-sizing: border-box; }
            html, body { width: 100%; height: 100%; margin: 0; }
            body { min-height: 100svh; margin: 0; background: #fff; color: #2d2d2d; }
            form { position: fixed; left: 16px; right: 16px; bottom: max(16px, env(safe-area-inset-bottom)); display: flex; max-width: 760px; margin: 0 auto; gap: 8px; align-items: flex-end; }
            textarea { flex: 1; min-height: 48px; max-height: 160px; resize: none; padding: 12px 14px; border: 1px solid #d9d9d4; border-radius: 12px; outline: none; font: inherit; line-height: 1.45; }
            textarea:focus { border-color: #a9a9a2; }
            button { width: 72px; height: 48px; border: 0; border-radius: 12px; background: #2d2d2d; color: #fff; font: inherit; cursor: pointer; }
            button:disabled { opacity: .45; cursor: default; }
            #state { position: fixed; left: 0; right: 0; bottom: calc(76px + env(safe-area-inset-bottom)); text-align: center; color: #777773; font-size: 12px; }
          </style>
        </head>
        <body>
          <div id="state"></div>
          <form id="send-form">
            <textarea id="message" rows="1" placeholder="输入要发送给 Codex 的内容"></textarea>
            <button id="send" type="submit">发送</button>
          </form>
          <script>
            const form = document.getElementById('send-form');
            const message = document.getElementById('message');
            const send = document.getElementById('send');
            const state = document.getElementById('state');
            form.addEventListener('submit', async (event) => {
              event.preventDefault();
              const text = message.value.trim();
              if (!text) return;
              send.disabled = true;
              state.textContent = '正在发送';
              try {
                const response = await fetch('/api/jobs', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ text }) });
                if (!response.ok) throw new Error('send_failed');
                message.value = '';
                state.textContent = '已发送';
              } catch (_) {
                state.textContent = '发送失败';
              } finally {
                send.disabled = false;
                message.focus();
              }
            });
          </script>
        </body>
        </html>
        """;
}
