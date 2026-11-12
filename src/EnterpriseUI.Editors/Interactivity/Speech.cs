using System.Threading.Channels;

namespace EnterpriseUI.Editors.Interactivity;

public readonly record struct AudioFormat(int SampleRate, int Channels, int BitsPerSample)
{
    public static AudioFormat Speech16kMono => new(16000, 1, 16);
    public int BytesPerSecond => SampleRate * Channels * BitsPerSample / 8;
}

/// <summary>Microphone (or any PCM source); data is signed little-endian PCM in the declared format.</summary>
public interface IAudioInput : IDisposable
{
    AudioFormat Format { get; }
    event Action<byte[]>? DataAvailable;
    void Start();
    void Stop();
}

public static class AudioLevel
{
    /// <summary>Root-mean-square level 0..1 of 16-bit PCM samples.</summary>
    public static double Rms16(ReadOnlySpan<byte> pcm)
    {
        var count = pcm.Length / 2;
        if (count == 0) return 0;
        double sum = 0;
        for (var i = 0; i < count; i++)
        {
            var sample = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
            sum += (double)sample * sample;
        }

        return Math.Min(1, Math.Sqrt(sum / count) / short.MaxValue * 2.5);
    }
}

public sealed record SpeechRecognitionOptions(string Language = "en-US", bool PartialResults = true, AudioFormat? Format = null);

public readonly record struct SpeechTranscript(string Text, bool IsFinal, double Confidence = 1);

/// <summary>An open recognition stream: audio goes in, transcripts come out through <see cref="Transcribed"/>.</summary>
public interface ISpeechRecognitionStream : IAsyncDisposable
{
    event EventHandler<SpeechTranscript>? Transcribed;
    ValueTask WriteAudioAsync(ReadOnlyMemory<byte> pcm, CancellationToken cancellationToken);

    /// <summary>Signals the end of audio and lets the provider flush a final transcript.</summary>
    Task CompleteAsync(CancellationToken cancellationToken);
}

public interface ISpeechRecognitionProvider
{
    Task<ISpeechRecognitionStream> StartAsync(SpeechRecognitionOptions options, CancellationToken cancellationToken);
}

public enum SpeechSessionState { Idle, Starting, Listening, Processing, Faulted }

/// <summary>Connects an <see cref="IAudioInput"/> to a recognition provider and reports levels, partial and final text on the creating context.</summary>
public sealed class SpeechDictationSession : IAsyncDisposable
{
    private readonly IAudioInput _input;
    private readonly ISpeechRecognitionProvider _provider;
    private readonly SpeechRecognitionOptions _options;
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private ISpeechRecognitionStream? _stream;
    private Channel<byte[]>? _audio;
    private Task? _pump;
    private CancellationTokenSource? _cts;
    private SpeechSessionState _state;

    public SpeechDictationSession(IAudioInput input, ISpeechRecognitionProvider provider, SpeechRecognitionOptions? options = null)
    {
        _input = input;
        _provider = provider;
        _options = options ?? new SpeechRecognitionOptions();
    }

    public SpeechSessionState State => _state;
    public string PartialText { get; private set; } = string.Empty;
    public string FinalText { get; private set; } = string.Empty;
    public Exception? Error { get; private set; }

    public event EventHandler<double>? LevelChanged;
    public event EventHandler<string>? PartialTextChanged;
    public event EventHandler<string>? FinalTextAvailable;
    public event EventHandler? StateChanged;

    private void Raise(Action action)
    {
        if (_context == null || SynchronizationContext.Current == _context) action();
        else _context.Post(_ => action(), null);
    }

    private void SetState(SpeechSessionState state)
    {
        if (_state == state) return;
        _state = state;
        Raise(() => StateChanged?.Invoke(this, EventArgs.Empty));
    }

