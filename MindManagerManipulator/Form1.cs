namespace MindManagerManipulator;

public partial class Form1 : Form
{
  public Form1()
  {
    InitializeComponent();
  }

  protected override void OnShown(EventArgs e)
  {
    base.OnShown(e);

    try {
      Type applicationType =
        Type.GetTypeFromProgID("MindManager.Application.6", throwOnError: true)!;

      dynamic application =
        Activator.CreateInstance(applicationType)
        ?? throw new InvalidOperationException("Failed to create MindManager.Application.6.");

      dynamic document = application.ActiveDocument;

      if (document is null) {
        Text = "MindManager Manipulator - no active document";
        return;
      }

      dynamic centralTopic = document.CentralTopic;

      Text = $"MindManager Manipulator - {centralTopic.Text}";
    }
    catch (Exception ex) {
      MessageBox.Show(
        this,
        ex.ToString(),
        "MindManager COM probe failed",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);
    }
  }
}
