namespace Axiom.Data
{
    /// <summary>
    /// All chemical conditions a character can carry during combat.
    ///
    /// Material conditions describe chemical composition — what an enemy IS MADE OF.
    /// They are innate (set on EnemyData), never applied directly by spells, and are
    /// permanent unless temporarily suppressed by a phase-change reaction.
    /// They gate which attack types are valid (Liquid/Vapor are immune to physical attacks).
    ///
    /// Status conditions describe an active chemical state — what is CURRENTLY HAPPENING
    /// to a character. They are applied mid-combat by spells or as reaction byproducts,
    /// produce ongoing turn effects (DoT, crowd control), and expire after a fixed number of turns.
    ///
    /// None is the safe default for all SpellData condition fields — has no gameplay effect.
    /// </summary>
    public enum ChemicalCondition
    {
        None,

        // ── Material Conditions (innate composition; no turn effects) ──────────
        Liquid,           // Phase Change — immune to physical attacks; reaction target: Freeze → Solid, Evaporate → Vapor
        Solid,            // Phase Change — vulnerable to physical attacks; reaction target: Melt → Liquid, Shatter (physical exploit)
        Vapor,            // Phase Change — immune to physical attacks; reaction target: Combust → explosion, Condense → Liquid
        Flammable,        // Combustion  — reaction target: Combust → Burning
        Pressurized,      // Combustion  — reaction target: Combust → explosion bonus + Burning
        AcidicFluid,      // Acid–Base   — passive acid damage; reaction target: Base Surge → bonus damage
        AlkalineBase,     // Acid–Base   — reaction target: Acid spell → bonus damage
        MineralSaturated, // Precipitation — reaction target: Crystallize → Crystallized

        // ── Status Conditions (active states; can have turn effects) ──────────
        Frozen,           // Phase Change  — target skips their action (1 turn)
        Evaporating,      // Phase Change  — minor DoT each turn (2 turns)
        Burning,          // Combustion    — fire DoT each turn (2 turns)
        Corroded,         // Acid–Base     — escalating acid DoT: ×1.0 / ×1.5 / ×2.0 per tick (3 turns)
        Crystallized      // Precipitation — target's damage output halved (2 turns)
    }
}
