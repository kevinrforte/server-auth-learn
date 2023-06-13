using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class FoodSpawner : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    private WaitForSeconds _waitForSeconds = new WaitForSeconds(2f);
    private const int MaxPrefabCount = 50;

    private void Start()
    {
        NetworkManager.Singleton.OnServerStarted += SpawnFoodStart;
    }

    private void SpawnFoodStart()
    {
        Debug.Log("SpawnFoodStart");
        NetworkManager.Singleton.OnServerStarted -= SpawnFoodStart;
        //NetworkObjectPool.Singleton.InitializePool();

        for (int i = 0; i < 30; ++i)
        {
            SpawnFood();
        }

        StartCoroutine(SpawnOverTime());
    }

    private void SpawnFood()
    {
        Debug.Log("Spawning Food");
        NetworkObject obj = NetworkObjectPool.Singleton.GetNetworkObject(prefab, GetRandomPositionOnMap(), Quaternion.identity);
        obj.GetComponent<Food>().prefab = prefab;
        if (!obj.IsSpawned) obj.Spawn(true);
    }

    private Vector3 GetRandomPositionOnMap()
    {
        return new Vector3(Random.Range(-9f, 9f), Random.Range(-5f, 5f), 0f);
    }

    private IEnumerator SpawnOverTime()
    {
        while (NetworkManager.Singleton.ConnectedClients.Count > 0)
        {
            yield return _waitForSeconds;

            Debug.Log("SpawnOverTime: Attempting food spawn...");

            if (NetworkObjectPool.Singleton.GetCurrentPrefabCount(prefab) < MaxPrefabCount)
            {
                SpawnFood();
            }
            else
            {
                Debug.Log("Too many foods spawned already. Skipping spawn.");
            }
        }
    }
}
