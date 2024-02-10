using System.Net.NetworkInformation;

namespace ConsoleApp1.Domain.Network.Utils
{
    public class ServerNetworkInterface
    {
        public ServerNetworkInterface() { }
        public static NetworkInterface GetServerNetworkInterface()
        {
            NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface networkInterface in networkInterfaces)
            {
                if (networkInterface.Name == "Radmin VPN")
                {
                    return networkInterface;
                }
            }

            return networkInterfaces[0];
        }
    }
}
