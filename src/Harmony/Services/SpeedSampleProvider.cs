using NAudio.Wave;

namespace Harmony.Services;

/// <summary>Changes playback speed via linear interpolation (pitch shifts with speed).</summary>
public sealed class SpeedSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly float _speed;

    public SpeedSampleProvider(ISampleProvider source, float speed)
    {
        _source = source;
        _speed = Math.Clamp(speed, 0.5f, 2f);
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        if (Math.Abs(_speed - 1f) < 0.001f)
            return _source.Read(buffer, offset, count);

        var channels = WaveFormat.Channels;
        var outFrames = count / channels;
        var srcFramesNeeded = (int)Math.Ceiling(outFrames * _speed) + 2;
        var src = new float[srcFramesNeeded * channels];
        var srcRead = _source.Read(src, 0, src.Length);
        var srcFrames = srcRead / channels;

        for (var i = 0; i < outFrames; i++)
        {
            var pos = i * _speed;
            var i0 = (int)pos;
            var frac = pos - i0;
            if (i0 >= srcFrames - 1)
            {
                for (var c = 0; c < channels; c++)
                    buffer[offset + i * channels + c] = 0;
                continue;
            }

            for (var c = 0; c < channels; c++)
            {
                var a = src[i0 * channels + c];
                var b = src[(i0 + 1) * channels + c];
                buffer[offset + i * channels + c] = (float)(a + (b - a) * frac);
            }
        }

        return outFrames * channels;
    }
}
