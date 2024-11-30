using System.Collections.Generic;
using UnityEngine;

static public class NetworkServerProcessing
{
    static NetworkServer networkServer;
    static GameLogic gameLogic;

    // Dictionary to track client positions
    static Dictionary<int, Vector2> clientPositions = new Dictionary<int, Vector2>();

    // Dictionary to track client colors
    static Dictionary<int, Color> clientColors = new Dictionary<int, Color>();

    #region Send and Receive Data Functions

    public static void ReceivedMessageFromClient(string msg, int clientConnectionID, TransportPipeline pipeline)
    {
        string[] csv = msg.Split(',');
        int signifier = int.Parse(csv[0]);

        if (signifier == ClientToServerSignifiers.UpdatePosition)
        {
            float velocityX = float.Parse(csv[1]);
            float velocityY = float.Parse(csv[2]);

            if (clientPositions.ContainsKey(clientConnectionID))
            {
                float fixedDeltaTime = 0.02f; // Fixed time step for updates
                clientPositions[clientConnectionID] += new Vector2(velocityX, velocityY) * fixedDeltaTime;

                Vector2 updatedPosition = clientPositions[clientConnectionID];

                // Broadcast updated position to all clients
                string positionUpdateMsg = $"{ServerToClientSignifiers.UpdatePosition},{clientConnectionID},{updatedPosition.x},{updatedPosition.y}";
                foreach (var otherClientID in networkServer.GetAllConnectedClientIDs())
                {
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

        // Generate random position
        float randomX = Random.Range(0.2f, 0.8f); // Within screen bounds
        float randomY = Random.Range(0.2f, 0.8f); // Within screen bounds
        clientPositions[clientConnectionID] = new Vector2(randomX, randomY);

        // Generate random color
        Color randomColor = new Color(Random.value, Random.value, Random.value);
        clientColors[clientConnectionID] = randomColor; // Store color for consistency

        // Notify all clients about the new client's avatar
        foreach (var otherClientID in networkServer.GetAllConnectedClientIDs())
        {
            string spawnMsg = $"{ServerToClientSignifiers.SpawnAvatar},{clientConnectionID},{randomX},{randomY},{randomColor.r},{randomColor.g},{randomColor.b}";
            SendMessageToClient(spawnMsg, otherClientID, TransportPipeline.ReliableAndInOrder);
        }

        // Send existing avatars to the new client
        foreach (var kvp in clientPositions)
        {
            Color existingColor = clientColors[kvp.Key];
            string spawnMsg = $"{ServerToClientSignifiers.SpawnAvatar},{kvp.Key},{kvp.Value.x},{kvp.Value.y},{existingColor.r},{existingColor.g},{existingColor.b}";
            SendMessageToClient(spawnMsg, clientConnectionID, TransportPipeline.ReliableAndInOrder);
        }
    }

    public static void DisconnectionEvent(int clientConnectionID)
    {
        // Remove from dictionary
        clientPositions.Remove(clientConnectionID);
        clientColors.Remove(clientConnectionID);

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
