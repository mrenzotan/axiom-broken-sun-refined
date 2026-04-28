using UnityEngine;

namespace Axiom.Data
{
    [CreateAssetMenu(fileName = "NewBattleEnvironmentData", menuName = "Axiom/Data/Battle Environment Data")]
    public class BattleEnvironmentData : ScriptableObject
    {
        [Tooltip("Background sprite shown during battle. Applied to the Battle scene's background SpriteRenderer at scene load.")]
        public Sprite backgroundSprite;

        [Tooltip("Optional ambient colour tint applied to the battle background SpriteRenderer. White = no tint.")]
        public Color ambientTint = Color.white;

        [SerializeField]
        [Tooltip("Optional looping battle music. Played on the BGM bus (MusicVol mixer group) when battle starts.")]
        private AudioClip _battleMusic;

        /// <summary>Optional battle music clip. Null = no music during battle.</summary>
        public AudioClip BattleMusic => _battleMusic;
    }
}
