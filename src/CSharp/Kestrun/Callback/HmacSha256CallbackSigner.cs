using System.Security.Cryptography;
using System.Text;

namespace Kestrun.Callback;
/// <summary>
/// Signer that uses HMAC-SHA256 to sign callback requests.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="HmacSha256CallbackSigner"/> class.
/// </remarks>
/// <param name="secret">The secret key used for HMAC-SHA256 signing.</param>
public sealed class HmacSha256CallbackSigner(string secret) : ICallbackSigner
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(secret);

    /// <summary>
    /// Signs the given HTTP request message based on the callback request.
    /// </summary>
    /// <param name="msg">The HTTP request message to sign.</param>
    /// <param name="r">The callback request containing signing information.</param>
    public void Sign(HttpRequestMessage msg, CallbackRequest r)
    {
        if (r.Body == null)
        {
            throw new InvalidOperationException("Cannot sign callback request with null body.");
        }
        // Body must be already set
        var hash = new HMACSHA256(_key).ComputeHash(r.Body);
        var signature = Convert.ToHexString(hash).ToLowerInvariant();

        _ = msg.Headers.TryAddWithoutValidation("X-Signature", $"sha256={signature}");
    }
}
