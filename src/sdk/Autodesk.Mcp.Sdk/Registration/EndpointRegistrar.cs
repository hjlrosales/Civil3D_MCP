using System.Text.Json;
using Autodesk.Mcp.Shared.Dtos;
using Autodesk.Mcp.Shared.Serialization;
using Microsoft.Extensions.Logging;

namespace Autodesk.Mcp.Sdk.Registration;

/// <summary>
/// Writes, refreshes and removes the endpoint descriptor file under the registry directory.
/// File name pattern: <c>&lt;product&gt;-&lt;pid&gt;.json</c>; wire field names follow
/// <see cref="SharedJson.Options"/> (including the <c>pid</c>/<c>startedUtc</c> names of AD-03).
/// </summary>
public sealed class EndpointRegistrar : IEndpointRegistrar
{
    private readonly EndpointRegistryOptions _options;
    private readonly ILogger<EndpointRegistrar> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _sync = new();
    private string? _fileName;

    /// <summary>Creates the registrar.</summary>
    /// <param name="options">Registry options.</param>
    /// <param name="logger">Logger.</param>
    public EndpointRegistrar(EndpointRegistryOptions options, ILogger<EndpointRegistrar> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RegisterAsync(EndpointDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_options.DirectoryPath);
        string fileName = Path.Combine(_options.DirectoryPath, $"{Sanitize(descriptor.Product)}-{descriptor.ProcessId}.json");
        await WriteAsync(fileName, descriptor, cancellationToken).ConfigureAwait(false);
        lock (_sync)
        {
            _fileName = fileName;
        }

        _logger.LogInformation("Endpoint descriptor registered at {Path}.", fileName);
    }

    /// <inheritdoc />
    public async Task UpdateHeartbeatAsync(DateTimeOffset timestamp, CancellationToken cancellationToken = default)
    {
        string? fileName;
        lock (_sync)
        {
            fileName = _fileName;
        }

        if (fileName is null || !File.Exists(fileName))
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(fileName))
            {
                return;
            }

            EndpointDescriptor? descriptor = JsonSerializer.Deserialize<EndpointDescriptor>(
                await File.ReadAllTextAsync(fileName, cancellationToken).ConfigureAwait(false),
                SharedJson.Options);
            if (descriptor is null)
            {
                return;
            }

            await WriteLockedAsync(fileName, descriptor with { LastHeartbeatAtUtc = timestamp }).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync()
    {
        string? fileName;
        lock (_sync)
        {
            fileName = _fileName;
            _fileName = null;
        }

        if (fileName is null)
        {
            return Task.CompletedTask;
        }

        try
        {
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            _logger.LogInformation("Endpoint descriptor removed.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete endpoint descriptor {Path}.", fileName);
        }

        return Task.CompletedTask;
    }

    private async Task WriteAsync(string fileName, EndpointDescriptor descriptor, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteLockedAsync(fileName, descriptor).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task WriteLockedAsync(string fileName, EndpointDescriptor descriptor)
        => await File.WriteAllTextAsync(fileName, JsonSerializer.Serialize(descriptor, SharedJson.Options)).ConfigureAwait(false);

    private static string Sanitize(string product)
        => string.Concat(product.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-'));
}
