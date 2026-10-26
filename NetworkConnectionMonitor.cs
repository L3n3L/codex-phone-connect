using System.Net.NetworkInformation;

namespace CodexPhoneConnect;

public sealed class ConnectionAddressesChangedEventArgs(IReadOnlyList<NetworkConnection> connections) : EventArgs
{
    public IReadOnlyList<NetworkConnection> Connections { get; } = connections;
}

public sealed class NetworkConnectionMonitor(int port) : IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan EventDelay = TimeSpan.FromMilliseconds(350);
    private readonly object _sync = new();
    private System.Threading.Timer? _timer;
    private NetworkConnection[]? _previous;
    private bool _disposed;

    public event EventHandler<ConnectionAddressesChangedEventArgs>? AddressesChanged;

    public void Start()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_timer is not null)
            {
                return;
            }

            _timer = new System.Threading.Timer(Refresh, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            NetworkChange.NetworkAddressChanged += NetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged += NetworkAvailabilityChanged;
            _timer.Change(TimeSpan.Zero, RefreshInterval);
        }
    }

    private void NetworkAddressChanged(object? sender, EventArgs e) => ScheduleRefresh();

    private void NetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e) => ScheduleRefresh();

    private void ScheduleRefresh()
    {
        lock (_sync)
        {
            if (!_disposed)
            {
                _timer?.Change(EventDelay, RefreshInterval);
            }
        }
    }

    private void Refresh(object? state)
    {
        // Serialize snapshots and publication so a slow scan cannot overwrite a newer one.
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                var current = NetworkConnectionDiscovery.Discover(port).ToArray();
                if (_previous is not null && _previous.SequenceEqual(current))
                {
                    return;
                }

                BridgeLog.Info("Network", current.Length == 0
                    ? "暂无可用连接地址"
                    : $"连接地址已更新：{string.Join("；", current.Select(connection => $"{connection.Label}/{connection.InterfaceName}={connection.Url}"))}");
                AddressesChanged?.Invoke(this, new ConnectionAddressesChangedEventArgs(Array.AsReadOnly(current)));
                _previous = current;
            }
            catch (Exception exception)
            {
                BridgeLog.Error("Network", "刷新连接地址失败，保留上一次地址列表", exception);
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            NetworkChange.NetworkAddressChanged -= NetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged -= NetworkAvailabilityChanged;
            _timer?.Dispose();
            _timer = null;
        }
    }
}