    public async Task StartAsync()
    {
        if (_state is SpeechSessionState.Starting or SpeechSessionState.Listening or SpeechSessionState.Processing) return;
        Error = null;
        PartialText = FinalText = string.Empty;
        SetState(SpeechSessionState.Starting);
        _cts = new CancellationTokenSource();
        try
        {
            _stream = await _provider.StartAsync(_options with { Format = _input.Format }, _cts.Token).ConfigureAwait(false);
            _stream.Transcribed += OnTranscribed;
            _audio = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.DropOldest });
            _pump = Task.Run(() => PumpAsync(_stream, _audio.Reader, _cts.Token));
            _input.DataAvailable += OnAudio;
            _input.Start();
            SetState(SpeechSessionState.Listening);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await FailAsync(ex).ConfigureAwait(false);
        }
    }

    private void OnAudio(byte[] data)
    {
        var level = AudioLevel.Rms16(data);
        Raise(() => LevelChanged?.Invoke(this, level));
        _audio?.Writer.TryWrite(data);
    }

    private async Task PumpAsync(ISpeechRecognitionStream stream, ChannelReader<byte[]> reader, CancellationToken token)
    {
        try
        {
            await foreach (var chunk in reader.ReadAllAsync(token).ConfigureAwait(false)) await stream.WriteAudioAsync(chunk, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Error = ex;
            SetState(SpeechSessionState.Faulted);
        }
    }

    private void OnTranscribed(object? sender, SpeechTranscript transcript)
    {
        if (transcript.IsFinal)
        {
            FinalText = string.IsNullOrEmpty(FinalText) ? transcript.Text : FinalText + " " + transcript.Text;
            PartialText = string.Empty;
            var text = transcript.Text;
            Raise(() => FinalTextAvailable?.Invoke(this, text));
        }
        else
        {
            PartialText = transcript.Text;
            var text = transcript.Text;
            Raise(() => PartialTextChanged?.Invoke(this, text));
        }
    }

    /// <summary>Stops capturing, lets the provider finish and returns the complete transcript.</summary>
    public async Task<string> StopAsync()
    {
        if (_state is not (SpeechSessionState.Listening or SpeechSessionState.Starting)) return FinalText;
        SetState(SpeechSessionState.Processing);
        try
        {
            _input.Stop();
            _input.DataAvailable -= OnAudio;
            _audio?.Writer.TryComplete();
            if (_pump != null) await _pump.ConfigureAwait(false);
            if (_stream != null) await _stream.CompleteAsync(_cts?.Token ?? CancellationToken.None).ConfigureAwait(false);
            if (_state == SpeechSessionState.Processing) SetState(SpeechSessionState.Idle);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await FailAsync(ex).ConfigureAwait(false);
        }
        finally
        {
            await CloseStreamAsync().ConfigureAwait(false);
        }

        return FinalText;
    }

    private async Task FailAsync(Exception ex)
    {
        Error = ex;
        try { _input.Stop(); } catch (Exception stopEx) when (stopEx is not OutOfMemoryException) { }
        _input.DataAvailable -= OnAudio;
        await CloseStreamAsync().ConfigureAwait(false);
        SetState(SpeechSessionState.Faulted);
    }

    private async Task CloseStreamAsync()
    {
        _cts?.Cancel();
        if (_stream != null)
        {
            _stream.Transcribed -= OnTranscribed;
            await _stream.DisposeAsync().ConfigureAwait(false);
            _stream = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_state is SpeechSessionState.Listening or SpeechSessionState.Starting) await StopAsync().ConfigureAwait(false);
        _cts?.Dispose();
    }
}

/// <summary>Audio source producing a speech-like synthetic signal; for demos, design time and tests.</summary>
public sealed class SyntheticAudioInput : IAudioInput
{
    private readonly Random _random = new(7);
    private Timer? _timer;
    private double _phase;

    public AudioFormat Format => AudioFormat.Speech16kMono;
    public event Action<byte[]>? DataAvailable;

    public void Start() => _timer = new Timer(_ => Emit(), null, 0, 50);

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void Emit()
    {
        var samples = Format.SampleRate / 20;
        var buffer = new byte[samples * 2];
        var amplitude = 3000 + _random.NextDouble() * 9000;
        for (var i = 0; i < samples; i++)
        {
            _phase += 2 * Math.PI * 180 / Format.SampleRate;
            var value = (short)(Math.Sin(_phase) * amplitude);
            buffer[i * 2] = (byte)(value & 0xFF);
            buffer[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }

        DataAvailable?.Invoke(buffer);
    }

    public void Dispose() => Stop();
}

/// <summary>Recognizer that "hears" a fixed script, one word per <see cref="BytesPerWord"/> bytes of audio; for demos and tests.</summary>
public sealed class ScriptedSpeechProvider : ISpeechRecognitionProvider
{
    public ScriptedSpeechProvider(string script) => Script = script;

    public string Script { get; }
    public int BytesPerWord { get; set; } = 6400;

    public Task<ISpeechRecognitionStream> StartAsync(SpeechRecognitionOptions options, CancellationToken cancellationToken) =>
        Task.FromResult<ISpeechRecognitionStream>(new ScriptedStream(Script.Split(' ', StringSplitOptions.RemoveEmptyEntries), BytesPerWord, options.PartialResults));

    private sealed class ScriptedStream : ISpeechRecognitionStream
    {
        private readonly string[] _words;
        private readonly int _bytesPerWord;
        private readonly bool _partial;
        private int _spoken;
        private long _bytes;

        public ScriptedStream(string[] words, int bytesPerWord, bool partial)
        {
            _words = words;
            _bytesPerWord = Math.Max(1, bytesPerWord);
            _partial = partial;
        }

        public event EventHandler<SpeechTranscript>? Transcribed;

        public ValueTask WriteAudioAsync(ReadOnlyMemory<byte> pcm, CancellationToken cancellationToken)
        {
            _bytes += pcm.Length;
            var target = (int)Math.Min(_words.Length, _bytes / _bytesPerWord);
            if (target > _spoken && _partial)
            {
                _spoken = target;
                Transcribed?.Invoke(this, new SpeechTranscript(string.Join(' ', _words.Take(_spoken)), false, 0.8));
            }

            return ValueTask.CompletedTask;
        }

        public Task CompleteAsync(CancellationToken cancellationToken)
        {
            var count = _partial ? _spoken : (int)Math.Min(_words.Length, _bytes / _bytesPerWord);
            if (count > 0) Transcribed?.Invoke(this, new SpeechTranscript(string.Join(' ', _words.Take(count)), true, 0.95));
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
