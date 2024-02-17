using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Client.Services.FileSend.Utils;

namespace UDP_SENDER_FILE_TEST
{
    public class UdpFileSender
    {
        #region Statics
        public static readonly UInt32 MaxBlockSize = 8 * 1024;   // 8KB
        #endregion 

        enum SenderState
        {
            NotRunning,
            WaitingForFileRequest,
            PreparingFileForTransfer,
            SendingFileInfo,
            WaitingForInfoACK,
            Transfering
        }

        private UdpClient _client;
        public readonly int Port;
        public bool Running { get; private set; } = false;

        public readonly string FilesDirectory;
        private HashSet<string> _transferableFiles;
        private Dictionary<UInt32, Block> _blocks = new Dictionary<UInt32, Block>();
        private Queue<NetworkMessage> _packetQueue = new Queue<NetworkMessage>();

        private MD5 _hasher;

        public UdpFileSender(string filesDirectory, int port)
        {
            FilesDirectory = filesDirectory;
            Port = port;
            _client = new UdpClient(Port, AddressFamily.InterNetwork);
            _hasher = MD5.Create();
        }

        public void Init()
        {
            List<string> files = new List<string>(Directory.EnumerateFiles(FilesDirectory));
            _transferableFiles = new HashSet<string>(files.Select(s => s.Substring(FilesDirectory.Length + 1)));

            if (_transferableFiles.Count != 0)
            {
                Running = true;

                Console.WriteLine("I'll transfer these files:");
                foreach (string s in _transferableFiles)
                    Console.WriteLine("  {0}", s);
            }
            else
                Console.WriteLine("I don't have any files to transfer.");
        }

        public void Shutdown()
        {
            Running = false;
        }

        public void Run()
        {
            SenderState state = SenderState.WaitingForFileRequest;
            string requestedFile = "";
            IPEndPoint receiver = null;

            Action ResetTransferState = new Action(() =>
            {
                state = SenderState.WaitingForFileRequest;
                requestedFile = "";
                receiver = null;
                _blocks.Clear();
            });

            while (Running)
            {
                _checkForNetworkMessages();
                NetworkMessage nm = (_packetQueue.Count > 0) ? _packetQueue.Dequeue() : null;

                bool isBye = (nm == null) ? false : nm.Packet.IsBye;
                if (isBye)
                {
                    ResetTransferState();
                    Console.WriteLine("Received a BYE message, waiting for next client.");
                }
                switch (state)
                {
                    case SenderState.WaitingForFileRequest:

                        bool isRequestFile = (nm == null) ? false : nm.Packet.IsRequestFile;
                        if (isRequestFile)
                        {
                            RequestFilePacket REQF = new RequestFilePacket(nm.Packet);
                            AckPacket ACK = new AckPacket();
                            requestedFile = REQF.Filename;

                            Console.WriteLine("{0} has requested file file \"{1}\".", nm.Sender, requestedFile);

                            if (_transferableFiles.Contains(requestedFile))
                            {
                                receiver = nm.Sender;
                                ACK.Message = requestedFile;
                                state = SenderState.PreparingFileForTransfer;

                                Console.WriteLine("  We have it.");
                            }
                            else
                                ResetTransferState();

                            byte[] buffer = ACK.GetBytes();
                            _client.Send(buffer, buffer.Length, nm.Sender);
                        }
                        break;

                    case SenderState.PreparingFileForTransfer:
                        byte[] checksum;
                        UInt32 fileSize;
                        if (_prepareFile(requestedFile, out checksum, out fileSize))
                        {
                            InfoPacket INFO = new InfoPacket();
                            INFO.Checksum = checksum;
                            INFO.FileSize = fileSize;
                            INFO.MaxBlockSize = MaxBlockSize;
                            INFO.BlockCount = Convert.ToUInt32(_blocks.Count);

                            byte[] buffer = INFO.GetBytes();
                            _client.Send(buffer, buffer.Length, receiver);

                            Console.WriteLine("Sending INFO, waiting for ACK...");
                            state = SenderState.WaitingForInfoACK;
                        }
                        else
                            ResetTransferState();
                        break;

                    case SenderState.WaitingForInfoACK:
                        bool isAck = (nm == null) ? false : (nm.Packet.IsAck);
                        if (isAck)
                        {
                            AckPacket ACK = new AckPacket(nm.Packet);
                            if (ACK.Message == "INFO")
                            {
                                Console.WriteLine("Starting Transfer...");
                                state = SenderState.Transfering;
                            }
                        }
                        break;

                    case SenderState.Transfering:
                        bool isRequestBlock = (nm == null) ? false : nm.Packet.IsRequestBlock;
                        if (isRequestBlock)
                        {
                            RequestBlockPacket REQB = new RequestBlockPacket(nm.Packet);
                            Console.WriteLine("Got request for Block #{0}", REQB.Number);

                            Block block = _blocks[REQB.Number];
                            SendPacket SEND = new SendPacket();
                            SEND.Block = block;

                            byte[] buffer = SEND.GetBytes();
                            _client.Send(buffer, buffer.Length, nm.Sender);
                            Console.WriteLine("Sent Block #{0} [{1} bytes]", block.Number, block.Data.Length);
                        }
                        break;
                }

                Thread.Sleep(1);
            }

            if (receiver != null)
            {
                Packet BYE = new Packet(Packet.Bye);
                byte[] buffer = BYE.GetBytes();
                _client.Send(buffer, buffer.Length, receiver);
            }

            state = SenderState.NotRunning;
        }

