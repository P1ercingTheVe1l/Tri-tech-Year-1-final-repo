using UnityEngine;

public class EnemyDeathTracker : MonoBehaviour
{
    public EnemySpawner spawner;
    public EnemySpawnOption spawnOption;

    void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.OnEnemyDied(spawnOption);
        }
    }
}