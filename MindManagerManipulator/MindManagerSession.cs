using System.Diagnostics;
namespace MindManagerManipulator;

internal sealed record MindManagerDocumentChoice(
  long Token,
  string DisplayName)
{
  public override string ToString() {
    return DisplayName;
  }
}

internal sealed record MindManagerDocumentSelection(
  string DisplayName,
  string CentralTopicText);

internal sealed record TopicTextUpdateResult(
  string Id,
  string OldText,
  string NewText,
  bool Changed);

internal sealed record FindTopicResult(
  string ObservationId,
  long Revision,
  OutlineTopicSearchNode? ContextRoot,
  int TotalHitCount,
  int ReturnedHitCount,
  int ReturnedNodeCount,
  bool Truncated);

internal sealed class TopicTextConflictException
: Exception
{
  public TopicTextConflictException(
    string id,
    string expectedText,
    string actualText)
    : base($"Topic {id} text changed since it was read.")
  {
    Id = id;
    ExpectedText = expectedText;
    ActualText = actualText;
  }

  public string Id { get; }
  public string ExpectedText { get; }
  public string ActualText { get; }
}

internal sealed class TopicNotExposedException
: Exception
{
  public TopicNotExposedException(string id)
    : base($"Topic {id} is not part of the exposed outline.") {
    Id = id;
  }

  public string Id { get; }
}

internal sealed class MindManagerSession
{
  private const string ApplicationProgId = "MindManager.Application.6";
  private const int MmRangeAllTopics = 2;
  private readonly int ownerThreadId_ = Environment.CurrentManagedThreadId;
  private readonly Dictionary<long, DocumentEntry> visibleDocuments_ = [];
  private readonly OutlineObservationTracker observationTracker_ = new();
  private object? application_;
  private object? selectedDocument_;
  private long nextDocumentToken_;

  public bool HasSelection {
    get {
      VerifyAccess();
      return selectedDocument_ is not null;
    }
  }

  public IReadOnlyList<MindManagerDocumentChoice>
  RefreshVisibleDocuments() {
    VerifyAccess();

    selectedDocument_ = null;
    visibleDocuments_.Clear();
    observationTracker_.Clear();

    try {
      dynamic application = GetApplication();
      dynamic documents = application.VisibleDocuments;

      int count = (int)documents.Count;

      List<MindManagerDocumentChoice> result = new(count);

      for (int i=1; i<=count; ++i) {
        dynamic document = documents[i];
        string name = Convert.ToString(document.Name) ?? $"Document {i}";
        object documentObject = (object)document;
        long token = ++nextDocumentToken_;
        visibleDocuments_.Add(token, new DocumentEntry(name, documentObject));
        result.Add(new MindManagerDocumentChoice(token, name));
      }

      return result;
    }
    catch {
      ResetConnection();
      throw;
    }
  }

  public MindManagerDocumentSelection
  SelectDocument(long token) {
    VerifyAccess();

    selectedDocument_ = null;
    observationTracker_.Clear();

    if (!visibleDocuments_.TryGetValue(token, out DocumentEntry? entry)
        || entry is null) {
      throw new InvalidOperationException(
          "The selected MindManager document is no longer available.");
    }

    try {
      dynamic document = entry.ComObject;
      dynamic centralTopic = document.CentralTopic;
      string centralTopicText =
        Convert.ToString(centralTopic.Text)
        ?? "";
      XmlOutlineReadResult baseline = MindManagerXmlOutlineReader.Read(entry.ComObject);
      TimeSpan observationElapsed = observationTracker_.Initialize(baseline.Root);
      TimeSpan total = baseline.XmlFetchElapsed + baseline.ParseElapsed + observationElapsed;
      Debug.WriteLine(
        $"Observation baseline {observationTracker_.ObservationId}: " +
        $"{baseline.NodeCount} topic(s), " +
        $"fetch {baseline.XmlFetchElapsed.TotalMilliseconds:F1} ms, " +
        $"parse {baseline.ParseElapsed.TotalMilliseconds:F1} ms, " +
        $"fingerprint {observationElapsed.TotalMilliseconds:F1} ms, " +
        $"total {total.TotalMilliseconds:F1} ms.");
      selectedDocument_ = entry.ComObject;
      return new MindManagerDocumentSelection(entry.DisplayName, centralTopicText);
    }
    catch {
      selectedDocument_ = null;
      observationTracker_.Clear();
      throw;
    }
  }

  public void ClearSelection() {
    VerifyAccess();
    selectedDocument_ = null;
    observationTracker_.Clear();
  }

  public XmlOutlineReadResult ReadSelectedOutline() {
    VerifyAccess();

    object document =
      selectedDocument_
      ?? throw new InvalidOperationException(
        "No MindManager document is selected.");

    return MindManagerXmlOutlineReader.Read(document);
  }

