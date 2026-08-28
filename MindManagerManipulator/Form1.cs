namespace MindManagerManipulator;


internal sealed record UiOutlineProbeResult(
  XmlOutlineReadResult Result,
  int CallbackThreadId);


public partial class Form1 : Form
{
  private readonly MindManagerSession session_;

  public Form1() {
    InitializeComponent();
    session_ = new MindManagerSession();
  }

  protected override void OnShown(EventArgs e) {
    base.OnShown(e);
    RefreshDocuments();
  }

  private void RefreshDocuments() {
    ClearSelectedDocumentView();

    documentComboBox.BeginUpdate();

    try {
      documentComboBox.Items.Clear();

      IReadOnlyList<MindManagerDocumentChoice> documents
        = session_.RefreshVisibleDocuments();

      foreach (
          MindManagerDocumentChoice document
          in documents) {
        documentComboBox.Items.Add(document);
      }

      documentComboBox.SelectedIndex = -1;

      statusLabel.Text =
        documents.Count == 0
          ? "No visible documents."
          : $"{documents.Count} visible document(s). Select one to expose.";
    }
    catch (Exception ex) {
      statusLabel.Text = "MindManager connection failed.";
      MessageBox.Show(
        this,
        ex.ToString(),
        "MindManager COM error",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);
    }
    finally {
      documentComboBox.EndUpdate();
    }
  }

  private void RefreshButton_Click(object? sender, EventArgs e) {
    RefreshDocuments();
  }

  private void DocumentComboBox_SelectedIndexChanged(
    object? sender,
    EventArgs e)
  {
    session_.ClearSelection();
    ClearSelectedDocumentView();

    if (documentComboBox.SelectedItem is not MindManagerDocumentChoice item) {
      return;
    }

    try {
      MindManagerDocumentSelection selection = session_.SelectDocument(item.Token);
      centralTopicTextBox.Text = selection.CentralTopicText;
      readOutlineButton.Enabled = true;
      writeProbeButton.Enabled = true;
      statusLabel.Text = $"Selected: {selection.DisplayName}";
    }
    catch (Exception ex) {
      statusLabel.Text = "Failed to read selected document.";
      MessageBox.Show(
        this,
        ex.ToString(),
        "MindManager document error",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);
    }
  }

  private void ReadOutlineButton_Click(
    object? sender,
    EventArgs e)
  {
    if (!session_.HasSelection) {
      return;
    }

    readOutlineButton.Enabled = false;
    writeProbeButton.Enabled = false;
    UseWaitCursor = true;

    try {
      XmlOutlineReadResult result = session_.ReadSelectedOutline();
      outlineTextBox.Text = MindManagerOutlineReader.Format(result.Root);
      TimeSpan total = result.XmlFetchElapsed + result.ParseElapsed;

      statusLabel.Text =
        $"Read {result.NodeCount} topic(s): " +
        $"fetch {result.XmlFetchElapsed.TotalMilliseconds:F1} ms, " +
        $"parse {result.ParseElapsed.TotalMilliseconds:F1} ms, " +
        $"total {total.TotalMilliseconds:F1} ms.";
    }
    catch (Exception ex) {
      outlineTextBox.Clear();

      statusLabel.Text = "Failed to read document outline.";

      MessageBox.Show(
        this,
        ex.ToString(),
        "MindManager outline error",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);
    }
    finally {
      UseWaitCursor = false;
      writeProbeButton.Enabled = readOutlineButton.Enabled = session_.HasSelection;
    }
  }

  private void ClearSelectedDocumentView() {
    centralTopicTextBox.Clear();
    outlineTextBox.Clear();
    readOutlineButton.Enabled = false;
    writeProbeButton.Enabled = false;
  }

  internal Task<UiOutlineProbeResult>
  ReadSelectedOutlineForGrpcProbeAsync(CancellationToken cancellationToken) {
    return this.InvokeAsync(
        () => new UiOutlineProbeResult(
            session_.ReadSelectedOutline(),
            Environment.CurrentManagedThreadId),
        cancellationToken);
  }

  internal Task<TopicTextUpdateResult>
  UpdateTopicTextForGrpcAsync(
    string id,
    string expectedText,
    string newText,
    CancellationToken cancellationToken)
  {
    return this.InvokeAsync(
      () => session_.UpdateTopicText(id, expectedText, newText),
      cancellationToken);
  }

  private void WriteProbeButton_Click(object? sender, EventArgs e) {
    if (!session_.HasSelection) {
      return;
    }

    writeProbeButton.Enabled = false;
    UseWaitCursor = true;

    try {
      XmlOutlineReadResult outline = session_.ReadSelectedOutline();
      OutlineNode target =
        outline.Root.Children.Count > 0
          ? outline.Root.Children[0]
          : outline.Root;

      string newText = target.Text + " [write-probe]";
      TopicTextUpdateResult result = session_.UpdateTopicText(target.Id, target.Text, newText);
      statusLabel.Text =
        $"UpdateTopicText OK: " +
        $"\"{result.OldText}\" -> " +
        $"\"{result.NewText}\".";
    }
    catch (Exception ex) {
      statusLabel.Text = "UpdateTopicText failed.";
      MessageBox.Show(
        this,
        ex.ToString(),
        "MindManager update error",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);
    }
    finally {
      UseWaitCursor = false;
      writeProbeButton.Enabled = session_.HasSelection;
    }
  }
}
