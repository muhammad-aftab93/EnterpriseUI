using System.Collections;

namespace EnterpriseUI.Grids;

/// <summary>Reference-identity comparer usable on every supported target framework.</summary>
internal sealed class GridReferenceComparer : IEqualityComparer<object?>, IEqualityComparer
{
    public static readonly GridReferenceComparer Instance = new();

    private GridReferenceComparer() { }

    public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

    public int GetHashCode(object? obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj!);
}
