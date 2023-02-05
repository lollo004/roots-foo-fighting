using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System.Threading.Tasks;
using Unity.Mathematics;
using System.Collections;

/// <summary>
/// A simple sample showing how to use the Relay Allocation package with the Unity Transport Protocol (UTP).
/// It demonstrates how UTP can be used as either Hosts or Joining Players, covering the entire connection flow.
/// As a bonus, a simple demonstration of Relaying messages from Host to Players, and vice versa, is included.
/// </summary>
public class LobbyRelayUTP : MonoBehaviour
{
    //buttons
    [SerializeField] GameObject[] login_buttons = new GameObject[4];
    [SerializeField] GameObject game_buttons;

    [SerializeField] GameObject[] tilemaps_player = new GameObject[5];
    [SerializeField] GameObject[] tilemaps_enemy = new GameObject[5];

    //game objects
    [SerializeField] GameObject game;

    // GUI vars
    string playerId = "Not signed in";
    // these are never used
    //string autoSelectRegionName = "auto-select (QoS)";
    //int regionAutoSelectIndex = 0;
    List<Region> regions = new List<Region>();
    List<string> regionOptions = new List<string>();
    string hostLatestMessageReceived;
    string playerLatestMessageReceived;

    // Lobby data
    Lobby lobby = new();

    // Allocation response objects
    Allocation hostAllocation;
    JoinAllocation playerAllocation;
    string allocationJoinCode;

    // Control vars
    bool isHost;
    bool isPlayer;

    // Message vars
    // Creates and initializes a new Queue.
    public static Queue<string> hostMessagesBuffer = new();
    public static Queue<string> playerMessagesBuffer = new();


    // UTP vars
    public static NetworkDriver hostDriver;
    public static NetworkDriver playerDriver;
    private static NativeList<NetworkConnection> serverConnections;
    private static NetworkConnection clientConnection;

    async void Start()
    {
        // Initialize Unity Services
        await UnityServices.InitializeAsync();
    }

    void Update()
    {
        if (isHost)
        {
            UpdateHost();
        }
        else if (isPlayer)
        {
            UpdatePlayer();
        }
    }

    void OnDestroy()
    {
        // Cleanup objects upon exit
        if (isHost)
        {
            hostDriver.Dispose();
            serverConnections.Dispose();
        }
        else if (isPlayer)
        {
            playerDriver.Dispose();
        }
    }

    public async void OnStartBattle()
    {
        if (playerId == "Not signed in") 
        {
            Debug.LogWarning("You have to sign in before matching!");
            return;
        }
        await CreateOrJoinLobby();

        // manca heartbeat della lobby (il relay heartbeat gia' implementato)

        if (isHost)
        {
            // Allocate relay and update relayJoinCode
            await AllocateRelay();

            // set relay join code as new lobby name
            UpdateLobbyOptions options = new()
            {
                Name = allocationJoinCode,
                MaxPlayers = 2,
                IsPrivate = false,
                HostId = AuthenticationService.Instance.PlayerId
            };
            await LobbyService.Instance.UpdateLobbyAsync(lobby.Id, options);

            BindHost(); // deve avvenire entro 10 sec.
        }
        else if (isPlayer)
        {
            var l = await LobbyService.Instance.GetLobbyAsync(lobby.Id);
            allocationJoinCode = l.Name;

            await JoinRelay();

            BindPlayer(); // deve avvenire entro 10 sec.

            ConnectPlayer(); // connessione al Host
        }

        // in generale bisogna aggiustare anche questa: OnHostSendMessage (e la relativa del client)

    }

    public async Task CreateOrJoinLobby()
    {
        /*
         * Tries to join a lobby
         * if not possible, return a created lobby
         */
        try
        {
            QuickJoinLobbyOptions options = new();
            options.Filter = new List<QueryFilter>()
            {
                new QueryFilter(
                    field: QueryFilter.FieldOptions.MaxPlayers,
                    op: QueryFilter.OpOptions.GE,
                    value: "2")
            };
            lobby = await LobbyService.Instance.QuickJoinLobbyAsync(options);
            isPlayer = true;
        }
        catch (LobbyServiceException)
        {
            try
            {
                // lobby is public as default
                lobby = await LobbyService.Instance.CreateLobbyAsync(
                    "__name__", 2);
            }
            catch
            {
                Debug.LogError("Error during lobby creation");
                throw;
            }
            isHost = true;
        }
        catch
        {
            Debug.LogError("Can't succeed lobby setup");
            throw;
        }

    }


    /// <summary>
    /// Event handler for when the Sign In button is clicked.
    /// </summary>
    public async void OnSignIn()
    {
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        playerId = AuthenticationService.Instance.PlayerId;

        Debug.Log($"Signed in. Player ID: {playerId}");
    }


