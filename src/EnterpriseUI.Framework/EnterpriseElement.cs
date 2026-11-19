using System.Collections.ObjectModel;

namespace EnterpriseUI.Framework;

/// <summary>Interaction state flags that pseudo-classes such as <c>:hover</c> match against.</summary>
[Flags]
public enum ElementStates
{
    None = 0,
    Hover = 1,
    Focus = 2,
    Pressed = 4,
    Disabled = 8,
    Selected = 16,
    Checked = 32
}

/// <summary>Node of the retained UI tree: a type name, an optional id, style classes, attributes, interaction state and children.</summary>
public class EnterpriseElement
{
    private readonly HashSet<string> _classes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _attributes = new(StringComparer.OrdinalIgnoreCase);
    private ElementStates _states;
    private string? _id;

    public EnterpriseElement(string typeName, string? id = null)
    {
        if (string.IsNullOrWhiteSpace(typeName)) throw new ArgumentException("A type name is required.", nameof(typeName));
        TypeName = typeName;
        _id = id;
        Children = new ElementCollection(this);
    }

    public string TypeName { get; }

    public string? Id
    {
        get => _id;
        set { if (_id == value) return; _id = value; OnChanged(ElementChange.Identity); }
    }

    public EnterpriseElement? Parent { get; private set; }

    public ElementCollection Children { get; }

    public IReadOnlyCollection<string> Classes => _classes;

    public IReadOnlyDictionary<string, string> Attributes => _attributes;

    public ElementStates States => _states;

    /// <summary>Raised whenever identity, classes, attributes, state or structure change; style resolution caches listen to it.</summary>
    public event EventHandler<ElementChangedEventArgs>? Changed;

    public bool AddClass(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !_classes.Add(name.Trim())) return false;
        OnChanged(ElementChange.Classes);
        return true;
    }

    public bool RemoveClass(string name)
    {
        if (!_classes.Remove(name)) return false;
        OnChanged(ElementChange.Classes);
        return true;
    }

    public bool HasClass(string name) => _classes.Contains(name);

    public void SetAttribute(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An attribute name is required.", nameof(name));
        if (value is null) { if (_attributes.Remove(name)) OnChanged(ElementChange.Attributes); return; }
        if (_attributes.TryGetValue(name, out var old) && old == value) return;
        _attributes[name] = value;
        OnChanged(ElementChange.Attributes);
    }

    public string? GetAttribute(string name) => _attributes.TryGetValue(name, out var value) ? value : null;

    public void SetState(ElementStates state, bool on)
    {
        var next = on ? _states | state : _states & ~state;
        if (next == _states) return;
        _states = next;
        OnChanged(ElementChange.State);
    }

    public bool HasState(ElementStates state) => (_states & state) == state;

    public IEnumerable<EnterpriseElement> Ancestors()
    {
        for (var p = Parent; p is not null; p = p.Parent) yield return p;
    }

    public IEnumerable<EnterpriseElement> Descendants()
    {
        foreach (var child in Children)
        {
            yield return child;
            foreach (var nested in child.Descendants()) yield return nested;
        }
    }

    public EnterpriseElement Root => Parent is null ? this : Parent.Root;

    /// <summary>Slash separated path of type names and ids from the root, for diagnostics.</summary>
    public string Path => (Parent is null ? string.Empty : Parent.Path + "/") + TypeName + (Id is null ? string.Empty : "#" + Id);

    /// <summary>First descendant (or this element) matching a selector such as <c>Panel &gt; Button.primary</c>.</summary>
    public EnterpriseElement? Find(string selector)
    {
        var parsed = StyleSelector.Parse(selector);
        return Descendants().Prepend(this).FirstOrDefault(parsed.Matches);
    }

    public IReadOnlyList<EnterpriseElement> FindAll(string selector)
    {
        var parsed = StyleSelector.Parse(selector);
        return Descendants().Prepend(this).Where(parsed.Matches).ToList();
    }

    protected virtual void OnChanged(ElementChange change)
    {
        for (var element = this; element is not null; element = element.Parent)
            element.Changed?.Invoke(this, new ElementChangedEventArgs(this, change));
    }

    internal void SetParent(EnterpriseElement? parent) => Parent = parent;

    internal void RaiseStructure() => OnChanged(ElementChange.Structure);

    public override string ToString() => Path;

    /// <summary>Child list that keeps parent links consistent and rejects cycles.</summary>
    public sealed class ElementCollection : Collection<EnterpriseElement>
    {
        private readonly EnterpriseElement _owner;

        internal ElementCollection(EnterpriseElement owner) => _owner = owner;

        protected override void InsertItem(int index, EnterpriseElement item)
        {
            Attach(item);
            base.InsertItem(index, item);
            _owner.RaiseStructure();
        }

        protected override void SetItem(int index, EnterpriseElement item)
        {
            this[index].SetParent(null);
            Attach(item);
            base.SetItem(index, item);
            _owner.RaiseStructure();
        }

        protected override void RemoveItem(int index)
        {
            this[index].SetParent(null);
            base.RemoveItem(index);
            _owner.RaiseStructure();
        }

        protected override void ClearItems()
        {
            foreach (var item in this) item.SetParent(null);
            base.ClearItems();
            _owner.RaiseStructure();
        }

        private void Attach(EnterpriseElement item)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (item == _owner || _owner.Ancestors().Contains(item)) throw new InvalidOperationException("An element cannot contain itself or one of its ancestors.");
            item.Parent?.Children.Remove(item);
            item.SetParent(_owner);
        }

        public EnterpriseElement Add(string typeName, string? id = null, params string[] classes)
        {
            var element = new EnterpriseElement(typeName, id);
            foreach (var name in classes) element.AddClass(name);
            Add(element);
            return element;
        }
    }
}

public enum ElementChange { Identity, Classes, Attributes, State, Structure }

public sealed class ElementChangedEventArgs : EventArgs
{
    public ElementChangedEventArgs(EnterpriseElement element, ElementChange change)
    {
        Element = element;
        Change = change;
    }

    public EnterpriseElement Element { get; }
    public ElementChange Change { get; }
}
