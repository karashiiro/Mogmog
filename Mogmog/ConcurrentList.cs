using ConcurrentLinkedList;
using System.Collections;
using System.Collections.Generic;

namespace Mogmog
{
    public class ConcurrentList<T> : ConcurrentLinkedList<T>, IEnumerable<T>
    {
        public IEnumerator<T> GetEnumerator()
        {
            var cur = First;
            while (cur != null)
            {
                yield return cur.Value;
                cur = cur.Next;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
