# Autodesk MCP Platform — Read-Only Query Framework (Phase 4B)

**Status:** Implemented
**Date:** 2026-08-07
**Scope:** The reusable query model (`Civil3D.Domain.Query`), the repository/service `Query`
surface, the 15 read-only MCP tools (`Civil3D.Tools.Query`), and the wire syntax every future
read-only tool reuses.

---

## 1. Purpose

Phase 4B makes read-only tools **thin orchestration classes** over one generic query model.
Instead of dozens of specialized methods (e.g. `findByLayer`, `findByTypeAndGreaterThan`), every
read-only tool accepts a `QueryRequest` and the shared `QueryEngine` applies it:

```text
MCP client  →  list_alignments(QueryRequest)  →  IAlignmentService.Query  →  QueryEngine
                                                                              │
                                    filters → sorts → page → field selection   │
                                                                              ▼
                                              PageResult<AlignmentInfo>
```

Filtering, sorting, pagination, field selection and search are implemented **exactly once** in
`QueryEngine` and reused by every repository and tool. No query logic is duplicated.

---

## 2. Request Shape (QueryRequest)

Every `list_*` tool takes a single JSON object with all members optional:

```json
{
  "filters": [
    { "field": "Name", "operator": 2, "value": "main" },
    { "field": "Length", "operator": 7, "value": 500 },
    { "field": "StyleId", "operator": 11 }
  ],
  "sorts": [
    { "field": "Name", "direction": 1 },
    { "field": "Id", "direction": 0 }
  ],
  "page": { "page": 1, "pageSize": 50 },
  "fields": { "fields": ["Id", "Name"] }
}
```

| Member | Type | Meaning |
|---|---|---|
| `filters` | `FilterExpression[]` | AND-ed comparisons over DTO properties (see §3). |
| `sorts` | `SortExpression[]` | Sort keys, first is primary (see §4). |
| `page` | `PageRequest` | 1-based page, size clamped to 1–500 (see §5). |
| `fields` | `FieldSelection` | Optional subset of DTO properties to return (see §6). |

An empty request returns the first 50 items in document order — the same contract as a plain
`GetAll()`.

---

## 3. Filtering Syntax

A filter is `{ "field", "operator", "value" | "values" }`. `field` is the case-insensitive
property name on the queried DTO. The operator is the **numeric value** of `FilterOperator`
(plain enum, no string aliases on the wire):

| Operator | Numeric | JSON shape | Semantics |
|---|---|---|---|
| `Equals` | 0 | `value` | `==` (case-insensitive for strings) |
| `NotEquals` | 1 | `value` | `!=` |
| `Contains` | 2 | `value` | string contains |
| `StartsWith` | 3 | `value` | string starts with |
| `EndsWith` | 4 | `value` | string ends with |
| `GreaterThan` | 5 | `value` | `>` |
| `GreaterThanOrEqual` | 6 | `value` | `>=` |
| `LessThan` | 7 | `value` | `<` |
| `LessThanOrEqual` | 8 | `value` | `<=` |
| `In` | 9 | `values` | membership (case-insensitive for strings) |
| `NotIn` | 10 | `values` | non-membership |
| `IsNull` | 11 | — | property is null |
| `IsNotNull` | 12 | — | property is not null |

**Rules:**

- Filters are AND-ed. There is no OR (keep requests predictable).
- Values arrive as JSON primitives or `JsonElement`; the engine normalizes them to the property
  type (numbers, booleans, enums, strings).
- Comparison operators against a null property never match (SQL-like).
- An unknown field name, an operator applied to an unsupported property type, or a missing
  operand raises `QueryException`, which the tool layer maps to `E_INVALID_PARAMETERS`.

Example — alignments whose name contains "main" and length ≥ 500:

```json
{
  "filters": [
    { "field": "Name", "operator": 2, "value": "main" },
    { "field": "Length", "operator": 6, "value": 500 }
  ]
}
```

---

## 4. Sorting Syntax

A sort is `{ "field", "direction" }`. `direction` is the numeric value of `SortDirection`:
`0` = Ascending, `1` = Descending. Multiple sorts are applied in order — the first key is
primary; ties fall through to the next.

```json
{
  "sorts": [
    { "field": "Name", "direction": 1 },
    { "field": "Id", "direction": 0 }
  ]
}
```

Sorting is stable: equal items keep their relative document order. Null values sort before
non-null values on ascending keys.

---

## 5. Pagination

`page` is `{ "page", "pageSize" }`. Pages are 1-based; `pageSize` is clamped to 1–500
(`PageRequest.MaxPageSize`) and defaults to 50. Every `list_*` tool returns a `PageResult<T>`:

