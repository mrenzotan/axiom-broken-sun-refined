using System.Collections.Generic;
using UnityEngine;

namespace Axiom.Core
{
    /// <summary>
    /// Plain C# — no Unity dependencies.
    /// Owns IsTransitioning state and per-style timing/color config.
    /// Injected into SceneTransitionController via constructor.
    /// </summary>
    public class SceneTransitionService
    {
        public bool IsTransitioning { get; private set; }

        private static readonly Dictionary<TransitionStyle, (Color color, float fadeOut, float fadeIn)> Config =
            new Dictionary<TransitionStyle, (Color, float, float)>
            {
                [TransitionStyle.WhiteFlash] = (Color.white, 0.2f, 0.8f),
                [TransitionStyle.BlackFade]  = (Color.black, 0.5f, 0.5f),
            };

        public Color GetColor(TransitionStyle style)          => Config[style].color;
        public float GetFadeOutDuration(TransitionStyle style) => Config[style].fadeOut;
        public float GetFadeInDuration(TransitionStyle style)  => Config[style].fadeIn;

        public void SetTransitioning(bool value) => IsTransitioning = value;
    }
}
