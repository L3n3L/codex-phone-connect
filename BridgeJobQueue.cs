using System.Threading.Channels;

namespace CodexPhoneConnect;

public sealed class BridgeJobQueue : IAsyncDisposable
{
    private readonly Channel<BridgeInputCommand> _jobs = Channel.CreateUnbounded<BridgeInputCommand>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
    });
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _worker;

    public void Start(Func<BridgeInputCommand, CancellationToken, Task> handler)
    {
        if (_worker is not null)
        {
            return;
        }

        _worker = Task.Run(() => RunAsync(handler));
        BridgeLog.Info("Queue", "单线程发送队列已启动");
    }

    public bool Enqueue(BridgeInputCommand command)
    {
        var accepted = _jobs.Writer.TryWrite(command);
        if (accepted)
        {
            BridgeLog.Info("Queue", $"输入命令进入串行队列，commandId={command.CommandId}，type={command.GetType().Name}");
        }
        else
        {
            BridgeLog.Warning("Queue", $"输入命令进入串行队列失败，commandId={command.CommandId}");
        }

        return accepted;
    }

    public async ValueTask DisposeAsync()
    {
        _jobs.Writer.TryComplete();
        _cancellation.Cancel();

        if (_worker is not null)
        {
            try
            {
                await _worker;
            }
            catch (OperationCanceledException)
            {
                // Expected during application shutdown.
            }
        }

        _cancellation.Dispose();
        BridgeLog.Info("Queue", "发送队列已停止");
    }

    private async Task RunAsync(Func<BridgeInputCommand, CancellationToken, Task> handler)
    {
        try
        {
            await foreach (var command in _jobs.Reader.ReadAllAsync(_cancellation.Token))
            {
                try
                {
                    await handler(command, _cancellation.Token);
                }
                catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    BridgeLog.Error("Queue", $"输入命令处理失败，commandId={command.CommandId}", exception);
                }
            }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
            // Expected during application shutdown.
        }
    }
}
