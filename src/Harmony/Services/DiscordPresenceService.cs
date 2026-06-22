using Harmony.Helpers;
using Harmony.Models;
using Harmony.Services.Interfaces;

namespace Harmony.Services;

/// <summary>Best-effort Discord Rich Presence via discord-rpc IPC (no extra package).</summary>
public sealed class DiscordPresenceService : IDisposable
{
    private readonly ISettingsService _settings;
    private readonly IAppLog _log;
    private DiscordIpcClient? _client;
    private bool _connected;

    public DiscordPresenceService(ISettingsService settings, IAppLog log)
    {
        _settings = settings;
        _log = log;
    }

    public void Update(Track? track, bool isPlaying)
    {
        if (!_settings.Current.DiscordPresenceEnabled)
        {
            Clear();
            return;
        }

        if (track == null || !isPlaying)
        {
            SetActivity("Browsing", AppBranding.Name, null);
            return;
        }

        var details = $"{track.ArtistName} — {track.Title}";
        if (details.Length > 128) details = details[..128];
        SetActivity(details, "Listening on " + AppBranding.Name, track.ThumbnailUrl);
    }

    public void Clear()
    {
        try { _client?.Clear(); } catch { /* ignore */ }
    }

    private void SetActivity(string details, string state, string? imageUrl)
    {
        try
        {
            _client ??= new DiscordIpcClient(_log);
            if (!_connected)
                _connected = _client.TryConnect();

            if (!_connected) return;
            _client.SetActivity(details, state, imageUrl);
        }
        catch (Exception ex)
        {
            _log.Info($"Discord presence: {ex.Message}");
        }
    }

    public void Dispose()
    {
        try
        {
            Clear();
            _client?.Dispose();
        }
        catch { /* ignore */ }
    }

    /// <summary>Minimal Discord IPC client (pipe discord-ipc-0..9).</summary>
    private sealed class DiscordIpcClient : IDisposable
    {
        private readonly IAppLog _log;
        private System.IO.Pipes.NamedPipeClientStream? _pipe;
        private int _nonce;

        public DiscordIpcClient(IAppLog log) => _log = log;

        public bool TryConnect()
        {
            for (var i = 0; i < 10; i++)
            {
                try
                {
                    var pipe = new System.IO.Pipes.NamedPipeClientStream(
                        ".", $"discord-ipc-{i}", System.IO.Pipes.PipeDirection.InOut);
                    pipe.Connect(300);
                    _pipe = pipe;
                    WriteFrame(0, "{\"v\":1,\"client_id\":\"000000000000000000\"}");
                    return true;
                }
                catch
                {
                    // try next pipe index
                }
            }
            return false;
        }

        public void SetActivity(string details, string state, string? _)
        {
            if (_pipe == null) return;
            var payload =
                "{\"cmd\":\"SET_ACTIVITY\",\"args\":{\"pid\":" + Environment.ProcessId +
                ",\"activity\":{\"details\":" + JsonEscape(details) +
                ",\"state\":" + JsonEscape(state) +
                ",\"timestamps\":{\"start\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() +
                "},\"assets\":{\"large_text\":\"Rezinas Music\"}}},\"nonce\":\"" + NextNonce() + "\"}";
            WriteFrame(1, payload);
        }

        public void Clear()
        {
            if (_pipe == null) return;
            var payload = "{\"cmd\":\"SET_ACTIVITY\",\"args\":{\"pid\":" + Environment.ProcessId +
                          ",\"activity\":null},\"nonce\":\"" + NextNonce() + "\"}";
            WriteFrame(1, payload);
        }

        private void WriteFrame(int opcode, string json)
        {
            if (_pipe == null) return;
            var data = System.Text.Encoding.UTF8.GetBytes(json);
            var header = new byte[8];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0), opcode);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), data.Length);
            _pipe.Write(header);
            _pipe.Write(data);
            _pipe.Flush();
        }

        private string NextNonce() => Interlocked.Increment(ref _nonce).ToString();

        private static string JsonEscape(string value) =>
            "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        public void Dispose() => _pipe?.Dispose();
    }
}