  public OutlineReadProjectionResult
  ReadSelectedOutline(string? startId, int? maxDepth) {
    VerifyAccess();
    if (maxDepth.HasValue && maxDepth.Value < 0) {
      throw new ArgumentOutOfRangeException(nameof(maxDepth), "maxDepth must not be negative.");
    }

    XmlOutlineReadResult outline = ReadSelectedOutline();
    List<OutlineNode> ancestors = [];
    OutlineNode start;
    if (startId is null) {
      start = outline.Root;
    }else if (! TryFindOutlineNode(outline.Root, startId, ancestors, out OutlineNode? found)) {
      throw new TopicNotExposedException(startId);
    }else {
      start = found!;
    }

    List<OutlineAncestorNode> ancestorPath =
      ancestors
      .Select(node => new OutlineAncestorNode(node.Id, node.Text))
      .ToList();

    OutlineObservationScanResult observation = observationTracker_.Scan(outline.Root);
    Stopwatch projectionStopwatch = Stopwatch.StartNew();
    OutlineReadProjectionResult result =
      OutlineReadProjection.Create(
        start,
        ancestorPath,
        maxDepth,
        observation.ObservationId,
        observation.Revision,
        observationTracker_.GetSubtreeRevision);

    projectionStopwatch.Stop();

    TimeSpan total =
      outline.XmlFetchElapsed
      + outline.ParseElapsed
      + observation.HashScanElapsed
      + observation.CacheUpdateElapsed
      + projectionStopwatch.Elapsed;

    Debug.WriteLine(
      $"ReadOutline {observation.ObservationId} " +
      $"revision {observation.Revision}, " +
      $"{outline.NodeCount} topic(s), " +
      $"{observation.ChangedNodeCount} changed: " +
      $"fetch {outline.XmlFetchElapsed.TotalMilliseconds:F1} ms, " +
      $"parse {outline.ParseElapsed.TotalMilliseconds:F1} ms, " +
      $"hash-scan {observation.HashScanElapsed.TotalMilliseconds:F1} ms, " +
      $"cache-update {observation.CacheUpdateElapsed.TotalMilliseconds:F1} ms, " +
      $"projection {projectionStopwatch.Elapsed.TotalMilliseconds:F1} ms, " +
      $"total {total.TotalMilliseconds:F1} ms.");

    return result;
  }

  /// <summary>
  /// 現在選択されているMindManager documentのTopic Textを検索する。
  /// </summary>
  ///
  /// <remarks>
  /// startIdを指定した場合はそのTopicを含むsubtreeだけを検索するが、
  /// ContextRootはCentral Topicからhitまでの文脈を保持する。
  /// groupsはOR-of-ANDで、比較はcase-insensitiveなsubstring一致である。
  /// </remarks>
  public FindTopicResult
  FindTopic(
    IReadOnlyList<IReadOnlyList<string>> groups,
    string? startId,
    int maxHits)
  {
    VerifyAccess();

    if (startId == "") {
      throw new ArgumentException("startId must not be empty.", nameof(startId));
    }

    if (maxHits < 1) {
      throw new ArgumentOutOfRangeException(nameof(maxHits), "maxHits must be at least 1.");
    }

    XmlOutlineReadResult outline = ReadSelectedOutline();
    List<OutlineNode> ancestors = [];
    OutlineNode searchRoot;

    if (startId is null) {
      searchRoot = outline.Root;
    }else if (!TryFindOutlineNode(outline.Root, startId, ancestors, out OutlineNode? found)) {
      throw new TopicNotExposedException(startId);
    }else {
      searchRoot = found!;
    }

    OutlineObservationScanResult observation = observationTracker_.Scan(outline.Root);
    Stopwatch searchStopwatch = Stopwatch.StartNew();

    OutlineTopicSearchProjection search =
      OutlineTopicSearch.Search(outline.Root, searchRoot, groups, maxHits);

    searchStopwatch.Stop();

    TimeSpan total =
      outline.XmlFetchElapsed
      + outline.ParseElapsed
      + observation.HashScanElapsed
      + observation.CacheUpdateElapsed
      + searchStopwatch.Elapsed;

    Debug.WriteLine(
      $"FindTopic {observation.ObservationId} " +
      $"revision {observation.Revision}, " +
      $"{outline.NodeCount} topic(s), " +
      $"{search.TotalHitCount} hit(s), " +
      $"{search.ReturnedHitCount} returned hit(s), " +
      $"{search.ReturnedNodeCount} returned node(s): " +
      $"fetch {outline.XmlFetchElapsed.TotalMilliseconds:F1} ms, " +
      $"parse {outline.ParseElapsed.TotalMilliseconds:F1} ms, " +
      $"hash-scan {observation.HashScanElapsed.TotalMilliseconds:F1} ms, " +
      $"cache-update {observation.CacheUpdateElapsed.TotalMilliseconds:F1} ms, " +
      $"search {searchStopwatch.Elapsed.TotalMilliseconds:F1} ms, " +
      $"total {total.TotalMilliseconds:F1} ms.");

    return new FindTopicResult(
      observation.ObservationId,
      observation.Revision,
      search.ContextRoot,
      search.TotalHitCount,
      search.ReturnedHitCount,
      search.ReturnedNodeCount,
      search.Truncated);
  }

