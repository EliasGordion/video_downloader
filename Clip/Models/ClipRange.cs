namespace Clip.Models;

public sealed class ClipRange : ObservableObject
{
    private double _durationSeconds = 60;
    private double _startSeconds;
    private double _endSeconds = 60;

    public double DurationSeconds
    {
        get => _durationSeconds;
        set
        {
            var duration = Math.Max(1, value);
            if (SetProperty(ref _durationSeconds, duration))
            {
                if (_endSeconds > duration)
                {
                    EndSeconds = duration;
                }

                OnPropertyChanged(nameof(StartText));
                OnPropertyChanged(nameof(EndText));
            }
        }
    }

    public double StartSeconds
    {
        get => _startSeconds;
        set
        {
            var safe = Math.Clamp(value, 0, Math.Max(0, EndSeconds - 1));
            if (SetProperty(ref _startSeconds, safe))
            {
                OnPropertyChanged(nameof(StartText));
                OnPropertyChanged(nameof(SelectedDurationText));
            }
        }
    }

    public double EndSeconds
    {
        get => _endSeconds;
        set
        {
            var safe = Math.Clamp(value, Math.Min(DurationSeconds, StartSeconds + 1), DurationSeconds);
            if (SetProperty(ref _endSeconds, safe))
            {
                OnPropertyChanged(nameof(EndText));
                OnPropertyChanged(nameof(SelectedDurationText));
            }
        }
    }

    public string StartText
    {
        get => FormatTime(StartSeconds);
        set
        {
            if (TryParseTime(value, out var seconds))
            {
                StartSeconds = seconds;
            }
        }
    }

    public string EndText
    {
        get => FormatTime(EndSeconds);
        set
        {
            if (TryParseTime(value, out var seconds))
            {
                EndSeconds = seconds;
            }
        }
    }

    public string SelectedDurationText => FormatTime(Math.Max(0, EndSeconds - StartSeconds));

    public ClipRange Clone()
    {
        return new ClipRange
        {
            DurationSeconds = DurationSeconds,
            StartSeconds = StartSeconds,
            EndSeconds = EndSeconds
        };
    }

    public static string FormatTime(double totalSeconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss")
            : time.ToString(@"m\:ss");
    }

    public static bool TryParseTime(string? text, out double seconds)
    {
        seconds = 0;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (double.TryParse(text, out var rawSeconds))
        {
            seconds = rawSeconds;
            return true;
        }

        var parts = text.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 2 or > 3)
        {
            return false;
        }

        var values = new double[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!double.TryParse(parts[i], out values[i]))
            {
                return false;
            }
        }

        seconds = parts.Length == 2
            ? values[0] * 60 + values[1]
            : values[0] * 3600 + values[1] * 60 + values[2];

        return true;
    }
}
