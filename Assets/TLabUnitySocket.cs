using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class TLabUnitySocket
{
    private const int MAX_SOCKET_COUNT = 4;
    private Socket[] socket;

    // Destination address.
    private EndPoint[] remote;

    // Own IP address used for communication.
    private EndPoint[] local;

    // flag whether the socket is in use.
    private bool[] socketFlag;

    public TLabUnitySocket()
    {
        socket = new Socket[MAX_SOCKET_COUNT];

        remote = new EndPoint[MAX_SOCKET_COUNT];

        local = new EndPoint[MAX_SOCKET_COUNT];

        socketFlag = new bool[MAX_SOCKET_COUNT];

        for (int i = 0; i < socketFlag.Length; i++) socketFlag[i] = false;
    }

    public Socket GetSocketFromID(int clientPort, int serverPort, string serverAddr, int socketID)
    {
        if (CheckSocketRange(socketID) == 1) return (Socket)null;

        if (CheckSocketFlag(socketID) == 0)
        {
            Debug.Log("Socket Id: " + socketID.ToString() + " is not used. so create now.");
            if(CreateSocket(clientPort, serverPort, serverAddr, socketID) == 1)
            {
                Debug.Log("Socket Id: " + socketID.ToString() + " filed in create socket.");
                return (Socket)null;
            }
        }

        return socket[socketID];
    }

    public EndPoint GetRemoteFromID(int socketID)
    {
        if (CheckSocketRange(socketID) == 1) return (EndPoint)null;

        if (CheckSocketFlag(socketID) == 0)
        {
            Debug.Log("Socket Id: " + socketID.ToString() + " is not used.");
            return (EndPoint)null;
        }

        return remote[socketID];
    }

    private int CheckSocketRange(int socketID)
    {
        if (socketID > MAX_SOCKET_COUNT)
        {
            Debug.Log("Socket Id: " + socketID.ToString() + " is out of range.");
            return 1;
        }

        return 0;
    }

    private int CheckSocketFlag(int socketID)
    {
        if (socketFlag[socketID] == true) return 1;

        return 0;
    }

    public int CreateSocket(int clientPort, int serverPort, string serverAddr, int socketID)
    {
        if(CheckSocketRange(socketID) == 1)
        {
            return 1;
        }

        if(CheckSocketFlag(socketID) == 1)
        {
            Debug.Log("Socket Id: " + socketID.ToString() + " is already used.");
            return 1;
        }

        Debug.Log("Socket create: start");

        local[socketID] = (EndPoint)(new IPEndPoint(IPAddress.Any, clientPort));
        remote[socketID] = (EndPoint)(new IPEndPoint(IPAddress.Parse(serverAddr), serverPort));
        socket[socketID] = new Socket(remote[socketID].AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        socket[socketID].Bind(local[socketID] as EndPoint);
        socket[socketID].Connect(remote[socketID]);
        Debug.Log("Socket" + socketID.ToString() + ": created.");

        socketFlag[socketID] = true;

        return 0;
    }

    public int CloseSocket(int socketID)
    {
        if (CheckSocketRange(socketID) == 1)
        {
            return 1;
        }

        if (CheckSocketFlag(socketID) == 0)
        {
            Debug.Log("Socket Id: " + socketID.ToString() + " is not used.");
            return 1;
        }

        Debug.Log("Socket" + socketID.ToString() + ": shutdown");
        socket[socketID].Shutdown(SocketShutdown.Both);

        socket[socketID].Close();
        Debug.Log("Socket" + socketID.ToString() + ": disconnect");

        socket[socketID].Dispose();
        remote[socketID] = null;
        local[socketID] = null;
        socketFlag[socketID] = false;

        return 0;
    }

    public int CloseAllSocket()
    {
        for (int i = 0; i < MAX_SOCKET_COUNT; i++) CloseSocket(i);
        return 0;
    }
}
