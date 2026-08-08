namespace Civil3D.Domain.Commands;

/// <summary>
/// Marker for domain events published by the command framework. Events carry only serializable
/// data and never reference Autodesk objects. Subscribers are optional (Phase 5A publishes the
/// events; wiring subscribers is left to future phases).
/// </summary>
public interface IDomainEvent
{
}
