public interface IOpenApiExampleAttribute
{
    /// <summary>Local name under content[contentType].examples</summary>
    string Key { get; set; }

    /// <summary>Id under #/components/examples/{ReferenceId}</summary>
    string ReferenceId { get; set; }

    /// <summary>
    /// When true, embeds the example directly instead of referencing it.
    /// </summary>
    bool Inline { get; set; }
}
