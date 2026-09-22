using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Voyage.EarthquakeWarning.Services;

public sealed record AudioCue(string FileName, double StartSeconds);

public sealed class AudioService
{
    private readonly string _audioDirectory;
    private readonly object _gate = new();

    private CancellationTokenSource? _currentCts;
    private WaveOutEvent? _currentOutput;
    private Task _playbackTask = Task.CompletedTask;
    private float? _originalMasterVolume;
    private DateTime _lastVolumeRestoreDeadline = DateTime.MinValue;
    private volatile bool _playing;

    // 是否还有音频在播放
    public bool IsPlaying => _playing;

    public AudioService()
    {
        var pluginDirectory =
            Path.GetDirectoryName(typeof(AudioService).Assembly.Location)
            ?? AppContext.BaseDirectory;

        _audioDirectory =
            Path.Combine(pluginDirectory, "Assets", "Audio");
    }

    private string GetAudioPath(string fileName)
    {
        var path = Path.Combine(_audioDirectory, fileName);

        if (!File.Exists(path))
            throw new FileNotFoundException("Audio file not found.", path);

        if (new FileInfo(path).Length <= 44)
            throw new InvalidDataException("Audio file is empty or invalid.");

        return path;
    }

    public double GetDurationSeconds(string fileName)
    {
        using var reader = new AudioFileReader(GetAudioPath(fileName));
        return reader.TotalTime.TotalSeconds;
    }

    // 播放一组音频，新的一组会立即打断上一组
    public void PlaySequence(
        IReadOnlyList<AudioCue> cues,
        CancellationToken cancellationToken)
    {
        if (cues.Count == 0)
            return;

        lock (_gate)
        {
            var previous = _playbackTask;

            _currentCts?.Cancel();

            var cts = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken);

            _currentCts = cts;
            _playing = true;
            _playbackTask =
                RunSequenceAsync(previous, cues, cts);
        }
    }

    private async Task RunSequenceAsync(
        Task previous,
        IReadOnlyList<AudioCue> cues,
        CancellationTokenSource cts)
    {
        try
        {
            await previous.ConfigureAwait(false);
        }
        catch
        {
        }

        try
        {
            foreach (var cue in cues)
            {
                cts.Token.ThrowIfCancellationRequested();

                await PlayFileAsync(cue, cts.Token)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_currentCts, cts))
                {
                    _currentCts = null;
                    _currentOutput = null;
                    _playing = false;
                }
            }
        }
    }

    private async Task PlayFileAsync(
        AudioCue cue,
        CancellationToken token)
    {
        using var reader =
            new AudioFileReader(GetAudioPath(cue.FileName));

        var maxStart = Math.Max(
            0,
            reader.TotalTime.TotalSeconds - 0.05);

        var start = Math.Clamp(
            cue.StartSeconds,
            0,
            maxStart);

        if (start > 0)
        {
            reader.CurrentTime =
                TimeSpan.FromSeconds(start);
        }

        using var output = new WaveOutEvent();

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        output.PlaybackStopped +=
            (_, _) => completion.TrySetResult();

        output.Init(reader);

        using var registration = token.Register(() =>
        {
            try
            {
                output.Stop();
            }
            catch
            {
            }
        });

        lock (_gate)
            _currentOutput = output;

        output.Play();

        await completion.Task.ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
    }

    public void StopAll()
    {
        CancellationTokenSource? cts;
        WaveOutEvent? output;

        lock (_gate)
        {
            cts = _currentCts;
            output = _currentOutput;
            _playing = false;
        }

        try
        {
            output?.Stop();
        }
        catch
        {
        }

        try
        {
            cts?.Cancel();
        }
        catch
        {
        }
    }

    public void ForceMasterVolume100()
    {
        try
        {
            var endpoint = new MMDeviceEnumerator()
                .GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            _originalMasterVolume ??=
                endpoint.AudioEndpointVolume.MasterVolumeLevelScalar;

            endpoint.AudioEndpointVolume.MasterVolumeLevelScalar = 1.0f;
            _lastVolumeRestoreDeadline =
                DateTime.UtcNow.AddSeconds(10);
        }
        catch
        {
        }
    }

    public void RestoreMasterVolume()
    {
        if (_originalMasterVolume is null)
            return;

        try
        {
            var endpoint = new MMDeviceEnumerator()
                .GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            endpoint.AudioEndpointVolume.MasterVolumeLevelScalar =
                _originalMasterVolume.Value;
        }
        catch
        {
        }

        _originalMasterVolume = null;
    }

    public void ScheduleRestoreAfter10Seconds()
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        _lastVolumeRestoreDeadline = deadline;

        _ = Task.Run(async () =>
        {
            var wait = deadline - DateTime.UtcNow;

            if (wait > TimeSpan.Zero)
                await Task.Delay(wait);

            if (_lastVolumeRestoreDeadline == deadline)
                RestoreMasterVolume();
        });
    }
}
