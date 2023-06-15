using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class ConnectionApprovalHandler : MonoBehaviour
{
    public static int MaxPlayers = 8;

    //private void Awake()
    //{
    //    NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
    //}

    private void OnEnable()
    {
        StartCoroutine(SubscribeToNetworkManagerEvents());
    }

    IEnumerator SubscribeToNetworkManagerEvents()
    {
        yield return new WaitUntil(() => NetworkManager.Singleton);
        NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        Debug.Log("Connect Approval");

        response.Approved = true;
        response.CreatePlayerObject = true;
        response.PlayerPrefabHash = null;

        if (NetworkManager.Singleton.ConnectedClients.Count >= MaxPlayers)
        {
            response.Approved = false;
            response.Reason = "Server is Full";
        }

        response.Pending = false;
    }
}
