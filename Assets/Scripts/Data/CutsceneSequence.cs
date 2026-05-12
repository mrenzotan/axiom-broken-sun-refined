using System.Collections.Generic;
using UnityEngine;

namespace Axiom.Data
{
    /// <summary>
    /// A sequence of cutscene steps. Executed in order by CutsceneRunner.
    /// Supports dialogue, camera moves, animations, waits, spell unlocks, and custom scene events.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCutsceneSequence", menuName = "Axiom/Data/Cutscene Sequence")]
    public class CutsceneSequence : ScriptableObject
    {
        [Tooltip("Ordered list of steps to execute in this cutscene.")]
        public List<CutsceneStep> steps = new List<CutsceneStep>();

        /// <summary>Read-only step count for validation.</summary>
        public int StepCount => steps?.Count ?? 0;
    }
}
