using System;
using System.Collections;
using System.Collections.Generic;

namespace DrumGame.Game.Utils;

public class CircularList<T> : IList<T>
{
    int nextPosition;
    int count;
    T[] inner;
    public CircularList(int size)
    {
        inner = new T[size];
    }

    public void Add(T e)
    {
        if (Capacity == 0) return;
        inner[nextPosition] = e;
        if (count < Capacity) count += 1;
        nextPosition += 1;
        if (nextPosition == Capacity) nextPosition = 0;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < count; i++)
            yield return this[i];
    }

    public T this[int i]
    {
        get => inner[IndexToInnerIndex(i)];
        set => inner[IndexToInnerIndex(i)] = value;
    }

    int IndexToInnerIndex(int i)
    {
        if (i < 0 || i >= count) throw new IndexOutOfRangeException();
        var truePos = nextPosition - count + i;
        if (truePos < 0) truePos += Capacity;
        return truePos;
    }
    int InnerIndexToOuterIndex(int i)
    {
        if (i < 0 || i >= count) throw new IndexOutOfRangeException();
        var o = i - nextPosition;
        if (o < 0) o += Capacity;
        return o;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int IndexOf(T item)
    {
        for (var i = 0; i < count; i++)
        {
            if (inner[i].Equals(item))
                return InnerIndexToOuterIndex(i);
        }
        return -1;
    }

    public void Insert(int index, T item) => throw new NotImplementedException();

    public void RemoveAt(int index) => throw new NotImplementedException();

    public void Clear()
    {
        if (count > 0)
        {
            Array.Clear(inner, 0, count); // clear to release GC references
            count = 0;
            nextPosition = 0;
        }
    }

    public bool Contains(T item)
    {
        for (var i = 0; i < count; i++)
            if (inner[i].Equals(item)) return true;
        return false;
    }

    public void CopyTo(T[] array, int arrayIndex) => throw new NotImplementedException();
    public bool Remove(T item) => throw new NotImplementedException();

    public int Count => count;
    public int Capacity => inner.Length;
    public bool IsReadOnly => false;
}
