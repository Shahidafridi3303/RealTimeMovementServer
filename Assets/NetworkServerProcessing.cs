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

    // Only send updates if there's a significant change in position.
    public static void ReceivedMessageFromClient(string msg, int clientConnectionID, TransportPipeline pipeline)
    {
        string[] csv = msg.Split(',');
        int signifier = int.Parse(csv[0]);

        if (signifier == ClientToServerSignifiers.UpdatePosition)
        {
            float velocityX = float.Parse(csv[1]);
            float velocityY = float.Parse(csv[2]);
            Vector2 newPosition = clientPositions[clientConnectionID] + new Vector2(velocityX, velocityY) * Time.deltaTime;
            clientPositions[clientConnectionID] = newPosition;  // Update position

            // Broadcast this new position to all clients
            string positionUpdateMsg = $"{ServerToClientSignifiers.UpdatePosition},{clientConnectionID},{newPosition.x},{newPosition.y}";
            foreach (var otherClientID in networkServer.GetAllConnectedClientIDs())
            {
                SendMessageToClient(positionUpdateMsg, otherClientID, TransportPipeline.ReliableAndInOrder);
            }
        }
    }


    private static void BroadcastPositionUpdate(int clientID, Vector2 position)
    {
        string message = $"{ServerToClientSignifiers.UpdatePosition},{clientID},{position.x},{position.y}";
        foreach (int otherClientID in networkServer.GetAllConnectedClientIDs())
        {
            SendMessageToClient(message, otherClientID, TransportPipeline.ReliableAndInOrder);
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

        // Generate random position and color
        float randomX = Random.Range(0.2f, 0.8f);
        float randomY = Random.Range(0.2f, 0.8f);
        Color randomColor = new Color(Random.value, Random.value, Random.value);

        // Store the new client's data
        clientPositions[clientConnectionID] = new Vector2(randomX, randomY);
        clientColors[clientConnectionID] = randomColor;

        // Notify all other clients about the new client
        string newClientSpawnMsg = $"{ServerToClientSignifiers.SpawnAvatar},{clientConnectionID},{randomX},{randomY},{randomColor.r},{randomColor.g},{randomColor.b}";
        foreach (int otherClientID in networkServer.GetAllConnectedClientIDs())
        {
            if (otherClientID != clientConnectionID) // Avoid sending to self
            {
                SendMessageToClient(newClientSpawnMsg, otherClientID, TransportPipeline.ReliableAndInOrder);
            }
        }

        // Send existing avatars only to the new client
        foreach (KeyValuePair<int, Vector2> kvp in clientPositions)
        {
            if (kvp.Key != clientConnectionID) // Don't resend to the new client
            {
                Color existingColor = clientColors[kvp.Key];
                string spawnMsg = $"{ServerToClientSignifiers.SpawnAvatar},{kvp.Key},{kvp.Value.x},{kvp.Value.y},{existingColor.r},{existingColor.g},{existingColor.b}";
                SendMessageToClient(spawnMsg, clientConnectionID, TransportPipeline.ReliableAndInOrder);
            }
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
