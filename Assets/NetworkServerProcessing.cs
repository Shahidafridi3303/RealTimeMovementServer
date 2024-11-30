using System.Collections.Generic;
using UnityEngine;

static public class NetworkServerProcessing
{
    static NetworkServer networkServer;
    static GameLogic gameLogic;

    // Dictionary to track client positions
    static Dictionary<int, Vector2> clientPositions = new Dictionary<int, Vector2>();

    #region Send and Receive Data Functions

    public static void ReceivedMessageFromClient(string msg, int clientConnectionID, TransportPipeline pipeline)
    {
        Debug.Log($"Server: Received message from Client {clientConnectionID}: {msg}");
        string[] csv = msg.Split(',');
        int signifier = int.Parse(csv[0]);

        if (signifier == ClientToServerSignifiers.UpdatePosition)
        {
            // Update client position based on received velocity
            float velocityX = float.Parse(csv[1]);
            float velocityY = float.Parse(csv[2]);

            if (clientPositions.ContainsKey(clientConnectionID))
            {
                clientPositions[clientConnectionID] += new Vector2(velocityX, velocityY) * Time.deltaTime;
                Debug.Log($"Server: Updated position for Client {clientConnectionID}: {clientPositions[clientConnectionID]}");

                // Broadcast the updated position to all clients
                string positionUpdateMsg = $"{ServerToClientSignifiers.UpdatePosition},{clientConnectionID},{clientPositions[clientConnectionID].x},{clientPositions[clientConnectionID].y}";
                foreach (var otherClientID in networkServer.GetAllConnectedClientIDs())
                {
                    Debug.Log($"Server: Broadcasting position update to Client {otherClientID}: {positionUpdateMsg}");
                    SendMessageToClient(positionUpdateMsg, otherClientID, TransportPipeline.ReliableAndInOrder);
                }
            }
        }
    }



    public static void SendMessageToClient(string msg, int clientConnectionID, TransportPipeline pipeline)
    {
        networkServer.SendMessageToClient(msg, clientConnectionID, pipeline);
    }

    #endregion

    #region Connection Events

    public static void ConnectionEvent(int clientConnectionID)
    {
        Debug.Log($"Server: Client connected, ID: {clientConnectionID}");

        // Initialize position for the new client
        clientPositions[clientConnectionID] = new Vector2(0.5f, 0.5f);

        // Notify all clients about the new client's avatar
        foreach (var otherClientID in networkServer.GetAllConnectedClientIDs())
        {
            string spawnMsg = $"{ServerToClientSignifiers.SpawnAvatar},{clientConnectionID},{clientPositions[clientConnectionID].x},{clientPositions[clientConnectionID].y}";
            SendMessageToClient(spawnMsg, otherClientID, TransportPipeline.ReliableAndInOrder);
        }

        // Send existing avatars to the new client
        foreach (var kvp in clientPositions)
        {
            string spawnMsg = $"{ServerToClientSignifiers.SpawnAvatar},{kvp.Key},{kvp.Value.x},{kvp.Value.y}";
            SendMessageToClient(spawnMsg, clientConnectionID, TransportPipeline.ReliableAndInOrder);
        }
    }

    public static void DisconnectionEvent(int clientConnectionID)
    {
        Debug.Log($"Server: Client disconnected, ID: {clientConnectionID}");

        // Remove from dictionary
        clientPositions.Remove(clientConnectionID);

        // Notify all clients to remove the disconnected client's avatar
        string removeMsg = $"{ServerToClientSignifiers.RemoveAvatar},{clientConnectionID}";
        foreach (var otherClientID in networkServer.GetAllConnectedClientIDs())
        {
            SendMessageToClient(removeMsg, otherClientID, TransportPipeline.ReliableAndInOrder);
        }
    }

    #endregion

    #region Setup Methods

    public static void SetNetworkServer(NetworkServer server) => networkServer = server;

    public static NetworkServer GetNetworkServer() => networkServer;

    public static void SetGameLogic(GameLogic logic) => gameLogic = logic;

    #endregion
}

#region Protocol Signifiers
static public class ClientToServerSignifiers
{
    public const int UpdatePosition = 1; // Sent when a client updates their velocity
}

static public class ServerToClientSignifiers
{
    public const int SpawnAvatar = 1; // Sent to spawn an avatar
    public const int UpdatePosition = 2; // Sent to update a client's position
    public const int RemoveAvatar = 3; // Sent to remove a disconnected client's avatar
}
#endregion
