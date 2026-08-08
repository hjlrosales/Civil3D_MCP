# Autodesk MCP Platform — Civil 3D Domain Layer (Phase 4A)

**Status:** Implemented
**Date:** 2026-08-07
**Scope:** Reusable Civil 3D domain services, repositories, DTOs and the patterns every future
tool must follow.

---

## 1. Purpose

The domain layer is the reusable, Autodesk-facing middle tier of the Bridge. Its job is to make
**tools thin orchestration classes** by moving every Civil 3D API interaction into a small number
of disciplined components:

- **Data sources** own the Autodesk API access (transactions, object enumeration, property reads).
- **Repositories** own query semantics (find by name, by id, exists, count) and the standard
  exception translation.
- **Services** own business results (a missing entity is a `null`, not an exception).
- **DTOs** are the only values that ever cross the layer — immutable records containing nothing
  but serializable types.

A tool never traverses Autodesk objects. It calls a service, gets DTOs, and returns a protocol
response.

```
MCP Server —► Bridge —► Tool —► Domain Service —► Repository —► Data Source —► Autodesk API
                                        └────────── DTOs only ───────────┘
```

---

## 2. Project Structure

```
src/domain/
├── Civil3D.Domain/                  (core — Autodesk-free contracts)
│   ├── Data/IAutodeskDocumentContext.cs
│   ├── Errors/DomainErrorCode.cs
│   ├── Errors/DomainException.cs
│   ├── Repositories/ReadOnlyRepositoryBase.cs
│   └── Services/DomainServiceBase.cs
├── Civil3D.Domain.Alignments/       (alignments discipline)
│   ├── Dtos/       AlignmentInfo, AlignmentKind, AlignmentCollection
│   ├── Data/       IAlignmentDataSource, AutodeskAlignmentDataSource
│   ├── Repositories/  IAlignmentRepository, AlignmentRepository
│   └── Services/      IAlignmentService, AlignmentService
├── Civil3D.Domain.Surfaces/         (surfaces)
├── Civil3D.Domain.Profiles/         (profiles)
├── Civil3D.Domain.Corridors/        (corridors)
├── Civil3D.Domain.Pipes/            (pipe networks, pipes, structures)
├── Civil3D.Domain.Cogo/             (COGO points)
└── Civil3D.Domain.Styles/           (styles)
```

Every discipline project follows the identical four-folder shape. `Civil3D.Domain` (core)
references no Autodesk assembly; the discipline projects reference the core plus the Autodesk
assemblies (`AcDbMgd`, `AeccDbMgd`, `AecBaseMgd`).

Dependency rule: a discipline may reference `Civil3D.Domain` and Autodesk assemblies — never
another discipline.

---

## 3. The Core Contracts (`Civil3D.Domain`)

### 3.1 Errors

`DomainErrorCode` is the single, stable error vocabulary of the domain layer:

| Code | Meaning |
|---|---|
| `NoActiveDocument` | No drawing is currently open in the host application. |
| `EntityNotFound` | A requested entity (alignment, surface, …) does not exist. |
| `TransactionFailed` | The read-only query against the Autodesk database failed. |
| `Internal` | An unexpected failure occurred inside the domain layer. |

`DomainException` carries one of these codes and, when present, the underlying Autodesk failure
as `InnerException`. Raw Autodesk exceptions never cross the domain boundary un-wrapped.

### 3.2 `IAutodeskDocumentContext` — the transaction seam

```csharp
public interface IAutodeskDocumentContext
{
    bool HasActiveDocument { get; }
    T ExecuteRead<T>(Func<object, T> read, CancellationToken cancellationToken = default);
}
```

- The delegate receives the active `Autodesk.AutoCAD.DatabaseServices.Database` as `object`,
  which keeps the contract Autodesk-free and testable.
- The **Bridge** registers `Civil3D.Bridge.Data.AutodeskDocumentContext`, which resolves
  `DocumentManager.MdiActiveDocument` (must run on the application context — the tool dispatcher
  guarantees this) and maps failures: no document → `NoActiveDocument`; anything else →
  `TransactionFailed`.
- **Tests** substitute an in-memory fake, so the whole domain layer is unit-testable without
  Civil 3D.

### 3.3 `ReadOnlyRepositoryBase`

Provides the two standard repository behaviors shared by all disciplines:

- `ExecuteRead<T>(Func<T>)` — standard exception translation: `DomainException` and
  `OperationCanceledException` pass through unchanged; any other failure becomes
  `DomainException(Internal)`.
- `RequireResult<T>(T?, string entityName)` — throws `EntityNotFound` for failed lookups.

### 3.4 `DomainServiceBase`

Provides the single shared service translation rule: `NotFoundAsNull` catches
`EntityNotFound` and returns `null`. Every other domain error propagates for the caller
(ultimately the tool) to map further.

---

## 4. Repository Pattern

**Contract** (`IAlignmentRepository` as the reference shape):

```csharp
public interface IAlignmentRepository
{
    AlignmentCollection GetAll();
    AlignmentInfo GetByName(string name);   // case-insensitive
    AlignmentInfo GetById(long id);
    bool Exists(string name);
    int Count();
}
```

