using System.Collections;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace TLab.MTPEG
{
    public class UDPSocket
    {
        public Socket socket;
        public EndPoint local;
        public EndPoint remote;
    }

    public static class UDPSocketUtil
    {
        private static Hashtable m_hashTable = new Hashtable();

        private static string THIS_NAME = "[udp socket util] ";

        public static bool GetSocket(ref UDPSocket udpSocket, int id)
        {
            if (!m_hashTable.ContainsKey(id))
            {
                Debug.LogError(THIS_NAME + $"No sockets are registered for id: {id}");

                return false;
            }

            udpSocket = m_hashTable[id] as UDPSocket;

            return true;
        }

        public static bool GetRemote(ref EndPoint endPoint, int id)
        {
            if (!m_hashTable.ContainsKey(id))
            {
                Debug.LogError(THIS_NAME + $"No sockets are registered for id: {id}");

                return false;
            }

            var udpSocket = m_hashTable[id] as UDPSocket;

            endPoint = udpSocket.remote;

            return true;
        }

        public static bool CreateSocket(ref UDPSocket udpSocket, int clientPort, int serverPort, string serverAddr, int id)
        {
            if (m_hashTable.ContainsKey(id))
            {
                Debug.LogError(THIS_NAME + $"the socket is already registered for id: {id}");

                udpSocket = m_hashTable[id] as UDPSocket;

                return false;
            }

            EndPoint local = new IPEndPoint(IPAddress.Any, clientPort);
            EndPoint remote = new IPEndPoint(IPAddress.Parse(serverAddr), serverPort);
            Socket socket = new Socket(remote.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

            socket.Bind(local);
            socket.Connect(remote);

            udpSocket = new UDPSocket()
            {
                local = local,
                remote = remote,
                socket = socket,
            };

            m_hashTable[id] = udpSocket;

            return true;
        }

        public static bool CloseSocket(int id)
        {
            if (!m_hashTable.ContainsKey(id))
            {
                Debug.LogError(THIS_NAME + $"no sockets are registered for id: {id}");

                return false;
            }

            var udpSocket = m_hashTable[id] as UDPSocket;
            udpSocket.socket.Shutdown(SocketShutdown.Both);
            udpSocket.socket.Close();
            udpSocket.socket.Dispose();

            m_hashTable.Remove(id);

            return true;
        }

        public static void CloseAllSocket()
        {
            foreach (int id in m_hashTable.Keys)
            {
                CloseSocket(id);
            }
        }
    }
}
