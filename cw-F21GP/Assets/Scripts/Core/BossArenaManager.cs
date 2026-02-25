using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using F21GP.Enemy;

namespace F21GP.Core
{
    public class BossArenaManager : MonoBehaviour
    {
        [Header("Player")]
        public Transform player;
        public Transform playerSpawnParent;

        [Header("Swarm Setup")]
        public bool enableWaves = true; // this toggle is for demo, where we can show pure swarm system, without waiting for waves to show up
        public int swarmsToSpawn = 1; // Used if waves are disabled
        public GameObject swarmDronePrefab;
        public Transform enemySpawnParent;
        public int dronesPerSwarm = 6;
        private int currentWave = 1;
        private int maxWaves = 3;
        private int activeDrones = 0;
        private bool isVictory = false;

        [Header("Exit Portal")]
        [SerializeField] private GameObject exitPortal;

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI objectiveMessageText;

        // Level timer
        private float levelTimer = 0f;
        private bool timerRunning = true;

        void Start()
        {
            SpawnPlayerRandomly();
            StartWave(currentWave);
            Debug.Log("BossArenaManager active on: " + name);

            // Ensure portal starts deactivated
            if (exitPortal != null)
                exitPortal.SetActive(false);

            UpdateObjectiveMessage();
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

        void UpdateObjectiveMessage()
        {
            if (objectiveMessageText == null) return;

            if (isVictory)
            {
                objectiveMessageText.gameObject.SetActive(true);
                objectiveMessageText.text = "Victory!\nObjective: Escape through the extraction portal!";
            }
            else
            {
                objectiveMessageText.gameObject.SetActive(true);
                if (enableWaves)
                {
                    objectiveMessageText.text = $"Boss Level\nWave {currentWave}/{maxWaves}\nObjective: Defeat all swarms!";
                }
                else
                {
                    objectiveMessageText.text = "Boss Level\nObjective: Defeat the swarms!";
                }
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

        void StartWave(int waveNumber)
        {
            Debug.Log($"Starting Wave {waveNumber}");
            
            if (swarmDronePrefab == null || enemySpawnParent == null || enemySpawnParent.childCount == 0)
            {
                Debug.LogWarning("BossArenaManager: Cannot spawn swarms. Missing prefab or spawn points.");
                return;
            }

            // If waves are enabled, spawn count corresponds to wave number. Otherwise, use inspector value.
            int currentSwarmsToSpawn = enableWaves ? waveNumber : swarmsToSpawn;
            activeDrones = currentSwarmsToSpawn * dronesPerSwarm;

            for (int s = 0; s < currentSwarmsToSpawn; s++)
            {
                // Each swarm gets its own parent GameObject with a DroneSwarmManager
                GameObject swarmParent = new GameObject($"Swarm_W{waveNumber}_{s}");
                DroneSwarmManager manager = swarmParent.AddComponent<DroneSwarmManager>();

                // Pick a spawn point (randomize or cycle)
                int spawnIndex = Random.Range(0, enemySpawnParent.childCount);
                Transform spawnPoint = enemySpawnParent.GetChild(spawnIndex);

                for (int i = 0; i < dronesPerSwarm; i++)
                {
                    Vector3 randomOffset = Random.insideUnitSphere * 2f;
                    randomOffset.y = 0;
                    
                    Vector3 spawnPos = spawnPoint.position + randomOffset;

                    if (UnityEngine.AI.NavMesh.SamplePosition(spawnPos, out UnityEngine.AI.NavMeshHit hit, 4f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        spawnPos = hit.position;
                    }
                    
                    GameObject drone = Instantiate(swarmDronePrefab, spawnPos, spawnPoint.rotation, swarmParent.transform);
                    
                    // Assign drone to this specific swarm
                    SwarmMember member = drone.GetComponent<SwarmMember>();
                    if (member != null)
                    {
                        member.AssignSwarm(manager);
                    }
                    
                    // Listen to death to progress wave
                    EnemyAI ai = drone.GetComponent<EnemyAI>();
                    if (ai != null)
                    {
                        ai.OnEnemyDeath += HandleDroneDeath;
                    }
                }
                
                Debug.Log($"Spawned Wave {waveNumber} Swarm {s} at {spawnPoint.name}");
            }
            
            UpdateObjectiveMessage();
        }

        void HandleDroneDeath()
        {
            activeDrones--;
            if (activeDrones <= 0)
            {
                if (enableWaves)
                {
                    currentWave++;
                    if (currentWave <= maxWaves)
                    {
                        StartWave(currentWave);
                    }
                    else
                    {
                        HandleBossDefeated();
                    }
                }
                else
                {
                    HandleBossDefeated();
                }
            }
        }

        void HandleBossDefeated()
        {
            isVictory = true;
            Debug.Log($"All Waves Defeated! Time: {levelTimer}s");

            ActivateExitPortal();
        }

        void ActivateExitPortal()
        {
            // Stop the timer when boss dies (or you can keep it running until portal entry)
            // Let's keep it running until portal entry, so we don't stop it here.
            // If you DO want it to stop exactly when boss dies, uncomment below:
            // timerRunning = false;

            // Activate the portal object
            if (exitPortal != null)
            {
                exitPortal.SetActive(true);
                Debug.Log("Exit Portal Activated!");
            }
            else
            {
                // Fallback: load next scene/main menu if no portal is present
                StartCoroutine(LoadMainMenuDelayed());
            }
            
            UpdateObjectiveMessage();
        }

        public void StopTimer()
        {
            timerRunning = false;
        }

        public float GetLevelTime() => levelTimer;

        private IEnumerator LoadMainMenuDelayed()
        {
            yield return new WaitForSeconds(5f);
            SceneManager.LoadScene("WinScreen");
        }
    }
}
