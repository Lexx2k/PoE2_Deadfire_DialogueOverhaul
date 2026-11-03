using System;
using System.Reflection;

namespace Deadfire.DialogOverhaul
{
    public static class MethodDispatch
    {
        public static object Invoke(object obj, string name, params object[] args)
        {
            if (obj == null) return null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (Type type = obj.GetType(); type != null; type = type.BaseType)
            {
                MethodInfo selected = null;
                int bestScore = -1;
                foreach (MethodInfo method in type.GetMethods(flags))
                {
                    if (method.Name != name || method.ContainsGenericParameters) continue;
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length != args.Length) continue;
                    int score = 0;
                    bool compatible = true;
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        Type parameter = parameters[i].ParameterType;
                        if (args[i] == null)
                        {
                            if (parameter.IsValueType && Nullable.GetUnderlyingType(parameter) == null) { compatible = false; break; }
                        }
                        else if (parameter == args[i].GetType()) score += 2;
                        else if (parameter.IsInstanceOfType(args[i])) score++;
                        else { compatible = false; break; }
                    }
                    if (compatible && score > bestScore) { selected = method; bestScore = score; }
                }
                if (selected != null) return selected.Invoke(obj, args);
            }
            throw new MissingMethodException(obj.GetType().FullName, name + " (compatible argument types)");
        }
    }
}
