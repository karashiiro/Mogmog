using Grpc.Core;
using System.Threading.Tasks;

namespace Mogmog.Tests
{
    public class TestServerStreamWriter<T> : IServerStreamWriter<T>
    {
        public T Current { get; private set; }

        public WriteOptions WriteOptions { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        private bool _written;

        public TestServerStreamWriter()
        {
            _written = false;
        }

        public Task WriteAsync(T message)
        {
            Current = message;
            _written = true;
            return Task.CompletedTask;
        }

        public Task ReturnOnWrite()
        {
            while (!_written)
                continue;
            return Task.CompletedTask;
        }
    }
}
