using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Harmony.Data;

namespace Harmony.Helpers;

/// <summary>Loads cover art off the UI thread with memory + disk cache and downscaling.</summary>
public static class ImageLoader
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    private static readonly ConcurrentDictionary<string, BitmapImage> Memory = new();
    private static readonly SemaphoreSlim Gate = new(6);

    public static readonly DependencyProperty SourceUrlProperty =
        DependencyProperty.RegisterAttached(
            "SourceUrl",
            typeof(string),
            typeof(ImageLoader),
            new PropertyMetadata(null, OnSourceUrlChanged));

    public static readonly DependencyProperty DecodeWidthProperty =
        DependencyProperty.RegisterAttached(
            "DecodeWidth",
            typeof(int),
            typeof(ImageLoader),
            new PropertyMetadata(128));

    public static void SetSourceUrl(Image element, string? value) => element.SetValue(SourceUrlProperty, value);
    public static string? GetSourceUrl(Image element) => (string?)element.GetValue(SourceUrlProperty);
    public static void SetDecodeWidth(Image element, int value) => element.SetValue(DecodeWidthProperty, value);
    public static int GetDecodeWidth(Image element) => (int)element.GetValue(DecodeWidthProperty);

    private static void OnSourceUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image image) return;

        var url = e.NewValue as string;
        image.Source = null;
        if (string.IsNullOrWhiteSpace(url)) return;

        var width = Math.Max(32, GetDecodeWidth(image));
        var key = CacheKey(url, width);

        if (Memory.TryGetValue(key, out var cached))
        {
            image.Source = cached;
            return;
        }

        var disk = DiskPath(key);
        if (File.Exists(disk))
        {
            try
            {
                var fromDisk = LoadBitmapFromFile(disk, width);
                Memory[key] = fromDisk;
                image.Source = fromDisk;
                return;
            }
            catch
            {
                try { File.Delete(disk); } catch { /* ignore */ }
            }
        }

        _ = LoadAsync(image, url, width, key, disk);
    }

    private static async Task LoadAsync(Image image, string url, int width, string key, string diskPath)
    {
        await Gate.WaitAsync();
        try
        {
            if (Memory.TryGetValue(key, out var hit))
            {
                if (GetSourceUrl(image) == url) image.Source = hit;
                return;
            }

            var bytes = await ReadBytesAsync(url);
            if (bytes.Length == 0) return;

            Directory.CreateDirectory(Path.GetDirectoryName(diskPath)!);
            await File.WriteAllBytesAsync(diskPath, bytes);

            await image.Dispatcher.InvokeAsync(() =>
            {
                if (GetSourceUrl(image) != url) return;
                var bmp = CreateBitmap(bytes, width);
                Memory.TryAdd(key, bmp);
                image.Source = bmp;
            });
        }
        catch
        {
            // Placeholder stays visible.
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<byte[]> ReadBytesAsync(string url)
    {
        if (File.Exists(url))
            return await File.ReadAllBytesAsync(url);

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsFile)
            return await File.ReadAllBytesAsync(uri.LocalPath);

        return await Http.GetByteArrayAsync(url);
    }

    private static BitmapImage LoadBitmapFromFile(string path, int width)
    {
        var bytes = File.ReadAllBytes(path);
        return CreateBitmap(bytes, width);
    }

    private static BitmapImage CreateBitmap(byte[] bytes, int width)
    {
        using var stream = new MemoryStream(bytes);
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.StreamSource = stream;
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.DecodePixelWidth = width;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    private static string CacheKey(string url, int width) => $"{width}|{url.Trim()}";

    private static string DiskPath(string key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        return Path.Combine(AppPaths.CacheFolder, "img", hash[..2], hash + ".bin");
    }
}
