using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;

    public Vector3 GetSpawnPosition()
    {
        return spawnPoint.position;
    }
}