using System.IO;
using System.Text;
using System.Text.Json;
using Harmony.Helpers;
using Harmony.Models;
using Harmony.Services.Interfaces;
using Harmony.Services.Localization;

namespace Harmony.Services;

/// <summary>Best-effort Discord Rich Presence via discord-ipc (no extra package).</summary>
public sealed class DiscordPresenceService : IDisposable
{
    private readonly ISettingsService _settings;
    private readonly IAppLog _log;
    private readonly ILocalizationService _loc;
    private DiscordIpcClient? _client;
    private string? _lastTrackKey;
    private long? _trackStartedUnix;

    public DiscordPresenceService(ISettingsService settings, IAppLog log, ILocalizationService localization)
    {
        _settings = settings;
        _log = log;
        _loc = localization;
    }

    public void Update(Track? track, bool isPlaying)
    {
        if (!_settings.Current.DiscordPresenceEnabled)
        {
            Clear();
            return;
        }

        if (track == null)
        {
            _lastTrackKey = null;
            _trackStartedUnix = null;
            SetActivity(_loc.T("player.discordBrowsing"), AppBranding.Name, null, null, null);
            return;
        }

        var trackKey = $"{(int)track.Source}:{track.SourceId}";
        if (!string.Equals(_lastTrackKey, trackKey, StringComparison.Ordinal))
        {
            _lastTrackKey = trackKey;
            _trackStartedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        var details = $"{track.ArtistName} — {track.Title}";
        if (details.Length > 128) details = details[..128];

        var state = isPlaying
            ? string.Format(_loc.T("player.discordListening"), AppBranding.Name)
            : _loc.T("player.discordPaused");

        var smallText = string.IsNullOrWhiteSpace(track.AlbumName) ? null : track.AlbumName;
        if (smallText is { Length: > 128 }) smallText = smallText[..128];

        SetActivity(
            details,
            state,
            track.ThumbnailUrl,
            smallText,
            isPlaying ? _trackStartedUnix : null);
    }

    public void Clear()
    {
        _lastTrackKey = null;
        _trackStartedUnix = null;
        try { _client?.Clear(); } catch { /* ignore */ }
    }

    private void SetActivity(
        string details,
        string state,
        string? smallImageUrl,
        string? smallText,
        long? startedUnix)
    {
        try
        {
            _client ??= new DiscordIpcClient(_log);
            if (!_client.IsConnected && !_client.TryConnect())
                return;

            _client.SetActivity(details, state, smallImageUrl, smallText, startedUnix);
        }
        catch (Exception ex)
        {
            _log.Info($"Discord presence: {ex.Message}");
            try { _client?.Dispose(); } catch { /* ignore */ }
            _client = null;
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
        _client = null;
    }

    /// <summary>Minimal Discord IPC client (pipe discord-ipc-0..9).</summary>
    private sealed class DiscordIpcClient : IDisposable
    {
        private readonly IAppLog _log;
        private System.IO.Pipes.NamedPipeClientStream? _pipe;
        private int _nonce;

        public DiscordIpcClient(IAppLog log) => _log = log;

        public bool IsConnected => _pipe is { IsConnected: true };

        public bool TryConnect()
        {
            DisposePipe();

            for (var i = 0; i < 10; i++)
            {
                try
                {
                    var pipe = new System.IO.Pipes.NamedPipeClientStream(
                        ".", $"discord-ipc-{i}", System.IO.Pipes.PipeDirection.InOut);
                    pipe.Connect(500);
                    _pipe = pipe;

                    WriteFrame(0, $"{{\"v\":1,\"client_id\":\"{AppBranding.DiscordApplicationId}\"}}");
                    if (!TryReadFrame(out var opcode, out _))
                    {
                        DisposePipe();
                        continue;
                    }

                    if (opcode != 1)
                    {
                        DisposePipe();
                        continue;
                    }

                    _log.Info("Discord Rich Presence connected.");
                    return true;
                }
                catch
                {
                    DisposePipe();
                }
            }

            return false;
        }

        public void SetActivity(
            string details,
            string state,
            string? smallImageUrl,
            string? smallText,
            long? startedUnix)
        {
            if (_pipe == null) return;

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);

            writer.WriteStartObject();
            writer.WriteString("cmd", "SET_ACTIVITY");
            writer.WriteStartObject("args");
            writer.WriteNumber("pid", Environment.ProcessId);
            writer.WriteStartObject("activity");
            writer.WriteString("details", details);
            writer.WriteString("state", state);

            if (startedUnix is long start)
            {
                writer.WriteStartObject("timestamps");
                writer.WriteNumber("start", start);
                writer.WriteEndObject();
            }

            writer.WriteStartObject("assets");
            writer.WriteString("large_image", AppBranding.DiscordLargeImageKey);
            writer.WriteString("large_text", AppBranding.DiscordLargeImageText);

            if (!string.IsNullOrWhiteSpace(smallImageUrl))
            {
                writer.WriteString("small_image", smallImageUrl);
                if (!string.IsNullOrWhiteSpace(smallText))
                    writer.WriteString("small_text", smallText);
            }

            writer.WriteEndObject(); // assets
            writer.WriteEndObject(); // activity
            writer.WriteEndObject(); // args
            writer.WriteString("nonce", NextNonce());
            writer.WriteEndObject();

            writer.Flush();
            WriteFrame(1, Encoding.UTF8.GetString(stream.ToArray()));
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
            var data = Encoding.UTF8.GetBytes(json);
            var header = new byte[8];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0), opcode);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), data.Length);
            _pipe.Write(header);
            _pipe.Write(data);
            _pipe.Flush();
        }

        private bool TryReadFrame(out int opcode, out string json)
        {
            opcode = 0;
            json = string.Empty;
            if (_pipe == null) return false;

            var header = new byte[8];
            var read = _pipe.Read(header, 0, 8);
            if (read < 8) return false;

            opcode = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0));
            var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
            if (length <= 0 || length > 1_048_576) return false;

            var buffer = new byte[length];
            var offset = 0;
            while (offset < length)
            {
                var chunk = _pipe.Read(buffer, offset, length - offset);
                if (chunk <= 0) return false;
                offset += chunk;
            }

            json = Encoding.UTF8.GetString(buffer);
            return true;
        }

        private string NextNonce() => Interlocked.Increment(ref _nonce).ToString();

        private void DisposePipe()
        {
            try { _pipe?.Dispose(); } catch { /* ignore */ }
            _pipe = null;
        }

        public void Dispose() => DisposePipe();
    }
}
