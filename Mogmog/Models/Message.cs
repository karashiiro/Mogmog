namespace Mogmog.Models
{
    /*
     * The Message object includes both the world name and the world ID to make client-side operations a mogtouch
     * easier to deal with. The client can provide an ID, and the server will return a world name.
     */
    public class Message
    {
        public ulong Id;
        public string Content;
        public string Author;
        public string Avatar;
        public string World;
        public int WorldId;
    }
}
