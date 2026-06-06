using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using Axiom.Platformer;

namespace Axiom.Voice
{
    public static class PlatformerSpellWorldCaster
    {
        public static bool TryCast(
            SpellData spell,
            IReadOnlyList<MeltableObstacleController> meltableObstacles,
            IReadOnlyList<FreezablePlatformController> freezablePlatforms,
            PlayerState playerState)
        {
            if (spell == null || string.IsNullOrWhiteSpace(spell.spellName)) return false;
            if ((meltableObstacles == null || meltableObstacles.Count == 0)
                && (freezablePlatforms == null || freezablePlatforms.Count == 0)) return false;
            if (playerState == null) return false;

            bool hasWorldTarget = false;
            if (meltableObstacles != null)
            {
                for (int i = 0; i < meltableObstacles.Count; i++)
                {
                    MeltableObstacleController obstacle = meltableObstacles[i];
                    if (obstacle != null && obstacle.CanMeltWith(spell.spellName))
                    {
                        hasWorldTarget = true;
                        break;
                    }
                }
            }

            if (!hasWorldTarget && freezablePlatforms != null)
            {
                for (int i = 0; i < freezablePlatforms.Count; i++)
                {
                    FreezablePlatformController platform = freezablePlatforms[i];
                    if (platform != null && platform.CanFreezeWith(spell.spellName))
                    {
                        hasWorldTarget = true;
                        break;
                    }
                }
            }

            if (!hasWorldTarget) return false;
            if (!playerState.TrySpendMp(spell.mpCost)) return false;

            bool handled = false;
            if (meltableObstacles != null)
            {
                for (int i = 0; i < meltableObstacles.Count; i++)
                {
                    MeltableObstacleController obstacle = meltableObstacles[i];
                    if (obstacle != null && obstacle.TryMelt(spell.spellName))
                        handled = true;
                }
            }

            if (freezablePlatforms != null)
            {
                for (int i = 0; i < freezablePlatforms.Count; i++)
                {
                    FreezablePlatformController platform = freezablePlatforms[i];
                    if (platform != null && platform.TryFreeze(spell.spellName))
                        handled = true;
                }
            }

            return handled;
        }
    }
}
