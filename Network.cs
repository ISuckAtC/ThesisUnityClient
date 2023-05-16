using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;


public enum PacketType
{
    rts,
    input,
    update
}
public enum NetworkMode
{
    servercontrol,
    serverpass
}
public class Network
{
    private static TcpClient tcpClient = new TcpClient();
    private static NetworkStream ns;
    private static Dictionary<int, UdpClient> udpClients = new Dictionary<int, UdpClient>();
    private static UdpClient mainUdpClient;
    public static int matchMakingPort = 6969;

    public delegate void OnPacketRecieveHandler(Packet packet);

    public static event OnPacketRecieveHandler onPacketRecieve;
    public static bool running = false;
    public static int localTickrate;
    public static PacketType packetType;
    public static NetworkMode networkMode;
    public static Packet sendPacket;
    public static List<RTSPacket> queuedActions = new List<RTSPacket>();
    public static int distanceLatency;
    public static int PacketSize
    {
        get
        {
            switch (packetType)
            {
                case PacketType.rts: return 16;
                case PacketType.update: return 16;
                case PacketType.input: return 16;
                default: return -1;
            }
        }
    }
    public static int localId;
    public static int hardPlayerCount = 2;
    public static long[] lowestLastRecieved = new long[hardPlayerCount - 1];
    public static int playerCount;
    public static bool playerCountChanged = false;
    public static int sendPort;
    public static long packetId = 0;
    public static long LowestLastReceived
    {
        get
        {
            if (hardPlayerCount < 2) throw new Exception("?");
            long lowest = long.MaxValue;
            for (int i = 0; i < lowestLastRecieved.Length; ++i) if (lowestLastRecieved[i] < lowest) lowest = lowestLastRecieved[i];
            return lowest;
        }
    }
    public static Dictionary<long, DateTimeOffset> packetTimes = new Dictionary<long, DateTimeOffset>();
    public static List<double> latencyTimes = new List<double>();

    public static void Connect()
    {
        Task connection = Task.Run(async () => await ConnectAsync());
    }

    public static async Task ConnectAsyncServercontrol()
    {
        byte[] buffer = new byte[17];

        await ns.ReadAsync(buffer, 0, 17);

        if (buffer[0] == 1)
        {
            Console.WriteLine("Recieved OK input from server");
        }

        playerCount = BitConverter.ToInt32(buffer, 1);
        playerCountChanged = true;
        localId = BitConverter.ToInt32(buffer, 5);

        Console.WriteLine("I am id: " + localId);

        sendPort = BitConverter.ToInt32(buffer, 9);

        if (packetType != PacketType.rts) sendPacket.Id = localId;

        //udpClients.Add(new UdpClient(0, AddressFamily.InterNetwork));
        mainUdpClient.Connect("127.0.0.1", sendPort);

        buffer = BitConverter.GetBytes(((IPEndPoint)mainUdpClient.Client.LocalEndPoint).Port);

        await ns.WriteAsync(buffer, 0, 4);

        buffer = new byte[1];

        await ns.ReadAsync(buffer, 0, 1);

        running = true;

        Task sendLoop = Task.Run(async () => await SendLoop());
        Task recieveLoop = Task.Run(async () => await RecieveLoop());
    }

    public static async Task ConnectAsyncServerpass()
    {
        byte[] buffer = new byte[17];

        await ns.ReadAsync(buffer, 0, 17);

        if (buffer[0] == 1)
        {
            UnityEngine.Debug.Log("Recieved OK input from server");
        }

        playerCount = BitConverter.ToInt32(buffer, 1);
        playerCountChanged = true;
        localId = BitConverter.ToInt32(buffer, 5);

        sendPort = BitConverter.ToInt32(buffer, 9);

        if (packetType != PacketType.rts) sendPacket.Id = localId;

        //udpClients.Add(new UdpClient(0, AddressFamily.InterNetwork));
        mainUdpClient.Connect("127.0.0.1", sendPort);

        buffer = BitConverter.GetBytes(((IPEndPoint)mainUdpClient.Client.LocalEndPoint).Port);

        await ns.WriteAsync(buffer, 0, 4);

        buffer = new byte[1];

        await ns.ReadAsync(buffer, 0, 1);

        running = true;

        Task sendLoop = Task.Run(async () => await SendLoop());
        Task recieveLoop = Task.Run(async () => await RecieveLoop());
    }

