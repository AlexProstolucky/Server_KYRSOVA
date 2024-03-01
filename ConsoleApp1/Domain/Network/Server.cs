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
        public void StartListen()
        {
            try
            {
                //var ip = GetInterfaceIpAddress();
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

        public void ClientAdded(object sender, EventArgs e)
        {
            Console.WriteLine("Client connected");
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
                    if (client.Friends.Count == 0 && client.FriendsRequests.Count == 0)
                    {
                        socket.Send(new Data(Command.Good_Auth, "Server", data.From, "", client.Id.ToString() + " "
                            + client.Nickname + " " + client.Friends.Count.ToString() + " " + client.FriendsRequests.Count.ToString()).ToBytes());
                    }
                    else
                    {
                        string rez_friends = string.Empty;
                        string rez_req = string.Empty;
                        if (client.Friends.Count > 0)
                        {
                            foreach (var friend in client.Friends)
                            {
                                var buff = AppContext.GeUserById(friend);
                                rez_friends += friend.ToString() + " " + buff.Nickname + " ";
                            }
                            rez_friends = rez_friends.TrimEnd();
                        }
                        if (client.FriendsRequests.Count > 0)
                        {
                            foreach (var req in client.FriendsRequests)
                            {
                                var buff = AppContext.GeUserById(req);
                                rez_req += req.ToString() + " " + buff.Nickname + " ";
                            }
                            rez_req = rez_req.TrimEnd();
                        }
                        socket.Send(new Data(Command.Good_Auth, "Server", data.From, "", client.Id.ToString() + " "
                            + client.Nickname + " " + client.Friends.Count.ToString() + " " + rez_friends + " "
                            + client.FriendsRequests.Count.ToString() + " " + rez_req).ToBytes());
                    }

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
                    // TODO DATATIME.PARSE
                    string[] fields = data.Message.Split(' ');
                    User user = new(Guid.NewGuid(), fields[0], fields[1], fields[2], fields[3], DateTime.Now);
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
        private void ParseRequest(ChatHelper.StateObject state, Socket handlerSocket)
        {
            Console.WriteLine("Start parse request");
            var data = Data.GetBytes(state.Buffer);
            if (data.Command == Command.Disconnect)
            {
                DisconnectClient(state.WorkSocket);
                return;
            }
            else if (data.Command == Command.Accept_File)
            {
                Console.WriteLine($"Transfer file to server from {data.From}");
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
                string[] parts = data.Message.Split(' ');
                string filename = parts[0];
                int port = int.Parse(parts[1]);
                Console.WriteLine($"Transfer file to user {data.From}");
                try
                {
                    FileUtility.CopyFileToDirectory(filename);
                }
                catch (Exception)
                {
                    Console.WriteLine("File not exist");
                    return;
                }
                var fileThread = new Thread(() => FileTool.SendFile("..\\..\\..\\Domain\\ServisTransef\\FileSendComm\\FileBuff", port));
                fileThread.Start();
                Console.WriteLine("Start transfer file");
            }
            else if (data.Command == Command.Accept_Port)
            {
                handlerSocket.Send(new Data(Command.Accept_Port, "Server", data.From, "", PortUtility.GetAvailablePort().ToString()).ToBytes());
            }
            else if (data.Command == Command.Request_Call)
            {
                try
                {
                    var secondCallMember = clients.Where(u => u.user.Id.ToString() == data.To).FirstOrDefault();
                    if (secondCallMember == null)
                    {
                        handlerSocket.Send(new Data(Command.UserNotConnected, "Server", data.From, "", "").ToBytes());
                        return;
                    }
                    secondCallMember.Connection.Send(data.ToBytes());
                }
                catch (Exception ex)
                {
                    handlerSocket.Send(new Data(Command.Cancel_Call, "Server", data.From, "", ex.Message).ToBytes());
                }
            }
            else if (data.Command == Command.Accept_Call)
            {
                try
                {
                    var firstCallMember = clients.Where(u => u.user.Id.ToString() == data.To).FirstOrDefault();
                    if (firstCallMember == null)
                    {
                        handlerSocket.Send(new Data(Command.UserNotConnected, "Server", data.From, "", "").ToBytes());
                        return;
                    }
                    firstCallMember.Connection.Send(data.ToBytes());

                }
                catch (Exception ex)
                {
                    handlerSocket.Send(new Data(Command.Cancel_Call, "Server", data.From, "", ex.Message).ToBytes());
                }
            }
            else if (data.Command == Command.Cancel_Call)
            {
                var firstCallMember = clients.Where(u => u.user.Id.ToString() == data.To).FirstOrDefault();
                if (firstCallMember != null)
                {
                    firstCallMember.Connection.Send(new Data(Command.Cancel_Call, data.From, data.To, data.ClientAddress, "").ToBytes());
                }

            }
            else if (data.Command == Command.Send_Message)
            {
                var user = clients.Where(u => u.user.Id.ToString() == data.To).FirstOrDefault();
                if (user != null)
                {
                    user.Connection.Send(data.ToBytes());
                }
                else handlerSocket.Send(new Data(Command.UserNotConnected, "Server", data.To, data.ClientAddress, "").ToBytes());
            }
            else if (data.Command == Command.FriendRequest)
            {
                var secUserNetwork = clients.Where(u => u.user.Id.ToString() == data.To).FirstOrDefault();
                var secUserDB = AppContext.Users.Where(u => u.Id.ToString() == data.To).FirstOrDefault();
                if (secUserNetwork == null && secUserDB != null)
                {
                    secUserDB.FriendsRequests.Add(Guid.Parse(data.From));
                }
                else if (secUserNetwork != null && secUserDB != null)
                {
                    secUserNetwork.Connection.Send(new Data(Command.FriendRequest, data.From, data.To, data.ClientAddress, "").ToBytes());
                    secUserDB.FriendsRequests.Add(Guid.Parse(data.From));
                }
                AppContext.SaveChanges();
            }
            else if (data.Command == Command.AcceptFriendRequest)
            {
                var firstUserDB = AppContext.Users.Where(u => u.Id.ToString() == data.From).FirstOrDefault();

                var secUserDB = AppContext.Users.Where(u => u.Id.ToString() == data.To).FirstOrDefault();
                if (firstUserDB != null && secUserDB != null)
                    if (firstUserDB.FriendsRequests.Contains(Guid.Parse(data.To)))
                    {
                        var secUserNetwork = clients.Where(u => u.user.Id.ToString() == data.To).FirstOrDefault();
                        firstUserDB.FriendsRequests.Remove(Guid.Parse(data.To));
                        AppContext.MakeFriends(Guid.Parse(data.From), Guid.Parse(data.To));//вже є saveChanges
                        if (secUserNetwork != null) secUserNetwork.Connection.Send(new Data(Command.NewFriend, data.From, data.To, data.ClientAddress, data.Message).ToBytes());
                    }
            }
            else if (data.Command == Command.DeclineFriendRequest)
            {
                var firstUserDB = AppContext.Users.Where(u => u.Id.ToString() == data.From).FirstOrDefault();
                if (firstUserDB != null && firstUserDB.FriendsRequests.Contains(Guid.Parse(data.To))) firstUserDB.FriendsRequests.Remove(Guid.Parse(data.To));
                AppContext.SaveChanges();
            }

            handlerSocket.BeginReceive(state.Buffer, 0, ChatHelper.StateObject.BUFFER_SIZE, 0,
              OnReceive, state);
        }

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
    public class NetworkInterfaceDescription
    {
        public string Description { get; set; }
        public NetworkInterfaceDescription(string description)
        {
            Description = description;
        }
    }
}
