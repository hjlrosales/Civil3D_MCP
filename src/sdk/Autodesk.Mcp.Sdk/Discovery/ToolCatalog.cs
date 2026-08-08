using System.Collections.Concurrent;
using System.Reflection;
using Autodesk.Mcp.Shared.Dtos;
using Autodesk.Mcp.Sdk.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NJsonSchema;

namespace Autodesk.Mcp.Sdk.Discovery;

/// <summary>
/// Discovers tool types via reflection, generates and caches their manifests and input schemas at
/// startup, and instantiates tool instances lazily on first use through the DI container. Lazy
/// instantiation lets tools declare a dependency on <see cref="IToolCatalog"/> itself (for example
/// the <c>bridge/getCapabilities</c> health tool) without a construction-time cycle.
/// </summary>
public sealed class ToolCatalog : IToolCatalog
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ToolCatalog> _logger;
    private readonly ConcurrentDictionary<string, Type> _toolTypes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ITool> _instances = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, JsonSchema> _inputSchemas = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<ToolManifest> _manifests;

    /// <summary>Creates the catalog by scanning the given assemblies.</summary>
    /// <param name="assemblies">Assemblies whose tool classes should be discovered.</param>
    /// <param name="manifestGenerator">Manifest generator.</param>
    /// <param name="services">Service provider used to instantiate tools lazily.</param>
    /// <param name="logger">Logger.</param>
    public ToolCatalog(
        IEnumerable<Assembly> assemblies,
        ManifestGenerator manifestGenerator,
        IServiceProvider services,
        ILogger<ToolCatalog> logger)
    {
        _services = services;
        _logger = logger;
        var manifests = new List<ToolManifest>();
        foreach (Type type in ToolScanner.FindToolTypes(assemblies))
        {
            try
            {
                McpToolAttribute? attribute = type.GetCustomAttribute<McpToolAttribute>(inherit: false);
                if (attribute is null)
                {
                    continue;
                }

                if (!_toolTypes.TryAdd(attribute.Name, type))
                {
                    _logger.LogError("Duplicate tool name '{ToolName}' declared by {Type}; the earlier registration wins.", attribute.Name, type.FullName);
                    continue;
                }

                manifests.Add(manifestGenerator.Generate(type));
                _inputSchemas[attribute.Name] = manifestGenerator.GenerateJsonSchema(ManifestGenerator.GetInputType(type));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize tool '{Type}'.", type.FullName);
            }
        }

        _manifests = manifests;
        _logger.LogInformation("Discovered {Count} tools.", _manifests.Count);
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolManifest> Manifests => _manifests;

    /// <inheritdoc />
    public IReadOnlyCollection<string> ToolNames => _toolTypes.Keys.ToArray();

    /// <inheritdoc />
    public bool TryGetTool(string name, out ITool tool)
    {
        if (_instances.TryGetValue(name, out ITool? cached))
        {
            tool = cached;
            return true;
        }

        if (!_toolTypes.TryGetValue(name, out Type? type))
        {
            tool = null!;
            return false;
        }

        try
        {
            var created = (ITool)ActivatorUtilities.CreateInstance(_services, type);
            tool = _instances.GetOrAdd(name, created);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create tool '{ToolName}'.", name);
            tool = null!;
            return false;
        }
    }

    /// <inheritdoc />
    public ToolManifest? GetManifest(string name)
        => _manifests.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.Ordinal));

    /// <inheritdoc />
    public JsonSchema? GetInputSchema(string name)
        => _inputSchemas.TryGetValue(name, out JsonSchema? schema) ? schema : null;
}
