using Mogmog.Protos;

namespace Mogmog
{
    public struct ChatMessageInterop
    {
        public ChatMessage Message { get; set; }
        public int ChannelId { get; set; }
    }

    public struct GenericInterop
    {
        public string Command { get; set; }
        public string Arg { get; set; }
    }
}