```json
{
  "items": [ { "id": 1, "name": "Mainline", "...": "..." } ],
  "page": 1,
  "pageSize": 50,
  "totalCount": 1,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false,
  "statistics": { "totalCount": 1, "matchedCount": 1, "executionTimeMs": 0 }
}
```

- `totalCount` / `totalPages` are computed **before** paging (the full match set), so clients can
  render pager controls.
- `statistics.matchedCount` is the count on the returned page; `totalCount` is the filtered total.

---

## 6. Field Selection

`fields` is `{ "fields": ["Id", "Name"] }`. When provided, only the listed DTO properties are
included in the returned objects; the engine compiles property accessors once per (type, field)
and caches them. Unknown field names raise `QueryException` → `E_INVALID_PARAMETERS`.

---

## 7. Search (search_objects)

`search_objects` takes a `SearchRequest`:

```json
{
  "query": "main",
  "kinds": ["alignment", "corridor"],
  "page": { "page": 1, "pageSize": 50 }
}
```

- `query` is required (empty → `E_INVALID_PARAMETERS`). Matching is case-insensitive **contains**
  over `Name`, `Description` and, when the kind carries a style reference, the resolved style name.
- `kinds` restricts the entity kinds searched; the default is all seven:
  `alignment`, `surface`, `profile`, `corridor`, `pipe_network`, `cogo_point`, `style`.
  Unknown kinds → `E_INVALID_PARAMETERS`.
- Results are typed `ObjectReference` hits (`kind`, `id`, `name`, `description`, `layer` (always
  null until the domain exposes layers), `styleName`). No geometry searching.

---

## 8. The 15 Read-Only Tools

| Tool | Input | Output |
|---|---|---|
| `list_alignments` | `QueryRequest` | `PageResult<AlignmentInfo>` |
| `list_profiles` | `QueryRequest` | `PageResult<ProfileInfo>` |
| `list_surfaces` | `QueryRequest` | `PageResult<SurfaceInfo>` |
| `list_corridors` | `QueryRequest` | `PageResult<CorridorInfo>` |
| `list_pipe_networks` | `QueryRequest` | `PageResult<PipeNetworkInfo>` |
| `list_cogo_points` | `QueryRequest` | `PageResult<CogoPointInfo>` |
| `list_styles` | `QueryRequest` | `PageResult<StyleInfo>` |
| `get_alignment` / `get_profile` / `get_surface` / `get_corridor` / `get_pipe_network` / `get_cogo_point` / `get_style` | `IdRequest` | `TInfo` |
| `search_objects` | `SearchRequest` | `SearchResult<ObjectReference>` |

`get_*` lookups use `repository.GetById(id)` and return `E_OBJECT_NOT_FOUND` when the id does not
match. All 15 tools are read-only, `ToolPermission.ReadOnly`, `ToolRisk.Low`.

---

## 9. How Future Tools Should Use QueryRequest

1. **Repository** — add `Query(QueryRequest)` to the interface and implement it as one read of
   the data source plus `QueryEngine.Apply(items, request)` (see the 7 discipline repositories).
2. **Service** — pass the request through unchanged. Services contain no query logic.
3. **Tool** — derive from `QueryToolBase<TIn, TOut>` and call the service inside `RunQuery`
   (the base maps `QueryException` → `E_INVALID_PARAMETERS` and `DomainException` codes to their
   protocol codes).

```csharp
[McpTool("list_alignments", "List Alignments", "...", Category = ToolCategory.Alignments,
    Permission = ToolPermission.ReadOnly, Risk = ToolRisk.Low)]
public sealed class ListAlignmentsTool : QueryToolBase<QueryRequest, PageResult<AlignmentInfo>>
{
    private readonly IAlignmentService _alignments;

    public ListAlignmentsTool(ICivil3DSession session, IAlignmentService alignments)
        : base(session) => _alignments = alignments;

    protected override Task<PageResult<AlignmentInfo>> ExecuteToolCoreAsync(
        QueryRequest input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireActiveDrawing(context);
        return Task.FromResult(RunQuery(context, () => _alignments.Query(input)));
    }
}
```

---

## 10. Performance & Error Contract

- Repositories open **one** read-only transaction via the data source, materialize DTOs once, and
  delegate filtering/sorting/paging to `QueryEngine` — never multiple enumerations, never
  repeated Autodesk calls, never loading unrelated objects.
- `QueryEngine` compiles and caches property accessors, so applying a query does not reflect per
  item.
- Errors: no active document → `E_NO_ACTIVE_DOCUMENT`; unknown tool or missing id →
  `E_OBJECT_NOT_FOUND`; malformed query (unknown field, bad operator) → `E_INVALID_PARAMETERS`;
  transaction failure → `E_TRANSACTION_FAILED`; anything else → `E_INTERNAL`. Raw Autodesk
  exceptions never cross the pipe.
