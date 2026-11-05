using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace EnterpriseUI.Data;

/// <summary>Cached, null-safe access to (nested) public instance properties by dotted path, ignoring case.</summary>
public static class PropertyPath
{
    private static readonly ConcurrentDictionary<(Type Type, string Name), Func<object, object?>?> SegmentGetters = new();
    private static readonly ConcurrentDictionary<(Type Type, string Name), PropertyInfo?> Properties = new();

    public static PropertyInfo? FindProperty(Type type, string name)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return Properties.GetOrAdd((type, name), static key =>
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            var exact = key.Type.GetProperty(key.Name, flags | BindingFlags.DeclaredOnly) ?? SafeGet(key.Type, key.Name, flags);
            if (exact != null)
            {
                return exact;
            }

            return key.Type.GetProperties(flags)
                .FirstOrDefault(p => p.GetIndexParameters().Length == 0 && string.Equals(p.Name, key.Name, StringComparison.OrdinalIgnoreCase));
        });
    }

    private static PropertyInfo? SafeGet(Type type, string name, BindingFlags flags)
    {
        try
        {
            return type.GetProperty(name, flags);
        }
        catch (AmbiguousMatchException)
        {
            return type.GetProperties(flags).FirstOrDefault(p => p.Name == name && p.GetIndexParameters().Length == 0);
        }
    }

    /// <summary>Resolves the CLR type a dotted path ends at, or null when any segment is unknown.</summary>
    public static Type? GetPathType(Type rootType, string path)
    {
        var current = rootType;
        foreach (var segment in path.Split('.'))
        {
            var property = FindProperty(current, segment);
            if (property == null)
            {
                return null;
            }

            current = property.PropertyType;
        }

        return current;
    }

    /// <summary>Reads a dotted path, returning null when any intermediate value is null or a segment is unknown.</summary>
    public static object? GetValue(object? instance, string path)
    {
        if (instance == null || string.IsNullOrEmpty(path))
        {
            return null;
        }

        var current = instance;
        var start = 0;
        while (current != null)
        {
            var dot = path.IndexOf('.', start);
            var segment = dot < 0 ? path.AsSpan(start) : path.AsSpan(start, dot - start);
            var getter = GetSegmentGetter(current.GetType(), segment.ToString());
            if (getter == null)
            {
                return null;
            }

            current = getter(current);
            if (dot < 0)
            {
                return current;
            }

            start = dot + 1;
        }

        return null;
    }

    /// <summary>Compiled getter for a path rooted at <paramref name="rootType"/>; null if the root has no such property.</summary>
    public static Func<object, object?>? GetGetter(Type rootType, string path)
    {
        if (string.IsNullOrEmpty(path) || GetPathType(rootType, path) == null)
        {
            return null;
        }

        if (!path.Contains('.'))
        {
            return GetSegmentGetter(rootType, path);
        }

        return instance => GetValue(instance, path);
    }

    public static bool TrySetValue(object instance, string path, object? value)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var lastDot = path.LastIndexOf('.');
        var owner = lastDot < 0 ? instance : GetValue(instance, path[..lastDot]);
        if (owner == null)
        {
            return false;
        }

        var property = FindProperty(owner.GetType(), lastDot < 0 ? path : path[(lastDot + 1)..]);
        if (property is not { CanWrite: true })
        {
            return false;
        }

        property.SetValue(owner, value);
        return true;
    }

    private static Func<object, object?>? GetSegmentGetter(Type type, string name) =>
        SegmentGetters.GetOrAdd((type, name), static key =>
        {
            var property = FindProperty(key.Type, key.Name);
            if (property is not { CanRead: true } || property.GetMethod is not { IsPublic: true })
            {
                return null;
            }

            var parameter = Expression.Parameter(typeof(object), "o");
            var body = Expression.Convert(Expression.Property(Expression.Convert(parameter, key.Type), property), typeof(object));
            return Expression.Lambda<Func<object, object?>>(body, parameter).Compile();
        });
}
