using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using Axiom.Platformer;

namespace Axiom.Voice
{
    public enum CastEvaluation
    {
        NoTarget,
        InsufficientMana,
        Castable
    }

    public static class PlatformerSpellWorldCaster
    {
        public static bool HasResolvableTarget(
            SpellData spell,
            IReadOnlyList<MeltableObstacleController> meltableObstacles,
            IReadOnlyList<FreezablePlatformController> freezablePlatforms,
            IReadOnlyList<BurnableObstacleController> burnableObstacles,
            IReadOnlyList<SteamVentController> steamVents,
            IReadOnlyList<AcidPuddleController> acidPuddles)
        {
            if (spell == null || string.IsNullOrWhiteSpace(spell.spellName)) return false;

            if (meltableObstacles != null)
                for (int i = 0; i < meltableObstacles.Count; i++)
                {
                    MeltableObstacleController obstacle = meltableObstacles[i];
                    if (obstacle != null && obstacle.CanMeltWith(spell.spellName)) return true;
                }

            if (freezablePlatforms != null)
                for (int i = 0; i < freezablePlatforms.Count; i++)
                {
                    FreezablePlatformController platform = freezablePlatforms[i];
                    if (platform != null && platform.CanFreezeWith(spell.spellName)) return true;
                }

            if (burnableObstacles != null)
                for (int i = 0; i < burnableObstacles.Count; i++)
                {
                    BurnableObstacleController obstacle = burnableObstacles[i];
                    if (obstacle != null && obstacle.CanIgniteWith(spell.spellName)) return true;
                }

            if (steamVents != null)
                for (int i = 0; i < steamVents.Count; i++)
                {
                    SteamVentController vent = steamVents[i];
                    if (vent != null && vent.CanIgniteWith(spell.spellName)) return true;
                }

            if (acidPuddles != null)
                for (int i = 0; i < acidPuddles.Count; i++)
                {
                    AcidPuddleController puddle = acidPuddles[i];
                    if (puddle != null && puddle.CanNeutralizeWith(spell.spellName)) return true;
                }

            return false;
        }

        public static CastEvaluation EvaluateCast(
            SpellData spell,
            int currentMp,
            IReadOnlyList<MeltableObstacleController> meltableObstacles,
            IReadOnlyList<FreezablePlatformController> freezablePlatforms,
            IReadOnlyList<BurnableObstacleController> burnableObstacles,
            IReadOnlyList<SteamVentController> steamVents,
            IReadOnlyList<AcidPuddleController> acidPuddles)
        {
            // HasResolvableTarget already guards spell == null, so mpCost below is null-safe.
            if (!HasResolvableTarget(spell, meltableObstacles, freezablePlatforms,
                    burnableObstacles, steamVents, acidPuddles))
                return CastEvaluation.NoTarget;

            if (currentMp < spell.mpCost)
                return CastEvaluation.InsufficientMana;

            return CastEvaluation.Castable;
        }

        public static bool TryCast(
            SpellData spell,
            IReadOnlyList<MeltableObstacleController> meltableObstacles,
            IReadOnlyList<FreezablePlatformController> freezablePlatforms,
            IReadOnlyList<BurnableObstacleController> burnableObstacles,
            IReadOnlyList<SteamVentController> steamVents,
            IReadOnlyList<AcidPuddleController> acidPuddles,
            PlayerState playerState)
        {
            if (!HasResolvableTarget(spell, meltableObstacles, freezablePlatforms,
                    burnableObstacles, steamVents, acidPuddles))
                return false;
            if (playerState == null) return false;
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
