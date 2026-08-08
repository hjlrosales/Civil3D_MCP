using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace Civil3D.Domain.Query;

/// <summary>
/// The single reusable implementation of the read-only query model: filtering (closed operator
/// set), multi-key sorting, pagination, field validation and free-text search matching. Every
/// repository <c>Query</c> method and the <c>search_objects</c> tool delegate here, so filtering
/// logic is never duplicated. Property accessors are compiled once per (type, field) and cached,
/// so applying a query does not reflect per item.
/// </summary>
public static class QueryEngine
{
    private sealed record Accessor(Func<object, object?> Getter, Type PropertyType);

    private static readonly ConcurrentDictionary<(Type Type, string Field), Accessor?> Accessors = new();

    /// <summary>
    /// Applies a request to a materialized list in a single pass: validate, filter (AND), sort
    /// (stable, first key primary), then page. Never enumerates the input more than once per stage.
    /// </summary>
    /// <typeparam name="T">The item DTO type.</typeparam>
    /// <param name="items">The materialized items (already read once from Autodesk).</param>
    /// <param name="request">The query request; null means no filtering and default paging.</param>
    public static PageResult<T> Apply<T>(IReadOnlyList<T> items, QueryRequest? request)
    {
        request ??= new QueryRequest();
        PageRequest page = Normalize(request.Page);
        Validate(request, typeof(T));

        var timer = Stopwatch.StartNew();

        IReadOnlyList<T> filtered = Filter(items, request.Filters);
        IReadOnlyList<T> sorted = Sort(filtered, request.Sorts);

        int total = sorted.Count;
        int skip = Math.Max(0, (page.Page - 1) * page.PageSize);
        T[] paged = sorted.Skip(skip).Take(page.PageSize).ToArray();

        timer.Stop();
        return new PageResult<T>(paged, page.Page, page.PageSize, total)
        {
            Statistics = new QueryStatistics(total, paged.Length, timer.ElapsedMilliseconds),
        };
    }

    /// <summary>Filters the items by every filter (AND semantics).</summary>
    public static IReadOnlyList<T> Filter<T>(IReadOnlyList<T> items, IReadOnlyList<FilterExpression>? filters)
    {
        if (filters is null || filters.Count == 0)
        {
            return items;
        }

        var result = new List<T>(items.Count);
        foreach (T item in items)
        {
            bool keep = true;
            foreach (FilterExpression filter in filters)
            {
                if (!Matches(item, filter))
                {
                    keep = false;
                    break;
                }
            }

            if (keep)
            {
                result.Add(item);
            }
        }

        return result;
    }

    /// <summary>
    /// Sorts the items by the given keys. The first expression is the primary key; ties keep the
    /// original order (stable, like SQL ORDER BY).
    /// </summary>
    public static IReadOnlyList<T> Sort<T>(IReadOnlyList<T> items, IReadOnlyList<SortExpression>? sorts)
    {
        if (sorts is null || sorts.Count == 0)
        {
            return items;
        }

        IOrderedEnumerable<T> ordered = sorts[0].Direction == SortDirection.Ascending
            ? items.OrderBy(i => ReadValue(i, sorts[0].Field))
            : items.OrderByDescending(i => ReadValue(i, sorts[0].Field));

        for (int i = 1; i < sorts.Count; i++)
        {
            SortExpression sort = sorts[i];
            ordered = sort.Direction == SortDirection.Ascending
                ? ordered.ThenBy(i2 => ReadValue(i2, sort.Field))
                : ordered.ThenByDescending(i2 => ReadValue(i2, sort.Field));
        }

        return ordered.ToArray();
    }

    /// <summary>Checks a single filter against one item.</summary>
    public static bool Matches<T>(T item, FilterExpression filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (string.IsNullOrWhiteSpace(filter.Field))
        {
            throw new QueryException("A filter is missing its 'field'.");
        }

        Accessor accessor = RequireAccessor(typeof(T), filter.Field);
        object? raw = accessor.Getter(item!);

        switch (filter.Operator)
        {
            case FilterOperator.IsNull:
                return raw is null;
            case FilterOperator.IsNotNull:
                return raw is not null;
            case FilterOperator.Equals:
                return ValueEquals(raw, filter.Value, accessor.PropertyType);
            case FilterOperator.NotEquals:
                return !ValueEquals(raw, filter.Value, accessor.PropertyType);
            case FilterOperator.Contains:
                return ToStringValue(raw).Contains(ToFilterString(filter.Value), StringComparison.OrdinalIgnoreCase);
            case FilterOperator.StartsWith:
                return ToStringValue(raw).StartsWith(ToFilterString(filter.Value), StringComparison.OrdinalIgnoreCase);
            case FilterOperator.EndsWith:
                return ToStringValue(raw).EndsWith(ToFilterString(filter.Value), StringComparison.OrdinalIgnoreCase);
            case FilterOperator.GreaterThan:
                return CompareValues(raw, filter.Value, accessor.PropertyType) > 0;
            case FilterOperator.GreaterThanOrEqual:
                return CompareValues(raw, filter.Value, accessor.PropertyType) >= 0;
            case FilterOperator.LessThan:
                return CompareValues(raw, filter.Value, accessor.PropertyType) < 0;
            case FilterOperator.LessThanOrEqual:
                return CompareValues(raw, filter.Value, accessor.PropertyType) <= 0;
            case FilterOperator.In:
                return filter.Values is not null && filter.Values.Any(v => ValueEquals(raw, v, accessor.PropertyType));
            case FilterOperator.NotIn:
                return filter.Values is null || !filter.Values.Any(v => ValueEquals(raw, v, accessor.PropertyType));
            default:
                throw new QueryException($"Unsupported operator '{filter.Operator}'.");
        }
    }

