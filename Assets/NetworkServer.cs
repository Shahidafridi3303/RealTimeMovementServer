using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using System.Text;
using System.Collections.Generic;

public class NetworkServer : MonoBehaviour
{
    public NetworkDriver networkDriver;
    private NativeList<NetworkConnection> networkConnections;
    NetworkPipeline reliableAndInOrderPipeline;
    NetworkPipeline nonReliableNotInOrderedPipeline;
    const ushort NetworkPort = 9001;
    Dictionary<int, NetworkConnection> idToConnectionLookup;
    Dictionary<NetworkConnection, int> connectionToIDLookup;

    void Start()
    {
        NetworkServerProcessing.SetNetworkServer(this);

        idToConnectionLookup = new Dictionary<int, NetworkConnection>();
        connectionToIDLookup = new Dictionary<NetworkConnection, int>();

        networkDriver = NetworkDriver.Create();
        reliableAndInOrderPipeline = networkDriver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
        nonReliableNotInOrderedPipeline = networkDriver.CreatePipeline(typeof(FragmentationPipelineStage));
        NetworkEndPoint endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = NetworkPort;

        if (networkDriver.Bind(endpoint) != 0)
            Debug.LogError($"Failed to bind to port {NetworkPort}");
        else
            networkDriver.Listen();

        networkConnections = new NativeList<NetworkConnection>(Allocator.Persistent);
    }

    public IEnumerable<int> GetAllConnectedClientIDs()
    {
        return idToConnectionLookup.Keys;
    }

    void OnDestroy()
    {
        networkDriver.Dispose();
        networkConnections.Dispose();
    }

    void Update()
    {
        networkDriver.ScheduleUpdate().Complete();

        RemoveInvalidConnections();

        while (AcceptIncomingConnection()) { }

        ManageNetworkEvents();
    }

    private void RemoveInvalidConnections()
    {
        for (int i = 0; i < networkConnections.Length; i++)
        {
            if (!networkConnections[i].IsCreated)
            {
                networkConnections.RemoveAtSwapBack(i);
                i--;
            }
        }
    }

    private bool AcceptIncomingConnection()
    {
        NetworkConnection connection = networkDriver.Accept();
        if (connection == default(NetworkConnection)) return false;

        networkConnections.Add(connection);

        int id = GenerateUniqueClientID();
        idToConnectionLookup[id] = connection;
        connectionToIDLookup[connection] = id;

        NetworkServerProcessing.ConnectionEvent(id);

        return true;
    }

    private int GenerateUniqueClientID()
    {
        int id = 0;
        while (idToConnectionLookup.ContainsKey(id)) id++;
        return id;
    }

    private void ManageNetworkEvents()
    {
        DataStreamReader streamReader;
        NetworkPipeline pipelineUsedToSendEvent;
        NetworkEvent.Type networkEventType;

        for (int i = 0; i < networkConnections.Length; i++)
        {
            while (PopNetworkEventAndCheckForData(networkConnections[i], out networkEventType, out streamReader, out pipelineUsedToSendEvent))
            {
                HandleNetworkEvent(networkConnections[i], networkEventType, streamReader, pipelineUsedToSendEvent);
            }
        }
    }

    private bool PopNetworkEventAndCheckForData(NetworkConnection connection, out NetworkEvent.Type eventType, out DataStreamReader reader, out NetworkPipeline pipeline)
    {
        eventType = connection.PopEvent(networkDriver, out reader, out pipeline);
        return eventType != NetworkEvent.Type.Empty;
    }

    private void HandleNetworkEvent(NetworkConnection connection, NetworkEvent.Type eventType, DataStreamReader reader, NetworkPipeline pipeline)
    {
        switch (eventType)
        {
            case NetworkEvent.Type.Data:
                int sizeOfDataBuffer = reader.ReadInt();
                NativeArray<byte> buffer = new NativeArray<byte>(sizeOfDataBuffer, Allocator.Persistent);
                reader.ReadBytes(buffer);
                string msg = Encoding.Unicode.GetString(buffer.ToArray());
                NetworkServerProcessing.ReceivedMessageFromClient(msg, connectionToIDLookup[connection], TransportPipeline.ReliableAndInOrder);
                buffer.Dispose();
                break;

            case NetworkEvent.Type.Disconnect:
                int clientId = connectionToIDLookup[connection];
                NetworkServerProcessing.DisconnectionEvent(clientId);
                idToConnectionLookup.Remove(clientId);
                connectionToIDLookup.Remove(connection);
                break;
        }
    }

    public void SendMessageToClient(string msg, int connectionID, TransportPipeline pipeline)
    {
        NetworkPipeline pipelineType = pipeline == TransportPipeline.FireAndForget ? nonReliableNotInOrderedPipeline : reliableAndInOrderPipeline;

        byte[] msgBytes = Encoding.Unicode.GetBytes(msg);
        NativeArray<byte> buffer = new NativeArray<byte>(msgBytes, Allocator.Persistent);
        DataStreamWriter streamWriter;

        networkDriver.BeginSend(pipelineType, idToConnectionLookup[connectionID], out streamWriter);
        streamWriter.WriteInt(buffer.Length);
        streamWriter.WriteBytes(buffer);
        networkDriver.EndSend(streamWriter);

        buffer.Dispose();
    }
}


public enum TransportPipeline
{
    NotIdentified,
    ReliableAndInOrder,
    FireAndForget
}
