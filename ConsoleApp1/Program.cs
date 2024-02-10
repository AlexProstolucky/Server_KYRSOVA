using ConsoleApp1.Domain.Network;
using ConsoleApp1.Domain.Network.Utils;

internal class Program
{
    private static void Main(string[] args)
    {
        Server sr = new(10000, ServerNetworkInterface.GetServerNetworkInterface(), "VortexServer");
        sr.StartServer();
    }
}