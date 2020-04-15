namespace Mogmog.OAuth2
{
    public interface IOAuth2Kit
    {
        string OAuth2Code { get; }

        void Authenticate(string serverId);
    }
}
