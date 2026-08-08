using System.Text.Json;
using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Cogo.Dtos;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Domain.Styles.Dtos;
using Civil3D.Domain.Surfaces.Dtos;
using Xunit;
using static Civil3D.Domain.Tests.TestDoubles;

namespace Civil3D.Domain.Tests;

/// <summary>
/// DTO wire-safety: every domain DTO round-trips through System.Text.Json with all values intact,
/// proving the DTOs contain only serializable types (no Autodesk references) as required.
/// </summary>
public class DtoSerializationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(AlignmentKind.Centerline)]
    [InlineData(AlignmentKind.Offset)]
    [InlineData(SurfaceKind.TinVolume)]
    [InlineData(StyleKind.Corridor)]
    public void Enums_AreSerializable(Enum value)
    {
        // Serialize and deserialize with the concrete enum type so both sides use the same
        // converter (numbers), mirroring how DTO properties carry strongly typed enums.
        string json = JsonSerializer.Serialize(value, value.GetType());
        Enum roundTripped = (Enum)JsonSerializer.Deserialize(json, value.GetType())!;

        Assert.Equal(value, roundTripped);
    }

    [Fact]
    public void AlignmentInfo_RoundTrips()
    {
        AlignmentInfo source = Alignment(1, "Mainline");

        string json = JsonSerializer.Serialize(source, Options);
        AlignmentInfo? result = JsonSerializer.Deserialize<AlignmentInfo>(json, Options);

        Assert.Equal(source, result);
    }

    [Fact]
    public void SurfaceInfo_RoundTrips()
    {
        SurfaceInfo source = Surface(1, "EG");

        string json = JsonSerializer.Serialize(source, Options);
        SurfaceInfo? result = JsonSerializer.Deserialize<SurfaceInfo>(json, Options);

        Assert.NotNull(result);
        Assert.Equal(source, result);
        Assert.Equal(40.0, result.MaximumElevation);
    }

    [Fact]
    public void ProfileInfo_RoundTrips()
    {
        ProfileInfo source = Profile(2, "EG Profile", 42);

        string json = JsonSerializer.Serialize(source, Options);
        ProfileInfo? result = JsonSerializer.Deserialize<ProfileInfo>(json, Options);

        Assert.NotNull(result);
        Assert.Equal(source, result);
        Assert.Equal(42, result.AlignmentId);
    }

    [Fact]
    public void CorridorInfo_RoundTrips()
    {
        CorridorInfo source = Corridor(3, "Corridor A", 42);

        string json = JsonSerializer.Serialize(source, Options);
        CorridorInfo? result = JsonSerializer.Deserialize<CorridorInfo>(json, Options);

        Assert.Equal(source, result);
    }

    [Fact]
    public void PipeNetworkInfo_RoundTripsWithNestedParts()
    {
        PipeNetworkInfo source = PipeNetwork(4, "Storm");

        string json = JsonSerializer.Serialize(source, Options);
        PipeNetworkInfo? result = JsonSerializer.Deserialize<PipeNetworkInfo>(json, Options);

        Assert.NotNull(result);
        Assert.Equal(source.Name, result.Name);
        Assert.Equal(source.Pipes.Select(p => p.Id), result.Pipes.Select(p => p.Id));
        Assert.Equal(source.Structures.Select(s => s.Id), result.Structures.Select(s => s.Id));
    }

    [Fact]
    public void CogoPointInfo_RoundTrips()
    {
        CogoPointInfo source = CogoPoint(5, 100);

        string json = JsonSerializer.Serialize(source, Options);
        CogoPointInfo? result = JsonSerializer.Deserialize<CogoPointInfo>(json, Options);

        Assert.Equal(source, result);
    }

    [Fact]
    public void StyleInfo_RoundTrips()
    {
        StyleInfo source = Style(6, "Road", StyleKind.Alignment);

        string json = JsonSerializer.Serialize(source, Options);
        StyleInfo? result = JsonSerializer.Deserialize<StyleInfo>(json, Options);

        Assert.Equal(source, result);
    }

    [Fact]
    public void Collections_RoundTrip()
    {
        var source = new AlignmentCollection([Alignment(1, "A"), Alignment(2, "B")]);

        string json = JsonSerializer.Serialize(source, Options);
        AlignmentCollection? result = JsonSerializer.Deserialize<AlignmentCollection>(json, Options);

        Assert.NotNull(result);
        Assert.Equal(source.Items.Select(i => i.Id), result.Items.Select(i => i.Id));
        Assert.Equal(source.Items.Select(i => i.Name), result.Items.Select(i => i.Name));
    }

    [Fact]
    public void Json_ContainsNoAutodeskTypes()
    {
        string json = JsonSerializer.Serialize(PipeNetwork(4, "Storm"), Options);

        Assert.DoesNotContain("Autodesk", json);
        Assert.DoesNotContain("ObjectId", json);
    }
}
