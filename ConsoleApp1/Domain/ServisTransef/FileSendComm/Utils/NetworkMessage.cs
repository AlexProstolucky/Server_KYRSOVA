using System.Net;

namespace Client.Services.FileSend.Utils
{
    internal class NetworkMessage
    {
        public IPEndPoint Sender { get; set; }
        public Packet Packet { get; set; }
    }
}
