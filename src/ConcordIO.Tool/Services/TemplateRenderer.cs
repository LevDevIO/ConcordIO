using System.Reflection;
using Scriban;

namespace ConcordIO.Tool.Services;

/// <summary>
/// Default implementation of <see cref="ITemplateRenderer"/> that renders Scriban templates
/// from embedded resources.
/// </summary>
public class TemplateRenderer : ITemplateRenderer
{
    private readonly Assembly _assembly;

    public TemplateRenderer() : this(Assembly.GetExecutingAssembly())
    {
    }

    public TemplateRenderer(Assembly assembly)
    {
        _assembly = assembly;
    }

    public async Task<string> RenderAsync(string templateName, Dictionary<string, object> model)
    {
        var resourceName = $"ConcordIO.Tool.Templates.{templateName}";

        using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Template not found: {templateName}");
        using var reader = new StreamReader(stream);
        var templateContent = await reader.ReadToEndAsync();

        var template = Template.Parse(templateContent);
        if (template.HasErrors)
        {
            throw new InvalidOperationException($"Template parse error: {string.Join(", ", template.Messages)}");
        }

        return template.Render(model);
    }
}
