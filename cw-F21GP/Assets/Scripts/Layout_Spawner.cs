using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class LayoutSpawner : MonoBehaviour
{
    [Header("Player")]
    public Transform player;
    public Transform playerSpawnParent;

    [Header("Enemies")]
    public GameObject enemyPrefab;
    public Transform enemySpawnParent;
    public float enemySpawnDelay = 5f;
    public int maxEnemies = 1;

    private int currentEnemyCount = 0;

    [Header("Exit")]
    public Transform exitPoint;

    void Start()
    {
        SpawnPlayerRandomly();
        StartCoroutine(SpawnEnemiesOverTime());
        Debug.Log("LayoutSpawner active on: " + name);

    }

    // ---------------- PLAYER ----------------

    void SpawnPlayerRandomly()
    {
        if (player == null || playerSpawnParent == null) return;

        int index = Random.Range(0, playerSpawnParent.childCount);
        Transform spawn = playerSpawnParent.GetChild(index);

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.position = spawn.position;
        player.rotation = spawn.rotation;

        if (cc != null) cc.enabled = true;
    }

    // ---------------- ENEMIES ----------------

    IEnumerator SpawnEnemiesOverTime()
    {
        if (enemyPrefab == null || enemySpawnParent == null)
            yield break;

        while (true)
        {
            // Wait first
            yield return new WaitForSeconds(enemySpawnDelay);

            // Hard guard: do nothing if at cap
            if (currentEnemyCount >= maxEnemies)
                continue;

            Transform spawn = enemySpawnParent.GetChild(
                Random.Range(0, enemySpawnParent.childCount)
            );

            Vector3 spawnPos = spawn.position;

            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                spawnPos = hit.position;
            }
            else
            {
                // fallback: raycast downward
                if (Physics.Raycast(spawnPos + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit, 10f))
                {
                    spawnPos = groundHit.point;
                }
            }

            GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity, transform);



            currentEnemyCount++;
            Debug.Log($"Enemies alive: {currentEnemyCount}/{maxEnemies}");


            EnemyAI ai = enemy.GetComponent<EnemyAI>();
            if (ai != null)
                ai.OnEnemyDeath += HandleEnemyDeath;
        }
    }


    void HandleEnemyDeath()
    {
        currentEnemyCount = Mathf.Max(0, currentEnemyCount - 1);
    }

    // ---------------- EXIT ----------------

    public Vector3 GetExitPosition()
    {
        return exitPoint != null ? exitPoint.position : Vector3.zero;
    }
}
