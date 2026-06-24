using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Axiom.Core
{
    /// <summary>
    /// Drives the End Credits scene.
    /// Spawns credit entries into a vertical layout that scrolls upward.
    /// Player can skip to MainMenu by holding Enter/Space for _holdToSkipDuration seconds,
    /// or tapping to speed up the scroll.
    ///
    /// Inspector setup:
    ///   _contentRoot     — RectTransform inside a ScrollRect (or just a plain RT).
    ///                      Credits entries are instantiated as children here.
    ///   _rolePrefab      — TMP_Text prefab for role labels (small, grey).
    ///   _namePrefab      — TMP_Text prefab for name labels (larger, white).
    ///   _headerPrefab    — TMP_Text prefab for section headers (bold, accent colour).
    ///   _spacerHeight    — pixels of blank space between sections.
    ///   _scrollSpeed     — units per second at normal speed.
    ///   _fastScrollSpeed — units per second when player taps to speed up.
    ///   _holdToSkipDuration — seconds to hold before skipping entirely.
    ///   _mainMenuScene   — scene name to load on skip/complete.
    /// </summary>
    public class CreditsController : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField]
        [Tooltip("Parent RectTransform that credit entry prefabs are spawned into.")]
        private RectTransform _contentRoot;

        [SerializeField]
        [Tooltip("TMP_Text prefab for role labels (small, grey). Shown above the name.")]
        private TMP_Text _rolePrefab;

        [SerializeField]
        [Tooltip("TMP_Text prefab for name labels (larger, white).")]
        private TMP_Text _namePrefab;

        [SerializeField]
        [Tooltip("TMP_Text prefab for section headers (bold, accent colour). Used when role is empty.")]
        private TMP_Text _headerPrefab;

        [SerializeField]
        [Tooltip("Vertical blank space in pixels inserted between credit groups.")]
        private float _spacerHeight = 40f;

        [Header("Scroll")]
        [SerializeField]
        [Tooltip("Pixels per second at normal scroll speed.")]
        private float _scrollSpeed = 60f;

        [SerializeField]
        [Tooltip("Pixels per second when the player taps to fast-forward.")]
        private float _fastScrollSpeed = 180f;

        [SerializeField]
        [Tooltip("Seconds to hold Enter/Space to skip the credits entirely.")]
        private float _holdToSkipDuration = 2f;

        [Header("Scene")]
        [SerializeField]
        private string _mainMenuScene = "MainMenu";

        [Header("Credits Content")]
        [SerializeField]
        private List<CreditsEntry> _entries = new List<CreditsEntry>
        {
            // ── Section headers have an empty role field ──────────────────────
            new CreditsEntry { role = "",              name = "— Development —"     },
            new CreditsEntry { role = "Game Design",   name = "Your Name Here"      },
            new CreditsEntry { role = "Programming",   name = "Your Name Here"      },
            new CreditsEntry { role = "Art",           name = "Your Name Here"      },
            new CreditsEntry { role = "",              name = ""                    }, // spacer
            new CreditsEntry { role = "",              name = "— Audio —"           },
            new CreditsEntry { role = "Music",         name = "Your Name Here"      },
            new CreditsEntry { role = "Sound Effects", name = "Your Name Here"      },
            new CreditsEntry { role = "",              name = ""                    }, // spacer
            new CreditsEntry { role = "",              name = "— Special Thanks —"  },
            new CreditsEntry { role = "",              name = "Your Name Here"      },
            new CreditsEntry { role = "",              name = ""                    }, // spacer
            new CreditsEntry { role = "",              name = "Thank you for playing Axiom." },
        };

        // ── Runtime ───────────────────────────────────────────────────────────
        private float _totalContentHeight;
        private float _holdTimer;
        private bool  _isFastScrolling;
        private bool  _skipTriggered;

        private void Start()
        {
            BuildCredits();
            StartCoroutine(ScrollCredits());
        }

        private void Update()
        {
            if (_skipTriggered) return;

            bool holding = (Keyboard.current != null &&
                           (Keyboard.current.enterKey.isPressed ||
                            Keyboard.current.spaceKey.isPressed)) ||
                           (Gamepad.current != null && Gamepad.current.aButton.isPressed);

            if (holding)
            {
                _holdTimer += Time.deltaTime;
                _isFastScrolling = true;

                if (_holdTimer >= _holdToSkipDuration)
                    TriggerSkip();
            }
            else
            {
                _holdTimer = 0f;
                _isFastScrolling = false;
            }
        }

        // ── Build ─────────────────────────────────────────────────────────────

        private void BuildCredits()
        {
            _totalContentHeight = 0f;

            foreach (var entry in _entries)
            {
                bool isSpacer = string.IsNullOrEmpty(entry.role) &&
                                string.IsNullOrEmpty(entry.name);
                bool isHeader = string.IsNullOrEmpty(entry.role) &&
                                !string.IsNullOrEmpty(entry.name);

                if (isSpacer)
                {
                    _totalContentHeight += _spacerHeight;
                    continue;
                }

                if (isHeader)
                {
                    TMP_Text header = Instantiate(_headerPrefab, _contentRoot);
                    header.text = entry.name;
                    LayoutRebuilder.ForceRebuildLayoutImmediate(
                        header.GetComponent<RectTransform>());
                    _totalContentHeight += header.preferredHeight + _spacerHeight * 0.5f;
                    continue;
                }

                // Role + Name pair
                if (!string.IsNullOrEmpty(entry.role))
                {
                    TMP_Text role = Instantiate(_rolePrefab, _contentRoot);
                    role.text = entry.role;
                    LayoutRebuilder.ForceRebuildLayoutImmediate(
                        role.GetComponent<RectTransform>());
                    _totalContentHeight += role.preferredHeight;
                }

                TMP_Text name = Instantiate(_namePrefab, _contentRoot);
                name.text = entry.name;
                LayoutRebuilder.ForceRebuildLayoutImmediate(
                    name.GetComponent<RectTransform>());
                _totalContentHeight += name.preferredHeight + _spacerHeight * 0.3f;
            }

            // Position content below the screen so it scrolls into view
            Vector2 pos = _contentRoot.anchoredPosition;
            pos.y = -Screen.height;
            _contentRoot.anchoredPosition = pos;
        }

        // ── Scroll ────────────────────────────────────────────────────────────

        private IEnumerator ScrollCredits()
        {
            // Wait one frame for the layout group to compute sizes
            yield return null;

            float target = _contentRoot.rect.height + Screen.height;

            while (_contentRoot.anchoredPosition.y < target && !_skipTriggered)
            {
                float speed = _isFastScrolling ? _fastScrollSpeed : _scrollSpeed;
                Vector2 pos = _contentRoot.anchoredPosition;
                pos.y += speed * Time.deltaTime;
                _contentRoot.anchoredPosition = pos;
                yield return null;
            }

            if (!_skipTriggered)
                LoadMainMenu();
        }

        // ── Skip ──────────────────────────────────────────────────────────────

        private void TriggerSkip()
        {
            if (_skipTriggered) return;
            _skipTriggered = true;
            StopAllCoroutines();
            LoadMainMenu();
        }

        private void LoadMainMenu()
        {
            GameManager gm = GameManager.Instance;
            if (gm?.SceneTransition != null)
                gm.SceneTransition.BeginTransition(_mainMenuScene, TransitionStyle.BlackFade);
            else
                SceneManager.LoadScene(_mainMenuScene);
        }
    }
}