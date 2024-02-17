using Client.Services.FileSend.Utils;
using ConsoleApp1.DataAccess.Entities;
using ConsoleApp1.DataAccess.Utils;
using ConsoleApp1.Domain.Network.Utils;
using ConsoleApp1.Domain.ServisTransef.FileSendComm.Utils;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ConsoleApp1.Domain.Network
{
    public class Server
    {
        #region Fields
        private TcpListener tcpServer;
        private Thread mainThread;
        private readonly int portNumber;
        private bool isRunning;
        private readonly NetworkInterface networkInterface;
        private readonly string serverName;
        private readonly List<ConnectedClient> clients = new List<ConnectedClient>();
        private ApplicationContext AppContext = new();
        public event EventHandler ClientConnected;
        public event EventHandler ClientDisconnected;
        #endregion

        #region Constructor

        public Server(int portNumber, object networkInterface, string serverName)
        {
            this.serverName = serverName;
            this.portNumber = portNumber;
            this.networkInterface = networkInterface as NetworkInterface;
            //CreateEventLog();
        }

        #endregion

        #region Server Start/Stop

        public void StartServer()
        {
            mainThread = new Thread(StartListen);
            mainThread.Start();
        }
        /// <summary>
        /// Server listens to specified port and accepts connection from client
        /// </summary>
        public void StartListen()
        {
            try
            {
                //var ip = (networkInterface != null)
                //? GetInterfaceIpAddress()
                //: IPAddress.Any;

                var ip = IPAddress.Parse("127.0.0.1");

                tcpServer = new TcpListener(ip, portNumber);
                tcpServer.Start();

                isRunning = true;
                Console.WriteLine("Server Started");
                while (isRunning)
                {
                    if (!tcpServer.Pending())
                    {
                        Thread.Sleep(500);
                        continue;
                    }
                    // New client is connected, call event to handle it
                    var clientThread = new Thread(NewClient);
                    var tcpClient = tcpServer.AcceptTcpClient();
                    tcpClient.ReceiveTimeout = 20000;
                    clientThread.Start(tcpClient.Client);
                }
            }
            catch (Exception ex)
            {
                ChatHelper.WriteToEventLog(Log.RadminNotDetected, EventLogEntryType.Error);
            }
        }

        private IPAddress GetInterfaceIpAddress()
        {
            if (networkInterface.Name == "Radmin VPN")
            {
                var ipAddresses = networkInterface.GetIPProperties().UnicastAddresses;
                return (from ip in ipAddresses where ip.Address.AddressFamily == AddressFamily.InterNetwork select ip.Address).FirstOrDefault();
            }
            throw new Exception();
            return IPAddress.None;
        }

        /// <summary>
        /// Method to stop TCP communication
        /// </summary>
        public void StopServer()
        {
            isRunning = false;
            if (tcpServer == null)
                return;
            clients.Clear();
            tcpServer.Stop();
        }

        #endregion

        #region Add/Remove Clients
        public void NewClient(object obj)
        {
            ClientAdded(this, new CustomEventArgs((Socket)obj));
        }

        /// <summary>
        /// When new client is added
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void ClientAdded(object sender, EventArgs e)
        {
            Console.WriteLine("CLIENT CONNECTED");
            var socket = ((CustomEventArgs)e).ClientSocket;
            var bytes = new byte[10240];
            var bytesRead = socket.Receive(bytes);

            var data = Data.GetBytes(bytes);

            if (data.Command == Command.Auth)
            {
                string email;
                string password;
                string[] parts = data.Message.Split(' ');
                email = parts[0];
                password = parts[1];
                if (AppContext.IsLogged(email, password))
                {
                    var client = AppContext.GeUserByIEmail(email);
                    clients.Add(new ConnectedClient(client, socket));
                    OnClientConnected(socket, client.Id);
                    socket.Send(new Data(Command.Good_Auth, "Server", data.From, "", "").ToBytes());

                    var state = new ChatHelper.StateObject
                    {
                        WorkSocket = socket
                    };

                    socket.BeginReceive(state.Buffer, 0, ChatHelper.StateObject.BUFFER_SIZE, 0,
                    OnReceive, state);

                    ChatHelper.WriteToEventLog(Log.ClientConnected, EventLogEntryType.Information);
                    return;
                }
                else
                {
                    socket.Send(new Data(Command.Bad_Auth, "Server", data.From, "", "").ToBytes());
                    return;
                }
            }
            else if (data.Command == Command.Reg)
            {
                try
                {
                    string[] fields = data.Message.Split(' ');
                    User user = new(Guid.NewGuid(), fields[0], fields[1], fields[2], fields[3], DateTime.Parse(fields[4]));
                    AppContext.AddUser(user);
                    socket.Send(new Data(Command.Good_Reg, "Server", data.From, "", "").ToBytes());
                }
                catch (Exception ex)
                {
                    socket.Send(new Data(Command.Bad_Reg, "Server", data.From, "", ex.Message).ToBytes());
                }
            }
        }

        public void OnReceive(IAsyncResult ar)
        {
            var state = ar.AsyncState as ChatHelper.StateObject;
            if (state == null)
                return;
            var handler = state.WorkSocket;
            if (!handler.Connected)
                return;
            try
            {
                var bytesRead = handler.EndReceive(ar);
                if (bytesRead <= 0)
                    return;

                ParseRequest(state, handler);
            }

            catch (Exception)
            {
                ChatHelper.WriteToEventLog(Log.TcpClientUnexpected, EventLogEntryType.Error);
                DisconnectClient(handler);
            }
        }

        /// <summary>
        /// Parse client request
        /// </summary>
        /// <param name="state"></param>
        /// <param name="handlerSocket"></param>
        private void ParseRequest(ChatHelper.StateObject state, Socket handlerSocket)
        {
            Console.WriteLine("Start parse");
            var data = Data.GetBytes(state.Buffer);
            if (data.Command == Command.Disconnect)
            {
                DisconnectClient(state.WorkSocket);
                return;
            }
            else if (data.Command == Command.Accept_File)
            {
                Console.WriteLine("Transfer file to server");
                var fileThread = new Thread(() => FileTool.ReceiverFile(data.ClientAddress, ChatHelper.file_client_port, data.Message));
                Thread.Sleep(1500);
                Console.WriteLine("Start transfer file");
                fileThread.Start();
                var client = clients.FirstOrDefault(cl => cl.user.Id.ToString() == data.To);
                if (client == null)
                    return;
                client.Connection.Send(data.ToBytes());
            }
            else if (data.Command == Command.Send_File)
            {
                Console.WriteLine($"Transfer file to user {data.From}");
                try
                {
                    FileUtility.CopyFileToDirectory(data.Message);
                }
                catch (Exception)
                {
                    Console.WriteLine("File not exist");
                    return;
                }

                var fileThread = new Thread(() => FileTool.SendFile("..\\..\\..\\Domain\\ServisTransef\\FileSendComm\\FileBuff\\", 62000));
            }

            handlerSocket.BeginReceive(state.Buffer, 0, ChatHelper.StateObject.BUFFER_SIZE, 0,
              OnReceive, state);
        }

        /// <summary>
        /// Disconnect connected  TCP client
        /// </summary>
        /// <param name="clientSocket"></param>
        public void DisconnectClient(Socket clientSocket)
        {
            var clientStr = clients.FirstOrDefault(k => k.Connection == clientSocket);
            if (clientStr == null)
                return;
            clientStr.IsConnected = false;
            OnClientDisconnected(clientSocket, clientStr.user.Id);

            clientSocket.Close();
            clients.Remove(clientStr);

            ChatHelper.WriteToEventLog(Log.ClientDisconnected, EventLogEntryType.Information);
        }


        private static void CreateEventLog()
        {
            if (EventLog.SourceExists(Log.ApplicationName))
                return;
            EventLog.CreateEventSource(Log.ApplicationName, Log.ApplicationName);
        }

        #endregion

        #region Event Invokers

        protected virtual void OnClientConnected(Socket clientSocket, Guid id)
        {
            var handler = ClientConnected;
            handler?.Invoke(id, new CustomEventArgs(clientSocket));
        }

        protected virtual void OnClientDisconnected(Socket clientSocket, Guid id)
        {
            var handler = ClientDisconnected;
            handler?.Invoke(id, new CustomEventArgs(clientSocket));
        }

        #endregion
    }
    /// <summary>
    /// Used to store custom network interface description
    /// </summary>
    public class NetworkInterfaceDescription
    {
        public string Description { get; set; }
        public NetworkInterfaceDescription(string description)
        {
            Description = description;
        }
    }
}
