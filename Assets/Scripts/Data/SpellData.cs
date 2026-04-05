using UnityEngine;

namespace Axiom.Data
{
    [CreateAssetMenu(fileName = "NewSpellData", menuName = "Axiom/Data/Spell Data")]
    public class SpellData : ScriptableObject
    {
        [Tooltip("The spoken trigger word or phrase the player says to cast this spell.")]
        public string spellName;
    }
}
