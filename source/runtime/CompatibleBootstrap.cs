using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Doorstop
{
    public static class Entrypoint
    {
        private static string core, log;
        public static void Start()
        {
            try
            {
                core = Path.GetDirectoryName(Environment.GetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH") ?? Assembly.GetExecutingAssembly().Location);
                string root = Path.GetDirectoryName(core);
                log = Path.Combine(root, "DialogOverhaul.bootstrap.log");
                string managed = Environment.GetEnvironmentVariable("DOORSTOP_MANAGED_FOLDER_DIR");
                bool modular = File.Exists(Path.Combine(managed, "UnityEngine.CoreModule.dll"));
                string target = modular ? "UnityEngine.CoreModule.dll" : "UnityEngine.dll";
                string config = Path.Combine(Path.Combine(root, "config"), "BepInEx.cfg");
                Directory.CreateDirectory(Path.GetDirectoryName(config));
                string[] lines = File.Exists(config) ? File.ReadAllLines(config) : new string[0];
                string[] updated = BootstrapConfig.SelectEngine(lines, target);
                if (String.Join("\n", lines) != String.Join("\n", updated)) File.WriteAllLines(config, updated);
                Write("CLR " + Environment.Version + "; loader entry point " + target);
                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
                try
                {
                    Assembly preloader = Assembly.LoadFile(Path.Combine(core, "BepInEx.Preloader.dll"));
                    Type entry = preloader.GetType("Doorstop.Entrypoint", true);
                    entry.GetMethod("Start", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
                    Write("BepInEx startup returned.");
                }
                finally { AppDomain.CurrentDomain.AssemblyResolve -= Resolve; }
            }
            catch (Exception ex) { Write(ex.ToString()); }
        }
        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            string path = Path.Combine(core, new AssemblyName(args.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFile(path) : null;
        }
        private static void Write(string message)
        {
            try { File.AppendAllText(log, DateTime.Now.ToString("O") + " " + message + Environment.NewLine); }
            catch { }
        }
    }
    public static class BootstrapConfig
    {
        public static string[] SelectEngine(string[] input, string target)
        {
            List<string> output = new List<string>();
            bool section = false, foundSection = false, foundAssembly = false;
            foreach (string line in input)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("["))
                {
                    if (section && !foundAssembly) { output.Add("Assembly = " + target); foundAssembly = true; }
                    section = String.Equals(trimmed, "[Preloader.Entrypoint]", StringComparison.OrdinalIgnoreCase);
                    if (section) foundSection = true;
                }
                int equals = trimmed.IndexOf('=');
                if (section && equals >= 0 && String.Equals(trimmed.Substring(0, equals).Trim(), "Assembly", StringComparison.OrdinalIgnoreCase))
                {
                    string value = trimmed.Substring(equals + 1).Trim();
                    // Keep custom entry-point assemblies chosen by other mods.
                    if (String.Equals(value, "UnityEngine.dll", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "UnityEngine.CoreModule.dll", StringComparison.OrdinalIgnoreCase)) output.Add("Assembly = " + target);
                    else output.Add(line);
                    foundAssembly = true;
                }
                else output.Add(line);
            }
            if (!foundSection) { output.Add(""); output.Add("[Preloader.Entrypoint]"); }
            if (!foundAssembly) output.Add("Assembly = " + target);
            return output.ToArray();
        }
    }
}