    public static async Task ConnectAsyncServerless()
    {
        try
        {
            byte[] buffer = new byte[5];

            await ns.ReadAsync(buffer, 0, 5);

            localId = BitConverter.ToInt32(buffer, 1);

            sendPacket.Id = localId;

            int localPort = ((IPEndPoint)mainUdpClient.Client.LocalEndPoint).Port;

            buffer = BitConverter.GetBytes(localPort);

            await ns.WriteAsync(buffer, 0, 4);

            buffer = new byte[8 * hardPlayerCount];

            await ns.ReadAsync(buffer, 0, 8 * hardPlayerCount);

            UnityEngine.Debug.Log("Got ports");

            for (int i = 0; i < hardPlayerCount; ++i)
            {
                int id = BitConverter.ToInt32(buffer, i * 8);
                int port = BitConverter.ToInt32(buffer, (i * 8) + 4);
                if (id != localId)
                {
                    udpClients.Add(id, new UdpClient(0, AddressFamily.InterNetwork));
                    udpClients[id].Connect("127.0.0.1", port);
                }
            }

            List<int> connections = new List<int>();
            List<int> doneConnections = new List<int>();

            byte[] myIdBuffer = BitConverter.GetBytes(localId);

            Task waitForConnections = Task.Run(async () =>
            {
                byte[] doneBuffer = new byte[5];
                doneBuffer[0] = 1;
                Array.Copy(myIdBuffer, 0, doneBuffer, 1, 4);
                while (doneConnections.Count < hardPlayerCount - 1)
                {
                    byte[] idBuffer = new byte[4];
                    UdpReceiveResult res = await mainUdpClient.ReceiveAsync();
                    Array.Copy(res.Buffer, 1, idBuffer, 0, 4);
                    int id = BitConverter.ToInt32(idBuffer);
                    if (res.Buffer[0] == 1)
                    {
                        doneConnections.Add(id);
                    }
                    if (!connections.Contains(id))
                    {
                        connections.Add(id);
                    }
                    await udpClients[id].SendAsync(doneBuffer, 4);
                }
            });

            Task sendConnections = Task.Run(async () =>
            {
                byte[] sendBuffer = new byte[5];
                sendBuffer[0] = 0;
                Array.Copy(myIdBuffer, 0, sendBuffer, 1, 4);
                while (doneConnections.Count < hardPlayerCount - 1)
                {
                    for (int i = 0; i < hardPlayerCount - 1; ++i)
                    {
                        int id = udpClients.ElementAt(i).Key;
                        if (!doneConnections.Contains(id))
                        {
                            await udpClients[id].SendAsync(sendBuffer, 5);
                        }
                    }
                }
            });

            UnityEngine.Debug.Log("Waiting for connections");

            await sendConnections;
            await waitForConnections;

            buffer = new byte[1];
            buffer[0] = 1;

            await ns.WriteAsync(buffer, 0, 1);

            await ns.ReadAsync(buffer, 0, 1);

            running = true;

            Task sendLoop = Task.Run(async () => await SendLoop());
            Task recieveLoop = Task.Run(async () => await RecieveLoop());
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log(e.Message + "\n\n" + e.StackTrace);
        }
    }

    public static async Task ConnectAsync()
    {
        try
        {
            await tcpClient.ConnectAsync("127.0.0.1", matchMakingPort);

            ns = tcpClient.GetStream();

            mainUdpClient = new UdpClient(0, AddressFamily.InterNetwork);

            switch (networkMode)
            {
                case NetworkMode.serverpass:
                    {
                        await ConnectAsyncServerpass();
                        break;
                    }
                case NetworkMode.servercontrol:
                    {
                        await ConnectAsyncServercontrol();
                        break;
                    }
            }
            Task overheadLoop = Task.Run(async () => await OverheadRecieveLoop());
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log(e.Message + "\n\n" + e.StackTrace);
        }
    }

