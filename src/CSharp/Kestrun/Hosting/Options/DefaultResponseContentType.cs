namespace Kestrun.Hosting.Options;
/// <summary>
/// Default response content type for routes.
/// </summary>
/// <param name="ContentType">The default response content type dictionary.</param>
public record DefaultResponseContentType(IDictionary<string, ICollection<string>> ContentType)
{

}
