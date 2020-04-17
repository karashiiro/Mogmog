using System;

namespace Mogmog.FFXIV
{
    public interface IChatCommandHandler : IDisposable
    {
        void AddChatHandler(int i);

        void RemoveChatHandler(int i);
    }
}
