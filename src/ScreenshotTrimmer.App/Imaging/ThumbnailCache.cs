using Avalonia.Media.Imaging;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.App.Imaging;

/// <summary>
/// Decodes strip thumbnails off the interface thread, a few at a time, and keeps the most recently used ones by path
/// and content version, so scrolling back or listing the folder again does not decode them again, while an
/// overwritten file gets a fresh thumbnail.
/// </summary>
public sealed class ThumbnailCache(int capacity = 400)
{
    /// <summary>The largest thumbnail, in pixels; the strip shows thumbnails in a box of this size.</summary>
    public const int Width = 200;

    /// <inheritdoc cref="Width"/>
    public const int Height = 150;

    /// <summary><see cref="Width"/> as a layout size, for the strip's markup.</summary>
    public static double BoxWidth => Width;

    /// <summary><see cref="Height"/> as a layout size, for the strip's markup.</summary>
    public static double BoxHeight => Height;

    private readonly SemaphoreSlim _decodeSlots = new(Math.Max(1, Environment.ProcessorCount / 2));
    private readonly Lock _lock = new();
    private readonly Dictionary<Key, LinkedListNode<(Key Key, Bitmap Bitmap)>> _byKey = [];
    private readonly LinkedList<(Key Key, Bitmap Bitmap)> _byUse = [];

    /// <summary>
    /// The thumbnail for <paramref name="path"/> at <paramref name="version"/>, from the cache or decoded off the
    /// interface thread. A load cancelled before its decode starts is dropped without decoding.
    /// </summary>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The file cannot be read.</exception>
    /// <exception cref="InvalidDataException">The file is not an image that can be decoded.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellation"/> was cancelled first.</exception>
    public async Task<Bitmap> GetAsync(string path, ContentVersion version, CancellationToken cancellation)
    {
        var key = new Key(path, version);
        if (TryGet(key) is { } cached)
            return cached;

        var bitmap = await Task.Run(async () =>
        {
            await _decodeSlots.WaitAsync(cancellation);
            try
            {
                // The entry may have scrolled out of view while it waited for a slot.
                cancellation.ThrowIfCancellationRequested();
                return ThumbnailDecoding.Decode(path, Width, Height);
            }
            finally
            {
                _decodeSlots.Release();
            }
        }, cancellation);

        Add(key, bitmap);
        return bitmap;
    }

    /// <summary>The cached thumbnail for <paramref name="path"/> at <paramref name="version"/>, or <c>null</c>.</summary>
    public Bitmap? TryGet(string path, ContentVersion version) => TryGet(new Key(path, version));

    private Bitmap? TryGet(Key key)
    {
        lock (_lock)
        {
            if (!_byKey.TryGetValue(key, out var node))
                return null;
            _byUse.Remove(node);
            _byUse.AddFirst(node);
            return node.Value.Bitmap;
        }
    }

    private void Add(Key key, Bitmap bitmap)
    {
        lock (_lock)
        {
            if (_byKey.TryGetValue(key, out var existing))
                _byUse.Remove(existing);
            _byKey[key] = _byUse.AddFirst((key, bitmap));

            // Evicted bitmaps are dropped, not disposed: an entry on screen may still be showing one.
            while (_byUse.Count > capacity)
            {
                var oldest = _byUse.Last!;
                _byUse.RemoveLast();
                _byKey.Remove(oldest.Value.Key);
            }
        }
    }

    private readonly record struct Key(string Path, ContentVersion Version);
}
