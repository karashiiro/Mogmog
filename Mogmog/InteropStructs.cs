using Mogmog.Protos;

namespace Mogmog
{
    public struct ChatMessageInterop
    {
        public ChatMessage Message;
        public int ChannelId;
    }

    public struct GenericInterop
    {
        public string Command;
        public string Arg;
    }
}
