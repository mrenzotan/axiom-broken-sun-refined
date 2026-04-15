using System;
using UnityEngine;

namespace Axiom.Data
{
    [Serializable]
    public class ReactionEntry
    {
        [Tooltip("The condition (material or status) this spell reacts with if present on the effect target.")]
        public ChemicalCondition reactsWith;

        [Tooltip("Flat bonus added to the spell's primary effect when this reaction fires.")]
        public int reactionBonusDamage;

        [Tooltip("Material condition temporarily applied to the target when a phase-change reaction fires. None if no transformation.")]
        public ChemicalCondition transformsTo;

        [Tooltip("How many turns the transformed material condition lasts. Only meaningful when transformsTo != None.")]
        public int transformationDuration;
    }
}
