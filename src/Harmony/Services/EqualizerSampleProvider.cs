using NAudio.Dsp;
using NAudio.Wave;

namespace Harmony.Services;

/// <summary>5-band peaking EQ (60 / 250 / 1k / 4k / 12k Hz).</summary>
public sealed class EqualizerSampleProvider : ISampleProvider
{
    private static readonly float[] Frequencies = [60f, 250f, 1000f, 4000f, 12000f];

    private readonly ISampleProvider _source;
    private readonly BiQuadFilter[] _left;
    private readonly BiQuadFilter[] _right;

    public EqualizerSampleProvider(ISampleProvider source, IReadOnlyList<float> bandGainsDb)
    {
        _source = source;
        var rate = source.WaveFormat.SampleRate;
        _left = new BiQuadFilter[5];
        _right = new BiQuadFilter[5];
        for (var i = 0; i < 5; i++)
        {
            var gain = i < bandGainsDb.Count ? bandGainsDb[i] : 0f;
            _left[i] = BiQuadFilter.PeakingEQ(rate, Frequencies[i], 1f, gain);
            _right[i] = BiQuadFilter.PeakingEQ(rate, Frequencies[i], 1f, gain);
        }
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        for (var i = 0; i < read; i += 2)
        {
            var l = buffer[offset + i];
            var r = buffer[offset + i + 1];
            foreach (var f in _left) l = f.Transform(l);
            foreach (var f in _right) r = f.Transform(r);
            buffer[offset + i] = l;
            buffer[offset + i + 1] = r;
        }

        return read;
    }
}
