using Client.Services.FileSend.Utils;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace UDP_SENDER_FILE_TEST
{
    class UdpFileReceiver
    {
        #region Statics
        public static readonly int MD5ChecksumByteSize = 16;
        #endregion 

        enum ReceiverState
        {
            NotRunning,
            RequestingFile,
            WaitingForRequestFileACK,
            WaitingForInfo,
            PreparingForTransfer,
            Transfering,
            TransferSuccessful,
        }

        private UdpClient _client;
        public readonly int Port;
        public readonly string Hostname;
        private bool _shutdownRequested = false;
        private bool _running = false;

        private Dictionary<UInt32, Block> _blocksReceived = new Dictionary<UInt32, Block>();
        private Queue<UInt32> _blockRequestQueue = new Queue<UInt32>();
        private Queue<NetworkMessage> _packetQueue = new Queue<NetworkMessage>();

        private MD5 _hasher;

        public UdpFileReceiver(string hostname, int port)
        {
            Port = port;
            Hostname = hostname;

            _client = new UdpClient(Hostname, Port);
            _hasher = MD5.Create();
        }


        public void Shutdown()
        {
            _shutdownRequested = true;
        }


        public void GetFile(string filename)
        {
            Console.WriteLine("Requesting file: {0}", filename);
            ReceiverState state = ReceiverState.RequestingFile;
            byte[] checksum = null;
            UInt32 fileSize = 0;
            UInt32 numBlocks = 0;
            UInt32 totalRequestedBlocks = 0;
            Stopwatch transferTimer = new Stopwatch();


            Action ResetTransferState = new Action(() =>
            {
                state = ReceiverState.RequestingFile;
                checksum = null;
                fileSize = 0;
                numBlocks = 0;
                totalRequestedBlocks = 0;
                _blockRequestQueue.Clear();
                _blocksReceived.Clear();
                transferTimer.Reset();
            });

            _running = true;
            bool senderQuit = false;
            bool wasRunning = _running;
            while (_running)
            {

                _checkForNetworkMessages();
                NetworkMessage nm = (_packetQueue.Count > 0) ? _packetQueue.Dequeue() : null;

                bool isBye = (nm == null) ? false : nm.Packet.IsBye;
                if (isBye)
                    senderQuit = true;

                switch (state)
                {
                    case ReceiverState.RequestingFile:
                        RequestFilePacket REQF = new RequestFilePacket();
                        REQF.Filename = filename;

                        byte[] buffer = REQF.GetBytes();
                        _client.Send(buffer, buffer.Length);

                        state = ReceiverState.WaitingForRequestFileACK;
                        break;

                    case ReceiverState.WaitingForRequestFileACK:
                        bool isAck = (nm == null) ? false : (nm.Packet.IsAck);
                        if (isAck)
                        {
                            AckPacket ACK = new AckPacket(nm.Packet);

                            if (ACK.Message == filename)
                            {
                                state = ReceiverState.WaitingForInfo;
                                Console.WriteLine("They have the file, waiting for INFO...");
                            }
                            else
                                ResetTransferState();
                        }
                        break;

                    case ReceiverState.WaitingForInfo:
                        bool isInfo = (nm == null) ? false : (nm.Packet.IsInfo);
                        if (isInfo)
                        {
                            InfoPacket INFO = new InfoPacket(nm.Packet);
                            fileSize = INFO.FileSize;
                            checksum = INFO.Checksum;
                            numBlocks = INFO.BlockCount;

                            Console.WriteLine("Received an INFO packet:");
                            Console.WriteLine("  Max block size: {0}", INFO.MaxBlockSize);
                            Console.WriteLine("  Num blocks: {0}", INFO.BlockCount);

                            AckPacket ACK = new AckPacket();
                            ACK.Message = "INFO";
                            buffer = ACK.GetBytes();
                            _client.Send(buffer, buffer.Length);

                            state = ReceiverState.PreparingForTransfer;
                        }
                        break;

                    case ReceiverState.PreparingForTransfer:
                        for (UInt32 id = 1; id <= numBlocks; id++)
                            _blockRequestQueue.Enqueue(id);
                        totalRequestedBlocks += numBlocks;

                        Console.WriteLine("Starting Transfer...");
                        transferTimer.Start();
                        state = ReceiverState.Transfering;
                        break;

                    case ReceiverState.Transfering:
                        if (_blockRequestQueue.Count > 0)
                        {
                            UInt32 id = _blockRequestQueue.Dequeue();
                            RequestBlockPacket REQB = new RequestBlockPacket();
                            REQB.Number = id;

                            buffer = REQB.GetBytes();
                            _client.Send(buffer, buffer.Length);

                            Console.WriteLine("Sent request for Block #{0}", id);
                        }

                        bool isSend = (nm == null) ? false : (nm.Packet.IsSend);
                        if (isSend)
                        {
                            SendPacket SEND = new SendPacket(nm.Packet);
                            Block block = SEND.Block;
                            _blocksReceived.Add(block.Number, block);

                            Console.WriteLine("Received Block #{0} [{1} bytes]", block.Number, block.Data.Length);
                        }

                        if ((_blockRequestQueue.Count == 0) && (_blocksReceived.Count != numBlocks))
                        {
                            for (UInt32 id = 1; id <= numBlocks; id++)
                            {
                                if (!_blocksReceived.ContainsKey(id) && !_blockRequestQueue.Contains(id))
                                {
                                    _blockRequestQueue.Enqueue(id);
                                    totalRequestedBlocks++;
                                }
                            }
                        }

                        if (_blocksReceived.Count == numBlocks)
                            state = ReceiverState.TransferSuccessful;
                        break;

                    case ReceiverState.TransferSuccessful:
                        transferTimer.Stop();

                        Packet BYE = new Packet(Packet.Bye);
                        buffer = BYE.GetBytes();
                        _client.Send(buffer, buffer.Length);

                        Console.WriteLine("Transfer successful; it took {0:0.000}s with a success ratio of {1:0.000}.",
                            transferTimer.Elapsed.TotalSeconds, (double)numBlocks / (double)totalRequestedBlocks);
                        Console.WriteLine("Decompressing the Blocks...");

                        if (_saveBlocksToFile(filename, checksum, fileSize))
                            Console.WriteLine("Saved file as {0}.", filename);
                        else
                            Console.WriteLine("There was some trouble in saving the Blocks to {0}.", filename);

                        _running = false;
                        break;

                }

                Thread.Sleep(1);

                _running &= !_shutdownRequested;
                _running &= !senderQuit;
            }

            if (_shutdownRequested && wasRunning)
            {
                Console.WriteLine("User canceled transfer.");

                Packet BYE = new Packet(Packet.Bye);
                byte[] buffer = BYE.GetBytes();
                _client.Send(buffer, buffer.Length);
            }

            if (senderQuit && wasRunning)
                Console.WriteLine("The sender quit on us, canceling the transfer.");

            ResetTransferState();
            _shutdownRequested = false;
        }

        public void Close()
        {
            _client.Close();
        }

        private void _checkForNetworkMessages()
        {
            if (!_running)
                return;

            int bytesAvailable = _client.Available;
            if (bytesAvailable >= 4)
            {
                IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                byte[] buffer = _client.Receive(ref ep);
                Packet p = new Packet(buffer);

                NetworkMessage nm = new NetworkMessage();
                nm.Sender = ep;
                nm.Packet = p;
                _packetQueue.Enqueue(nm);
            }
        }

        private bool _saveBlocksToFile(string filename, byte[] networkChecksum, UInt32 fileSize)
        {
            bool good = false;

            try
            {
                int compressedByteSize = 0;
                foreach (Block block in _blocksReceived.Values)
                    compressedByteSize += block.Data.Length;
                byte[] compressedBytes = new byte[compressedByteSize];

                int cursor = 0;
                for (UInt32 id = 1; id <= _blocksReceived.Keys.Count; id++)
                {
                    Block block = _blocksReceived[id];
                    block.Data.CopyTo(compressedBytes, cursor);
                    cursor += Convert.ToInt32(block.Data.Length);
                }

                using (MemoryStream uncompressedStream = new MemoryStream())
                using (MemoryStream compressedStream = new MemoryStream(compressedBytes))
                using (DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
                {
                    deflateStream.CopyTo(uncompressedStream);

                    uncompressedStream.Position = 0;
                    byte[] checksum = _hasher.ComputeHash(uncompressedStream);
                    if (!Enumerable.SequenceEqual(networkChecksum, checksum))
                        throw new Exception("Checksum of uncompressed blocks doesn't match that of INFO packet.");

                    uncompressedStream.Position = 0;
                    using (FileStream fileStream = new FileStream("E:\\Курсова\\Server\\Server\\ConsoleApp1\\Data\\Files\\" + filename, FileMode.Create))
                        uncompressedStream.CopyTo(fileStream);
                }

                good = true;
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not save the blocks to \"{0}\", reason:", filename);
                Console.WriteLine(e.Message);
            }

            return good;
        }
    }
}
