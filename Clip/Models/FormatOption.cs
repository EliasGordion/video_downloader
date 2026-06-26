namespace Clip.Models;

public sealed class FormatOption
{
    public FormatOption()
    {
    }

    public FormatOption(string label, string extension, bool isAudioOnly = false)
    {
        Label = label;
        Extension = extension;
        IsAudioOnly = isAudioOnly;
    }

    public string Label { get; set; } = "MP4";
    public string Extension { get; set; } = "mp4";
    public bool IsAudioOnly { get; set; }

    public override string ToString() => Label;
}
