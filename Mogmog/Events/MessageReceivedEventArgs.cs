using Mogmog.Protos;
using System;

namespace Mogmog.Events
{
    public class MessageReceivedEventArgs : EventArgs
    {
        public ChatMessage Message { get; set; }
        public int ChannelId { get; set; }
    }
}
