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
    private readonly Dictionary<string, InFlightLoad> _inFlight = new(StringComparer.Ordinal);
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

            // Existing callers may finish, but invalidated work must not be retained.
            foreach (var load in _inFlight.Values)
            {
                load.Cancellation.Cancel();
            }
            _inFlight.Clear();
        }
    }

    private async Task<IconInfo?> GetIconForIdAsync(string? artworkId, CancellationToken cancellationToken)
    {
        if (!IsValidArtworkId(artworkId)) return null;

        var key = artworkId!;
        InFlightLoad load;
        lock (_gate)
        {
            if (TryGetCached(key, out var cached)) return cached;

            if (!_inFlight.TryGetValue(key, out load!) || load.Cancellation.IsCancellationRequested)
            {
                load = new InFlightLoad();
                _inFlight[key] = load;
                load.Task = LoadAndCacheAsync(key, _generation, load);
            }

            load.Waiters++;
        }

        try
        {
            return await load.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                load.Waiters--;
                if (load.Waiters == 0 && !load.Task.IsCompleted)
                {
                    // Shared work is cancelled only after its final interested caller leaves.
                    try { load.Cancellation.Cancel(); }
                    catch (ObjectDisposedException) { /* The shared operation completed concurrently. */ }
                }
            }
        }
    }

    private async Task<IconInfo?> LoadAndCacheAsync(string artworkId, int generation, InFlightLoad load)
    {
        await Task.Yield();
        IconInfo? icon = null;
        string? validPath = null;
        var enteredLoadGate = false;
        try
        {
            var token = load.Cancellation.Token;
            await _loadGate.WaitAsync(token).ConfigureAwait(false);
            enteredLoadGate = true;
            token.ThrowIfCancellationRequested();

            // The bridge resolver is synchronous; cancellation prevents queued work from
            // starting and is checked immediately after an active resolver returns.
            var path = await Task.Run(() => _resolvePath(artworkId), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();

            if (path is not null && await _isDecodable(path, token).ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested();
                validPath = path;
                icon = new IconInfo(new Uri(path).AbsoluteUri);
            }
        }
        catch (OperationCanceledException) when (load.Cancellation.IsCancellationRequested)
        {
            throw;
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
                if (_inFlight.TryGetValue(artworkId, out var current) && ReferenceEquals(current, load))
                {
                    _inFlight.Remove(artworkId);
                }

                // Never negative-cache a transient resolver or decoder failure.
                if (icon is not null && validPath is not null && generation == _generation &&
                    !load.Cancellation.IsCancellationRequested)
                {
                    AddToCache(artworkId, icon, validPath);
                }
            }
            load.Cancellation.Dispose();
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

    private void AddToCache(string artworkId, IconInfo icon, string path)
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
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = await file.OpenReadAsync();
            cancellationToken.ThrowIfCancellationRequested();
            var decoder = await BitmapDecoder.CreateAsync(stream);
            cancellationToken.ThrowIfCancellationRequested();
            var width = decoder.PixelWidth;
            var height = decoder.PixelHeight;
            if (width == 0 || height == 0 ||
                width > MaximumImageDimension || height > MaximumImageDimension ||
                (ulong)width * height > MaximumDecodedPixelCount)
            {
                return false;
            }

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

    private sealed record CacheEntry(string ArtworkId, IconInfo Icon, string Path);

    private sealed class InFlightLoad
    {
        internal CancellationTokenSource Cancellation { get; } = new();
        internal Task<IconInfo?> Task { get; set; } = System.Threading.Tasks.Task.FromResult<IconInfo?>(null);
        internal int Waiters { get; set; }
    }
}
