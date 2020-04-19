using Mogmog.Protos;
using System;

namespace Mogmog
{
    public struct ChatMessageInterop : IEquatable<ChatMessageInterop>
    {
        public ChatMessage Message { get; set; }
        public int ChannelId { get; set; }

        public bool Equals(ChatMessageInterop other)
        {
            return Message == other.Message && ChannelId == other.ChannelId;
        }
    }

    public struct GenericInterop : IEquatable<GenericInterop>
    {
        public string Command { get; set; }
        public string Arg { get; set; }

        public bool Equals(GenericInterop other)
        {
            return Command == other.Command && Arg == other.Arg;
        }
    }
}
