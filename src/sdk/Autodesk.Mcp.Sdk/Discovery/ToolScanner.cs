using System.Reflection;
using Autodesk.Mcp.Sdk.Tools;

namespace Autodesk.Mcp.Sdk.Discovery;

/// <summary>
/// Reflection scan that finds concrete tool classes (classes implementing <see cref="ITool"/>
/// decorated with <see cref="McpToolAttribute"/>).
/// </summary>
public static class ToolScanner
{
    /// <summary>Scans the given assemblies for tool types.</summary>
    /// <param name="assemblies">Assemblies to scan.</param>
    public static IReadOnlyList<Type> FindToolTypes(IEnumerable<Assembly> assemblies)
    {
        var result = new List<Type>();
        foreach (Assembly assembly in assemblies.Distinct())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(static t => t is not null).Cast<Type>().ToArray();
            }

            foreach (Type type in types)
            {
                if (type.IsAbstract || type.IsInterface || !typeof(ITool).IsAssignableFrom(type))
                {
                    continue;
                }

                if (type.GetCustomAttribute<McpToolAttribute>(inherit: false) is null)
                {
                    continue;
                }

                result.Add(type);
            }
        }

        return result;
    }
}
