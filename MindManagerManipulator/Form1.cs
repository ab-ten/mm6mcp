namespace MindManagerManipulator;

public partial class Form1 : Form
{
  private object? application_;
  private object? selectedDocument_;

  public Form1()
  {
    InitializeComponent();
  }

  protected override void OnShown(EventArgs e)
  {
    base.OnShown(e);
    RefreshDocuments();
  }

  private object GetApplication()
  {
    if (application_ is not null) {
      return application_;
    }

    Type applicationType =
      Type.GetTypeFromProgID(
        "MindManager.Application.6",
        throwOnError: true)!;

    application_ =
      Activator.CreateInstance(applicationType)
      ?? throw new InvalidOperationException(
        "Failed to create MindManager.Application.6.");

    return application_;
  }

  private void RefreshDocuments()
  {
    selectedDocument_ = null;
    centralTopicTextBox.Clear();
    outlineTextBox.Clear();
    readOutlineButton.Enabled = false;
    measureXmlButton.Enabled = false;

    documentComboBox.BeginUpdate();

    try {
      documentComboBox.Items.Clear();

      dynamic application = GetApplication();
      dynamic documents = application.VisibleDocuments;

      int count = (int)documents.Count;

      for (int i=1; i<=count; ++i) {
        dynamic document = documents[i];

        string name =
          Convert.ToString(document.Name)
          ?? $"Document {i}";

        documentComboBox.Items.Add(
          new DocumentItem(name, (object)document));
      }

      documentComboBox.SelectedIndex = -1;

      statusLabel.Text =
        count == 0
          ? "No visible documents."
          : $"{count} visible document(s). Select one to expose.";
    }
    catch (Exception ex) {
      application_ = null;
      selectedDocument_ = null;

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

  private void RefreshButton_Click(object? sender, EventArgs e)
  {
    RefreshDocuments();
  }

  private void DocumentComboBox_SelectedIndexChanged(
    object? sender,
    EventArgs e)
  {
    selectedDocument_ = null;
    centralTopicTextBox.Clear();
    outlineTextBox.Clear();
    readOutlineButton.Enabled = false;
    measureXmlButton.Enabled = false;

    if (documentComboBox.SelectedItem is not DocumentItem item) {
      return;
    }

    try {
      dynamic document = item.ComObject;
      dynamic centralTopic = document.CentralTopic;

      centralTopicTextBox.Text =
        Convert.ToString(centralTopic.Text) ?? "";

      selectedDocument_ = item.ComObject;
      readOutlineButton.Enabled = true;
      measureXmlButton.Enabled = true;

      statusLabel.Text =
        $"Selected: {item.DisplayName}";
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
    if (selectedDocument_ is null) {
      return;
    }

    readOutlineButton.Enabled = false;
    measureXmlButton.Enabled = false;
    UseWaitCursor = true;

    try {
      OutlineReadResult result =
        MindManagerOutlineReader.Read(
          selectedDocument_);

      outlineTextBox.Text =
        MindManagerOutlineReader.Format(
          result.Root);

      statusLabel.Text =
        $"Read {result.NodeCount} topic(s) " +
        $"in {result.Elapsed.TotalMilliseconds:F1} ms.";
    }
    catch (Exception ex) {
      outlineTextBox.Clear();

      statusLabel.Text =
        "Failed to read document outline.";

      MessageBox.Show(
        this,
        ex.ToString(),
        "MindManager outline error",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);
    }
    finally {
      UseWaitCursor = false;

      measureXmlButton.Enabled = readOutlineButton.Enabled =
        selectedDocument_ is not null;
    }
  }

  private void measureXmlButton_Click(
    object? sender,
    EventArgs e)
  {
    if (selectedDocument_ is null) {
      return;
    }

    try {
      XmlOutlineReadResult result =
        MindManagerXmlOutlineReader.Read(
          selectedDocument_);

      outlineTextBox.Text =
        MindManagerOutlineReader.Format(
          result.Root);

      statusLabel.Text =
        $"XML: {result.NodeCount} topic(s), " +
        $"fetch {result.XmlFetchElapsed.TotalMilliseconds:F1} ms, " +
        $"parse {result.ParseElapsed.TotalMilliseconds:F1} ms, " +
        $"total {(result.XmlFetchElapsed+result.ParseElapsed).TotalMilliseconds:F1} ms.";
    }
    catch (Exception ex) {
      statusLabel.Text =
        "Failed to read XML outline.";

      MessageBox.Show(
        this,
        ex.ToString(),
        "MindManager XML error",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);
    }
  }

  private sealed class DocumentItem
  {
    public DocumentItem(
      string displayName,
      object comObject)
    {
      DisplayName = displayName;
      ComObject = comObject;
    }

    public string DisplayName { get; }
    public object ComObject { get; }

    public override string ToString()
    {
      return DisplayName;
    }
  }
}
