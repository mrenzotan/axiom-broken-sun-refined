using System;
using Axiom.Data;

namespace Axiom.Battle
{
    public class ItemEffectResolver
    {
        public ItemUseResult Resolve(ItemData item, CharacterStats target)
        {
            if (item   == null) throw new ArgumentNullException(nameof(item));
            if (target == null) throw new ArgumentNullException(nameof(target));

            int amount = 0;

            switch (item.effectType)
            {
                case ItemEffectType.RestoreHP:
                {
                    int before = target.CurrentHP;
                    target.Heal(item.effectPower);
                    amount = target.CurrentHP - before;
                    break;
                }
                case ItemEffectType.RestoreMP:
                {
                    int before = target.CurrentMP;
                    target.RestoreMP(item.effectPower);
                    amount = target.CurrentMP - before;
                    break;
                }
                case ItemEffectType.Revive:
                {
                    if (target.IsDefeated)
                    {
                        target.Heal(item.effectPower);
                        amount = target.CurrentHP;
                    }
                    break;
                }
            }

            if (item.curesConditions != null)
            {
                foreach (ChemicalCondition condition in item.curesConditions)
                {
                    if (condition != ChemicalCondition.None)
                        target.ConsumeCondition(condition);
                }
            }

            return new ItemUseResult
            {
                EffectType = item.effectType,
                Amount     = amount
            };
        }
    }
}
