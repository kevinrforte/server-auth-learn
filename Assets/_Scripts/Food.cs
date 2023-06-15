using UnityEngine;
using Unity.Netcode;

public class Food : NetworkBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsServer) return;
        if (!collision.CompareTag("Player")) return;

        if (!NetworkManager.Singleton.IsServer) return;

        if (collision.TryGetComponent(out PlayerLength playerLength))
        {
            playerLength.AddLengthServer();
        }

        if (NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
    }
}
