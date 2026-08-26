namespace MindManagerManipulator;

partial class Form1
{
  private System.ComponentModel.IContainer components = null;

  private Label documentLabel;
  private ComboBox documentComboBox;
  private Button refreshButton;
  private Label centralTopicLabel;
  private TextBox centralTopicTextBox;
  private Label statusLabel;

  protected override void Dispose(bool disposing)
  {
    if (disposing && components is not null) {
      components.Dispose();
    }

    base.Dispose(disposing);
  }

  #region Windows Form Designer generated code

  private void InitializeComponent()
  {
    documentLabel = new Label();
    documentComboBox = new ComboBox();
    refreshButton = new Button();
    centralTopicLabel = new Label();
    centralTopicTextBox = new TextBox();
    statusLabel = new Label();
    SuspendLayout();

    //
    // documentLabel
    //
    documentLabel.AutoSize = true;
    documentLabel.Location = new Point(12, 15);
    documentLabel.Name = "documentLabel";
    documentLabel.Size = new Size(66, 15);
    documentLabel.TabIndex = 0;
    documentLabel.Text = "Document:";

    //
    // documentComboBox
    //
    documentComboBox.DropDownStyle =
      ComboBoxStyle.DropDownList;
    documentComboBox.FormattingEnabled = true;
    documentComboBox.Location = new Point(84, 12);
    documentComboBox.Name = "documentComboBox";
    documentComboBox.Size = new Size(424, 23);
    documentComboBox.TabIndex = 1;
    documentComboBox.SelectedIndexChanged +=
      DocumentComboBox_SelectedIndexChanged;

    //
    // refreshButton
    //
    refreshButton.Location = new Point(514, 11);
    refreshButton.Name = "refreshButton";
    refreshButton.Size = new Size(94, 25);
    refreshButton.TabIndex = 2;
    refreshButton.Text = "Refresh";
    refreshButton.UseVisualStyleBackColor = true;
    refreshButton.Click += RefreshButton_Click;

    //
    // centralTopicLabel
    //
    centralTopicLabel.AutoSize = true;
    centralTopicLabel.Location = new Point(12, 54);
    centralTopicLabel.Name = "centralTopicLabel";
    centralTopicLabel.Size = new Size(78, 15);
    centralTopicLabel.TabIndex = 3;
    centralTopicLabel.Text = "Central topic:";

    //
    // centralTopicTextBox
    //
    centralTopicTextBox.Location = new Point(12, 72);
    centralTopicTextBox.Multiline = true;
    centralTopicTextBox.Name = "centralTopicTextBox";
    centralTopicTextBox.ReadOnly = true;
    centralTopicTextBox.ScrollBars = ScrollBars.Vertical;
    centralTopicTextBox.Size = new Size(596, 110);
    centralTopicTextBox.TabIndex = 4;

    //
    // statusLabel
    //
    statusLabel.AutoEllipsis = true;
    statusLabel.Location = new Point(12, 197);
    statusLabel.Name = "statusLabel";
    statusLabel.Size = new Size(596, 23);
    statusLabel.TabIndex = 5;
    statusLabel.Text = "Not connected.";

    //
    // Form1
    //
    AutoScaleDimensions = new SizeF(7F, 15F);
    AutoScaleMode = AutoScaleMode.Font;
    ClientSize = new Size(620, 232);
    Controls.Add(statusLabel);
    Controls.Add(centralTopicTextBox);
    Controls.Add(centralTopicLabel);
    Controls.Add(refreshButton);
    Controls.Add(documentComboBox);
    Controls.Add(documentLabel);
    FormBorderStyle = FormBorderStyle.FixedDialog;
    MaximizeBox = false;
    Name = "Form1";
    StartPosition = FormStartPosition.CenterScreen;
    Text = "MindManager Manipulator";
    ResumeLayout(false);
    PerformLayout();
  }

  #endregion
}
