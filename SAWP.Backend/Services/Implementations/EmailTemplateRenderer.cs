using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace Services.Implementations;

/// <summary>Names of the email templates in Services/EmailTemplates (embedded resources).</summary>
public static class EmailTemplates
{
    public const string Activation = "Activation";
    public const string PasswordRecovery = "PasswordRecovery";

    public static readonly IReadOnlyList<string> All = new[] { Activation, PasswordRecovery };
}

/// <summary>
/// Renders an email from Services/EmailTemplates: the subject from the template's first line, the body inside
/// _Layout.html. Every {{Placeholder}} value is HTML-encoded; a placeholder without a value throws.
/// </summary>
public static class EmailTemplateRenderer
{
    private const string LayoutName = "_Layout";
    private const string SubjectPrefix = "Subject:";
    private static readonly Regex Placeholder = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);
    private static readonly Regex HtmlComment = new(@"<!--.*?-->\s*", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly ConcurrentDictionary<string, string> Cache = new();

    // Keeps Vietnamese letters readable in the HTML source; encodes <, >, &, quotes and the like
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    public static (string Subject, string HtmlBody) Render(string templateName, IReadOnlyDictionary<string, string> values)
    {
        var (subject, body) = SplitSubject(templateName, Load(templateName));
        var content = Fill(templateName, body, values);
        var layout = Fill(LayoutName, Load(LayoutName), new Dictionary<string, string> { ["Subject"] = subject },
            rawValues: new Dictionary<string, string> { ["Content"] = content });
        return (subject, layout);
    }

    private static string Fill(string templateName, string text, IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, string>? rawValues = null)
    {
        text = HtmlComment.Replace(text, string.Empty);
        return Placeholder.Replace(text, m =>
        {
            var name = m.Groups[1].Value;
            if (rawValues != null && rawValues.TryGetValue(name, out var raw))
            {
                return raw;
            }
            if (values.TryGetValue(name, out var value))
            {
                return Encoder.Encode(value);
            }
            throw new InvalidOperationException($"Email template '{templateName}' needs a value for {{{{{name}}}}}.");
        });
    }

    private static (string Subject, string Body) SplitSubject(string templateName, string text)
    {
        var firstBreak = text.IndexOf('\n');
        var firstLine = (firstBreak < 0 ? text : text[..firstBreak]).Trim();
        if (!firstLine.StartsWith(SubjectPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Email template '{templateName}' must start with a '{SubjectPrefix}' line.");
        }
        return (firstLine[SubjectPrefix.Length..].Trim(), firstBreak < 0 ? string.Empty : text[(firstBreak + 1)..].Trim());
    }

    private static string Load(string templateName) => Cache.GetOrAdd(templateName, name =>
    {
        var assembly = typeof(EmailTemplateRenderer).Assembly;
        var resource = $"Services.EmailTemplates.{name}.html";
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Email template '{name}' not found (embedded resource {resource}).");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });
}