    public static async Task OverheadRecieveLoop()
    {
        byte[] buffer = new byte[4];
        await ns.ReadAsync(buffer, 0, 4);
        int newPlayerCount = BitConverter.ToInt32(buffer, 0);
        Console.WriteLine("New player count: " + newPlayerCount);
        playerCount = newPlayerCount;
        playerCountChanged = true;
    }

    public static async Task SendLoop()
    {
        try
        {
            while (running)
            {
                Task waitTask = Task.Delay(localTickrate);

                packetTimes.Add(packetId, DateTimeOffset.Now);
                byte[] send;

                switch (packetType)
                {
                    case PacketType.rts:
                        {
                            UnityEngine.Debug.Log("Sending: " + queuedActions.Count);
                            send = new byte[4 + 4 + 4 + 8 + (PacketSize * queuedActions.Count)];
                            BitConverter.GetBytes(packetId).CopyTo(send, 4);
                            BitConverter.GetBytes(localId).CopyTo(send, 12);
                            BitConverter.GetBytes(queuedActions.Count).CopyTo(send, 16);
                            for (int i = 0; i < queuedActions.Count; ++i)
                            {
                                queuedActions[i].Serialize().CopyTo(send, 20 + (i * PacketSize));
                            }
                            queuedActions.Clear();
                            break;
                        }
                    case PacketType.input:
                        {
                            send = new byte[4 + 8 + PacketSize];
                            BitConverter.GetBytes(packetId).CopyTo(send, 4);
                            sendPacket.Serialize().CopyTo(send, 12);
                            break;
                        }
                    case PacketType.update:
                        {
                            send = new byte[4 + 8 + PacketSize];
                            BitConverter.GetBytes(packetId).CopyTo(send, 4);
                            sendPacket.Serialize().CopyTo(send, 12);
                            break;
                        }
                    default:
                        {
                            send = new byte[4];
                            break;
                        }
                }

                BitConverter.GetBytes(distanceLatency).CopyTo(send, 0);

                UnityEngine.Debug.Log(send.Length);
                await mainUdpClient.SendAsync(send, send.Length);

                packetId++;

                await waitTask;
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log(e.Message + "\n\n" + e.StackTrace);
        }
    }

    public static async Task DelayRecieve(byte[] buffer, int delay)
    {
        await Task.Delay(delay);

        long packetId = BitConverter.ToInt64(buffer, 0);
        int id = BitConverter.ToInt32(buffer, 8);

        switch (packetType)
        {
            case PacketType.rts:
                {
                    int packetCount = BitConverter.ToInt32(buffer, 12);

                    UnityEngine.Debug.Log(packetCount);

                    for (int i = 0; i < packetCount; ++i)
                    {
                        byte[] packetBuffer = new byte[16];
                        Array.Copy(buffer, 16 + (16 * i), packetBuffer, 0, 16);
                        RTSPacket packet = RTSPacket.Deserialize(packetBuffer);

                        onPacketRecieve?.Invoke(packet);
                    }
                    break;
                }

            case PacketType.input:
                {
                    byte[] packetBuffer = new byte[16];
                    Array.Copy(buffer, 12, packetBuffer, 0, 16);
                    InputPacket packet = InputPacket.Deserialize(packetBuffer);

                    onPacketRecieve?.Invoke(packet);
                    break;
                }
            case PacketType.update:
                {
                    byte[] packetBuffer = new byte[16];
                    Array.Copy(buffer, 12, packetBuffer, 0, 16);
                    UpdatePacket packet = UpdatePacket.Deserialize(packetBuffer);

                    onPacketRecieve?.Invoke(packet);

                    break;
                }
        }
        if (lowestLastRecieved[id] < packetId) lowestLastRecieved[id] = packetId;
    }

    public static async Task RecieveLoop()
    {
        try
        {
            while (running)
            {
                if (networkMode == NetworkMode.servercontrol)
                {
                    Task waitTask = Task.Delay(localTickrate);

                    UdpReceiveResult result = await mainUdpClient.ReceiveAsync();

                    int updateCount = BitConverter.ToInt32(result.Buffer, 0);
                    int idCount = BitConverter.ToInt32(result.Buffer, 4);

                    for (int i = 0; i < updateCount; ++i)
                    {
                        byte[] packetBuffer = new byte[16];
                        Array.Copy(result.Buffer, 8 + (16 * i), packetBuffer, 0, 16);
                        UpdatePacket packet = UpdatePacket.Deserialize(packetBuffer);

                        onPacketRecieve?.Invoke(packet);
                    }

                    for (int i = 0; i < idCount; ++i)
                    {
                        int senderId = BitConverter.ToInt32(result.Buffer, 8 + (16 * updateCount) + (12 * i));
                        int packetId = BitConverter.ToInt32(result.Buffer, 8 + (16 * updateCount) + (12 * i) + 4);

                        if (senderId == localId)
                        {
                            TimeSpan latency = DateTimeOffset.Now - packetTimes[packetId];

                            latencyTimes.Add(latency.TotalMilliseconds);
                            if (latencyTimes.Count > 60)
                            {
                                latencyTimes.RemoveAt(0);
                            }
                        }
                    }

                    await waitTask;
                }
                if (networkMode == NetworkMode.serverpass)
                {
                    Task waitTask = Task.Delay(localTickrate);

                    UdpReceiveResult result = await mainUdpClient.ReceiveAsync();

                    int playerCount = BitConverter.ToInt32(result.Buffer, 0);

                    int skip = 4;

                    for (int i = 0; i < playerCount; ++i)
                    {
                        if (skip > result.Buffer.Length - 4) UnityEngine.Debug.Log(result.Buffer.Length + " asdjonsdjkfh " + playerCount);
                        long packetId = BitConverter.ToInt64(result.Buffer, skip);
                        int senderId = BitConverter.ToInt32(result.Buffer, skip + 8);

                        switch (packetType)
                        {
                            case PacketType.rts:
                                {
                                    if (senderId == localId)
                                    {
                                        TimeSpan latency = DateTimeOffset.Now - packetTimes[packetId];

                                        latencyTimes.Add(latency.TotalMilliseconds);
                                        if (latencyTimes.Count > 60)
                                        {
                                            latencyTimes.RemoveAt(0);
                                        }
                                    }

                                    int packetCount = BitConverter.ToInt32(result.Buffer, skip + 12);

                                    UnityEngine.Debug.Log(
                                        "packetCount: " + packetCount + " | " +
                                        "result.Buffer.Length: " + result.Buffer.Length + " | " +
                                        "packetId: " + packetId + " | " +
                                        "senderId: " + senderId);

                                    for (int k = 0; k < packetCount; ++k)
                                    {
                                        byte[] packetBuffer = new byte[16];
                                        Array.Copy(result.Buffer, skip + 16 + (16 * k), packetBuffer, 0, 16);
                                        RTSPacket packet = RTSPacket.Deserialize(packetBuffer);

                                        onPacketRecieve?.Invoke(packet);
                                    }

                                    skip += 24 + (16 * packetCount);
                                    break;
                                }
                            case PacketType.input:
                                {
                                    byte[] packetBuffer = new byte[16];
                                    Array.Copy(result.Buffer, skip + 8, packetBuffer, 0, 16);
                                    InputPacket packet = InputPacket.Deserialize(packetBuffer);

                                    onPacketRecieve?.Invoke(packet);
                                    skip += 24;
                                    break;
                                }
                            case PacketType.update:
                                {
                                    byte[] packetBuffer = new byte[16];
                                    //UnityEngine.Debug.Log(result.Buffer.Length + " | " + skip);
                                    Array.Copy(result.Buffer, skip + 8, packetBuffer, 0, 16);
                                    UpdatePacket packet = UpdatePacket.Deserialize(packetBuffer);

                                    onPacketRecieve?.Invoke(packet);
                                    skip += 24;
                                    break;
                                }
                        }
                    }

                    await waitTask;
                }
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogException(e);
        }
    }
}
