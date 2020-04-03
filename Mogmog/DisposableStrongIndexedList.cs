using System;

namespace Mogmog
{
    /// <summary>
    /// A specialized form of <see cref="StrongIndexedList{T}"/> that calls the Dispose()
    /// methods of its objects upon removal. Upon disposal of this list, the Dispose()
    /// methods of all contained objects are called.
    /// </summary>
    public class DisposableStrongIndexedList<T> : StrongIndexedList<T> where T : class, IDisposable
    {
        public new void Remove(T value)
        {
            value.Dispose();
            base.Remove(value);
        }

        public new void RemoveAt(int index)
        {
            base[index].Dispose();
            base.RemoveAt(index);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (var value in this)
                    {
                        value.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
