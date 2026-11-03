using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length != 4) throw new ArgumentException("Patcher original helper output managed");
var resolver = new DefaultAssemblyResolver();
foreach (var dir in new[] { Path.GetDirectoryName(args[0])!, Path.GetDirectoryName(args[1])!, args[3] }) resolver.AddSearchDirectory(dir);
using var game = AssemblyDefinition.ReadAssembly(args[0], new ReaderParameters { AssemblyResolver = resolver, InMemory = true });
using var helper = AssemblyDefinition.ReadAssembly(args[1], new ReaderParameters { AssemblyResolver = resolver, InMemory = true });
var module = game.MainModule;
if (module.AssemblyReferences.Any(r => r.Name == "Deadfire.DialogueOverhaul")) throw new InvalidDataException("Already patched input");
var hooks = helper.MainModule.Types.Single(t => t.FullName == "Deadfire.DialogueOverhaul.Hooks");
var manager = module.Types.Single(t => t.FullName == "Game.UI.UIConversationManager");
var records = new List<object>();
var optionsManager = module.Types.Single(t => t.FullName == "Game.UI.UIOptionsManager");
var optionsShow = optionsManager.Methods.Single(m => m.Name == "Show");
var optionsHook = module.ImportReference(hooks.Methods.Single(m => m.Name == "Options"));
foreach (var ret in optionsShow.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray())
{
    ret.OpCode = OpCodes.Ldarg_0; ret.Operand = null;
    var call = Instruction.Create(OpCodes.Call, optionsHook);
    optionsShow.Body.GetILProcessor().InsertAfter(ret, call);
    optionsShow.Body.GetILProcessor().InsertAfter(call, Instruction.Create(OpCodes.Ret));
}
optionsShow.Body.MaxStackSize = Math.Max(optionsShow.Body.MaxStackSize, 1);
records.Add(new { method = optionsShow.FullName, hook = "Options", sites = optionsShow.Body.Instructions.Count(i => i.Operand == optionsHook) });
var opcodes = typeof(OpCodes).GetFields().Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => o.Name);
foreach (var ins in optionsShow.Body.Instructions.Where(i => i.OpCode.OperandType == OperandType.ShortInlineBrTarget)) ins.OpCode = opcodes[ins.OpCode.Name[..^2]];
foreach (var pair in new[] { ("Show", "Begin"), ("CheckRebuildContent", "Capture"), ("OnWillAdvanceToPlayerResponse", "Reply"), ("OnWillContinue", "Advance"), ("Hide", "End") })
{
    var method = manager.Methods.Single(m => m.Name == pair.Item1);
    var target = module.ImportReference(hooks.Methods.Single(m => m.Name == pair.Item2));
    var il = method.Body.GetILProcessor();
    int count = 0;
    foreach (var ret in method.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray())
    {
        // Transform the existing ret so branch targets also execute the hook.
        // Hide's Boolean result remains on the stack for End(bool, Component).
        ret.OpCode = OpCodes.Ldarg_0; ret.Operand = null;
        var tail = ret;
        if (pair.Item2 == "Reply") { var arg = Instruction.Create(OpCodes.Ldarg_1); il.InsertAfter(tail, arg); tail = arg; }
        var call = Instruction.Create(OpCodes.Call, target); il.InsertAfter(tail, call); il.InsertAfter(call, Instruction.Create(OpCodes.Ret)); count++;
    }
    method.Body.MaxStackSize = Math.Max(method.Body.MaxStackSize, 3);
    foreach (var ins in method.Body.Instructions.Where(i => i.OpCode.OperandType == OperandType.ShortInlineBrTarget)) ins.OpCode = opcodes[ins.OpCode.Name[..^2]];
    records.Add(new { method = method.FullName, hook = pair.Item2, sites = count });
    if (count == 0) throw new InvalidDataException("No return site: " + method.FullName);
}
// Adjust only the two dialogue focus requests, not the global camera getter.
var rebuild = manager.Methods.Single(m => m.Name == "CheckRebuildContent");
var beforeCapture = module.ImportReference(hooks.Methods.Single(m => m.Name == "BeforeCapture"));
var entry = rebuild.Body.Instructions[0];
rebuild.Body.GetILProcessor().InsertBefore(entry, Instruction.Create(OpCodes.Ldarg_0));
rebuild.Body.GetILProcessor().InsertBefore(entry, Instruction.Create(OpCodes.Call, beforeCapture));
records.Add(new { method = rebuild.FullName, hook = "BeforeCapture", sites = 1 });
var offsetHook = module.ImportReference(hooks.Methods.Single(m => m.Name == "CameraOffset"));
var offsetSites = rebuild.Body.Instructions.Where(i => i.Operand is MethodReference mr && mr.DeclaringType.FullName == "Game.CameraControl" && mr.Name == "get_DefaultSpeakerCameraOffset").ToArray();
if (offsetSites.Length != 2) throw new InvalidDataException("Unexpected dialogue camera offset sites");
foreach (var site in offsetSites)
{
    var self = Instruction.Create(OpCodes.Ldarg_0); rebuild.Body.GetILProcessor().InsertAfter(site, self);
    rebuild.Body.GetILProcessor().InsertAfter(self, Instruction.Create(OpCodes.Call, offsetHook));
}
rebuild.Body.MaxStackSize = Math.Max(rebuild.Body.MaxStackSize, 8);
records.Add(new { method = rebuild.FullName, hook = "CameraOffset", sites = offsetSites.Length });
game.Write(args[2]);
using var verify = AssemblyDefinition.ReadAssembly(args[2]);
using var pristine = AssemblyDefinition.ReadAssembly(args[0]);
static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types)
{
    foreach (var type in types) { yield return type; foreach (var nested in AllTypes(type.NestedTypes)) yield return nested; }
}
static string BodyShape(MethodDefinition method)
{
    if (!method.HasBody) return "no-body";
    string Operand(object? value) => value switch {
        null => "",
        Instruction target => "branch:" + method.Body.Instructions.IndexOf(target),
        Instruction[] targets => "switch:" + string.Join(",", targets.Select(t => method.Body.Instructions.IndexOf(t))),
        MemberReference member => member.FullName,
        ParameterDefinition parameter => "param:" + parameter.Index,
        VariableDefinition variable => "local:" + variable.Index,
        _ => value.ToString()!
    };
    return method.Body.InitLocals + "\n" + string.Join("\n", method.Body.Variables.Select(v => v.VariableType.FullName)) + "\n" +
        string.Join("\n", method.Body.Instructions.Select(i => i.OpCode.Name + " " + Operand(i.Operand))) + "\n" +
        string.Join("\n", method.Body.ExceptionHandlers.Select(e => e.HandlerType + ":" + Operand(e.TryStart) + ":" + Operand(e.TryEnd) + ":" + Operand(e.HandlerStart) + ":" + Operand(e.HandlerEnd) + ":" + Operand(e.FilterStart) + ":" + e.CatchType?.FullName));
}
var allowed = new HashSet<string>(new[] { "Show", "CheckRebuildContent", "OnWillAdvanceToPlayerResponse", "OnWillContinue", "Hide" });
var outputMethods = AllTypes(verify.MainModule.Types).SelectMany(t => t.Methods).ToDictionary(m => m.FullName);
int unchangedMethods = 0;
foreach (var method in AllTypes(pristine.MainModule.Types).SelectMany(t => t.Methods))
{
    if (!outputMethods.TryGetValue(method.FullName, out var written)) throw new InvalidDataException("Method disappeared: " + method.FullName);
    if (method.DeclaringType.FullName == manager.FullName && allowed.Contains(method.Name)) continue;
    if (method.FullName == optionsShow.FullName) continue;
    if (BodyShape(method) != BodyShape(written)) throw new InvalidDataException("Unrelated method changed: " + method.FullName);
    unchangedMethods++;
}
var outputFields = AllTypes(verify.MainModule.Types).SelectMany(t => t.Fields).ToDictionary(f => f.FullName);
int unchangedFields = 0;
foreach (var field in AllTypes(pristine.MainModule.Types).SelectMany(t => t.Fields))
{
    if (!outputFields.TryGetValue(field.FullName, out var written) || field.Attributes != written.Attributes || !Equals(field.Constant, written.Constant) || !field.InitialValue.SequenceEqual(written.InitialValue))
        throw new InvalidDataException("Unrelated field changed: " + field.FullName);
    unchangedFields++;
}
foreach (var record in new[] { "Begin", "BeforeCapture", "Capture", "Reply", "Advance", "End" })
    if (verify.MainModule.Types.Single(t => t == verify.MainModule.Types.Single(x => x.FullName == manager.FullName)).Methods.SelectMany(m => m.HasBody ? m.Body.Instructions : Enumerable.Empty<Instruction>()).Count(i => i.Operand is MethodReference mr && mr.DeclaringType.FullName == hooks.FullName && mr.Name == record) != 1)
        throw new InvalidDataException("Hook verification failed: " + record);
if (verify.MainModule.Types.Single(t => t.FullName == optionsManager.FullName).Methods.Single(m => m.Name == "Show").Body.Instructions.Count(i => i.Operand is MethodReference mr && mr.DeclaringType.FullName == hooks.FullName && mr.Name == "Options") != 1)
    throw new InvalidDataException("Options hook verification failed");
if (verify.MainModule.Types.Single(t => t.FullName == manager.FullName).Methods.SelectMany(m => m.HasBody ? m.Body.Instructions : Enumerable.Empty<Instruction>()).Count(i => i.Operand is MethodReference mr && mr.DeclaringType.FullName == hooks.FullName && mr.Name == "CameraOffset") != 2)
    throw new InvalidDataException("Camera offset hook verification failed");
Console.WriteLine(JsonSerializer.Serialize(new { originalSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0]))), patchedSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[2]))), unchangedMethods, unchangedFields, records }, new JsonSerializerOptions { WriteIndented = true }));
