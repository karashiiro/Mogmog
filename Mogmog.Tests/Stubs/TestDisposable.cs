using System;

namespace Mogmog.Tests.Stubs
{
    public class TestDisposable : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public TestDisposable()
        {
            IsDisposed = false;
        }

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    IsDisposed = true;
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