    /// <summary>
    /// Free-text matching used by <c>search_objects</c>: true when any of the named fields contains
    /// the query (case-insensitive). Unknown fields throw <see cref="QueryException"/>.
    /// </summary>
    public static bool MatchesSearch<T>(T item, string query, params string[] fields)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        foreach (string field in fields)
        {
            Accessor accessor = RequireAccessor(typeof(T), field);
            if (accessor.Getter(item!) is { } value
                && ToStringValue(value).Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates a request against a DTO type: every filter/sort field and every requested field
    /// must exist, and <c>In</c>/<c>NotIn</c> must carry operands. Throws <see cref="QueryException"/>.
    /// </summary>
    public static void Validate(QueryRequest request, Type type)
    {
        if (request.Filters is not null)
        {
            foreach (FilterExpression filter in request.Filters)
            {
                RequireAccessor(type, filter.Field);
                if (filter.Operator is FilterOperator.In or FilterOperator.NotIn && filter.Values is null)
                {
                    throw new QueryException($"Operator '{filter.Operator}' on field '{filter.Field}' requires a non-null 'values' array.");
                }
            }
        }

        if (request.Sorts is not null)
        {
            foreach (SortExpression sort in request.Sorts)
            {
                RequireAccessor(type, sort.Field);
            }
        }

        if (request.Fields?.Fields is { Count: > 0 } fields)
        {
            foreach (string field in fields)
            {
                RequireAccessor(type, field);
            }
        }
    }

    /// <summary>Returns the normalized page request (page ≥ 1, page size clamped to 1..500).</summary>
    public static PageRequest Normalize(PageRequest? page)
    {
        page ??= new PageRequest();
        return new PageRequest
        {
            Page = Math.Max(1, page.Page),
            PageSize = Math.Clamp(page.PageSize, 1, PageRequest.MaxPageSize),
        };
    }

    private static bool ValueEquals(object? raw, object? filterValue, Type propertyType)
    {
        if (raw is null)
        {
            return filterValue is null;
        }

        if (filterValue is null)
        {
            return false;
        }

        object? converted = ConvertToType(Normalize(filterValue), raw.GetType() is { } t && t != typeof(object) ? t : propertyType);
        if (converted is null)
        {
            return false;
        }

        if (raw is string left && converted is string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        return raw.Equals(converted);
    }

    private static int CompareValues(object? raw, object? filterValue, Type propertyType)
    {
        if (raw is null)
        {
            return filterValue is null ? 0 : -1;
        }

        object? converted = ConvertToType(Normalize(filterValue), raw.GetType());
        if (converted is null)
        {
            return 1;
        }

        if (raw is IComparable comparable)
        {
            return comparable.CompareTo(converted);
        }

        return string.CompareOrdinal(ToStringValue(raw), ToStringValue(converted));
    }

    private static object? ReadValue<T>(T item, string field)
        => RequireAccessor(typeof(T), field).Getter(item!);

    private static Accessor RequireAccessor(Type type, string field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            throw new QueryException("A query field must not be empty.");
        }

        return Accessors.GetOrAdd((type, field), key =>
        {
            PropertyInfo? property = key.Type.GetProperty(
                key.Field,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is null)
            {
                throw new QueryException($"Unknown field '{key.Field}' on type '{key.Type.Name}'.");
            }

            ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
            UnaryExpression cast = Expression.Convert(instance, key.Type);
            MemberExpression access = Expression.Property(cast, property);
            UnaryExpression boxed = Expression.Convert(access, typeof(object));
            Func<object, object?> getter = Expression.Lambda<Func<object, object?>>(boxed, instance).Compile();
            return new Accessor(getter, property.PropertyType);
        })!;
    }

    private static object? Normalize(object? value)
        => value is JsonElement element ? NormalizeJson(element) : value;

    private static object? NormalizeJson(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => element.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => element.TryGetInt64(out long integer) ? integer : element.GetDouble(),
        JsonValueKind.Array => element.EnumerateArray().Select(NormalizeJson).ToArray(),
        _ => element.GetRawText(),
    };

    private static object? ConvertToType(object? value, Type targetType)
    {
        if (value is null)
        {
            return null;
        }

        Type target = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (target.IsInstanceOfType(value))
        {
            return value;
        }

        if (target.IsEnum)
        {
            return value is string text
                ? Enum.Parse(target, text, ignoreCase: true)
                : Enum.ToObject(target, Convert.ChangeType(value, Enum.GetUnderlyingType(target), CultureInfo.InvariantCulture));
        }

        if (target == typeof(string))
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        if (value is IConvertible convertible)
        {
            return Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }

        return value;
    }

    private static string ToStringValue(object? value) => value?.ToString() ?? string.Empty;

    private static string ToFilterString(object? value) => Normalize(value)?.ToString() ?? string.Empty;
}