    /// <summary>
    /// Event handler for when the Allocate button is clicked.
    /// </summary>
    public async Task AllocateRelay()
    {
        Debug.Log("Host - Creating an allocation. Upon success, I have 10 seconds to BIND to the Relay server that I've allocated.");


        // Set max connections. Can be up to 100, but note the more players connected, the higher the bandwidth/latency impact.
        int maxConnections = 2;

        // Important: Once the allocation is created, you have ten seconds to BIND, else the allocation times out.
        hostAllocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
        Debug.Log($"Host Allocation ID: {hostAllocation.AllocationId}, region: {hostAllocation.Region}");

        // Automatically update allocationJoinCode when AllocateRelay
        allocationJoinCode = await RelayService.Instance.GetJoinCodeAsync(hostAllocation.AllocationId);

        // Initialize NetworkConnection list for the server (Host).
        // This list object manages the NetworkConnections which represent connected players.
        serverConnections = new NativeList<NetworkConnection>(maxConnections, Allocator.Persistent);
    }

    /// <summary>
    /// Event handler for when the Join button is clicked.
    /// </summary>
    public async Task JoinRelay()
    {
        Debug.Log("Player - Joining host allocation using join code. Upon success, I have 10 seconds to BIND to the Relay server that I've allocated.");

        try
        {
            playerAllocation = await RelayService.Instance.JoinAllocationAsync(allocationJoinCode);
            Debug.Log("Player Allocation ID: " + playerAllocation.AllocationId);
        }
        catch (RelayServiceException ex)
        {
            Debug.LogError(ex.Message + "\n" + ex.StackTrace);
            throw;
        }
    }

    /// <summary>
    /// Bind Host to Relay (UTP).
    /// </summary>
    public void BindHost()
    {
        Debug.Log("Host - Binding to the Relay server using UTP.");

        // Extract the Relay server data from the Allocation response.
        var relayServerData = new RelayServerData(hostAllocation, "udp");

        // Create NetworkSettings using the Relay server data.
        var settings = new NetworkSettings();
        settings.WithRelayParameters(ref relayServerData);

        // Create the Host's NetworkDriver from the NetworkSettings.
        hostDriver = NetworkDriver.Create(settings);

        // Bind to the Relay server.
        if (hostDriver.Bind(NetworkEndPoint.AnyIpv4) != 0)
        {
            Debug.LogError("Host client failed to bind");
        }
        else
        {
            if (hostDriver.Listen() != 0)
            {
                Debug.LogError("Host client failed to listen");
            }
            else
            {
                Debug.Log("Host client bound to Relay server");
            }
        }
    }


    /// <summary>
    /// Bind Player to Relay (UTP).
    /// </summary>
    public void BindPlayer()
    {
        Debug.Log("Player - Binding to the Relay server using UTP.");

        // Extract the Relay server data from the Join Allocation response.
        var relayServerData = new RelayServerData(playerAllocation, "udp");

        // Create NetworkSettings using the Relay server data.
        var settings = new NetworkSettings();
        settings.WithRelayParameters(ref relayServerData);

        // Create the Player's NetworkDriver from the NetworkSettings object.
        playerDriver = NetworkDriver.Create(settings);

        // Bind to the Relay server.
        if (playerDriver.Bind(NetworkEndPoint.AnyIpv4) != 0)
        {
            Debug.LogError("Player client failed to bind");
        }
        else
        {
            Debug.Log("Player client bound to Relay server");
        }
    }

    /// <summary>
    /// Connect Player to Relay Host (UTP).
    /// </summary>
    public void ConnectPlayer()
    {
        Debug.Log("Player - Connecting to Host's client.");

        // Sends a connection request to the Host Player.
        clientConnection = playerDriver.Connect();
    }

    /// <summary>
    /// Event handler for when the Send message from Host to Relay (UTP) button is clicked.
    /// Sends a random string
    /// </summary>
    public static void OnHostSendMessage(string msg)
    {
        if (serverConnections.Length == 0)
        {
            Debug.LogError("No players connected to send messages to.");
            return;
        }

        // In this sample, we will simply broadcast a message to all connected clients.
        for (int i = 0; i < serverConnections.Length; i++)
        {
            if (hostDriver.BeginSend(serverConnections[i], out var writer) == 0)
            {
                // Send the message. Aside from FixedString32, many different types can be used.
                writer.WriteFixedString32(msg.ToString());
                hostDriver.EndSend(writer);
            }
        }
    }

    /// <summary>
    /// Event handler for when the Send message from Player to Host (UTP) button is clicked.
    /// </summary>
    public static void OnPlayerSendMessage(string msg)
    {
        if (!clientConnection.IsCreated)
        {
            Debug.LogError("Player is not connected. No Host client to send message to.");
            return;
        }

        if (playerDriver.BeginSend(clientConnection, out var writer) == 0)
        {
            // Send the message. Aside from FixedString32, many different types can be used.
            writer.WriteFixedString32(msg.ToString());
            playerDriver.EndSend(writer);
        }
    }

