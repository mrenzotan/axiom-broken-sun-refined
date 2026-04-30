using UnityEngine;

namespace Axiom.Data
{
    /// <summary>
    /// Enum defining the type of action a cutscene step performs.
    /// </summary>
    public enum CutsceneStepType
    {
        Dialogue,        // Show dialogue from DialogueData
        CameraMove,      // Lerp camera to a target position/rotation
        PlayAnimation,   // Trigger an animator parameter
        WaitSeconds,     // Pause for a duration
        UnlockSpell,     // Grant a spell via SpellUnlockService
        SceneEvent,      // Emit a custom event (door, NPC facing, etc.)
    }

    /// <summary>
    /// A serializable step within a CutsceneSequence. Each step encapsulates
    /// one action (dialogue, camera, animation, wait, spell unlock, or custom event).
    /// </summary>
    [System.Serializable]
    public class CutsceneStep
    {
        [Tooltip("Type of action this step performs.")]
        public CutsceneStepType stepType = CutsceneStepType.Dialogue;

        [Tooltip("Dialogue asset to display (required if stepType == Dialogue).")]
        public DialogueData dialogueData;

        [Tooltip("Target position for camera move (required if stepType == CameraMove).")]
        public Vector3 cameraTargetPosition = Vector3.zero;

        [Tooltip("Target rotation for camera move (required if stepType == CameraMove).")]
        public Vector3 cameraTargetRotation = Vector3.zero;

        [Tooltip("Duration of camera lerp in seconds.")]
        public float cameraDuration = 1f;

        [Tooltip("Animator parameter name to set (required if stepType == PlayAnimation).")]
        public string animationParameter = "Idle";

        [Tooltip("Duration to wait in seconds (required if stepType == WaitSeconds).")]
        public float waitDuration = 1f;

        [Tooltip("Spell to unlock (required if stepType == UnlockSpell).")]
        public SpellData spellToUnlock;

        [Tooltip("Custom event name to emit (required if stepType == SceneEvent).")]
        public string sceneEventName = "";
    }
}
