using Civil3D.Domain.Alignments.Data;
using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Cogo.Data;
using Civil3D.Domain.Cogo.Dtos;
using Civil3D.Domain.Corridors.Data;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Data;
using Civil3D.Domain.Errors;
using Civil3D.Domain.Pipes.Data;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Profiles.Data;
using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Domain.Styles.Data;
using Civil3D.Domain.Styles.Dtos;
using Civil3D.Domain.Surfaces.Data;
using Civil3D.Domain.Surfaces.Dtos;

namespace Civil3D.Domain.Tests;

/// <summary>
/// Shared test doubles: in-memory data sources standing in for the Autodesk implementations,
/// plus canned samples. Mocking at the data-source seam tests repositories without Autodesk.
/// </summary>
internal static class TestDoubles
{
    /// <summary>An in-memory alignment data source.</summary>
    internal sealed class FakeAlignmentDataSource : IAlignmentDataSource
    {
        private readonly Func<CancellationToken, AlignmentCollection> _factory;

        public FakeAlignmentDataSource(AlignmentCollection collection)
            : this(_ => collection)
        {
        }

        public FakeAlignmentDataSource(Func<CancellationToken, AlignmentCollection> factory)
            => _factory = factory;

        public AlignmentCollection ReadAll(CancellationToken cancellationToken = default)
            => _factory(cancellationToken);
    }

    /// <summary>An in-memory surface data source.</summary>
    internal sealed class FakeSurfaceDataSource : ISurfaceDataSource
    {
        private readonly Func<CancellationToken, SurfaceCollection> _factory;

        public FakeSurfaceDataSource(SurfaceCollection collection)
            : this(_ => collection)
        {
        }

        public FakeSurfaceDataSource(Func<CancellationToken, SurfaceCollection> factory)
            => _factory = factory;

        public SurfaceCollection ReadAll(CancellationToken cancellationToken = default)
            => _factory(cancellationToken);
    }

    /// <summary>An in-memory profile data source.</summary>
    internal sealed class FakeProfileDataSource : IProfileDataSource
    {
        private readonly Func<CancellationToken, ProfileCollection> _factory;

        public FakeProfileDataSource(ProfileCollection collection)
            : this(_ => collection)
        {
        }

        public FakeProfileDataSource(Func<CancellationToken, ProfileCollection> factory)
            => _factory = factory;

        public ProfileCollection ReadAll(CancellationToken cancellationToken = default)
            => _factory(cancellationToken);
    }

    /// <summary>An in-memory corridor data source.</summary>
    internal sealed class FakeCorridorDataSource : ICorridorDataSource
    {
        private readonly Func<CancellationToken, CorridorCollection> _factory;

        public FakeCorridorDataSource(CorridorCollection collection)
            : this(_ => collection)
        {
        }

        public FakeCorridorDataSource(Func<CancellationToken, CorridorCollection> factory)
            => _factory = factory;

        public CorridorCollection ReadAll(CancellationToken cancellationToken = default)
            => _factory(cancellationToken);
    }

    /// <summary>An in-memory pipe data source.</summary>
    internal sealed class FakePipeDataSource : IPipeDataSource
    {
        private readonly Func<CancellationToken, PipeNetworkCollection> _factory;

        public FakePipeDataSource(PipeNetworkCollection collection)
            : this(_ => collection)
        {
        }

        public FakePipeDataSource(Func<CancellationToken, PipeNetworkCollection> factory)
            => _factory = factory;

        public PipeNetworkCollection ReadAll(CancellationToken cancellationToken = default)
            => _factory(cancellationToken);
    }

    /// <summary>An in-memory COGO data source.</summary>
    internal sealed class FakeCogoDataSource : ICogoDataSource
    {
        private readonly Func<CancellationToken, CogoPointCollection> _factory;

        public FakeCogoDataSource(CogoPointCollection collection)
            : this(_ => collection)
        {
        }

        public FakeCogoDataSource(Func<CancellationToken, CogoPointCollection> factory)
            => _factory = factory;

        public CogoPointCollection ReadAll(CancellationToken cancellationToken = default)
            => _factory(cancellationToken);
    }

    /// <summary>An in-memory style data source.</summary>
    internal sealed class FakeStyleDataSource : IStyleDataSource
    {
        private readonly Func<CancellationToken, StyleCollection> _factory;

        public FakeStyleDataSource(StyleCollection collection)
            : this(_ => collection)
        {
        }

