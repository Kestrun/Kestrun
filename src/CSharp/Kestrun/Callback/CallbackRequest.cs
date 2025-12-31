namespace Kestrun.Callback;

/// <summary>
/// Represents a request to perform a callback operation.
/// </summary>

public class CallbackRequest
{
    /// <summary>
    /// Stable id for tracking
    /// </summary>
    public string CallbackId { get; }              // stable id for tracking

    /// <summary>
    /// E.g. createPayment
    /// </summary>
    public string OperationId { get; set; }             // e.g. createPayment
    /// <summary>
    /// The URL to which the callback request will be sent
    /// </summary>
    public Uri TargetUrl { get; set; }
    /// <summary>
    /// HTTP method to use for the callback (e.g., POST, PUT)
    /// </summary>
    public string HttpMethod { get; }              // POST, PUT, etc.
    /// <summary>
    /// HTTP headers to include in the callback request
    /// </summary>
    public IDictionary<string, string> Headers { get; set; }
    /// <summary>
    /// The content type of the callback request body
    /// </summary>
    public string ContentType { get; }
    /// <summary>
    /// The body of the callback request
    /// </summary>
    public byte[]? Body { get; set; }
    /// <summary>
    /// Trace or correlation identifier
    /// </summary>
    public string CorrelationId { get; set; }           // trace/correlation

    /// <summary>
    /// Key for receiver deduplication
    /// </summary>
    public string IdempotencyKey { get; set; }          // for receiver dedupe
    /// <summary>
    /// Attempt number, starting at 0
    /// </summary>
    public int Attempt { get; set; }                    // starts at 0
    /// <summary>
    /// Timestamp when the callback request was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; }
    /// <summary>
    /// Timestamp for the next attempt
    /// </summary>
    public DateTimeOffset NextAttemptAt { get; set; }
    /// <summary>
    /// Timeout duration for the callback request
    /// </summary>
    public TimeSpan Timeout { get; set; }
    /// <summary>
    /// Identifier for the signature key, if signing
    /// </summary>
    public string? SignatureKeyId { get; set; }   // if signing

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackRequest"/> class.
    /// </summary>
    /// <param name="callbackId"> </param>
    /// <param name="operationId"></param>
    /// <param name="targetUrl"></param>
    /// <param name="httpMethod"></param>
    /// <param name="headers"></param>
    /// <param name="contentType"></param>
    /// <param name="body"></param>
    /// <param name="correlationId"></param>
    /// <param name="idempotencyKey"></param>
    /// <param name="timeout"></param>
    /// <param name="signatureKeyId"></param>
    public CallbackRequest(
       string callbackId,
       string operationId,
       Uri targetUrl,
       string httpMethod,
       IDictionary<string, string> headers,
       string contentType,
       byte[]? body,
       string correlationId,
       string idempotencyKey,
       TimeSpan timeout,
       string? signatureKeyId = null)
    {
        CallbackId = callbackId;
        OperationId = operationId;
        TargetUrl = targetUrl;
        HttpMethod = httpMethod;

        Headers = headers;
        ContentType = contentType;
        Body = body;

        CorrelationId = correlationId;
        IdempotencyKey = idempotencyKey;

        Attempt = 0;
        CreatedAt = DateTimeOffset.UtcNow;
        NextAttemptAt = CreatedAt;
        Timeout = timeout;
        SignatureKeyId = signatureKeyId;
    }
}