  public TopicTextUpdateResult
  UpdateTopicText(
    string id,
    string expectedText,
    string newText)
  {
    VerifyAccess();
    if (string.IsNullOrEmpty(id)) {
      throw new ArgumentException("Topic OId must not be empty.", nameof(id));
    }

    ArgumentNullException.ThrowIfNull(expectedText);
    ArgumentNullException.ThrowIfNull(newText);
    object documentObject =
      selectedDocument_
      ?? throw new InvalidOperationException("No MindManager document is selected.");

    //
    // First verify that the requested topic belongs to
    // the logical outline currently exposed by the GUI.
    //
    XmlOutlineReadResult before =
      MindManagerXmlOutlineReader.Read(documentObject);

    List<OutlineNode> ancestors = [];
    if (! TryFindOutlineNode(before.Root, id, ancestors, out OutlineNode? target)) {
      throw new TopicNotExposedException(id);
    }

    dynamic document = documentObject;
    dynamic range = document.Range(MmRangeAllTopics, true);
    dynamic? topic = range.FindByGuid(id);
    if (topic is null) {
      throw new InvalidOperationException($"FindByGuid failed for {id}.");
    }

    string guid = Convert.ToString(topic.Guid) ?? "";
    if (guid != id) {
      throw new InvalidOperationException($"FindByGuid returned unexpected Guid: {guid}.");
    }

    if (Convert.ToBoolean(topic.IsFloatingTopic())) {
      throw new InvalidOperationException(
        "Logical outline topic unexpectedly reports itself as floating.");
    }

    string oldText = target!.Text;
    if (oldText != expectedText) {
      throw new TopicTextConflictException(id, expectedText, oldText);
    }

    if (oldText == newText) {
      return new TopicTextUpdateResult(id, oldText, newText, false);
    }

    topic.Text = newText;
    XmlOutlineReadResult after = MindManagerXmlOutlineReader.Read(documentObject);
    List<OutlineNode> ancestors2 = [];
    if (! TryFindOutlineNode(before.Root, id, ancestors2, out OutlineNode? updated)) {
      throw new InvalidOperationException("The topic OId disappeared after the write.");
    }
    if (updated!.Text != newText) {
      throw new InvalidOperationException("Document.Xml does not contain the updated text.");
    }

    return new TopicTextUpdateResult(id, oldText, newText, true);
  }

  public OutlineDocumentInfo ReadSelectedDocumentInfo() {
    VerifyAccess();
    XmlOutlineReadResult outline = ReadSelectedOutline();
    return OutlineDocumentInfoCalculator.Calculate(outline.Root, outline.NodeCount);
  }

  public OutlineReadProjectionResult ReadSelectedOutline(int? maxDepth) {
    return ReadSelectedOutline(null, maxDepth);
  }

  public string ReadSelectedDocumentXml() {
    VerifyAccess();
    dynamic document =
      selectedDocument_
      ?? throw new InvalidOperationException("No MindManager document is selected.");
    return
      Convert.ToString(document.Xml)
      ?? throw new InvalidOperationException("MindManager returned no document XML.");
  }

  private static bool
  TryFindOutlineNode(
    OutlineNode node,
    string id,
    List<OutlineNode> ancestorPath,
    out OutlineNode? found)
  {
    if (node.Id == id) {
      found = node;
      return true;
    }

    ancestorPath.Add(node);

    foreach (OutlineNode child in node.Children) {
      if (TryFindOutlineNode(child, id, ancestorPath, out found)) {
        return true;
      }
    }

    ancestorPath.RemoveAt(ancestorPath.Count-1);

    found = null;
    return false;
  }

  private object GetApplication() {
    VerifyAccess();

    if (application_ is not null) {
      return application_;
    }

    Type applicationType = Type.GetTypeFromProgID(ApplicationProgId, throwOnError: true)!;

    application_ =
      Activator.CreateInstance(applicationType)
      ?? throw new InvalidOperationException(
          $"Failed to create {ApplicationProgId}.");

    return application_;
  }

  private void ResetConnection() {
    selectedDocument_ = null;
    visibleDocuments_.Clear();
    application_ = null;
    observationTracker_.Clear();
  }

  private void VerifyAccess() {
    if (Environment.CurrentManagedThreadId != ownerThreadId_) {
      throw new InvalidOperationException(
        "MindManagerSession may only be used from its owning UI thread.");
    }
  }

  private sealed record DocumentEntry(
    string DisplayName,
    object ComObject);
}
