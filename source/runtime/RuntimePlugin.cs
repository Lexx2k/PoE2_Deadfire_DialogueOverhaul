using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Deadfire.DialogOverhaul.Runtime
{
    [BepInPlugin(Id, "Deadfire Dialog Overhaul", "0.2.0")]
    [BepInProcess("PillarsOfEternityII.exe")]
    public sealed class RuntimePlugin : BaseUnityPlugin
    {
        public const string Id = "local.deadfire.dialogoverhaul";
        private void Awake()
        {
            Harmony harmony = new Harmony(Id);
            try
            {
                Type conversation = AccessTools.TypeByName("Game.UI.UIConversationManager");
                if (conversation == null) throw new InvalidOperationException("Deadfire conversation manager is unavailable.");
                if (conversation.Assembly.GetReferencedAssemblies().Any(a => a.Name == "Deadfire.DialogOverhaul" || a.Name == "Deadfire.DialogueOverhaul"))
                    throw new InvalidOperationException("Legacy permanently patched Assembly-CSharp.dll detected. Restore the verified original from DialogueOverhaulBackup before using the runtime version. Runtime hooks were not applied.");
                RuntimePatches.Apply(harmony);
                Logger.LogInfo("Runtime hooks installed: 6 game methods; original assemblies on disk unchanged. Unity " + Application.unityVersion);
            }
            catch (Exception ex)
            {
                harmony.UnpatchSelf();
                Logger.LogError("Runtime initialization failed; this plugin's patches removed. " + ex);
            }
        }
    }

    public static class RuntimePatches
    {
        public static void Apply(Harmony harmony)
        {
            Type conversation = AccessTools.TypeByName("Game.UI.UIConversationManager");
            Type options = AccessTools.TypeByName("Game.UI.UIOptionsManager");
            // Resolve every target before applying the first patch.
            MethodInfo show = Target(conversation, "Show", Type.EmptyTypes, typeof(void));
            MethodInfo rebuild = Target(conversation, "CheckRebuildContent", Type.EmptyTypes, typeof(void));
            MethodInfo reply = Target(conversation, "OnWillAdvanceToPlayerResponse", new Type[] { typeof(string) }, typeof(void));
            MethodInfo advance = Target(conversation, "OnWillContinue", Type.EmptyTypes, typeof(void));
            MethodInfo hide = Target(conversation, "Hide", new Type[] { typeof(bool) }, typeof(bool));
            MethodInfo optionShow = Target(options, "Show", Type.EmptyTypes, typeof(void));
            harmony.Patch(show, postfix: Patch("AfterShow"));
            harmony.Patch(rebuild, prefix: Patch("BeforeRebuild"), postfix: Patch("AfterRebuild"), transpiler: Patch("CameraTranspiler"));
            harmony.Patch(reply, postfix: Patch("AfterReply"));
            harmony.Patch(advance, postfix: Patch("AfterAdvance"));
            harmony.Patch(hide, postfix: Patch("AfterHide"));
            harmony.Patch(optionShow, postfix: Patch("AfterOptions"));
        }
        private static MethodInfo Target(Type type, string name, Type[] args, Type result)
        {
            MethodInfo method = type == null ? null : AccessTools.DeclaredMethod(type, name, args);
            if (method == null || method.ReturnType != result) throw new MissingMethodException(type == null ? "Missing game type" : type.FullName, name);
            return method;
        }
        private static HarmonyMethod Patch(string name) { return new HarmonyMethod(typeof(RuntimePatches), name); }
        public static void AfterShow(Component __instance) { Hooks.Begin(__instance); }
        public static void BeforeRebuild(Component __instance) { Hooks.BeforeCapture(__instance); }
        public static void AfterRebuild(Component __instance) { Hooks.Capture(__instance); }
        public static void AfterReply(Component __instance, string __0) { Hooks.Reply(__instance, __0); }
        public static void AfterAdvance(Component __instance) { Hooks.Advance(__instance); }
        public static void AfterHide(Component __instance, bool __result) { Hooks.End(__result, __instance); }
        public static void AfterOptions(Component __instance) { Hooks.Options(__instance); }
        public static IEnumerable<CodeInstruction> CameraTranspiler(IEnumerable<CodeInstruction> input)
        {
            List<CodeInstruction> output = new List<CodeInstruction>();
            MethodInfo hook = typeof(Hooks).GetMethod("CameraOffset");
            int count = 0;
            foreach (CodeInstruction instruction in input)
            {
                output.Add(instruction);
                MethodInfo method = instruction.operand as MethodInfo;
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && method != null && method.DeclaringType.FullName == "Game.CameraControl" && method.Name == "get_DefaultSpeakerCameraOffset")
                {
                    output.Add(new CodeInstruction(OpCodes.Ldarg_0));
                    output.Add(new CodeInstruction(OpCodes.Call, hook)); count++;
                }
            }
            if (count != 2) throw new InvalidOperationException("Expected two dialogue camera offset sites, found " + count + ". No camera patch applied.");
            return output;
        }
    }
}
