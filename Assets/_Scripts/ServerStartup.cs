using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Threading.Tasks;
using Unity.Services.Core;
using System;
using Unity.Services.Multiplay;
using Unity.Services.Matchmaker.Models;
using Newtonsoft.Json;
using Unity.Services.Matchmaker;

public class ServerStartup : MonoBehaviour
{
    public static event System.Action ClientInstance;

    private const string InternalServerIP = "0.0.0.0";
    private string _externalServerIP = "0.0.0.0";
    private ushort _serverPort = 7777;

    private string _externalConnectionString => $"{_externalServerIP}:{_serverPort}";

    private IMultiplayService _multiplayService;
    const int _multiplayServiceTimeout = 20000;

    private string _allocationId;
    private MultiplayEventCallbacks _serverCallbacks;
    private IServerEvents _serverEvents;

    private BackfillTicket _localBackfillTicket;
    private CreateBackfillTicketOptions _createBackfillTicketOptions;
    private const int _ticketCheckMs = 1000;
    private MatchmakingResults _matchmakingPayload;

    private bool _backfilling = false;


    async void Start()
    {
        bool server = false;
        string[] args = System.Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length; ++i)
        {
            if (args[i] == "-dedicatedServer")
            {
                server = true;
            }

            if (args[i] == "-port" && (i + 1 < args.Length))
            {
                _serverPort = (ushort)int.Parse(args[i + 1]);
            }

            if (args[i] == "-ip" && (i + 1 < args.Length))
            {
                _externalServerIP = args[i + 1];
            }
        }

        if (server)
        {
            StartServer();
            await StartServerServices();
        }
        else
        {
            ClientInstance?.Invoke();
        }
    }

    private void StartServer()
    {
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(InternalServerIP, _serverPort);
        NetworkManager.Singleton.StartServer();
        NetworkManager.Singleton.OnClientDisconnectCallback += ClientDisconnected;
    }

    async Task StartServerServices()
    {
        await UnityServices.InitializeAsync();
        try
        {
            _multiplayService = MultiplayService.Instance;
            await _multiplayService.StartServerQueryHandlerAsync((ushort)ConnectionApprovalHandler.MaxPlayers, "n/a", "n/a", "0", "n/a");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Something went wrong trying to set up the SQP Service:\n{e}");
        }

        try
        {
            _matchmakingPayload = await GetMatchmakerPayload(_multiplayServiceTimeout);
            if (_matchmakingPayload != null)
            {
                Debug.Log($"Got payload: {_matchmakingPayload}");
                await StartBackfill(_matchmakingPayload);
            }
            else
            {
                Debug.LogWarning("Getting the Matchmaker Payload timed out, using defaults.");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Something went wrong trying to set up the Allocation & Backfill Services:\n{e}");
        }
    }

    private async Task<MatchmakingResults> GetMatchmakerPayload(int timeout)
    {
        var matchmakerPayloadTask = SubscribeAndAwaitMatchmakerAllocation();
        if (await Task.WhenAny(matchmakerPayloadTask, Task.Delay(timeout)) == matchmakerPayloadTask)
        {
            return matchmakerPayloadTask.Result;
        }
        else
        {
            return null;
        }
    }

    private async Task<MatchmakingResults> SubscribeAndAwaitMatchmakerAllocation()
    {
        if (_multiplayService == null) return null;

        _allocationId = null;
        _serverCallbacks = new MultiplayEventCallbacks();
        _serverCallbacks.Allocate += OnMultiplayAllocation;
        _serverEvents = await _multiplayService.SubscribeToServerEventsAsync(_serverCallbacks);

        _allocationId = await AwaitAllocationID();
        var mmPayload = await GetMatchmakerAllocationPayloadAsync();
        return mmPayload;
    }

    private async Task<string> AwaitAllocationID()
    {
        var config = _multiplayService.ServerConfig;
        Debug.Log("Awaiting Allocation.");

        while (string.IsNullOrEmpty(_allocationId))
        {
            var configId = config.AllocationId;

            if (!string.IsNullOrEmpty(configId) && string.IsNullOrEmpty(_allocationId))
            {
                _allocationId = configId;
                break;
            }

            await Task.Delay(100);
        }

        return _allocationId;
    }

    private void OnMultiplayAllocation(MultiplayAllocation allocation)
    {
        Debug.Log($"On Allocation: {allocation.AllocationId}");
        if (string.IsNullOrEmpty(allocation.AllocationId)) return;
        _allocationId = allocation.AllocationId;
    }

    private async Task<MatchmakingResults> GetMatchmakerAllocationPayloadAsync()
    {
        try
        {
            var payloadAllocation = await MultiplayService.Instance.GetPayloadAllocationFromJsonAs<MatchmakingResults>();
            var modelAsJson = JsonConvert.SerializeObject(payloadAllocation, Formatting.Indented);
            Debug.Log($"{nameof(GetMatchmakerAllocationPayloadAsync)}:\n{modelAsJson}");

            return payloadAllocation;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Something went wrong trying to get the Matchmaker Payload in GetMatchmakerAllocationPayloadAsync:\n{e}");
        }

        return null;
    }

    private async Task StartBackfill(MatchmakingResults payload)
    {
        var backfillProperties = new BackfillTicketProperties(payload.MatchProperties);
        _localBackfillTicket = new BackfillTicket { Id = payload.MatchProperties.BackfillTicketId, Properties = backfillProperties };
        await BeginBackfilling(payload);
    }

    private async Task BeginBackfilling(MatchmakingResults payload)
    {
        if (string.IsNullOrEmpty(_localBackfillTicket.Id))
        {
            var matchProperties = payload.MatchProperties;

            _createBackfillTicketOptions = new CreateBackfillTicketOptions
            {
                Connection = _externalConnectionString,
                QueueName = payload.QueueName,
                Properties = new BackfillTicketProperties(matchProperties)
            };

            _localBackfillTicket.Id = await MatchmakerService.Instance.CreateBackfillTicketAsync(_createBackfillTicketOptions);
        }

#pragma warning disable 4014
        _backfilling = true;
        BackfillLoop();
#pragma warning restore 4014
    }

    private async Task BackfillLoop()
    {
        while (_backfilling && NeedsPlayers())
        {
            _localBackfillTicket = await MatchmakerService.Instance.ApproveBackfillTicketAsync(_localBackfillTicket.Id);
            if (!NeedsPlayers())
            {
                await MatchmakerService.Instance.DeleteBackfillTicketAsync(_localBackfillTicket.Id);
                _localBackfillTicket.Id = null;
                _backfilling = false;
                return;
            }

            await Task.Delay(_ticketCheckMs);
        }

        _backfilling = false;
    }

    private void ClientDisconnected(ulong clientId)
    {
        if (!_backfilling && NetworkManager.Singleton.ConnectedClients.Count > 0 && NeedsPlayers())
        {
            BeginBackfilling(_matchmakingPayload);
        }
    }

    private bool NeedsPlayers()
    {
        return NetworkManager.Singleton.ConnectedClients.Count < ConnectionApprovalHandler.MaxPlayers;
    }

    private void Dispose()
    {
        _serverCallbacks.Allocate -= OnMultiplayAllocation;
        _serverEvents?.UnsubscribeAsync();
    }
}
