namespace ConsoleApp1.Domain.ServisTransef.FileSendComm.Utils
{
    public class FileUtility
    {
        public static string sourceDirectory = "..\\..\\..\\Data\\Files\\";
        public static string destinationDirectory = "..\\..\\..\\Domain\\ServisTransef\\FileSendComm\\FileBuff\\";
        public static void CopyFileToDirectory(string fileName)
        {
            string sourceFilePath = Path.Combine(sourceDirectory, fileName);
            string destinationFilePath = Path.Combine(destinationDirectory, fileName);

            if (File.Exists(sourceFilePath))
            {
                ClearDirectory(destinationDirectory);

                File.Copy(sourceFilePath, destinationFilePath, true);
                Console.WriteLine($"Файл {fileName} був скопійований в {destinationDirectory}");
            }
            else
            {
                throw new Exception();
            }
        }

        public static void ClearDirectory(string directory)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(directory);
            foreach (FileInfo file in directoryInfo.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo subDirectory in directoryInfo.GetDirectories())
            {
                subDirectory.Delete(true);
            }
            Console.WriteLine($"Папка {directory} була очищена");
        }
    }
}
