using System.IO;
using NUnit.Framework;

namespace PlatformerTests
{
    public class PlatformerAssemblyBoundaryTests
    {
        [Test]
        public void PlatformerAsmdef_DoesNotReferenceAxiomBattle()
        {
            string json = File.ReadAllText("Assets/Scripts/Platformer/Platformer.asmdef");
            StringAssert.DoesNotContain("\"Axiom.Battle\"", json);
        }
    }
}
