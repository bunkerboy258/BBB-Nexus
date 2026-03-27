#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace BBBNexus
{
    /// <summary>
    /// Detects optional 3rd-party dependencies (any import method: UPM/Assets/Plugins/precompiled dll)
    /// by checking for well-known types and toggles scripting define symbols.
    ///
    /// This lets optional feature assemblies use asmdef defineConstraints to be excluded from compilation
    /// when the dependency is not present.
    /// </summary>
    [InitializeOnLoad]
    internal static class BBBNexusDependencyDefines
    {
        private const string DefineUar = "BBBNEXUS_HAS_UAR";
        private const string DefineFinalIk = "BBBNEXUS_HAS_FINALIK";
        private const string DefineCinemachine = "BBBNEXUS_HAS_CINEMACHINE";

        static BBBNexusDependencyDefines()
        {
            UpdateDefines();

            // Re-run when Unity recompiles scripts.
            UnityEditor.Compilation.CompilationPipeline.compilationFinished += _ => UpdateDefines();
        }

        private static void UpdateDefines()
        {
            // NOTE: We intentionally use type presence. This works regardless of import method (UPM/Assets/Plugins/dll).
            bool hasUar = HasType("UnityEngine.Animations.Rigging.RigBuilder", "UnityEngine.Animations.Rigging");

            // FinalIK is imported as source scripts under Plugins and ends up in Assembly-CSharp-firstpass.
            // When BBBNexus isn't yet in its own asmdef, a separate optional asmdef can't reliably reference
            // both Assembly-CSharp and Assembly-CSharp-firstpass in all IDE/build pipelines.
            // So we DON'T auto-enable this symbol; keep FinalIK adapter code stubbed unless the user manually wires assemblies.
            bool hasFinalIk = HasType("RootMotion.FinalIK.AimIK", "Assembly-CSharp-firstpass") || HasType("RootMotion.FinalIK.AimIK", "Assembly-CSharp");

            bool hasCinemachine = HasType("Cinemachine.CinemachineBrain", "Cinemachine");

            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            SetDefine(group, DefineUar, hasUar);
            SetDefine(group, DefineFinalIk, hasFinalIk);
            SetDefine(group, DefineCinemachine, hasCinemachine);
        }

        private static bool HasType(string fullTypeName, string preferredAssemblyName)
        {
            // Fast path: specify the assembly name if known.
            if (Type.GetType($"{fullTypeName}, {preferredAssemblyName}") != null)
                return true;

            // Fallback: search loaded assemblies by type name.
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.GetType(fullTypeName, false) != null)
                        return true;
                }
                catch { }
            }

            return false;
        }

        private static void SetDefine(BuildTargetGroup group, string define, bool enabled)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .Where(d => !string.IsNullOrEmpty(d))
                .ToList();

            bool has = defines.Contains(define);
            if (enabled && !has)
            {
                defines.Add(define);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", defines));
            }
            else if (!enabled && has)
            {
                defines.RemoveAll(d => d == define);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", defines));
            }
        }
    }
}
#endif
