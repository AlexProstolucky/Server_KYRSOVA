using System.Text;

namespace Client.Services.FileSend.Utils
{
    public class Block
    {
        public uint Number { get; set; }
        public byte[] Data { get; set; } = new byte[0];

        #region Constructors
        public Block(uint number = 0)
        {
            Number = number;
        }

        public Block(byte[] bytes)
        {
            Number = BitConverter.ToUInt32(bytes, 0);

            Data = bytes.Skip(4).ToArray();
        }
        #endregion 

        public override string ToString()
        {
            string dataStr;
            if (Data.Length > 8)
                dataStr = Encoding.ASCII.GetString(Data, 0, 8) + "...";
            else
                dataStr = Encoding.ASCII.GetString(Data, 0, Data.Length);

            return string.Format(
                "[Block:\n" +
                "  Number={0},\n" +
                "  Size={1},\n" +
                "  Data=`{2}`]",
                Number, Data.Length, dataStr);
        }

        public byte[] GetBytes()
        {
            byte[] numberBytes = BitConverter.GetBytes(Number);

            byte[] bytes = new byte[numberBytes.Length + Data.Length];
            numberBytes.CopyTo(bytes, 0);
            Data.CopyTo(bytes, 4);

            return bytes;
        }
    }
}
