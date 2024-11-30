using UnityEngine;
using UnityEngine.Assertions;
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
    const int MaxNumberOfClientConnections = 1000;
    Dictionary<int, NetworkConnection> idToConnectionLookup;
    Dictionary<NetworkConnection, int> connectionToIDLookup;

    void Start()
    {
        if (NetworkServerProcessing.GetNetworkServer() == null)
        {
            NetworkServerProcessing.SetNetworkServer(this);
            DontDestroyOnLoad(this.gameObject);

            #region Connect

            idToConnectionLookup = new Dictionary<int, NetworkConnection>();
            connectionToIDLookup = new Dictionary<NetworkConnection, int>();

            networkDriver = NetworkDriver.Create();
            reliableAndInOrderPipeline = networkDriver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
            nonReliableNotInOrderedPipeline = networkDriver.CreatePipeline(typeof(FragmentationPipelineStage));
            NetworkEndPoint endpoint = NetworkEndPoint.AnyIpv4;
            endpoint.Port = NetworkPort;

            int error = networkDriver.Bind(endpoint);
            if (error != 0)
                Debug.LogError($"Failed to bind to port {NetworkPort}");
            else
                networkDriver.Listen();

            networkConnections = new NativeList<NetworkConnection>(MaxNumberOfClientConnections, Allocator.Persistent);

            #endregion
        }
        else
        {
            Debug.LogWarning("Multiple NetworkServer instances detected! Destroying duplicate.");
            Destroy(this.gameObject);
        }
    }

    public IEnumerable<int> GetAllConnectedClientIDs()
    {
        return idToConnectionLookup.Keys;
    }

    public static NetworkServer GetNetworkServer()
    {
        return FindObjectOfType<NetworkServer>();
    }

    void OnDestroy()
    {
        networkDriver.Dispose();
        networkConnections.Dispose();
    }

    void Update()
    {
        networkDriver.ScheduleUpdate().Complete();

        RemoveUnusedConnections();

        while (AcceptIncomingConnection()) { }

        ManageNetworkEvents();
    }

    private void RemoveUnusedConnections()
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
        idToConnectionLookup.Add(id, connection);
        connectionToIDLookup.Add(connection, id);

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
            if (!networkConnections[i].IsCreated) continue;

            while (PopNetworkEventAndCheckForData(networkConnections[i], out networkEventType, out streamReader, out pipelineUsedToSendEvent))
            {
                HandleNetworkEvent(networkConnections[i], networkEventType, streamReader, pipelineUsedToSendEvent);
            }
        }
    }

    private bool PopNetworkEventAndCheckForData(NetworkConnection networkConnection, out NetworkEvent.Type networkEventType, out DataStreamReader streamReader, out NetworkPipeline pipelineUsedToSendEvent)
    {
        networkEventType = networkConnection.PopEvent(networkDriver, out streamReader, out pipelineUsedToSendEvent);
        return networkEventType != NetworkEvent.Type.Empty;
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
        NetworkPipeline networkPipeline = pipeline == TransportPipeline.FireAndForget ? nonReliableNotInOrderedPipeline : reliableAndInOrderPipeline;

        byte[] msgAsByteArray = Encoding.Unicode.GetBytes(msg);
        NativeArray<byte> buffer = new NativeArray<byte>(msgAsByteArray, Allocator.Persistent);
        DataStreamWriter streamWriter;

        networkDriver.BeginSend(networkPipeline, idToConnectionLookup[connectionID], out streamWriter);
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
