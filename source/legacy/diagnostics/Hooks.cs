using UnityEngine;

namespace Deadfire.DialogueOverhaul
{
    // Minimal loading/lifecycle probe: no MonoBehaviour, canvas, textures,
    // reflection, event system, input handlers, or scene state changes.
    public static class Hooks
    {
        public static void Begin(Component manager) { Debug.Log("[DeadfireDialogueDiagnostic] Begin"); }
        public static void Capture(Component manager) { Debug.Log("[DeadfireDialogueDiagnostic] Capture"); }
        public static void BeforeCapture(Component manager) { }
        public static void Reply(Component manager, string text) { Debug.Log("[DeadfireDialogueDiagnostic] Reply"); }
        public static void Advance(Component manager) { Debug.Log("[DeadfireDialogueDiagnostic] Advance"); }
        public static void Options(Component manager) { Debug.Log("[DeadfireDialogueDiagnostic] Options"); }
        public static bool End(bool hidden, Component manager) { Debug.Log("[DeadfireDialogueDiagnostic] End"); return hidden; }
        public static Vector3 CameraOffset(Vector3 original, Component manager) { return original; }
    }
}
