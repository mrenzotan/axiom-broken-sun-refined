using System;

namespace Axiom.Data
{
    [Serializable]
    public struct InventorySaveEntry
    {
        public string itemId;
        public int quantity;
    }

    [Serializable]
    public struct EnemyHpSaveEntry
    {
        public string enemyId;
        public int currentHp;
    }

    [Serializable]
    public struct DefeatedEnemiesSceneEntry
    {
        public string sceneName;
        public string[] enemyIds;
    }

    [Serializable]
    public struct DamagedEnemyHpSceneEntry
    {
        public string sceneName;
        public EnemyHpSaveEntry[] entries;
    }

    /// <summary>
    /// Disk-serializable snapshot only. No UnityEngine.Object references.
    /// Field names are stable for JSON on disk.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int saveVersion = 2;

        public int playerLevel;
        public int playerXp;
        public int currentHp;
        public int currentMp;
        public int maxHp;
        public int maxMp;

        public int attack;
        public int defense;
        public int speed;

        public string[] unlockedSpellIds = Array.Empty<string>();
        public InventorySaveEntry[] inventory = Array.Empty<InventorySaveEntry>();

        public float worldPositionX;
        public float worldPositionY;
        public string activeSceneName = string.Empty;

        public string[] activatedCheckpointIds = Array.Empty<string>();

        public float lastCheckpointPositionX;
        public float lastCheckpointPositionY;
        public string lastCheckpointSceneName = string.Empty;

        // Progression snapshot at last checkpoint touch — restored on respawn so XP/levels
        // gained between checkpoints roll back. Default zero means "no snapshot captured".
        public int checkpointLevel;
        public int checkpointXp;
        public int checkpointMaxHp;
        public int checkpointMaxMp;
        public int checkpointAttack;
        public int checkpointDefense;
        public int checkpointSpeed;
        public string[] checkpointUnlockedSpellIds = Array.Empty<string>();

        // Legacy (saveVersion 1). Read on load for migration; never written by current builds.
        public string[] defeatedEnemyIds = Array.Empty<string>();
        public EnemyHpSaveEntry[] damagedEnemyHp = Array.Empty<EnemyHpSaveEntry>();

        // Per-scene tracking (saveVersion 2+). Avoids a death in one level wiping defeated
        // enemies in another, and lets RespawnAtLastCheckpoint reset only the dying scene.
        public DefeatedEnemiesSceneEntry[] defeatedEnemiesPerScene = Array.Empty<DefeatedEnemiesSceneEntry>();
        public DamagedEnemyHpSceneEntry[] damagedEnemyHpPerScene = Array.Empty<DamagedEnemyHpSceneEntry>();

        public string[] collectedPickupIds = Array.Empty<string>();

        // Tutorial completion flags (DEV-46). Default false on legacy saves missing these keys.
        public bool hasSeenFirstDeath;
        public bool hasSeenFirstSpikeHit;
        public bool hasCompletedFirstBattleTutorial;
        public bool hasCompletedSpellTutorialBattle;

        public bool hasExplorationMenusUnlocked;
    }
}
