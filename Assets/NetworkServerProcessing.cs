using System.Collections.Generic;
using UnityEngine;

static public class NetworkServerProcessing
{
    static NetworkServer networkServer;
    static GameLogic gameLogic;

    // Track client positions and colors
    static Dictionary<int, Vector2> clientPositions = new Dictionary<int, Vector2>();
    static Dictionary<int, Color> clientColors = new Dictionary<int, Color>();
    static Dictionary<int, float> lastUpdateTimes = new Dictionary<int, float>();
    private const float UpdateInterval = 0.1f; // Send updates every 100ms

    #region Data Handling

    public static void ReceivedMessageFromClient(string msg, int clientConnectionID, TransportPipeline pipeline)
    {
        string[] csv = msg.Split(',');
        int signifier = int.Parse(csv[0]);

        if (signifier == ClientToServerSignifiers.UpdatePosition)
        {
            float velocityX = float.Parse(csv[1]);
            float velocityY = float.Parse(csv[2]);
            UpdateClientPosition(clientConnectionID, velocityX, velocityY);
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
        float randomX = Random.Range(0.2f, 0.8f);
        float randomY = Random.Range(0.2f, 0.8f);
        clientPositions[clientConnectionID] = new Vector2(randomX, randomY);
        clientColors[clientConnectionID] = new Color(Random.value, Random.value, Random.value);

        // Notify new client about all existing avatars
        foreach (var kvp in clientPositions)
        {
            Color color = clientColors[kvp.Key];
            string spawnMsg = $"{ServerToClientSignifiers.SpawnAvatar},{kvp.Key},{kvp.Value.x},{kvp.Value.y},{color.r},{color.g},{color.b}";
            SendMessageToClient(spawnMsg, clientConnectionID, TransportPipeline.ReliableAndInOrder);
        }

        // Notify all other clients about the new client
        Vector2 spawnPosition = clientPositions[clientConnectionID];
        Color newColor = clientColors[clientConnectionID];
        foreach (var otherClientID in networkServer.GetAllConnectedClientIDs())
        {
            if (otherClientID != clientConnectionID)
            {
                string spawnMsg = $"{ServerToClientSignifiers.SpawnAvatar},{clientConnectionID},{spawnPosition.x},{spawnPosition.y},{newColor.r},{newColor.g},{newColor.b}";
                SendMessageToClient(spawnMsg, otherClientID, TransportPipeline.ReliableAndInOrder);
            }
        }
    }


    public static void DisconnectionEvent(int clientConnectionID)
    {
        clientPositions.Remove(clientConnectionID);
        clientColors.Remove(clientConnectionID);

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

    private static void UpdateClientPosition(int clientConnectionID, float velocityX, float velocityY)
    {
        if (clientPositions.ContainsKey(clientConnectionID))
        {
            float currentTime = Time.time;
            if (!lastUpdateTimes.ContainsKey(clientConnectionID) || currentTime - lastUpdateTimes[clientConnectionID] >= UpdateInterval)
            {
                clientPositions[clientConnectionID] += new Vector2(velocityX, velocityY) * 0.02f;
                lastUpdateTimes[clientConnectionID] = currentTime;

                Vector2 updatedPosition = clientPositions[clientConnectionID];
                BroadcastPositionUpdate(clientConnectionID, updatedPosition);
            }
        }
    }

    private static void BroadcastPositionUpdate(int clientConnectionID, Vector2 position)
    {
        string positionUpdateMsg = $"{ServerToClientSignifiers.UpdatePosition},{clientConnectionID},{position.x},{position.y}";
        foreach (var otherClientID in networkServer.GetAllConnectedClientIDs())
        {
            SendMessageToClient(positionUpdateMsg, otherClientID, TransportPipeline.ReliableAndInOrder);
        }
    }


    #endregion
}

#region Protocol Signifiers

static public class ClientToServerSignifiers
{
    public const int UpdatePosition = 1; 
}

static public class ServerToClientSignifiers
{
    public const int SpawnAvatar = 1;  
    public const int UpdatePosition = 2;
    public const int RemoveAvatar = 3; 
}

#endregion
