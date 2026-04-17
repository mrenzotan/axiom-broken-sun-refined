using System;
using UnityEngine;
using Axiom.Data;

namespace Axiom.Core
{
    /// <summary>
    /// Temporary debug MonoBehaviour for manual smoke-testing of the spell unlock system.
    /// Attach to the GameManager prefab during testing. Remove before shipping.
    ///
    /// Usage: In Play Mode, right-click this component in the Inspector → pick a test.
    /// Check the Console for results.
    /// </summary>
    public class SpellUnlockDebug : MonoBehaviour
    {
        [Header("Story Unlock Testing")]
        [Tooltip("Assign a SpellData with requiredLevel = 0 to test story-driven unlock (ST-2.1, ST-2.2, ST-2.4).")]
        [SerializeField] private SpellData _storySpell;

        [Tooltip("Assign a SpellData with requiredLevel = 0 AND a prerequisiteSpell to test story prerequisite unlock (ST-2.3).")]
        [SerializeField] private SpellData _storySpellWithPrerequisite;

        // ── Group 1: Level-Up Spell Unlocks ──

        [ContextMenu("ST-1.1: New Game (starter spells at level 1)")]
        private void ST_1_1_StarterSpells()
        {
            GameManager.Instance.StartNewGame();
            LogUnlockedSpells("ST-1.1 — StartNewGame → starter spells granted");
        }

        [ContextMenu("ST-1.2: Set Level 3 + Grant Spells")]
        private void ST_1_2_LevelUpToLevel3()
        {
            GameManager.Instance.PlayerState.ApplyProgression(3, 0);
            GameManager.Instance.SpellUnlockService.NotifyPlayerLevel(3);
            LogUnlockedSpells("ST-1.2 — Level 3 → level-gated spells granted");
        }

        [ContextMenu("ST-1.3: Set Level 3 (prerequisite chain test)")]
        private void ST_1_3_PrerequisiteChain()
        {
            // Both crystal spike (level 3, no prereq) and shatter (level 3, requires crystal spike)
            // should unlock in a single NotifyPlayerLevel(3) call.
            GameManager.Instance.PlayerState.ApplyProgression(3, 0);
            GameManager.Instance.SpellUnlockService.NotifyPlayerLevel(3);
            LogUnlockedSpells("ST-1.3 — Level 3 → prerequisite chain should resolve in one call");

            bool hasShatter = GameManager.Instance.SpellUnlockService.Contains(
                FindSpellByName("shatter"));

            if (hasShatter)
                Debug.Log("[SpellUnlockDebug] PASS: 'shatter' unlocked via prerequisite chain.");
            else
                Debug.LogWarning("[SpellUnlockDebug] FAIL: 'shatter' NOT unlocked — prerequisite chain did not resolve.");
        }

        [ContextMenu("ST-1.5: Repeated NotifyPlayerLevel (no re-fire test)")]
        private void ST_1_5_NoDuplicateEvents()
        {
            // First call — grants starters.
            GameManager.Instance.PlayerState.ApplyProgression(1, 0);
            GameManager.Instance.SpellUnlockService.NotifyPlayerLevel(1);
            int countAfterFirst = GameManager.Instance.SpellUnlockService.UnlockedSpells.Count;

            // Second call — should not re-fire or duplicate.
            int eventsFired = 0;
            void Counter(SpellData _) => eventsFired++;
            GameManager.Instance.SpellUnlockService.OnSpellUnlocked += Counter;

            GameManager.Instance.SpellUnlockService.NotifyPlayerLevel(1);

            GameManager.Instance.SpellUnlockService.OnSpellUnlocked -= Counter;

            int countAfterSecond = GameManager.Instance.SpellUnlockService.UnlockedSpells.Count;

            Debug.Log($"[SpellUnlockDebug] ST-1.5 — Count before: {countAfterFirst}, after: {countAfterSecond}, events fired on repeat: {eventsFired}");
            if (eventsFired == 0 && countAfterFirst == countAfterSecond)
                Debug.Log("[SpellUnlockDebug] PASS: No duplicate events or spells on repeated call.");
            else
                Debug.LogWarning("[SpellUnlockDebug] FAIL: Duplicate events or spells detected.");
        }

        // ── Group 2: Story-Driven Spell Unlocks ──

        [ContextMenu("ST-2.1: Level 99 — story spell NOT auto-granted")]
        private void ST_2_1_StorySpellNotAutoGranted()
        {
            if (_storySpell == null)
            {
                Debug.LogError("[SpellUnlockDebug] Assign _storySpell (requiredLevel = 0) in the Inspector first.");
                return;
            }

            GameManager.Instance.PlayerState.ApplyProgression(99, 0);
            GameManager.Instance.SpellUnlockService.NotifyPlayerLevel(99);

            bool contains = GameManager.Instance.SpellUnlockService.Contains(_storySpell);
            LogUnlockedSpells("ST-2.1 — Level 99 → story-only spell should NOT be in list");

            if (!contains)
                Debug.Log($"[SpellUnlockDebug] PASS: '{_storySpell.spellName}' correctly excluded from level-up grants.");
            else
                Debug.LogWarning($"[SpellUnlockDebug] FAIL: '{_storySpell.spellName}' was auto-granted — requiredLevel should be 0.");
        }

