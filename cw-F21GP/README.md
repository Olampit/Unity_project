# F21GP - Coursework 1 ReadMe

This unity project is built around a simple game idea, of a first person shooter, with a twist of survival, featuring character control in 3 dimensions, an EnemyAI system with full Finite State Machines, and navigation with Navmeshes, along with interactive crates that affect the behaviour of enemies's physics.

## Architecture

These scripts are located in Assets/Scripts:

1. **Player Mechanics (F21GP.Player)**:
    * PlayerCharacterController.cs: A full player controller using Unity's built-in CharacterController. Handles movement (walking/running), jumping, mouselook (camera rotation), logic for throwing crash crates, and health and damage processing.
    * RayCastShoot.cs: Allows the player to shoot using raycasts, detecting hits on enemy's collider.

2. **Enemy AI (F21GP.Enemy)**:
    * EnemyAI.cs: Logic to handle enemy pathfinding, targeting player, movement, and damaging health of the player.

3. **Interactables (F21GP.Interactions)**:
    * CrashCrate.cs: Logic for physics-based interactable crate that can be placed on the map, or the player can throw.

4. **Game Management (F21GP.Managers)**:
    * GameManager.cs: A singleton that can be initiated once and accessed globally is our class to manage the game states and global references (such as the Player transform).

5. **UI System (F21GP.UI)**:
    * Contains scripts for game canvas screens including MainMenu, PauseMenu, GameWonScreen and a GameOverScreen. Includes a HealthBarController for health indication.

6. **Data Models (F21GP.ScriptableObjects)**:
    * Uses Unity's ScriptableObject concept. Data like PlayerStats.cs and EnemyStats.cs are separated from the behavior scripts for easier management in the Unity Editor.

7. **Core Systems (F21GP.Core)**:
    * Layout_Spawner: For generating enemies in their designated spawn points and sticking to the max enemy count, spawning players in the designated player spawn points, advancing player to consecutive levels.

## Getting Started

1. **Engine version**: The unity editor version the game is built on is 2022.3.62f3
2. **Opening the project**: Add the project path cw-F21GP in Unity Hub and open it.
3. **Import all Assets**: Add all the required assets from the asset manager.
3. **Scenes**: Look in Assets/Scenes and ensure GameManager and the Player character persist correctly on entry.
4. **Edit Stats**: To modify player or enemy health, speed, and damage and other attributes, check the ScriptableObjects in the Project window.

## Controls
* **Movement**: W, A, S, D
* **Jump**: Space
* **Sprint**: Left Shift
* **Look**: Mouse
* **Throw Crate**: C
* **Shoot**: Left Click (assuming default for Raycast Shoot)
* **Pause**: Escape

##

All custom code uses C# and is under the F21GP namespace.
