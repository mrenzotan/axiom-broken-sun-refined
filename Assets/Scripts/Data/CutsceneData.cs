using System;
using System.Collections.Generic;
using UnityEngine;

namespace Axiom.Data
{
    [CreateAssetMenu(fileName = "NewCutsceneData", menuName = "Axiom/Data/Cutscene Data")]
    public class CutsceneData : ScriptableObject
    {
        [Tooltip("Scene loaded after the cutscene completes. Default: Level_1-1.")]
        public string nextSceneName = "Level_1-1";

        [Tooltip("Optional background music for this cutscene. Played on the BGM bus (MusicVol).")]
        public AudioClip cutsceneMusic;

        [Tooltip("Slides displayed in order. Each slide has an image and text with typewriter effect.")]
        public List<CutsceneSlide> slides = new List<CutsceneSlide>();
    }

    [Serializable]
    public class CutsceneSlide
    {
        [Tooltip("Full-screen image for this slide.")]
        public Sprite image;

        [TextArea(3, 10)]
        [Tooltip("Text revealed character-by-character with typewriter effect.")]
        public string text;

        [Tooltip("Seconds after typewriter completes before auto-advancing. 0 = manual advance only.")]
        [Min(0f)]
        public float autoAdvanceDelay = 3f;
    }
}
