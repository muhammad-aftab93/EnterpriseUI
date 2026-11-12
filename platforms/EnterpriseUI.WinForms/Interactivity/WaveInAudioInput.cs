using System.Runtime.InteropServices;
using EnterpriseUI.Editors.Interactivity;

// Cleanup calls into winmm cannot be recovered from; only waveInOpen is checked.
#pragma warning disable CA1806

namespace EnterpriseUI.WinForms;

/// <summary>Microphone capture through the Windows multimedia API, delivering 16-bit PCM in 50 ms chunks.</summary>
public sealed class WaveInAudioInput : IAudioInput
{
    private const uint WaveMapper = 0xFFFFFFFF, CallbackEvent = 0x50000, WhdrDone = 1, MmsyserrNoError = 0;
    private const int BufferCount = 4;

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormatEx
    {
        public ushort FormatTag, Channels;
        public uint SamplesPerSec, AvgBytesPerSec;
        public ushort BlockAlign, BitsPerSample, Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHdr
    {
        public IntPtr Data;
        public uint BufferLength, BytesRecorded;
        public IntPtr User;
        public uint Flags, Loops;
        public IntPtr Next, Reserved;
    }

    [DllImport("winmm.dll")] private static extern uint waveInOpen(out IntPtr handle, uint device, ref WaveFormatEx format, IntPtr callback, IntPtr instance, uint flags);
    [DllImport("winmm.dll")] private static extern uint waveInPrepareHeader(IntPtr handle, IntPtr header, int size);
    [DllImport("winmm.dll")] private static extern uint waveInUnprepareHeader(IntPtr handle, IntPtr header, int size);
    [DllImport("winmm.dll")] private static extern uint waveInAddBuffer(IntPtr handle, IntPtr header, int size);
    [DllImport("winmm.dll")] private static extern uint waveInStart(IntPtr handle);
    [DllImport("winmm.dll")] private static extern uint waveInStop(IntPtr handle);
    [DllImport("winmm.dll")] private static extern uint waveInReset(IntPtr handle);
    [DllImport("winmm.dll")] private static extern uint waveInClose(IntPtr handle);

    private readonly IntPtr[] _headers = new IntPtr[BufferCount];
    private readonly IntPtr[] _buffers = new IntPtr[BufferCount];
    private readonly AutoResetEvent _signal = new(false);
    private readonly object _gate = new();
    private IntPtr _handle;
    private Thread? _pump;
    private volatile bool _running;

    public AudioFormat Format => AudioFormat.Speech16kMono;

    public event Action<byte[]>? DataAvailable;

    public void Start()
    {
        lock (_gate)
        {
            if (_running) return;
            var format = new WaveFormatEx
            {
                FormatTag = 1,
                Channels = (ushort)Format.Channels,
                SamplesPerSec = (uint)Format.SampleRate,
                BitsPerSample = (ushort)Format.BitsPerSample,
                BlockAlign = (ushort)(Format.Channels * Format.BitsPerSample / 8),
                AvgBytesPerSec = (uint)Format.BytesPerSecond
            };
            var result = waveInOpen(out _handle, WaveMapper, ref format, _signal.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CallbackEvent);
            if (result != MmsyserrNoError) throw new InvalidOperationException($"No microphone is available (error {result}).");

            var size = Marshal.SizeOf<WaveHdr>();
            var bufferSize = Format.BytesPerSecond / 20;
            for (var i = 0; i < BufferCount; i++)
            {
                _buffers[i] = Marshal.AllocHGlobal(bufferSize);
                _headers[i] = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(new WaveHdr { Data = _buffers[i], BufferLength = (uint)bufferSize }, _headers[i], false);
                waveInPrepareHeader(_handle, _headers[i], size);
                waveInAddBuffer(_handle, _headers[i], size);
            }

            _running = true;
            _pump = new Thread(Pump) { IsBackground = true, Name = "Microphone capture" };
            _pump.Start();
            waveInStart(_handle);
        }
    }

    private void Pump()
    {
        var size = Marshal.SizeOf<WaveHdr>();
        while (_running)
        {
            _signal.WaitOne(100);
            for (var i = 0; i < BufferCount && _running; i++)
            {
                var header = Marshal.PtrToStructure<WaveHdr>(_headers[i]);
                if ((header.Flags & WhdrDone) == 0) continue;
                if (header.BytesRecorded > 0)
                {
                    var data = new byte[header.BytesRecorded];
                    Marshal.Copy(header.Data, data, 0, data.Length);
                    DataAvailable?.Invoke(data);
                }

                lock (_gate)
                {
                    if (_running) waveInAddBuffer(_handle, _headers[i], size);
                }
            }
        }
    }

    public void Stop()
    {
        Thread? pump;
        lock (_gate)
        {
            if (!_running) return;
            _running = false;
            pump = _pump;
            _pump = null;
            waveInStop(_handle);
            waveInReset(_handle);
        }

        _signal.Set();
        pump?.Join(500);
        lock (_gate)
        {
            var size = Marshal.SizeOf<WaveHdr>();
            for (var i = 0; i < BufferCount; i++)
            {
                if (_headers[i] == IntPtr.Zero) continue;
                waveInUnprepareHeader(_handle, _headers[i], size);
                Marshal.FreeHGlobal(_headers[i]);
                Marshal.FreeHGlobal(_buffers[i]);
                _headers[i] = _buffers[i] = IntPtr.Zero;
            }

            waveInClose(_handle);
            _handle = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        Stop();
        _signal.Dispose();
    }
}