**Rules:**

- Read-only. There is no edit, create or delete surface.
- Lookups either return the DTO or throw `EntityNotFound` (via `RequireResult`).
- Name lookups are case-insensitive; ids are stable `long`s derived from the Autodesk database
  handle.
- `GetAll()` reads once and returns a collection — no per-item re-queries.
- Each method is wrapped in `ExecuteRead` (from `ReadOnlyRepositoryBase`), so a single place
  defines the failure translation for the discipline.

The five-member shape is deliberately repeated per discipline: the strongly typed
interfaces are the DI contracts tools depend on, and each repository stays independently
testable. Only the genuinely shared machinery (exception translation, `RequireResult`) lives in
`ReadOnlyRepositoryBase`; per-discipline code is ~5 trivial LINQ lines.

The repository delegates the actual Autodesk access to the discipline's **data source**
(`IAlignmentDataSource`), which is the seam tests mock:

```csharp
public sealed class AlignmentRepository : ReadOnlyRepositoryBase, IAlignmentRepository
{
    private readonly IAlignmentDataSource _dataSource;
    public AlignmentRepository(IAlignmentDataSource dataSource) => _dataSource = dataSource;

    public AlignmentInfo GetByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return ExecuteRead(() => RequireResult(
            _dataSource.ReadAll().Items.FirstOrDefault(a =>
                string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)),
            "alignment"));
    }
    // ...
}
```

---

## 5. Service Pattern

**Contract** (`IAlignmentService`):

```csharp
public interface IAlignmentService
{
    AlignmentCollection GetAll();
    AlignmentInfo? GetByName(string name);   // null when not found
    AlignmentInfo? GetById(long id);         // null when not found
    bool Exists(string name);
    int Count();
}
```

**Rules:**

- Thin orchestration over the repository — no Autodesk knowledge, no data logic.
- A missing entity is a **`null` return value**, never an exception:
  `GetByName` → `NotFoundAsNull(() => _repository.GetByName(name))`.
- Other domain errors (`NoActiveDocument`, `Internal`) propagate to the tool, which maps them
  to protocol responses (`E_NO_ACTIVE_DOCUMENT`, …).

```csharp
public sealed class AlignmentService : DomainServiceBase, IAlignmentService
{
    private readonly IAlignmentRepository _repository;
    public AlignmentService(IAlignmentRepository repository)
        => _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public AlignmentInfo? GetByName(string name) => NotFoundAsNull(() => _repository.GetByName(name));
    // ...
}
```

---

## 6. DTO Pattern

**Rules:**

- `sealed record`, immutable (`init`-only setters or positional parameters).
- Only serializable types: `long`/`long?` ids, `string`/`string?`, `double`, `bool`, small enums,
  `IReadOnlyList<T>` for children.
- **No Autodesk types.** References between entities are ids, never `ObjectId`/objects.
  `SurfaceInfo` references its style by `long? StyleId`; `PipeNetworkInfo` embeds
  `PipeInfo`/`StructureInfo` by value.
- No anonymous objects, no `dynamic`, no dictionaries unless a tool genuinely needs them.
- Collection DTOs are thin immutable wrappers (`SurfaceCollection(IReadOnlyList<SurfaceInfo> Items)`
  with `Count` and `IsEmpty`), giving repositories a stable return type.
- `Description`-style optional strings use `null` (not `string.Empty`) when absent.

Example:

```csharp
public sealed record AlignmentInfo
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public AlignmentKind Kind { get; init; }
    public double Length { get; init; }
    public double StartingStation { get; init; }
    public double EndingStation { get; init; }
    public long? SiteId { get; init; }
    public long? StyleId { get; init; }
}
```

DTOs round-trip through `System.Text.Json` (verified by `DtoSerializationTests`) and are the
payloads tools return in protocol responses.

---

## 7. Data Source & Transaction Pattern

Each discipline has exactly one Autodesk implementation (`Autodesk*DataSource`) behind a small
interface (`I*DataSource`), which is the mock seam:

```csharp
public interface IAlignmentDataSource
{
    AlignmentCollection ReadAll(CancellationToken cancellationToken = default);
}
```

The implementation owns all Autodesk access:

```csharp
public sealed class AutodeskAlignmentDataSource : IAlignmentDataSource
{
    private readonly IAutodeskDocumentContext _context;
    public AutodeskAlignmentDataSource(IAutodeskDocumentContext context)
        => _context = context ?? throw new ArgumentNullException(nameof(context));

    public AlignmentCollection ReadAll(CancellationToken cancellationToken = default)
        => _context.ExecuteRead(
            database => ReadCore((Database)database, cancellationToken),
            cancellationToken);

    private static AlignmentCollection ReadCore(Database database, CancellationToken cancellationToken)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        CivilDocument civilDocument = CivilDocument.GetCivilDocument(database);
        ObjectIdCollection ids = civilDocument.GetAlignmentIds();

        var items = new List<AlignmentInfo>(ids.Count);
        foreach (ObjectId id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var alignment = (Alignment)transaction.GetObject(id, OpenMode.ForRead);
            items.Add(Map(alignment));
        }
        return new AlignmentCollection(items);
    }
    // Map(...) copies properties into the DTO — one read, one map, never retained.
}
```

