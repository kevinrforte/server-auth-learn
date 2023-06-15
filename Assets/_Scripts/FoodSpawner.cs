using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class FoodSpawner : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    private WaitForSeconds _waitForSeconds = new WaitForSeconds(2f);
    private const int MaxPrefabCount = 30;
    private bool _firstSpawn = false;
    private bool _spawning = false;

    //private void Awake()
    //{
    //    NetworkManager.Singleton.OnServerStarted += SpawnFoodStart;
    //}

    private void OnEnable()
    {
        //NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnect;
        //NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
        StartCoroutine(SubscribeToNetworkManagerEvents());
    }

    private void OnDisable()
    {
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnect;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
        //StartCoroutine(UnsubscribeFromNetworkManagerEvents());
    }

    IEnumerator SubscribeToNetworkManagerEvents()
    {
        yield return new WaitUntil(() => NetworkManager.Singleton);
        //NetworkManager.Singleton.OnServerStarted += SpawnFoodStart;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnect;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
    }

    //IEnumerator UnsubscribeFromNetworkManagerEvents()
    //{
    //    yield return new WaitUntil(() => NetworkManager.Singleton);
    //    NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnect;
    //    NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
    //}

    private void SpawnFoodStart()
    {
        Debug.Log("SpawnFoodStart");
        //NetworkManager.Singleton.OnServerStarted -= SpawnFoodStart;
        //NetworkObjectPool.Singleton.InitializePool();

        for (int i = 0; i < 30; ++i)
        {
            SpawnFood();
        }

        _firstSpawn = true;
        //StartCoroutine(SpawnOverTime());
    }

    private void SpawnFood()
    {
        Debug.Log("Spawning Food");
        NetworkObject obj = NetworkObjectPool.Singleton.GetNetworkObject(prefab, GetRandomPositionOnMap(), Quaternion.identity);
        if (!obj.IsSpawned) obj.Spawn(true);
    }

    private Vector3 GetRandomPositionOnMap()
    {
        return new Vector3(Random.Range(-9f, 9f), Random.Range(-5f, 5f), 0f);
    }

    private IEnumerator SpawnOverTime()
    {
        _spawning = true;

        while (_spawning && NetworkManager.Singleton.ConnectedClients.Count > 0)
        {
            yield return _waitForSeconds;

            Debug.Log("SpawnOverTime: Attempting food spawn...");
            Debug.Log($"Current spawned food count: {NetworkObjectPool.Singleton.GetCurrentPrefabCount(prefab)}");

            if (NetworkObjectPool.Singleton.GetCurrentPrefabCount(prefab) < MaxPrefabCount)
            {
                SpawnFood();
            }
            else
            {
                Debug.Log("Too many foods spawned already. Skipping spawn.");
            }
        }

        _spawning = false;
    }

    private void OnClientConnect(ulong clientId)
    {
        Debug.Log("On Client Connect: " + NetworkManager.Singleton.IsServer);
        if (!NetworkManager.Singleton.IsServer) return;
        if (_spawning) return;

        if (!_firstSpawn)
        {
            SpawnFoodStart();
        }
        
        StartCoroutine(SpawnOverTime());
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (_spawning && NetworkManager.Singleton.ConnectedClients.Count == 0)
        {
            _spawning = false;
            _firstSpawn = false;
        }
    }
}
