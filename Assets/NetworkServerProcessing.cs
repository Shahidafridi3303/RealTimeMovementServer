using System.Collections.Generic;
using UnityEngine;

static public class NetworkServerProcessing
{
    static NetworkServer networkServer;
    static GameLogic gameLogic;

    // Track client positions and colors
    static Dictionary<int, Vector2> clientPositions = new Dictionary<int, Vector2>();
    static Dictionary<int, Color> clientColors = new Dictionary<int, Color>();

    #region Data Handling

    // Handle incoming data from clients
    public static void ReceivedMessageFromClient(string msg, int clientConnectionID, TransportPipeline pipeline)
    {
        string[] csv = msg.Split(',');
        int signifier = int.Parse(csv[0]);

        if (signifier == ClientToServerSignifiers.UpdatePosition)
        {
            // Update the client's position based on received data
            float velocityX = float.Parse(csv[1]);
            float velocityY = float.Parse(csv[2]);
            UpdateClientPosition(clientConnectionID, velocityX, velocityY);
        }
    }

    // Send a message to a specific client
    public static void SendMessageToClient(string msg, int clientConnectionID, TransportPipeline pipeline)
    {
        networkServer.SendMessageToClient(msg, clientConnectionID, pipeline);
    }

    #endregion

    #region Connection Events

    // Called when a new client connects
    public static void ConnectionEvent(int clientConnectionID)
    {
        float randomX = Random.Range(0.2f, 0.8f);
        float randomY = Random.Range(0.2f, 0.8f);
        clientPositions[clientConnectionID] = new Vector2(randomX, randomY);

        Color randomColor = new Color(Random.value, Random.value, Random.value);
        clientColors[clientConnectionID] = randomColor;

        // Notify all clients about the new avatar
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

    // Called when a client disconnects
    public static void DisconnectionEvent(int clientConnectionID)
    {
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

    #region Helper Methods

    // Update the position of a client based on velocity data
    private static void UpdateClientPosition(int clientConnectionID, float velocityX, float velocityY)
    {
        if (clientPositions.ContainsKey(clientConnectionID))
        {
            clientPositions[clientConnectionID] += new Vector2(velocityX, velocityY) * 0.02f; // Fixed time step
            Vector2 updatedPosition = clientPositions[clientConnectionID];

            // Broadcast updated position to all clients
            string positionUpdateMsg = $"{ServerToClientSignifiers.UpdatePosition},{clientConnectionID},{updatedPosition.x},{updatedPosition.y}";
            foreach (var otherClientID in networkServer.GetAllConnectedClientIDs())
            {
                SendMessageToClient(positionUpdateMsg, otherClientID, TransportPipeline.ReliableAndInOrder);
            }
        }
    }

    #endregion
}

#region Protocol Signifiers
static public class ClientToServerSignifiers
{
    public const int UpdatePosition = 1; // Sent when a client updates their position
}

static public class ServerToClientSignifiers
{
    public const int SpawnAvatar = 1;   // Sent to spawn an avatar
    public const int UpdatePosition = 2; // Sent to update a client's position
    public const int RemoveAvatar = 3;   // Sent to remove a disconnected client's avatar
}
#endregion
