using System;

namespace Mogmog.FFXIV
{
    public interface ICommandHandler : IDisposable
    {
        void AddCommandHandler(int i);

        void RemoveCommandHandler(int i);
    }
}
