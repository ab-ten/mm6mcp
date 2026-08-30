using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace MindManagerManipulator;

internal sealed record OutlineObservationScanResult(
  string ObservationId,
  long Revision,
  int ChangedNodeCount,
  TimeSpan HashScanElapsed,
  TimeSpan CacheUpdateElapsed);

internal sealed class OutlineObservationTracker
{
  private const string FingerprintDomain = "mm6mcp-semantic-subtree-v1";
  private readonly Dictionary<string, CacheEntry> cache_ = [];
  private string? observationId_;
  private long revision_;

  public string ObservationId =>
    observationId_
    ?? throw new InvalidOperationException(
      "Outline observation has not been initialized.");
  public long Revision => revision_;

  public void Clear() {
    cache_.Clear();
    observationId_ = null;
    revision_ = 0;
  }

  public TimeSpan Initialize(OutlineNode root) {
    Stopwatch stopwatch = Stopwatch.StartNew();
    Dictionary<string, ulong> fingerprints = [];
    BuildFingerprints(root, fingerprints);
    cache_.Clear();

    foreach (KeyValuePair<string, ulong> item in fingerprints) {
      cache_.Add(item.Key, new CacheEntry(item.Value, 0));
    }

    observationId_ = CreateObservationId();
    revision_ = 0;

    stopwatch.Stop();
    return stopwatch.Elapsed;
  }

  public OutlineObservationScanResult Scan(OutlineNode root) {
    string observationId = ObservationId;
    Stopwatch hashScanStopwatch = Stopwatch.StartNew();
    Dictionary<string, ulong> currentFingerprints = [];
    HashSet<string> changedIds = [];

    ScanNode(root, currentFingerprints, changedIds);
    hashScanStopwatch.Stop();
    Stopwatch cacheUpdateStopwatch = Stopwatch.StartNew();

    if (changedIds.Count > 0) {
      ++revision_;
    }

    foreach (KeyValuePair<string, ulong> item in currentFingerprints) {
      if (cache_.TryGetValue(item.Key, out CacheEntry? cached)) {
        cached.Fingerprint = item.Value;
        if (changedIds.Contains(item.Key)) {
          cached.SubtreeRevision = revision_;
        }
      }else {
        cache_.Add(item.Key, new CacheEntry(item.Value, revision_));
      }
    }

    List<string> removedIds =
      cache_.Keys
        .Where(id => !currentFingerprints.ContainsKey(id))
        .ToList();

    foreach (string id in removedIds) {
      cache_.Remove(id);
    }

    cacheUpdateStopwatch.Stop();

    return new OutlineObservationScanResult(
      observationId,
      revision_,
      changedIds.Count,
      hashScanStopwatch.Elapsed,
      cacheUpdateStopwatch.Elapsed);
  }

  public long GetSubtreeRevision(string id) {
    if (!cache_.TryGetValue(id, out CacheEntry? entry)) {
      throw new InvalidOperationException(
        $"No observation cache entry exists for topic {id}.");
    }

    return entry.SubtreeRevision;
  }

  private bool ScanNode(
    OutlineNode node,
    Dictionary<string, ulong> currentFingerprints,
    HashSet<string> changedIds)
  {
    List<ulong> childFingerprints = new(node.Children.Count);

    bool descendantChanged = false;

    foreach (OutlineNode child in node.Children) {
      bool childChanged = ScanNode(child, currentFingerprints, changedIds);
      childFingerprints.Add(currentFingerprints[child.Id]);
      descendantChanged |= childChanged;
    }

    ulong currentFingerprint = ComputeFingerprint(node, childFingerprints);
    currentFingerprints.Add(node.Id, currentFingerprint);

    //
    // A detected descendant change is sufficient to prove that this
    // complete subtree changed. Do not let an accidental 64-bit
    // fingerprint collision at this ancestor hide that fact.
    //
    bool changed = descendantChanged;

    if (!changed) {
      changed =
        !cache_.TryGetValue(node.Id, out CacheEntry? cached)
        || cached.Fingerprint != currentFingerprint;
    }

    if (changed) {
      changedIds.Add(node.Id);
    }

    return changed;
  }

  private static ulong BuildFingerprints(
    OutlineNode node,
    Dictionary<string, ulong> fingerprints)
  {
    List<ulong> childFingerprints = new(node.Children.Count);
    foreach (OutlineNode child in node.Children) {
      childFingerprints.Add(BuildFingerprints(child, fingerprints));
    }

    ulong fingerprint = ComputeFingerprint(node, childFingerprints);
    fingerprints.Add(node.Id, fingerprint);

    return fingerprint;
  }

  private static ulong ComputeFingerprint(
    OutlineNode node,
    IReadOnlyList<ulong> childFingerprints)
  {
    using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    AppendString(hash, FingerprintDomain);
    AppendString(hash, node.Id);
    AppendString(hash, node.Text);
    AppendInt32(hash, node.IconMarkers.Count);

    foreach (string iconMarker in node.IconMarkers) {
      AppendString(hash, iconMarker);
    }

    AppendInt32(hash, childFingerprints.Count);

    Span<byte> childBuffer = stackalloc byte[sizeof(ulong)];

    foreach (ulong childFingerprint in childFingerprints) {
      BinaryPrimitives.WriteUInt64BigEndian(childBuffer, childFingerprint);
      hash.AppendData(childBuffer);
    }

    byte[] digest = hash.GetHashAndReset();
    return BinaryPrimitives.ReadUInt64BigEndian(digest.AsSpan(0, sizeof(ulong)));
  }

  private static void AppendString(IncrementalHash hash, string value) {
    byte[] bytes = Encoding.UTF8.GetBytes(value);
    AppendInt32(hash, bytes.Length);
    hash.AppendData(bytes);
  }

  private static void AppendInt32(IncrementalHash hash,int value) {
    Span<byte> buffer = stackalloc byte[sizeof(int)];
    BinaryPrimitives.WriteInt32BigEndian(buffer, value);
    hash.AppendData(buffer);
  }

  private static string CreateObservationId() {
    string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
    string nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
    return $"{timestamp}-{nonce}";
  }

  private sealed class CacheEntry {
    public CacheEntry(ulong fingerprint, long subtreeRevision) {
      Fingerprint = fingerprint;
      SubtreeRevision = subtreeRevision;
    }
    public ulong Fingerprint { get; set; }
    public long SubtreeRevision { get; set; }
  }
}
