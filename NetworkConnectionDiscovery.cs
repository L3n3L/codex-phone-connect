using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace CodexPhoneConnect;

public enum NetworkConnectionKind
{
    Usb,
    WiFi,
    Ethernet,
}

public sealed record NetworkConnection(
    string InterfaceId,
    string InterfaceName,
    NetworkConnectionKind Kind,
    string IpAddress,
    string Url)
{
    public string Label => Kind switch
    {
        NetworkConnectionKind.Usb => "USB 网络共享",
        NetworkConnectionKind.WiFi => "Wi-Fi",
        _ => "有线网络",
    };
}

public static class NetworkConnectionDiscovery
{
    // Windows exposes USB tethering as Ethernet. Inspect device descriptions before
    // filtering virtual interfaces, but do not label every USB Ethernet dock as a phone.
    private static readonly string[] UsbTetheringMarkers =
    [
        "rndis", "remote ndis", "internet sharing", "usb ncm", "usb tether",
        "apple mobile device", "iphone", "android",
    ];

    private static readonly string[] VirtualInterfaceMarkers =
    [
        "vmware", "vmnet", "virtual", "vethernet", "hyper-v", "virtualbox",
        "wsl", "docker", "container", "loopback", "vpn", "wireguard", "wintun",
        "tap-", "tap ", "tun ", "tunnel", "tailscale", "zerotier", "hamachi",
        "sing-box", "mihomo", "clash", "cloudflare warp", "bluetooth", "蓝牙", "虚拟",
    ];

    public static IReadOnlyList<NetworkConnection> Discover(int port)
    {
        var connections = new List<NetworkConnection>();
        foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (network.OperationalStatus != OperationalStatus.Up || !IsLanInterface(network.NetworkInterfaceType))
            {
                continue;
            }

            var description = $"{network.Name} {network.Description}";
            var isUsb = ContainsAny(description, UsbTetheringMarkers);
            if (ContainsAny(description, VirtualInterfaceMarkers))
            {
                continue;
            }

            var kind = isUsb
                ? NetworkConnectionKind.Usb
                : network.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                    ? NetworkConnectionKind.WiFi
                    : NetworkConnectionKind.Ethernet;

            try
            {
                foreach (var unicast in network.GetIPProperties().UnicastAddresses)
                {
                    if (!IsUsableAddress(unicast.Address))
                    {
                        continue;
                    }

                    var address = unicast.Address.ToString();
                    connections.Add(new NetworkConnection(
                        network.Id, network.Name, kind, address, $"http://{address}:{port}"));
                }
            }
            catch (NetworkInformationException exception)
            {
                // An adapter can disappear between enumeration and reading its IPs.
                BridgeLog.Warning("Network", $"读取网卡地址失败，接口={network.Name}，原因={exception.Message}");
            }
        }

        return connections
            .OrderBy(connection => connection.Kind)
            .ThenBy(connection => connection.InterfaceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(connection => connection.IpAddress, StringComparer.Ordinal)
            .DistinctBy(connection => connection.IpAddress)
            .ToArray();
    }

    private static bool IsLanInterface(NetworkInterfaceType type) => type is
        NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet or
        NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetFx or
        NetworkInterfaceType.FastEthernetT;

    private static bool IsUsableAddress(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] is > 0 and < 224 && !(bytes[0] == 169 && bytes[1] == 254);
    }

    private static bool ContainsAny(string text, IEnumerable<string> markers) =>
        markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