    /// <summary>
    /// Event handler for when the DisconnectPlayers (UTP) button is clicked.
    /// </summary>
    public void OnDisconnectPlayers()
    {
        if (serverConnections.Length == 0)
        {
            Debug.LogError("No players connected to disconnect.");
            return;
        }

        // In this sample, we will simply disconnect all connected clients.
        for (int i = 0; i < serverConnections.Length; i++)
        {
            // This sends a disconnect event to the destination client,
            // letting them know they are disconnected from the Host.
            hostDriver.Disconnect(serverConnections[i]);

            // Here, we set the destination client's NetworkConnection to the default value.
            // It will be recognized in the Host's Update loop as a stale connection, and be removed.
            serverConnections[i] = default(NetworkConnection);
        }
    }

    /// <summary>
    /// Event handler for when the Disconnect (UTP) button is clicked.
    /// </summary>
    public void OnDisconnect()
    {
        // This sends a disconnect event to the Host client,
        // letting them know they are disconnecting.
        playerDriver.Disconnect(clientConnection);

        // We remove the reference to the current connection by overriding it.
        clientConnection = default(NetworkConnection);
    }

    void UpdateHost()
    {
        // Skip update logic if the Host is not yet bound.
        if (!hostDriver.IsCreated || !hostDriver.Bound)
        {
            login_buttons[0].SetActive(false);
            login_buttons[1].SetActive(false);
            login_buttons[2].SetActive(true);
            return;
        }

        // This keeps the binding to the Relay server alive,
        // preventing it from timing out due to inactivity.
        hostDriver.ScheduleUpdate().Complete();

        // Clean up stale connections.
        for (int i = 0; i < serverConnections.Length; i++)
        {
            if (!serverConnections[i].IsCreated)
            {
                Debug.Log("Stale connection removed");
                serverConnections.RemoveAt(i);
                --i;
            }
        }

        // Accept incoming client connections.
        NetworkConnection incomingConnection;
        while ((incomingConnection = hostDriver.Accept()) != default(NetworkConnection))
        {
            // Adds the requesting Player to the serverConnections list.
            // This also sends a Connect event back the requesting Player,
            // as a means of acknowledging acceptance.
            Debug.Log("Accepted an incoming connection.");
            serverConnections.Add(incomingConnection);

            System.Random rnd = new System.Random();
            int month = rnd.Next(0, 5);
            Data.selected_tilemap = month;
            LobbyRelayUTP.OnHostSendMessage(
                "START" + "|"
                + month
                );
            tilemaps_player[month].SetActive(true);
            tilemaps_enemy[month].SetActive(true);
            login_buttons[2].SetActive(false);
            login_buttons[3].SetActive(false);
            game_buttons.SetActive(true);
            game.SetActive(true);
        }

        // Process events from all connections.
        for (int i = 0; i < serverConnections.Length; i++)
        {
            Assert.IsTrue(serverConnections[i].IsCreated);

            // Resolve event queue.
            NetworkEvent.Type eventType;
            while ((eventType = hostDriver.PopEventForConnection(serverConnections[i], out var stream)) != NetworkEvent.Type.Empty)
            {
                switch (eventType)
                {
                    // Handle Relay events.
                    case NetworkEvent.Type.Data:
                        FixedString32Bytes msg = stream.ReadFixedString32();
                        Debug.Log($"Server received msg: {msg}");
                        hostLatestMessageReceived = msg.ToString();
                        hostMessagesBuffer.Enqueue(msg.ToString());
                        break;

                    // Handle Disconnect events.
                    case NetworkEvent.Type.Disconnect:
                        Debug.Log("Server received disconnect from client");
                        serverConnections[i] = default(NetworkConnection);
                        break;
                }
            }
        }
    }

    void UpdatePlayer()
    {
        // Skip update logic if the Player is not yet bound.
        if (!playerDriver.IsCreated || !playerDriver.Bound)
        {
            login_buttons[0].SetActive(false);
            login_buttons[1].SetActive(false);
            login_buttons[2].SetActive(true);
            return;
        }

        // This keeps the binding to the Relay server alive,
        // preventing it from timing out due to inactivity.
        playerDriver.ScheduleUpdate().Complete();

        // Resolve event queue.
        NetworkEvent.Type eventType;
        while ((eventType = clientConnection.PopEvent(playerDriver, out var stream)) != NetworkEvent.Type.Empty)
        {
            switch (eventType)
            {
                // Handle Relay events.
                case NetworkEvent.Type.Data:
                    FixedString32Bytes msg = stream.ReadFixedString32();
                    Debug.Log($"Player received msg: {msg}");
                    playerLatestMessageReceived = msg.ToString();
                    playerMessagesBuffer.Enqueue(msg.ToString());
                    break;

                // Handle Connect events.
                case NetworkEvent.Type.Connect:
                    Debug.Log("Player connected to the Host");

                    login_buttons[2].SetActive(false);
                    game_buttons.SetActive(true);
                    login_buttons[3].SetActive(false);
                    game.SetActive(true);
                    break;

                // Handle Disconnect events.
                case NetworkEvent.Type.Disconnect:
                    Debug.Log("Player got disconnected from the Host");
                    clientConnection = default(NetworkConnection);
                    break;
            }
        }
    }
}
