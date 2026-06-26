namespace Clip.Models;

public sealed class LanguageOption(string code, string badge, string displayName)
{
    public string Code { get; } = code;
    public string Badge { get; } = badge;
    public string DisplayName { get; } = displayName;
}
