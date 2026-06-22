#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;

public static class ForceRecompileScripts
{
    [MenuItem("Tools/Force Recompile Scripts")]
    private static void Recompile()
    {
        CompilationPipeline.RequestScriptCompilation(
            RequestScriptCompilationOptions.CleanBuildCache);
    }
}
#endif