        public void Close()
        {
            _client.Close();
        }

        private void _checkForNetworkMessages()
        {
            if (!Running)
                return;

            int bytesAvailable = _client.Available;
            if (bytesAvailable >= 4)
            {
                IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                byte[] buffer = _client.Receive(ref ep);

                NetworkMessage nm = new NetworkMessage();
                nm.Sender = ep;
                nm.Packet = new Packet(buffer);
                _packetQueue.Enqueue(nm);
            }
        }

        private bool _prepareFile(string requestedFile, out byte[] checksum, out UInt32 fileSize)
        {
            Console.WriteLine("Preparing the file to send...");
            bool good = false;
            fileSize = 0;

            try
            {
                byte[] fileBytes = File.ReadAllBytes(Path.Combine(FilesDirectory, requestedFile));
                checksum = _hasher.ComputeHash(fileBytes);
                fileSize = Convert.ToUInt32(fileBytes.Length);
                Console.WriteLine("{0} is {1} bytes large.", requestedFile, fileSize);

                Stopwatch timer = new Stopwatch();
                using (MemoryStream compressedStream = new MemoryStream())
                {
                    DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionMode.Compress, true);
                    timer.Start();
                    deflateStream.Write(fileBytes, 0, fileBytes.Length);
                    deflateStream.Close();
                    timer.Stop();

                    compressedStream.Position = 0;
                    long compressedSize = compressedStream.Length;
                    UInt32 id = 1;
                    while (compressedStream.Position < compressedSize)
                    {
                        long numBytesLeft = compressedSize - compressedStream.Position;
                        long allocationSize = (numBytesLeft > MaxBlockSize) ? MaxBlockSize : numBytesLeft;
                        byte[] data = new byte[allocationSize];
                        compressedStream.Read(data, 0, data.Length);

                        Block b = new Block(id++);
                        b.Data = data;
                        _blocks.Add(b.Number, b);
                    }
                    Console.WriteLine("{0} compressed is {1} bytes large in {2:0.000}s.", requestedFile, compressedSize, timer.Elapsed.TotalSeconds);
                    Console.WriteLine("Sending the file in {0} blocks, using a max block size of {1} bytes.", _blocks.Count, MaxBlockSize);
                    good = true;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not prepare the file for transfer, reason:");
                Console.WriteLine(e.Message);

                _blocks.Clear();
                checksum = null;
            }

            return good;
        }

    }
}
