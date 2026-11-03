using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;

var root = Path.GetFullPath(args[0]);
var game = Path.GetFullPath(args[1]);
var core = Path.Combine(root, "PillarsOfEternityII_Data/DialogOverhaul/BepInEx/core");
var plugin = Path.Combine(root, "PillarsOfEternityII_Data/DialogOverhaul/BepInEx/plugins/DeadfireDialogOverhaul");
AssemblyLoadContext.Default.Resolving += (context, name) => {
    foreach (var dir in new[] { core, plugin, Path.Combine(root, "PillarsOfEternityII_Data/Managed"), Path.Combine(game, "PillarsOfEternityII_Data/Managed") }) {
        var path = Path.Combine(dir, name.Name + ".dll");
        if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
    }
    return null;
};
Run();
void Run()
{
    var assembly = Assembly.LoadFrom(Path.Combine(plugin, "Deadfire.DialogOverhaul.Runtime.dll"));
    var patches = assembly.GetType("Deadfire.DialogOverhaul.Runtime.RuntimePatches", true)!;
    var harmony = Assembly.LoadFrom(Path.Combine(core, "0Harmony.dll"));
    var instruction = harmony.GetType("HarmonyLib.CodeInstruction", true)!;
    var listType = typeof(List<>).MakeGenericType(instruction);
    var camera = typeof(Game.CameraControl).GetProperty("DefaultSpeakerCameraOffset")!.GetMethod!;
    var opcode = instruction.GetField("opcode")!;
    var operand = instruction.GetField("operand")!;
    var labels = instruction.GetField("labels")!;
    object Make(OpCode code, object? arg = null) => Activator.CreateInstance(instruction, code, arg)!;
    var input = (System.Collections.IList)Activator.CreateInstance(listType)!;
    var marker = Make(OpCodes.Nop);
    var label = new DynamicMethod("labels", typeof(void), Type.EmptyTypes).GetILGenerator().DefineLabel();
    ((System.Collections.IList)labels.GetValue(marker)!).Add(label);
    input.Add(marker); input.Add(Make(OpCodes.Call, camera)); input.Add(Make(OpCodes.Nop)); input.Add(Make(OpCodes.Callvirt, camera));
    var rewrite = patches.GetMethod("CameraTranspiler")!;
    var result = ((System.Collections.IEnumerable)rewrite.Invoke(null, new object[] { input })!).Cast<object>().ToList();
    Check(result.Count == 8, "Both native camera sites gain two instructions");
    Check(ReferenceEquals(result[0], marker) && ((System.Collections.IList)labels.GetValue(marker)!).Contains(label), "Existing instruction labels are preserved");
    Check(result.Count(i => operand.GetValue(i) is MethodInfo m && m.Name == "CameraOffset") == 2, "Two helper camera hooks inserted");
    Check((OpCode)opcode.GetValue(result[2])! == OpCodes.Ldarg_0 && (OpCode)opcode.GetValue(result[6])! == OpCodes.Ldarg_0, "Each hook receives its owning conversation manager");
    foreach (int count in new[] { 0, 1, 3 }) {
        var invalid = (System.Collections.IList)Activator.CreateInstance(listType)!;
        for (int i = 0; i < count; i++) invalid.Add(Make(OpCodes.Call, camera));
        bool refused = false;
        try { rewrite.Invoke(null, new object[] { invalid }); }
        catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException) { refused = true; }
        Check(refused, "Unexpected camera site count refuses the patch: " + count);
    }
    Check(patches.GetMethod("AfterHide")!.ReturnType == typeof(void), "Hide postfix does not rewrite the game's return value");
    Check(!assembly.GetReferencedAssemblies().Any(a => a.Name == "Assembly-CSharp"), "Runtime plugin has no compile-time game assembly replacement dependency");
    Console.WriteLine("9 runtime transpiler/bridge checks passed.");
}
static void Check(bool value, string message) { if (!value) throw new Exception(message); }
namespace Game { public static class CameraControl { public static object? DefaultSpeakerCameraOffset { get { return null; } } } }
