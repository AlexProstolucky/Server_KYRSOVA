using System.Net.Sockets;

namespace ConsoleApp1.Domain.Network
{
    public class CustomEventArgs : EventArgs
    {
        public Socket ClientSocket { get; set; }

        public CustomEventArgs(Socket clientSocket)
        {
            ClientSocket = clientSocket;
        }
    }
}