        public FakeStyleDataSource(Func<CancellationToken, StyleCollection> factory)
            => _factory = factory;

        public StyleCollection ReadAll(CancellationToken cancellationToken = default)
            => _factory(cancellationToken);
    }

    /// <summary>A document context fake with a canned database payload and no-document toggle.</summary>
    internal sealed class FakeDocumentContext : IAutodeskDocumentContext
    {
        private readonly bool _hasDocument;
        private readonly object? _database;

        public FakeDocumentContext(bool hasDocument = true, object? database = null)
        {
            _hasDocument = hasDocument;
            _database = database;
        }

        public bool HasActiveDocument => _hasDocument;

        public T ExecuteRead<T>(Func<object, T> read, CancellationToken cancellationToken = default)
        {
            if (!_hasDocument)
            {
                throw new DomainException(DomainErrorCode.NoActiveDocument, "No active document.");
            }

            // Mirrors the real AutodeskDocumentContext mapping so tests of the real data sources
            // observe the same error translation.
            try
            {
                return read(_database!);
            }
            catch (DomainException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DomainException(
                    DomainErrorCode.TransactionFailed,
                    "The read-only query against the drawing database failed.",
                    ex);
            }
        }

        public T ExecuteWrite<T>(Func<object, T> write, CancellationToken cancellationToken = default)
        {
            if (!_hasDocument)
            {
                throw new DomainException(DomainErrorCode.NoActiveDocument, "No active document.");
            }

            try
            {
                return write(_database!);
            }
            catch (DomainException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DomainException(
                    DomainErrorCode.TransactionFailed,
                    "The write operation against the drawing database failed.",
                    ex);
            }
        }
    }

    internal static AlignmentInfo Alignment(long id, string name) => new()
    {
        Id = id,
        Name = name,
        Description = $"Description of {name}",
        Kind = AlignmentKind.Centerline,
        Length = 1_000,
        StartingStation = 0,
        EndingStation = 1_000,
        SiteId = 7,
        StyleId = 11,
    };

    internal static SurfaceInfo Surface(long id, string name) => new()
    {
        Id = id,
        Name = name,
        Kind = SurfaceKind.Tin,
        PointCount = 500,
        MinimumElevation = 10.0,
        MaximumElevation = 40.0,
        MeanElevation = 25.0,
    };

    internal static ProfileInfo Profile(long id, string name, long alignmentId) => new()
    {
        Id = id,
        Name = name,
        AlignmentId = alignmentId,
        TypeName = "Layout",
        Length = 1_000,
        StartingStation = 0,
        EndingStation = 1_000,
    };

    internal static CorridorInfo Corridor(long id, string name, long alignmentId) => new()
    {
        Id = id,
        Name = name,
        AlignmentId = alignmentId,
        BaselineCount = 1,
        CorridorSurfaceCount = 2,
    };

    internal static PipeNetworkInfo PipeNetwork(long id, string name) => new()
    {
        Id = id,
        Name = name,
        PartsListName = "Standard",
        Pipes = new[] { new PipeInfo { Id = 101, Name = "P-1", NetworkId = id, StartStation = 0, EndStation = 100 } },
        Structures = new[] { new StructureInfo { Id = 201, Name = "S-1", NetworkId = id, Easting = 1, Northing = 2 } },
    };

    internal static CogoPointInfo CogoPoint(long id, uint pointNumber) => new()
    {
        Id = id,
        PointNumber = pointNumber,
        Easting = 100.5,
        Northing = 200.5,
        Elevation = 12.5,
        FullDescription = "GPS point",
    };

    internal static StyleInfo Style(long id, string name, StyleKind kind) => new()
    {
        Id = id,
        Name = name,
        Kind = kind,
    };

    internal static AlignmentCollection AlignmentCollection(params AlignmentInfo[] items) => new(items);

    internal static SurfaceCollection SurfaceCollection(params SurfaceInfo[] items) => new(items);

    internal static ProfileCollection ProfileCollection(params ProfileInfo[] items) => new(items);

    internal static CorridorCollection CorridorCollection(params CorridorInfo[] items) => new(items);

    internal static PipeNetworkCollection PipeNetworkCollection(params PipeNetworkInfo[] items) => new(items);

    internal static CogoPointCollection CogoPointCollection(params CogoPointInfo[] items) => new(items);

    internal static StyleCollection StyleCollection(params StyleInfo[] items) => new(items);
}
