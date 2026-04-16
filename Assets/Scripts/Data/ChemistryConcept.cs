namespace Axiom.Data
{
    /// <summary>
    /// The core chemistry concept a spell belongs to. Mirrors the four pillars defined in
    /// <c>docs/LORE_AND_MECHANICS.md</c> → "The Four Core Chemistry Concepts":
    /// States of Matter &amp; Phase Changes, Acid–Base (Neutralization), Combustion &amp;
    /// Exothermic Reactions, and Solubility &amp; Precipitation.
    ///
    /// UI-only classification tag for spellbook sorting, tooltips, and tutorial callouts.
    /// Combat math is driven by <see cref="ChemicalCondition"/>, never this enum — this is
    /// deliberately NOT a Pokémon-style element/weakness system.
    /// </summary>
    public enum ChemistryConcept
    {
        None,
        PhaseChange,    // States of Matter — Freeze, Melt, Evaporate, Condense
        AcidBase,       // Neutralization   — Neutralize, Corrode
        Combustion,     // Exothermic       — Combust, Ignite Vapor
        Precipitation   // Solubility       — Crystal Bridge, Mineral Bind
    }
}
