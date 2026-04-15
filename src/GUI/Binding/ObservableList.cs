using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Sokol.GUI;

/// <summary>
/// Observable list — raises <see cref="CollectionChanged"/> on mutations.
/// </summary>
public sealed class ObservableList<T> : IList<T>
{
    private readonly List<T> _inner = [];

    public event Action? CollectionChanged;

    // IList<T>
    public T    this[int i] { get => _inner[i]; set { _inner[i] = value; CollectionChanged?.Invoke(); } }
    public int  Count       => _inner.Count;
    public bool IsReadOnly  => false;

    public void Add(T item)       { _inner.Add(item);       CollectionChanged?.Invoke(); }
    public void Insert(int i, T x){ _inner.Insert(i, x);    CollectionChanged?.Invoke(); }
    public bool Remove(T item)    { bool r = _inner.Remove(item); if (r) CollectionChanged?.Invoke(); return r; }
    public void RemoveAt(int i)   { _inner.RemoveAt(i);     CollectionChanged?.Invoke(); }
    public void Clear()           { _inner.Clear();          CollectionChanged?.Invoke(); }

    public bool     Contains(T item)        => _inner.Contains(item);
    public int      IndexOf(T item)         => _inner.IndexOf(item);
    public void     CopyTo(T[] a, int idx)  => _inner.CopyTo(a, idx);

    public IEnumerator<T> GetEnumerator()   => _inner.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();
}
