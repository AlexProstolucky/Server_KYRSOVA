using System.Net.NetworkInformation;

namespace ConsoleApp1.Domain.Network.Utils
{
    public class ServerNetworkInterface
    {
        public ServerNetworkInterface() { }
        public static NetworkInterface GetServerNetworkInterface()
        {
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

            NetworkInterface radminVPNInterface = interfaces.FirstOrDefault(
                iface => iface.Name.Equals("Radmin VPN", StringComparison.OrdinalIgnoreCase));

            return radminVPNInterface;
        }
    }
}
