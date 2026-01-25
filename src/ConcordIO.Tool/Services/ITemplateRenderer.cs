namespace ConcordIO.Tool.Services;

/// <summary>
/// Service for rendering Scriban templates.
/// </summary>
public interface ITemplateRenderer
{
    /// <summary>
    /// Renders a template with the given model.
    /// </summary>
    /// <param name="templateName">The name of the embedded template resource (e.g., "Contract.Contract.nuspec").</param>
    /// <param name="model">The model data to render into the template.</param>
    /// <returns>The rendered template content.</returns>
    Task<string> RenderAsync(string templateName, Dictionary<string, object> model);
}
