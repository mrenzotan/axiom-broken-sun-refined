using Axiom.Data;
using UnityEngine;

namespace Axiom.Core
{
    public sealed class CutscenePlayer
    {
        private CutsceneData _data;
        private int _index;

        public int CurrentSlideIndex => _index;
        public bool IsComplete { get; private set; }

        public CutsceneSlide CurrentSlide
        {
            get
            {
                if (IsComplete || _data == null || _data.slides == null || _index >= _data.slides.Count)
                    return null;
                return _data.slides[_index];
            }
        }

        public string NextSceneName => _data != null ? _data.nextSceneName ?? "" : "";

        public AudioClip CutsceneMusic => _data?.cutsceneMusic;

        public void Start(CutsceneData data)
        {
            _data = data;
            _index = 0;
            IsComplete = false;

            if (_data == null || _data.slides == null || _data.slides.Count == 0)
                IsComplete = true;
        }

        public void Advance()
        {
            if (IsComplete) return;

            _index++;
            if (_index >= (_data?.slides?.Count ?? 0))
                IsComplete = true;
        }

        public void Skip() => IsComplete = true;
    }
}
