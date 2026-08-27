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

internal sealed class MindManagerSession
{
  private const string ApplicationProgId = "MindManager.Application.6";
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
