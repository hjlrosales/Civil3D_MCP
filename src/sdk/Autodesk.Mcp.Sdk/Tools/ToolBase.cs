using System.Reflection;
using System.Text.Json;
using Autodesk.Mcp.Shared.Serialization;

namespace Autodesk.Mcp.Sdk.Tools;

/// <summary>
/// Base class for strongly typed tools. Subclasses declare <c>TIn</c>/<c>TOut</c> DTOs that drive
/// JSON Schema generation and parameter binding.
/// </summary>
/// <typeparam name="TIn">Input DTO; must be a class with a parameterless constructor.</typeparam>
/// <typeparam name="TOut">Output DTO (or plain class).</typeparam>
public abstract class ToolBase<TIn, TOut> : ITool
    where TIn : class, new()
    where TOut : class
{
    private readonly string _name;

    /// <summary>Reads the tool name from the <see cref="McpToolAttribute"/>.</summary>
    protected ToolBase()
    {
        McpToolAttribute? attribute = GetType().GetCustomAttribute<McpToolAttribute>(inherit: false);
        _name = attribute?.Name
            ?? throw new InvalidOperationException($"Tool '{GetType().FullName}' is missing the [McpTool] attribute.");
    }

    /// <inheritdoc />
    public string Name => _name;

    /// <inheritdoc />
    public Type InputType => typeof(TIn);

    /// <inheritdoc />
    public Type OutputType => typeof(TOut);

    /// <inheritdoc />
    public virtual bool RequiresApplicationContext => false;

    /// <inheritdoc />
    public async Task<object?> ExecuteAsync(ToolExecutionContext context, JsonElement? parameters)
    {
        TIn input = Bind(parameters);
        TOut result = await ExecuteCoreAsync(input, context, context.CancellationToken);
        return result;
    }

    /// <summary>Executes the tool with strongly typed input.</summary>
    /// <param name="input">Bound input parameters.</param>
    /// <param name="context">Per-invocation context.</param>
    /// <param name="cancellationToken">Effective cancellation token.</param>
    protected abstract Task<TOut> ExecuteCoreAsync(TIn input, ToolExecutionContext context, CancellationToken cancellationToken);

    private static TIn Bind(JsonElement? parameters)
    {
        if (parameters is null or { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
        {
            return new TIn();
        }

        return parameters.Value.Deserialize<TIn>(SharedJson.Options) ?? new TIn();
    }
}
