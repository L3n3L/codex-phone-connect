using System.Threading.Channels;

namespace CodexPhoneConnect;

public sealed record BridgeJob(string JobId, string Text);

public sealed class BridgeJobQueue : IAsyncDisposable
{
    private readonly Channel<BridgeJob> _jobs = Channel.CreateUnbounded<BridgeJob>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
    });
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _worker;

    public void Start(Func<BridgeJob, CancellationToken, Task> handler)
    {
        if (_worker is not null)
        {
            return;
        }

        _worker = Task.Run(() => RunAsync(handler));
        BridgeLog.Info("Queue", "单线程发送队列已启动");
    }

    public bool Enqueue(BridgeJob job)
    {
        var accepted = _jobs.Writer.TryWrite(job);
        if (accepted)
        {
            BridgeLog.Info("Queue", $"任务进入发送队列，jobId={job.JobId}");
        }
        else
        {
            BridgeLog.Warning("Queue", $"任务进入发送队列失败，jobId={job.JobId}");
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

    private async Task RunAsync(Func<BridgeJob, CancellationToken, Task> handler)
    {
        try
        {
            await foreach (var job in _jobs.Reader.ReadAllAsync(_cancellation.Token))
            {
                try
                {
                    await handler(job, _cancellation.Token);
                }
                catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    BridgeLog.Error("Queue", $"任务处理失败，jobId={job.JobId}", exception);
                }
            }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
            // Expected during application shutdown.
        }
    }
}
