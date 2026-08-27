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
  private Button readOutlineButton;
  private TextBox outlineTextBox;

  protected override void Dispose(bool disposing)
  {
    if (disposing && components is not null) {
      components.Dispose();
    }

    base.Dispose(disposing);
  }

  #region Windows Form Designer generated code

  private void InitializeComponent() {
    documentLabel = new Label();
    documentComboBox = new ComboBox();
    refreshButton = new Button();
    centralTopicLabel = new Label();
    centralTopicTextBox = new TextBox();
    statusLabel = new Label();
    readOutlineButton = new Button();
    outlineTextBox = new TextBox();
    SuspendLayout();

    //
    // documentLabel
    //
    documentLabel.AutoSize = true;
    documentLabel.Location = new Point(10, 12);
    documentLabel.Name = "documentLabel";
    documentLabel.Size = new Size(58, 12);
    documentLabel.TabIndex = 0;
    documentLabel.Text = "Document:";

    //
    // documentComboBox
    //
    documentComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
    documentComboBox.FormattingEnabled = true;
    documentComboBox.Location = new Point(72, 10);
    documentComboBox.Margin = new Padding(3, 2, 3, 2);
    documentComboBox.Name = "documentComboBox";
    documentComboBox.Size = new Size(364, 20);
    documentComboBox.TabIndex = 1;
    documentComboBox.SelectedIndexChanged += DocumentComboBox_SelectedIndexChanged;

    //
    // refreshButton
    //
    refreshButton.Location = new Point(441, 9);
    refreshButton.Margin = new Padding(3, 2, 3, 2);
    refreshButton.Name = "refreshButton";
    refreshButton.Size = new Size(81, 20);
    refreshButton.TabIndex = 2;
    refreshButton.Text = "Refresh";
    refreshButton.UseVisualStyleBackColor = true;
    refreshButton.Click += RefreshButton_Click;

    //
    // centralTopicLabel
    //
    centralTopicLabel.AutoSize = true;
    centralTopicLabel.Location = new Point(10, 43);
    centralTopicLabel.Name = "centralTopicLabel";
    centralTopicLabel.Size = new Size(73, 12);
    centralTopicLabel.TabIndex = 3;
    centralTopicLabel.Text = "Central topic:";

    //
    // centralTopicTextBox
    //
    centralTopicTextBox.Location = new Point(10, 58);
    centralTopicTextBox.Margin = new Padding(3, 2, 3, 2);
    centralTopicTextBox.Multiline = true;
    centralTopicTextBox.Name = "centralTopicTextBox";
    centralTopicTextBox.ReadOnly = true;
    centralTopicTextBox.ScrollBars = ScrollBars.Vertical;
    centralTopicTextBox.Size = new Size(511, 89);
    centralTopicTextBox.TabIndex = 4;

    //
    // statusLabel
    //
    statusLabel.AutoEllipsis = true;
    statusLabel.Location = new Point(10, 149);
    statusLabel.Name = "statusLabel";
    statusLabel.Size = new Size(511, 18);
    statusLabel.TabIndex = 5;
    statusLabel.Text = "Not connected.";

    //
    // readOutlineButton
    //
    readOutlineButton.Enabled = false;
    readOutlineButton.Location = new Point(10, 170);
    readOutlineButton.Name = "readOutlineButton";
    readOutlineButton.Size = new Size(114, 23);
    readOutlineButton.TabIndex = 6;
    readOutlineButton.Text = "Read Outline";
    readOutlineButton.UseVisualStyleBackColor = true;
    readOutlineButton.Click += ReadOutlineButton_Click;

    //
    // outlineTextBox
    //
    outlineTextBox.Location = new Point(10, 199);
    outlineTextBox.Multiline = true;
    outlineTextBox.Name = "outlineTextBox";
    outlineTextBox.ReadOnly = true;
    outlineTextBox.ScrollBars = ScrollBars.Both;
    outlineTextBox.Size = new Size(509, 95);
    outlineTextBox.TabIndex = 7;
    outlineTextBox.WordWrap = false;

    //
    // Form1
    //
    AutoScaleDimensions = new SizeF(6F, 12F);
    AutoScaleMode = AutoScaleMode.Font;
    ClientSize = new Size(531, 306);
    Controls.Add(outlineTextBox);
    Controls.Add(readOutlineButton);
    Controls.Add(statusLabel);
    Controls.Add(centralTopicTextBox);
    Controls.Add(centralTopicLabel);
    Controls.Add(refreshButton);
    Controls.Add(documentComboBox);
    Controls.Add(documentLabel);
    FormBorderStyle = FormBorderStyle.FixedDialog;
    Margin = new Padding(3, 2, 3, 2);
    MaximizeBox = false;
    Name = "Form1";
    StartPosition = FormStartPosition.CenterScreen;
    Text = "MindManager Manipulator";
    ResumeLayout(false);
    PerformLayout();
  }

  #endregion
}
