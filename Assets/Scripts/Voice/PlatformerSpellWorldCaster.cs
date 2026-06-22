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
            IReadOnlyList<BurnableObstacleController> burnableObstacles,
            IReadOnlyList<SteamVentController> steamVents,
            IReadOnlyList<AcidPuddleController> acidPuddles,
            PlayerState playerState)
        {
            if (spell == null || string.IsNullOrWhiteSpace(spell.spellName)) return false;
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

            if (!hasWorldTarget && burnableObstacles != null)
            {
                for (int i = 0; i < burnableObstacles.Count; i++)
                {
                    BurnableObstacleController obstacle = burnableObstacles[i];
                    if (obstacle != null && obstacle.CanIgniteWith(spell.spellName))
                    {
                        hasWorldTarget = true;
                        break;
                    }
                }
            }

            if (!hasWorldTarget && steamVents != null)
            {
                for (int i = 0; i < steamVents.Count; i++)
                {
                    SteamVentController vent = steamVents[i];
                    if (vent != null && vent.CanIgniteWith(spell.spellName))
                    {
                        hasWorldTarget = true;
                        break;
                    }
                }
            }

            if (!hasWorldTarget && acidPuddles != null)
            {
                for (int i = 0; i < acidPuddles.Count; i++)
                {
                    AcidPuddleController puddle = acidPuddles[i];
                    if (puddle != null && puddle.CanNeutralizeWith(spell.spellName))
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

            if (burnableObstacles != null)
            {
                for (int i = 0; i < burnableObstacles.Count; i++)
                {
                    BurnableObstacleController obstacle = burnableObstacles[i];
                    if (obstacle != null && obstacle.TryIgnite(spell.spellName))
                        handled = true;
                }
            }

            if (steamVents != null)
            {
                for (int i = 0; i < steamVents.Count; i++)
                {
                    SteamVentController vent = steamVents[i];
                    if (vent != null && vent.TryIgnite(spell.spellName))
                        handled = true;
                }
            }

            if (acidPuddles != null)
            {
                for (int i = 0; i < acidPuddles.Count; i++)
                {
                    AcidPuddleController puddle = acidPuddles[i];
                    if (puddle != null && puddle.TryNeutralize(spell.spellName))
                        handled = true;
                }
            }

            return handled;
        }
    }
}
