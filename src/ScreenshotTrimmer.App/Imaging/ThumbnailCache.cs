using Avalonia.Media.Imaging;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.App.Imaging;

/// <summary>
/// Decodes strip thumbnails off the interface thread, a few at a time, and keeps the most recently used ones by path
/// and content version, so scrolling back or listing the folder again does not decode them again, while an
/// overwritten file gets a fresh thumbnail.
/// </summary>
/// <remarks>
/// Thumbnails are handed out as <see cref="Lease"/>s. A thumbnail evicted from the cache is disposed once the last
/// lease on it is released, so a bitmap an entry still shows is never disposed under it.
/// </remarks>
public sealed class ThumbnailCache(int capacity = 400)
{
    /// <summary>The largest thumbnail, in pixels; the strip shows thumbnails in a box of this size.</summary>
    public const double Width = 200;

    /// <inheritdoc cref="Width"/>
    public const double Height = 150;

    private readonly SemaphoreSlim _decodeSlots = new(Math.Max(1, Environment.ProcessorCount / 2));
    private readonly Lock _lock = new();
    private readonly Dictionary<Key, LinkedListNode<Slot>> _byKey = [];
    private readonly LinkedList<Slot> _byUse = [];

    /// <summary>
    /// A lease on the thumbnail for <paramref name="path"/> at <paramref name="version"/>, from the cache or decoded
    /// off the interface thread. A load cancelled before its decode starts is dropped without decoding.
    /// </summary>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The file cannot be read.</exception>
    /// <exception cref="InvalidDataException">The file is not an image that can be decoded.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellation"/> was cancelled first.</exception>
    public Task<Lease> RentAsync(string path, ContentVersion version, CancellationToken cancellation) =>
        RentAsync(path, version, () => ThumbnailDecoding.Decode(path, (int)Width, (int)Height), cancellation);

    /// <summary>
    /// A lease on the thumbnail kept under <paramref name="key"/> at <paramref name="version"/>, from the cache or made
    /// by <paramref name="decode"/> off the interface thread, as <see cref="RentAsync(string, ContentVersion, CancellationToken)"/>
    /// does for a file. <paramref name="key"/> is a path or a <see cref="WorkspaceImage.Key"/>.
    /// </summary>
    public async Task<Lease> RentAsync(string key, ContentVersion version, Func<Bitmap> decode,
                                       CancellationToken cancellation)
    {
        var cacheKey = new Key(key, version);
        if (TryRent(cacheKey) is { } cached)
            return cached;

        var bitmap = await Task.Run(async () =>
        {
            await _decodeSlots.WaitAsync(cancellation);
            try
            {
                // The entry may have scrolled out of view while it waited for a slot.
                cancellation.ThrowIfCancellationRequested();
                return decode();
            }
            finally
            {
                _decodeSlots.Release();
            }
        }, cancellation);

        return Add(cacheKey, bitmap);
    }

    /// <summary>A lease on the cached thumbnail for <paramref name="path"/> at <paramref name="version"/>, or <c>null</c>.</summary>
    public Lease? TryRent(string path, ContentVersion version) => TryRent(new Key(path, version));

    private Lease? TryRent(Key key)
    {
        lock (_lock)
        {
            if (!_byKey.TryGetValue(key, out var node))
                return null;
            _byUse.Remove(node);
            _byUse.AddFirst(node);
            return new Lease(this, node.Value);
        }
    }

    private Lease Add(Key key, Bitmap bitmap)
    {
        lock (_lock)
        {
            if (_byKey.Remove(key, out var existing))
                Evict(existing);
            var slot = new Slot(key, bitmap);
            _byKey[key] = _byUse.AddFirst(slot);
            var lease = new Lease(this, slot);
            while (_byUse.Count > capacity)
            {
                var oldest = _byUse.Last!;
                _byKey.Remove(oldest.Value.Key);
                Evict(oldest);
            }
            return lease;
        }
    }

    /// <summary>Drops <paramref name="node"/> from the cache, disposing its bitmap now if no lease is held on it.</summary>
    private void Evict(LinkedListNode<Slot> node)
    {
        _byUse.Remove(node);
        node.Value.Evicted = true;
        if (node.Value.Leases == 0)
            node.Value.Bitmap.Dispose();
    }

    private void Release(Slot slot)
    {
        lock (_lock)
        {
            if (--slot.Leases == 0 && slot.Evicted)
                slot.Bitmap.Dispose();
        }
    }

    /// <summary>A thumbnail in use; release it when it is no longer shown.</summary>
    public sealed class Lease : IDisposable
    {
        private ThumbnailCache? _cache;
        private readonly Slot _slot;

        internal Lease(ThumbnailCache cache, Slot slot)
        {
            _cache = cache;
            _slot = slot;
            slot.Leases++;
        }

        /// <summary>The thumbnail; valid until this lease is disposed.</summary>
        public Bitmap Bitmap => _slot.Bitmap;

        /// <summary>Releases the thumbnail; disposing twice releases it once.</summary>
        public void Dispose()
        {
            _cache?.Release(_slot);
            _cache = null;
        }
    }

    internal sealed class Slot(Key key, Bitmap bitmap)
    {
        public Key Key { get; } = key;

        public Bitmap Bitmap { get; } = bitmap;

        /// <summary>The leases held on <see cref="Bitmap"/>; changed under the cache's lock.</summary>
        public int Leases { get; set; }

        /// <summary>Whether the cache has dropped this slot, so the bitmap is disposed with its last lease.</summary>
        public bool Evicted { get; set; }
    }

    internal readonly record struct Key(string Path, ContentVersion Version);
}
