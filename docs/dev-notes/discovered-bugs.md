# Discovered Bugs

## Battle scene

### Mishandled enemy death due to status condition

Enemy death is not handled when the enemy's HP bar goes to zero due to a status condition. I believe it is because the death handling is only triggered when the HP bar is updated, and status conditions do not trigger an HP bar update. This can lead to situations where an enemy is effectively dead but still appears alive in the battle scene, which can cause confusion for players.

**How to replicate**

1. Go to Batttle scene (Assets/Scenes/BattleScene.unity)
2. Set up a battle with an enemy that has a status condition that can reduce HP to zero (e.g., burning). I recommend using "Combust" spell every turn, and eventually the spell damage will reduce the enemy's HP enough to the point that the status effect will completely deplete the enemy's HP.
3. Observe that when the enemy's HP reaches zero due to the status condition, the enemy does not die and remains in the battle scene, allowing it to continue attacking and participating in the battle as if it were still alive. This can lead to confusion for players, as they may expect the enemy to be defeated when its HP reaches zero, but instead, it continues to function as if it were still alive.
   ![Image of mishandled enemy death](./images/mishandled-enemy-death.png)

## Platformer scene

### ~~Player sprite doesn't slide down walls~~ (Resolved)

In platformer scene, the player sprite doesn't slide down walls when colliding with them. This is likely due to a missing capsule collider material that should allow the player to slide along walls instead of sticking to them. This can disrupt the player's immersion and make it difficult to navigate the platformer scene, as they may find themselves stuck to walls and unable to move freely.

![alt text](./images/player-stuck-to-walls.png)

**Resolution:** Created `Assets/Settings/Physics/PlayerFrictionless.physicsMaterial2D` (Friction = 0, Bounciness = 0) and assigned it to the **Rigidbody2D → Material** slot on `Player (Exploration).prefab`. Unity's default physics material applies friction = 0.4 when the slot is empty, which generated a static friction force at the wall contact opposing gravity. Assigning on the Rigidbody2D (rather than the CapsuleCollider2D) makes the material apply to all current and future colliders on the body. No code change required — `PlayerMovement.Move()` already preserves Y-velocity correctly.

### ~~Player-wall collision interaction bug when jumping to a higher ground~~ (Resolved)

When the player jumps to a higher ground, there's a collision behavioral bug in which if the player jumps just enough to land or hit the floor of the higher ground, the player slides off the corner edge and bounces up. This happens when the player taps the jump button and keeps on holding the movement input towards the higher ground, which causes the player to collide with the corner edge of the higher ground and then bounce off due to the collision. This can be frustrating for players, as it can make it difficult to successfully jump to higher grounds and can lead to unintended consequences such as overshooting the jump which is crucial for a platformer game. See images for more context. In the images, the player is trying to jump to the higher ground on the right, but instead of landing on it, the player slides off the corner edge and bounces up, which can be seen in the second and third images. This bug can disrupt the player's experience and make it difficult to navigate the platformer scene effectively.

![alt text](image.png)
![alt text](image-1.png)
![alt text](image-2.png)
