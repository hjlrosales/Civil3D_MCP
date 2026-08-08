namespace Autodesk.Mcp.Sdk.Registration;

/// <summary>Configuration for the on-disk endpoint registry.</summary>
public sealed class EndpointRegistryOptions
{
    /// <summary>
    /// Directory that holds endpoint descriptor files; defaults to
    /// <c>%LOCALAPPDATA%\AutodeskMcp\endpoints</c>.
    /// </summary>
    public string DirectoryPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AutodeskMcp",
        "endpoints");
}
