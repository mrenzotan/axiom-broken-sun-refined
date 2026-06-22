namespace Axiom.Platformer
{
    /// <summary>
    /// Something a steam-vent explosion can clear (a burnable crate, a rubble barrier).
    /// The vent talks only to this contract, never to concrete obstacle types.
    /// </summary>
    public interface IExplosionDestructible
    {
        /// <summary>Clear/destroy self as a consequence of a nearby explosion. Idempotent.</summary>
        void Detonate();
    }
}
