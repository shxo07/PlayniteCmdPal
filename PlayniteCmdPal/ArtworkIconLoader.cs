using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal.Core;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace PlayniteCmdPal;

internal sealed class ArtworkIconLoader
{
    private const uint PreviewEdge = 128;
    private const uint MaximumImageDimension = 16384;
    private const ulong MaximumDecodedPixelCount = 40_000_000;
    private const int DefaultCacheCapacity = 96;

    internal static IconInfo FallbackIcon { get; } = new("\uE7FC");

    private readonly Func<string, string?> _resolvePath;
    private readonly Func<string, CancellationToken, Task<bool>> _isDecodable;
    private readonly Func<string, bool> _fileExists;
    private readonly int _cacheCapacity;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _loadGate = new(4, 4);
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _cache = new(StringComparer.Ordinal);
    private readonly LinkedList<CacheEntry> _leastRecentlyUsed = new();
    private readonly Dictionary<string, Task<IconInfo?>> _inFlight = new(StringComparer.Ordinal);
    private int _generation;

    internal ArtworkIconLoader(
        Func<string, string?> resolvePath,
        Func<string, CancellationToken, Task<bool>>? isDecodable = null,
        int cacheCapacity = DefaultCacheCapacity,
        Func<string, bool>? fileExists = null)
    {
        _resolvePath = resolvePath ?? throw new ArgumentNullException(nameof(resolvePath));
        _isDecodable = isDecodable ?? IsDecodableImageAsync;
        _fileExists = fileExists ?? File.Exists;
        if (cacheCapacity < 1) throw new ArgumentOutOfRangeException(nameof(cacheCapacity));
        _cacheCapacity = cacheCapacity;
    }

    internal async Task<IconInfo?> GetIconAsync(GameEntry game, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();

        // Prefer cover art for recognition, then fall back to the game's icon.
        var cover = await GetIconForIdAsync(game.CoverArtworkReference, cancellationToken).ConfigureAwait(false);
        return cover ?? await GetIconForIdAsync(game.IconReference, cancellationToken).ConfigureAwait(false);
    }

    internal void Invalidate()
    {
        lock (_gate)
        {
            _generation++;
            _cache.Clear();
            _leastRecentlyUsed.Clear();
        }
    }

    private async Task<IconInfo?> GetIconForIdAsync(string? artworkId, CancellationToken cancellationToken)
    {
        if (!IsValidArtworkId(artworkId)) return null;

        Task<IconInfo?> load;
        lock (_gate)
        {
            if (TryGetCached(artworkId!, out var cached)) return cached;

            var generation = _generation;
            var key = $"{generation}:{artworkId}";
            if (!_inFlight.TryGetValue(key, out var existing))
            {
                load = LoadAndCacheAsync(artworkId!, generation, key);
                _inFlight.Add(key, load);
            }
            else
            {
                load = existing;
            }
        }

        return await load.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IconInfo?> LoadAndCacheAsync(string artworkId, int generation, string inFlightKey)
    {
        // Ensure the in-flight entry is published before this load can complete.
        await Task.Yield();

        IconInfo? icon = null;
        string? validPath = null;
        var enteredLoadGate = false;
        try
        {
            // Bound resolver reads and decoder work across overlapping search requests.
            await _loadGate.WaitAsync().ConfigureAwait(false);
            enteredLoadGate = true;

            // The bridge resolver verifies ID, containment, signature, size, and SHA-256.
            var path = await Task.Run(() => _resolvePath(artworkId)).ConfigureAwait(false);
            if (path is not null && await _isDecodable(path, CancellationToken.None).ConfigureAwait(false))
            {
                validPath = path;
                icon = new IconInfo(new Uri(path).AbsoluteUri);
            }
        }
        catch (Exception)
        {
            // Artwork is optional and must never affect search or launch.
        }
        finally
        {
            if (enteredLoadGate) _loadGate.Release();
            lock (_gate)
            {
                _inFlight.Remove(inFlightKey);
                if (generation == _generation) AddToCache(artworkId, icon, validPath);
            }
        }

        return icon;
    }

    private bool TryGetCached(string artworkId, out IconInfo? icon)
    {
        if (!_cache.TryGetValue(artworkId, out var node))
        {
            icon = null;
            return false;
        }

        if (node.Value.Path is { } path && !_fileExists(path))
        {
            _leastRecentlyUsed.Remove(node);
            _cache.Remove(artworkId);
            icon = null;
            return false;
        }

        _leastRecentlyUsed.Remove(node);
        _leastRecentlyUsed.AddFirst(node);
        icon = node.Value.Icon;
        return true;
    }

    private void AddToCache(string artworkId, IconInfo? icon, string? path)
    {
        if (_cache.Remove(artworkId, out var existing)) _leastRecentlyUsed.Remove(existing);
        var node = _leastRecentlyUsed.AddFirst(new CacheEntry(artworkId, icon, path));
        _cache.Add(artworkId, node);

        while (_cache.Count > _cacheCapacity)
        {
            var last = _leastRecentlyUsed.Last!;
            _leastRecentlyUsed.RemoveLast();
            _cache.Remove(last.Value.ArtworkId);
        }
    }

    private static bool IsValidArtworkId(string? value) =>
        value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static async Task<bool> IsDecodableImageAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var stream = await file.OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var width = decoder.PixelWidth;
            var height = decoder.PixelHeight;
            if (width == 0 || height == 0 ||
                width > MaximumImageDimension || height > MaximumImageDimension ||
                (ulong)width * height > MaximumDecodedPixelCount)
            {
                return false;
            }

            // Force a real decode, but retain only a small preview-sized pixel buffer.
            var scale = Math.Min(1d, PreviewEdge / (double)Math.Max(width, height));
            var transform = new BitmapTransform
            {
                ScaledWidth = Math.Max(1u, (uint)Math.Round(width * scale)),
                ScaledHeight = Math.Max(1u, (uint)Math.Round(height * scale)),
            };
            var decoded = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.DoNotColorManage);

            cancellationToken.ThrowIfCancellationRequested();
            var pixels = decoded.DetachPixelData();
            return pixels.Length > 0 && pixels.Length <= PreviewEdge * PreviewEdge * 4;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private sealed record CacheEntry(string ArtworkId, IconInfo? Icon, string? Path);
}
