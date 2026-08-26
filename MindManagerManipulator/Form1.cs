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

    if (documentComboBox.SelectedItem is not DocumentItem item) {
      return;
    }

    try {
      dynamic document = item.ComObject;
      dynamic centralTopic = document.CentralTopic;

      centralTopicTextBox.Text =
        Convert.ToString(centralTopic.Text) ?? "";

      selectedDocument_ = item.ComObject;

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