**Transaction rules (read-only):**

- Open **one** read-only transaction (`StartTransaction()`), read, dispose. No `Commit` needed —
  nothing was edited; `using` guarantees disposal.
- Read each object **once** (`OpenMode.ForRead`), map it immediately, keep only the DTO.
- Never retain Autodesk objects past the transaction.
- Honor `cancellationToken` inside the enumeration loop.
- No geometry calculations, no expensive scans, no repeated traversals — the tool completes
  quickly by construction.

---

## 8. Error Handling — The Complete Chain

| Layer | Behavior |
|---|---|
| Data source | `IAutodeskDocumentContext.ExecuteRead` maps no-document → `NoActiveDocument`, Autodesk failures → `TransactionFailed`. |
| Repository | `ExecuteRead` passes `DomainException`/cancellation through, maps anything else → `Internal`; failed lookups → `EntityNotFound`. |
| Service | `NotFoundAsNull` turns `EntityNotFound` into `null`. |
| Tool | Maps `DomainException.Code` to the protocol `errorCode` (`E_NO_ACTIVE_DOCUMENT`, `E_OBJECT_NOT_FOUND`, `E_TRANSACTION_FAILED`, `E_INTERNAL`) and returns the standard response envelope. |

`TransactionFailed` vs `Internal`: failures raised *through* `IAutodeskDocumentContext.ExecuteRead`
(no document, Autodesk exceptions inside the read) become `TransactionFailed`; failures raised by
the repository body itself or the data source outside the context (including Autodesk exceptions
thrown after the context returns, e.g. inside a `Map`) become `Internal`. Both map to protocol
errors, so the distinction is diagnostic.

Raw Autodesk exceptions never reach the pipe.

---

## 9. Dependency Injection

`Civil3D.Bridge.DependencyInjection.BridgeServiceCollectionExtensions.AddCivil3DBridge` registers
the whole layer — one `IAutodeskDocumentContext`, then for each discipline its data source,
repository and service, all as singletons:

```csharp
services.AddSingleton<IAutodeskDocumentContext, AutodeskDocumentContext>();

services.AddSingleton<IAlignmentDataSource, AutodeskAlignmentDataSource>();
services.AddSingleton<IAlignmentRepository, AlignmentRepository>();
services.AddSingleton<IAlignmentService, AlignmentService>();
// ... and so on for Surfaces, Profiles, Corridors, Pipes, Cogo, Styles
```

No service locator, no static state. A tool takes only the `I*Service` interfaces it needs via
constructor injection.

---

## 10. How to Add a New Discipline

1. Create `src/domain/Civil3D.Domain.<Discipline>/` with the four folders, copying the Alignments
   discipline as the reference. Add the core + Autodesk references (`AcDbMgd`, `AeccDbMgd`,
   `AecBaseMgd`) to the csproj.
2. **DTOs** — `*Info` record(s), a small `*Kind` enum if the Autodesk type needs classifying, and
   a `*Collection` wrapper.
3. **Data source** — `I*DataSource` (the seam) + `Autodesk*DataSource` (one read-only transaction
   through `IAutodeskDocumentContext`).
4. **Repository** — `I*Repository` + `*Repository` extending `ReadOnlyRepositoryBase`, delegating
   to the data source.
5. **Service** — `I*Service` + `*Service` extending `DomainServiceBase`, translating
   `EntityNotFound` to `null`.
6. Register the three in `AddCivil3DBridge` and add the project to `AutodeskMcp.slnx`.
7. Add tests (Section 12) and one section to this document.

## 11. How Future Tools Use the Services

A Phase 4B tool (e.g. `list_alignments`) depends on `IAlignmentService` and nothing else from the
domain layer:

1. Inject the service(s) via constructor.
2. Call the service (runs entirely on the application context — the dispatcher guarantees it).
3. Translate `DomainException` codes to protocol errors; return DTOs as the response `data`.
4. Log execution time, drawing name, correlation/session ids, result size, errors — the tool
   never touches `Autodesk.*` namespaces.

---

## 12. Testing Strategy (`tests/Civil3D.Domain.Tests`)

The domain layer is tested **without Civil 3D** by mocking at two seams:

| Test class | Mocks | Verifies |
|---|---|---|
| `*RepositoryTests` | in-memory data sources (`Fake*DataSource` in `TestDoubles`) | DTO mapping, case-insensitive lookups, `EntityNotFound`, `NoActiveDocument` pass-through, unexpected failures → `Internal` |
| `*ServiceTests` | repositories (via fakes) | `EntityNotFound` → `null`, other errors propagate, `GetAll`/`Exists`/`Count` pass through |
| `DtoSerializationTests` | — | `System.Text.Json` round-trips for every DTO/enum/collection; JSON contains no Autodesk types |
| `DomainCompositionTests` | full fake chain | constructor-injection wiring data source → repository → service for all seven disciplines |

The bridge-level `AddCivil3DBridge` registration is exercised by the `Civil3D.Tools.Drawing`
discovery tests.
