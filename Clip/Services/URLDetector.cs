using System.Text.RegularExpressions;
using Clip.Models;

namespace Clip.Services;

public sealed partial class URLDetector
{
    public bool TryGetVideoUrl(string? text, out string url, out Platform platform)
    {
        url = "";
        platform = Platform.Unknown;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = UrlRegex().Match(text);
        if (!match.Success)
        {
            return false;
        }

        url = match.Value.TrimEnd('.', ',', ')', ']', '"', '\'');
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        platform = DetectPlatform(url);
        return true;
    }

    public Platform DetectPlatform(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            !Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return Platform.Unknown;
        }

        var host = uri.Host.ToLowerInvariant();

        if (HostMatches(host, "youtube.com") || HostMatches(host, "youtu.be"))
        {
            return Platform.YouTube;
        }

        if (HostMatches(host, "twitter.com") || HostMatches(host, "x.com"))
        {
            return Platform.Twitter;
        }

        if (HostMatches(host, "instagram.com"))
        {
            return Platform.Instagram;
        }

        if (HostMatches(host, "tiktok.com"))
        {
            return Platform.TikTok;
        }

        if (HostMatches(host, "reddit.com") || HostMatches(host, "redd.it"))
        {
            return Platform.Reddit;
        }

        return Platform.Unknown;
    }

    private static bool HostMatches(string host, string domain)
    {
        return host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"https?://[^\s<>()]+", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();
}
