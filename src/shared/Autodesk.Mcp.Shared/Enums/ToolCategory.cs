using System.Text.Json.Serialization;
using Autodesk.Mcp.Shared.Serialization;

namespace Autodesk.Mcp.Shared.Enums;

/// <summary>
/// Functional category of a tool, mirroring the bridge's tool folders. Serialized on the wire as
/// the exact member name (for example <c>Alignments</c>); unknown values read from a newer bridge
/// fall back to <see cref="Unknown"/> so older clients stay compatible.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<ToolCategory>))]
public enum ToolCategory
{
    /// <summary>Unclassified or unrecognized category (fallback for forward compatibility).</summary>
    Unknown = 0,

    /// <summary>General / cross-cutting tools.</summary>
    General,

    /// <summary>Drawing-level operations.</summary>
    Drawing,

    /// <summary>Layer management.</summary>
    Layers,

    /// <summary>Alignment operations.</summary>
    Alignments,

    /// <summary>Profile (vertical) operations.</summary>
    Profiles,

    /// <summary>Surface operations.</summary>
    Surfaces,

    /// <summary>Corridor operations.</summary>
    Corridors,

    /// <summary>Pipe network operations.</summary>
    PipeNetworks,

    /// <summary>Pressure network operations.</summary>
    PressureNetworks,

    /// <summary>COGO point operations.</summary>
    Cogo,

    /// <summary>Parcel operations.</summary>
    Parcels,

    /// <summary>Style management.</summary>
    Styles,

    /// <summary>Generic object operations.</summary>
    Objects,

    /// <summary>Export and interoperability tools.</summary>
    Export,

    /// <summary>Engineering workflow orchestrations (cut/fill, QTO, reports).</summary>
    Engineering,
}
