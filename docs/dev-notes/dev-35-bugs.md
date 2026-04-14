## Bug with dev-35 world state preservation.

### Bug 1

- After completing battle by "Flee", the scene briefly shows the player spawning in the initial spawn point then teleports back to the last position before the battle. This is a bug that needs to be fixed to ensure a smoother gameplay experience. The issue seems to be related to the way the game handles world state preservation during battles, and it may require adjustments to the code that manages player positioning and state saving/loading during combat scenarios.

### Bug 2

- After completing battle by defeating the enemy, the scene briefly shows the player spawning in the initial spawn point then the scene flashes white, triggering the white flash transition effect to battle scene. The battle scene then loads the same enemy that was just defeated. This then continues on a loop of the same battle scene loading after defeating the enemy. This is a critical bug that disrupts the flow of the game and can lead to player frustration. The issue appears to be related to the way the game handles scene transitions and enemy state management after a battle is won. It may require a thorough review of the code responsible for transitioning between scenes and managing enemy states to ensure that once an enemy is defeated, it does not respawn or trigger unintended transitions.
