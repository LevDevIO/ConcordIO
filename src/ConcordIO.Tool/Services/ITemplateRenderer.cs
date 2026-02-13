namespace ConcordIO.Tool.Services;

/// <summary>
/// Service for rendering Scriban templates embedded in the assembly.
/// </summary>
/// <remarks>
/// <para>
/// This service loads Scriban templates from embedded resources and renders them
/// with provided model data. Templates are located in the <c>Templates/</c> folder
/// and embedded as assembly resources.
/// </para>
/// <para>
/// Template naming convention: <c>ConcordIO.Tool.Templates.{Folder}.{FileName}</c>
/// </para>
/// <para>
/// Example embedded resources:
/// </para>
/// <list type="bullet">
/// <item><description><c>Contract.Contract.nuspec</c></description></item>
/// <item><description><c>Contract.Client.Contract.Client.targets</c></description></item>
/// </list>
/// </remarks>
/// <example>
/// <para>Rendering a template:</para>
/// <code>
/// var model = new Dictionary&lt;string, object&gt;
/// {
///     ["package_id"] = "MyService.Contracts",
///     ["version"] = "1.0.0",
///     ["authors"] = "MyCompany",
///     ["specs_by_kind"] = specsByKind
/// };
/// 
/// var content = await renderer.RenderAsync("Contract.Contract.nuspec", model);
/// </code>
/// </example>
public interface ITemplateRenderer
{
    /// <summary>
    /// Renders a Scriban template with the given model data.
    /// </summary>
    /// <param name="templateName">
    /// The name of the embedded template resource, using dot notation for folder paths.
    /// Example: <c>"Contract.Contract.nuspec"</c> maps to <c>Templates/Contract/Contract.nuspec</c>.
    /// </param>
    /// <param name="model">
    /// A dictionary containing the model data to render into the template.
    /// Keys should match Scriban variable names in the template (e.g., <c>package_id</c>, <c>version</c>).
    /// </param>
    /// <returns>The rendered template content as a string.</returns>
    /// <remarks>
    /// <para>
    /// Common model keys used across templates:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>package_id</c> - The NuGet package ID</description></item>
    /// <item><description><c>version</c> - The package version</description></item>
    /// <item><description><c>authors</c> - Package author(s)</description></item>
    /// <item><description><c>description</c> - Package description</description></item>
    /// <item><description><c>specs_by_kind</c> - Dictionary of spec files grouped by kind</description></item>
    /// <item><description><c>has_openapi</c>, <c>has_proto</c>, <c>has_asyncapi</c> - Flags for spec kinds</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var model = new Dictionary&lt;string, object&gt;
    /// {
    ///     ["package_id"] = "OrderService.Contracts",
    ///     ["version"] = "2.0.0",
    ///     ["has_asyncapi"] = true,
    ///     ["specs_by_kind"] = new Dictionary&lt;string, List&lt;string&gt;&gt;
    ///     {
    ///         ["asyncapi"] = new List&lt;string&gt; { "api.yaml" }
    ///     }
    /// };
    /// 
    /// var nuspec = await renderer.RenderAsync("Contract.Contract.nuspec", model);
    /// var targets = await renderer.RenderAsync("Contract.Contract.targets", model);
    /// </code>
    /// </example>
    Task<string> RenderAsync(string templateName, Dictionary<string, object> model);
}
