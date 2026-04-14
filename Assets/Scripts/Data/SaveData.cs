using System;

namespace Axiom.Data
{
    [Serializable]
    public struct InventorySaveEntry
    {
        public string itemId;
        public int quantity;
    }

    /// <summary>
    /// Disk-serializable snapshot only. No UnityEngine.Object references.
    /// Field names are stable for JSON on disk.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int saveVersion = 1;

        public int playerLevel;
        public int playerXp;
        public int currentHp;
        public int currentMp;
        public int maxHp;
        public int maxMp;

        public string[] unlockedSpellIds = Array.Empty<string>();
        public InventorySaveEntry[] inventory = Array.Empty<InventorySaveEntry>();

        public float worldPositionX;
        public float worldPositionY;
        public string activeSceneName = string.Empty;

        public string[] activatedCheckpointIds = Array.Empty<string>();
    }
}