        [ContextMenu("ST-2.2: Story Unlock — direct Unlock() call")]
        private void ST_2_2_StoryUnlock()
        {
            if (_storySpell == null)
            {
                Debug.LogError("[SpellUnlockDebug] Assign _storySpell in the Inspector first.");
                return;
            }

            bool result = GameManager.Instance.SpellUnlockService.Unlock(_storySpell);
            LogUnlockedSpells("ST-2.2 — Direct Unlock() call");

            if (result)
                Debug.Log($"[SpellUnlockDebug] PASS: '{_storySpell.spellName}' unlocked via story trigger.");
            else
                Debug.LogWarning($"[SpellUnlockDebug] '{_storySpell.spellName}' was already unlocked (expected if running after ST-2.2).");
        }

        [ContextMenu("ST-2.3: Story Unlock — spell with prerequisite")]
        private void ST_2_3_StoryUnlockWithPrerequisite()
        {
            if (_storySpellWithPrerequisite == null)
            {
                Debug.LogError("[SpellUnlockDebug] Assign _storySpellWithPrerequisite in the Inspector first.");
                return;
            }

            bool result = GameManager.Instance.SpellUnlockService.Unlock(_storySpellWithPrerequisite);
            LogUnlockedSpells("ST-2.3 — Direct Unlock() of spell with prerequisite");

            Debug.Log($"[SpellUnlockDebug] ST-2.3 — Unlock returned: {result}. " +
                      "Note: direct Unlock() bypasses prerequisite checks by design (story triggers are authoritative).");
        }

        [ContextMenu("ST-2.4: Duplicate story unlock — silent no-op")]
        private void ST_2_4_DuplicateStoryUnlock()
        {
            if (_storySpell == null)
            {
                Debug.LogError("[SpellUnlockDebug] Assign _storySpell in the Inspector first.");
                return;
            }

            // Ensure it's unlocked first.
            GameManager.Instance.SpellUnlockService.Unlock(_storySpell);

            int eventsFired = 0;
            void Counter(SpellData _) => eventsFired++;
            GameManager.Instance.SpellUnlockService.OnSpellUnlocked += Counter;

            bool result = GameManager.Instance.SpellUnlockService.Unlock(_storySpell);

            GameManager.Instance.SpellUnlockService.OnSpellUnlocked -= Counter;

            Debug.Log($"[SpellUnlockDebug] ST-2.4 — Duplicate Unlock returned: {result}, events fired: {eventsFired}");
            if (!result && eventsFired == 0)
                Debug.Log("[SpellUnlockDebug] PASS: Duplicate unlock silently ignored.");
            else
                Debug.LogWarning("[SpellUnlockDebug] FAIL: Duplicate unlock was not ignored.");
        }

        // ── Group 5: Edge Cases ──

        [ContextMenu("ST-5.2: Empty catalog — no errors")]
        private void ST_5_2_EmptyCatalog()
        {
            GameManager.Instance.SpellUnlockService.NotifyPlayerLevel(10);
            int count = GameManager.Instance.SpellUnlockService.UnlockedSpells.Count;

            Debug.Log($"[SpellUnlockDebug] ST-5.2 — NotifyPlayerLevel(10) on current catalog. Unlocked count: {count}");
        }

        [ContextMenu("ST-5.3: Rapid consecutive unlocks")]
        private void ST_5_3_RapidUnlocks()
        {
            if (_storySpell == null || _storySpellWithPrerequisite == null)
            {
                Debug.LogError("[SpellUnlockDebug] Assign both story spell fields in the Inspector first.");
                return;
            }

            // Rapid-fire three operations.
            GameManager.Instance.SpellUnlockService.Unlock(_storySpell);
            GameManager.Instance.SpellUnlockService.Unlock(_storySpellWithPrerequisite);
            GameManager.Instance.PlayerState.ApplyProgression(5, 0);
            GameManager.Instance.SpellUnlockService.NotifyPlayerLevel(5);

            LogUnlockedSpells("ST-5.3 — Rapid consecutive unlocks + level grant");
            Debug.Log("[SpellUnlockDebug] PASS if no exceptions above and all spells listed.");
        }

        // ── Tear Down ──

        [ContextMenu("TEAR DOWN: Reset to New Game state")]
        private void TearDown()
        {
            // Restore player to fresh level-1 state with only starter spells.
            GameManager.Instance.PlayerState.ApplyProgression(1, 0);
            GameManager.Instance.SpellUnlockService.RestoreFromIds(Array.Empty<string>());
            GameManager.Instance.SpellUnlockService.NotifyPlayerLevel(1);

            LogUnlockedSpells("TEAR DOWN — Reset to level 1 with starter spells only");
            Debug.Log("[SpellUnlockDebug] Game state reverted. Only starter spells (requiredLevel = 1) remain.");
        }

        // ── Helpers ──

        private static void LogUnlockedSpells(string header)
        {
            var service = GameManager.Instance.SpellUnlockService;
            var spells = service.UnlockedSpells;

            Debug.Log($"[SpellUnlockDebug] {header}\n" +
                      $"  Player Level: {GameManager.Instance.PlayerState.Level}\n" +
                      $"  Unlocked ({spells.Count}): {string.Join(", ", service.UnlockedSpellNames)}");
        }

        private static SpellData FindSpellByName(string spellName)
        {
            var spells = GameManager.Instance.SpellUnlockService.UnlockedSpells;
            for (int i = 0; i < spells.Count; i++)
            {
                if (spells[i].spellName == spellName)
                    return spells[i];
            }
            return null;
        }
    }
}
