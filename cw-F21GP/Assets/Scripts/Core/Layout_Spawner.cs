using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using F21GP.Enemy;

namespace F21GP.Core
{
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

        [Header("Kill Tracking")]
        [SerializeField] private int requiredKills = 30;
        private int totalKillsTracker = 0;

        [Header("Exit Portal")]
        public GameObject exitPortal;

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI killCountText;
        [SerializeField] private TextMeshProUGUI objectiveMessageText;

        // Level timer
        private float levelTimer = 0f;
        private bool timerRunning = true;

        // Level identification
        private bool isLevel2 = false;

        void Start()
        {
            // Determine level from scene name
            string sceneName = SceneManager.GetActiveScene().name.ToLower();
            isLevel2 = sceneName.Contains("bossdevscene") || sceneName.Contains("bossdevscene");

            SpawnPlayerRandomly();
            StartCoroutine(SpawnEnemiesOverTime());
            Debug.Log("LayoutSpawner active on: " + name);

            // Ensure portal starts deactivated
            if (exitPortal != null)
                exitPortal.SetActive(false);

            UpdateObjectiveMessage();
            UpdateKillCountUI();
        }

        void Update()
        {
            // Tick the level timer
            if (timerRunning)
            {
                levelTimer += Time.deltaTime;
                UpdateTimerUI();
                UpdateObjectiveMessage();
            }
        }

        void UpdateTimerUI()
        {
            if (timerText != null)
            {
                int minutes = Mathf.FloorToInt(levelTimer / 60f);
                int seconds = Mathf.FloorToInt(levelTimer % 60f);
                timerText.text = $"{minutes:00}:{seconds:00}";
            }
        }

        void UpdateKillCountUI()
        {
            if (killCountText != null)
                killCountText.text = $"Kills: {totalKillsTracker} / {requiredKills}";
        }

        void UpdateObjectiveMessage()
        {
            if (objectiveMessageText == null) return;

            // Show for the first 5 seconds of the level, then permanently hide
            if (levelTimer < 5f)
            {
                objectiveMessageText.gameObject.SetActive(true);
                string levelLabel = isLevel2 ? "Level 2" : "Level 1";
                objectiveMessageText.text = $"{levelLabel}\nObjective: Kill {requiredKills} enemies to open the extraction portal.";
            }
            else if (totalKillsTracker >= requiredKills)
            {
                objectiveMessageText.gameObject.SetActive(true);
                objectiveMessageText.text = $"Extraction portal is now open!\nObjective: Find it and escape!";
            }
            else
            {
                objectiveMessageText.gameObject.SetActive(false);
            }
        }

        // spawn player randomly
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

        // spawn enemies (or squads) over time
        IEnumerator SpawnEnemiesOverTime()
        {
            if (enemyPrefab == null || enemySpawnParent == null)
                yield break;

            while (true)
            {
                // Wait for next spawn
                yield return new WaitForSeconds(enemySpawnDelay);

                // Check capacity and level
                if (!isLevel2)
                {
                    // Level 1: spawn single enemies
                    if (currentEnemyCount >= maxEnemies)
                        continue;

                    Transform spawn = enemySpawnParent.GetChild(
                        Random.Range(0, enemySpawnParent.childCount)
                    );
                    Vector3 spawnPos = spawn.position;
                    if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                        spawnPos = hit.position;
                    else if (Physics.Raycast(spawnPos + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit, 10f))
                        spawnPos = groundHit.point;

                    GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity, transform);
                    currentEnemyCount++;
                    Debug.Log($"Enemies alive: {currentEnemyCount}/{maxEnemies}");

                    EnemyAI ai = enemy.GetComponent<EnemyAI>();
                    if (ai != null)
                        ai.OnEnemyDeath += HandleEnemyDeath;
                }
                else
                {
                    // Level 2: spawn squads of enemies as waves
                    int squadSize = Random.Range(2, 5); // 2 to 4 enemies per squad
                    if (currentEnemyCount + squadSize > maxEnemies)
                        continue;

                    // Choose a spawn point for this squad
                    Transform spawn = enemySpawnParent.GetChild(
                        Random.Range(0, enemySpawnParent.childCount)
                    );
                    Vector3 basePos = spawn.position;
                    if (NavMesh.SamplePosition(basePos, out NavMeshHit hit2, 3f, NavMesh.AllAreas))
                        basePos = hit2.position;

                    List<EnemyAI> squadMembers = new List<EnemyAI>();
                    // Spawn each member around the base position
                    for (int i = 0; i < squadSize; i++)
                    {
                        Vector3 offset = Random.insideUnitSphere * 2f;
                        offset.y = 0; // keep on horizontal plane
                        Vector3 memberPos = basePos + offset;

                        if (NavMesh.SamplePosition(memberPos, out NavMeshHit hit3, 3f, NavMesh.AllAreas))
                            memberPos = hit3.position;
                        else if (Physics.Raycast(memberPos + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit2, 10f))
                            memberPos = groundHit2.point;

                        GameObject enemy = Instantiate(enemyPrefab, memberPos, Quaternion.identity, transform);
                        EnemyAI ai = enemy.GetComponent<EnemyAI>();
                        if (ai != null)
                        {
                            squadMembers.Add(ai);
                            ai.OnEnemyDeath += HandleEnemyDeath;
                        }
                        currentEnemyCount++;
                    }

                    // Assign formation and leader for this squad
                    if (squadMembers.Count > 0)
                    {
                        int leaderIndex = Random.Range(0, squadMembers.Count);
                        EnemyAI.FormationType formation = (Random.value > 0.5f) ? EnemyAI.FormationType.Line : EnemyAI.FormationType.Triangle;
                        if (formation == EnemyAI.FormationType.Triangle && squadMembers.Count < 3)
                            formation = EnemyAI.FormationType.Line;

                        int squadId = EnemyAI.CreateSquad(squadMembers, leaderIndex, formation);
                        Debug.Log($"Spawned squad #{squadId} of size {squadMembers.Count} with formation {formation}");
                    }
                }
            }
        }

        void HandleEnemyDeath()
        {
            currentEnemyCount = Mathf.Max(0, currentEnemyCount - 1);
            totalKillsTracker++;
            UpdateKillCountUI();
            Debug.Log($"Total kills: {totalKillsTracker}/{requiredKills}");

            if (totalKillsTracker >= requiredKills)
            {
                ActivateExitPortal();
            }
        }

        public void StopTimer()
        {
            timerRunning = false;
        }

        void ActivateExitPortal()
        {
            if (exitPortal != null)
            {
                exitPortal.SetActive(true);
                Debug.Log("Exit Portal Activated!");
            }
        }

        // get exit position
        public Vector3 GetExitPosition()
        {
            return exitPortal != null ? exitPortal.transform.position : Vector3.zero;
        }

        // Public getter for the final level time
        public float GetLevelTime() => levelTimer;
    }
}