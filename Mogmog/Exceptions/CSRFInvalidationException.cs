using System;

namespace Mogmog.Exceptions
{
    /// <summary>
    /// An exception that should be thrown when a CSRF (cross-site request forgery) attack on the user is suspected.
    /// </summary>
    [Serializable]
    public class CSRFInvalidationException : Exception
    {
        public CSRFInvalidationException() {}
        public CSRFInvalidationException(string message) : base(message) {}
    }
}
