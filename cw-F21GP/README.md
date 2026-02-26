# F21GP - Coursework 1

This Unity project is built around a first-person survival shooter concept, combining 3D character control, NavMesh-based enemy navigation, Finite State Machine AI, and swarm-based crowd behaviour. Interactive crash crates introduce physics-based gameplay elements that directly influence enemy states and formations.



## Architecture

All scripts are located in `Assets/Scripts` and organised under the `F21GP` namespace.



## 1. Player Mechanics (F21GP.Player)

### PlayerCharacterController.cs
A full first-person controller built using Unity's `CharacterController`. Handles movement (walking, sprinting, jumping), gravity, mouse look, crate throwing logic, health management, and damage processing.  
This script acts as the central hub for all player-driven gameplay interactions.

### RayCastShoot.cs
Implements hitscan shooting using raycasting from the camera forward direction. Detects collisions with enemies and applies damage without using projectile physics.  
This ensures responsive combat while maintaining performance efficiency.



## 2. Enemy AI (F21GP.Enemy)

### EnemyAI.cs
Controls individual enemy behaviour using a Finite State Machine (Idle, Patrol, Wander, Chase, Attack, Stunned). Uses NavMeshAgent for navigation and raycasting for perception logic.  
Also manages damage handling, knockback reactions, and transitions between behavioural states.

### DroneSwarmManager.cs
Manages swarm-level coordination for Level 2 enemies. Implements cohesion, alignment, and separation behaviours inspired by the Boids algorithm.  
Responsible for assigning leader-follower roles and maintaining structured group movement.

### SwarmMember.cs
Represents an individual drone within a swarm. Applies steering forces and tracks the swarm leader to maintain formation behaviour.  
Separates local movement logic from global swarm coordination for modularity.



## 3. Interactables (F21GP.Interactions)

### CrashCrate.cs
Physics-based explosive crate that can be placed in the environment or thrown by the player. Applies radial knockback and stun effects to nearby enemies.  



## 4. Game Management (F21GP.Managers)

### GameManager.cs
Global manager providing shared references for all other files  



## 5. UI System (F21GP.UI)

### MainMenu
Handles scene loading and starting the game.  
Acts as the primary entry point into gameplay.

### PauseMenu
Controls time scaling and allows resume or quit actions.  
Ensures gameplay can be safely paused without disrupting system states.

### GameWonScreen
Displays victory state once objectives are completed.  
Provides transition options back to the main menu.

### GameOverScreen
Triggers when player health reaches zero.  
Allows restart or return to the main menu.

### HealthBarController
Updates UI elements based on the player’s health value.  
Provides real-time feedback linked directly to PlayerCharacterController.



## 6. Data Models (F21GP.ScriptableObjects)

### PlayerStats.cs
Stores configurable player attributes such as health, movement speed, sprint multiplier, jump force, and crate throw force.  
Separates gameplay data from logic for easier balancing inside the Unity Editor.

### EnemyStats.cs
Stores enemy configuration values including speeds, attack damage, sight range, sight angle, cooldowns, knockback force, and stun duration.  
Enables rapid tuning of AI behaviour without modifying source code.



## 7. Core Systems (F21GP.Core)

### Layout_Spawner.cs
Handles randomised enemy spawning, player spawn positioning, kill tracking, wave progression, and level transitions.  
Controls pacing, difficulty scaling, and objective completion logic.

### ExitPortal.cs
Detects player interaction and triggers asynchronous scene transitions between levels.  
Implements cross-level progression once kill requirements are met.



## Controls

- **W, A, S, D** – Movement  
- **Space** – Jump  
- **Left Shift** – Sprint  
- **Mouse** – Look  
- **C** – Throw Crash Crate  
- **Left Click** – Shoot  
- **Escape** – Pause  



## Assets Used

Enemies  
https://assetstore.unity.com/packages/3d/characters/robots/low-poly-combat-drone-82234  

Maze Layout  
https://assetstore.unity.com/packages/3d/environments/dungeons/tileable-maze-and-dungeon-blocks-259878  

Crash Crate  
https://assetstore.unity.com/packages/3d/props/industrial/crash-crate-161268  

general audio : https://assetstore.unity.com/packages/audio/sound-fx/free-sound-effects-pack-155776

doors and levers https://assetstore.unity.com/packages/tools/physics/interactive-physical-door-pack-163249

Loading animation : https://assetstore.unity.com/packages/tools/loading-screen-animation-98505

arm and gun : https://learn.unity.com/tutorial/let-s-try-shooting-with-raycasts