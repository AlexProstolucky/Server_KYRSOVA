using ConsoleApp1.DataAccess.Entities;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace ConsoleApp1.Domain.Network.Utils
{
    public static class ChatHelper
    {
        #region Titles

        public const string CONVERSATION = "Conversation";
        public const string INCOMING_CALL = "Incoming call from";
        public const string OUTCOMING_CALL = "Calling to";
        public const string FILE_TRANSFER = "Recieve file from {0}?";
        public const string TRANSFER_CANCELED = "File Transfer canceled";
        public const string TRANSFERED = "File {0} successfully transfered";
        public const string GLOBAL = "Global";
        public const string SETTINGS = "Settings";
        public const string CONNECTED = "connected";
        public const string DISCONNECTED = "disconnected";
        public const string LOCAL = "127.0.0.1";
        public const string VERSION = "1.0";
        public const string APP_NAME = "VortexServer";
        public const string SOFTWARE = "Software";
        public const string NO_USERS_ONLINE = "no users online";
        public const string PROFILE = "Profile";
        public const string FILE_FILTER_ALL = "All files (*.*)|*.*";
        #endregion

        #region Registry Keys

        public const string LAUNCH_ON_STARTUP = "LaunchOnStartup";
        public const string DOUBLE_CLICK_TO_CALL = "DoubleClickToCall";
        public const string SCHEME = "Scheme";
        public const string DARK = "Dark";
        public const string LIGHT = "Light";

        #endregion

        #region Errors
        public const string PORT_ERROR = "Port number should be between 0 and 65535";
        #endregion

        #region Messages 
        public static string WelcomeMessage = string.Format("{0}: ** Welcome to main chat room, Click on any user to start chat**\n", DateTime.Now.ToString("HH:mm:ss"));
        #endregion

        public const int file_client_port = 63001;
        public class StateObject
        {
            // Client  socket.
            public Socket WorkSocket = null;
            // Size of receive buffer.
            public const int BUFFER_SIZE = 5242880;
            // Receive buffer.
            public byte[] Buffer = new byte[BUFFER_SIZE];
            // Received data string.
            public StringBuilder Sb = new StringBuilder();
        }

        public static void WriteToEventLog(string message, EventLogEntryType type)
        {
            Console.WriteLine(message);
            // EventLog.WriteEntry(Log.ApplicationName, message, type);
        }

        public static string ChatWith(string name)
        {
            return string.Format("** Conversation with {0} **\n", name);
        }
    }

    /// <summary>
    /// Data structure to interact with server
    /// </summary>
    internal class Data
    {
        public Command Command { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string ClientAddress { get; set; }
        public string Message { get; set; }

        public Data(Command command, string from, string to, string clientAddress, string message)
        {
            Command = command;
            From = from;
            To = to;
            ClientAddress = clientAddress;
            Message = message;
        }

        public byte[] ToBytes()
        {
            string json = JsonConvert.SerializeObject(this);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public static Data GetBytes(byte[] dataBytes)
        {
            string json = System.Text.Encoding.UTF8.GetString(dataBytes);
            return JsonConvert.DeserializeObject<Data>(json);
        }

        public override string ToString()
        {
            return $"Command: {Command}, From: {From}, To: {To}, Client Address: {ClientAddress}, Message: {Message}";
        }
    }

    // Команди які обробляє сервер і клієнт
    public enum Command
    {
        Good_Auth, // Успішний вхід
        Bad_Auth, // Вхід пішов по пизді
        Good_Reg, // Успішна реєстрація
        Bad_Reg, // Реєстрація пішла по пизді
        Accept_Port, // Прийняти порт
        Accept_File, // Cервер отримує файл, і клієнта, якому цей файл надіслати, але надсилає тільки відомісті, а саме назву і розширення
        Send_File, // Запит клієнта на завантаження файла, який є в нього в доступі(інфа)
        Send_Message, // Просте повідомлення
        Request_Call, // Запит на дзвінок
        Accept_Call, // Відповідь від клієнта на  Request
        Cancel_Call, // Відповідь від клієнта на  Request
        Check_Сonnection, // При включенні прогарми, буде чек зєднання
        Synchronization, // Коли клієнт заходить в чат, потрібно йому получити дані, а саме текст і які файли йому доступні(переписку)
        Disconnect, // Від'єднання клієнта
        Auth, // Вхід
        Reg, // Реєстрація
        Null,
    }

    public class ConnectedClient
    {
        public User user;
        private readonly Socket connection;
        public bool IsConnected { get; set; }
        public Socket Connection
        {
            get { return connection; }
        }

        public ConnectedClient(User _user, Socket connection)
        {
            this.user = _user;
            this.connection = connection;
        }
    }
}
