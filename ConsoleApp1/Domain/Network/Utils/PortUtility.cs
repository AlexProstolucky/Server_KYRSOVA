using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1.Domain.Network.Utils
{
    public class PortUtility
    {
        public static int GetAvailablePort()
        {
            int minPort = 49152; // Мінімальний порт, що може бути призначений динамічно
            int maxPort = 65535; // Максимальний порт

            for (int port = minPort; port <= maxPort; port++)
            {
                // Створення TCP сокету для отримання вільного порту
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    try
                    {
                        socket.Bind(new IPEndPoint(IPAddress.Any, port)); // Прив'язка сокету до будь-якої IP адреси та поточного порту
                        return port; // Повернення поточного порту, якщо прив'язка пройшла успішно
                    }
                    catch (SocketException)
                    {
                        // Порт використовується, спробуємо наступний
                    }
                }
            }

            throw new SocketException(); // Якщо не вдалося знайти вільний порт
        }
    }
}
