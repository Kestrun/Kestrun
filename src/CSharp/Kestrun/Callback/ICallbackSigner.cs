namespace Kestrun.Callback;

/// <summary>
/// Signer for signing callback requests.
/// </summary>
public interface ICallbackSigner
{
    /// <summary>
    /// Signs the given HTTP request message based on the callback request.
    /// </summary>
    /// <param name="message">The HTTP request message to sign.</param>
    /// <param name="request">The callback request containing signing information.</param> 
    void Sign(HttpRequestMessage message, CallbackRequest request);
}
