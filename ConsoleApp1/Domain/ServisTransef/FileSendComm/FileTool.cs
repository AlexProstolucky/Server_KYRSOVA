using UDP_SENDER_FILE_TEST;

namespace Client.Services.FileSend.Utils
{
    internal class FileTool
    {
        public static void SendFile(string filedirectory, int port)
        {
            UdpFileSender fileSender = new(filedirectory, port);

            fileSender.Init();
            fileSender.Run();
            fileSender.Close();
        }


        public static void ReceiverFile(string server_ip, int port, string filename)
        {
            UdpFileReceiver fileReceiver = new(server_ip, port);

            fileReceiver.GetFile(filename);
            fileReceiver.Close();
        }
    }
}
