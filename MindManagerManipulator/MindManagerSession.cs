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
      selectedDocument_ = entry.ComObject;

      return new MindManagerDocumentSelection(entry.DisplayName, centralTopicText);
    }
    catch {
      selectedDocument_ = null;
      throw;
    }
  }

  public void ClearSelection() {
    VerifyAccess();
    selectedDocument_ = null;
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
    XmlOutlineReadResult outline = ReadSelectedOutline();
    OutlineNode start =
      startId is null
        ? outline.Root
        : FindOutlineNode(outline.Root, startId)
          ?? throw new TopicNotExposedException(startId);

    return OutlineReadProjection.Create(start, maxDepth);
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

    OutlineNode? target =
      FindOutlineNode(before.Root, id);

    if (target is null) {
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

    string oldText = target.Text;
    if (oldText != expectedText) {
      throw new TopicTextConflictException(id, expectedText, oldText);
    }

    if (oldText == newText) {
      return new TopicTextUpdateResult(id, oldText, newText, false);
    }

    topic.Text = newText;
    XmlOutlineReadResult after = MindManagerXmlOutlineReader.Read(documentObject);
    OutlineNode? updated = FindOutlineNode(after.Root, id);
    if (updated is null) {
      throw new InvalidOperationException("The topic OId disappeared after the write.");
    }
    if (updated.Text != newText) {
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
    VerifyAccess();
    XmlOutlineReadResult outline = ReadSelectedOutline();
    return OutlineReadProjection.Create(outline.Root, maxDepth);
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

  private static OutlineNode?
  FindOutlineNode(OutlineNode node, string id) {
    if (node.Id == id) {
      return node;
    }

    foreach (OutlineNode child in node.Children) {
      OutlineNode? found = FindOutlineNode(child, id);
      if (found is not null) {
        return found;
      }
    }

    return null;
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
