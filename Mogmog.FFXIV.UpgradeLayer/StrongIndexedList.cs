using System.Collections.Generic;

namespace Mogmog.FFXIV.UpgradeLayer
{
    /// <summary>
    /// A list in which removed objects are replaced with <see cref="null"/>, unless
    /// the object in question is the last object in the collection. Objects added to
    /// the collection, unless accessed by index, are assigned the first <see cref="null"/>
    /// position. Objects can still be appended to the structure via LINQ.
    /// </summary>
    /// <typeparam name="T">A class type.</typeparam>
    public class StrongIndexedList<T> : List<T> where T : class
    {
        public new void Add(T value)
        {
            var nidx = IndexOf(null);
            if (nidx == -1)
                base.Add(value);
            else
                base[nidx] = value;
        }

        public new void Remove(T value)
        {
            var idx = IndexOf(value);
            if (idx == Count - 1)
                base.Remove(value); // Will resize the list since the element is at the end.
            else
                base[idx] = null;
        }

        public new void RemoveAt(int index)
        {
            if (index == Count - 1)
                base.RemoveAt(index); // Will resize the list since the element is at the end.
            else
                base[index] = null;
        }
    }
}
