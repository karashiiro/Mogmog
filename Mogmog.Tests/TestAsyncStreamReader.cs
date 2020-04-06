using Grpc.Core;
using System.Threading;
using System.Threading.Tasks;

namespace Mogmog.Tests
{
    public class TestAsyncStreamReader<T> : IAsyncStreamReader<T>
    {
        public T Current { get; private set; }

        public TestAsyncStreamReader(T input)
        {
            Current = input;
        }

        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(1);
            return true;
        }
    }
}
