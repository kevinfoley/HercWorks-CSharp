namespace HercWorks.UI;

partial class CampaignResourcesForm {
	/// <summary>Required designer variable.</summary>
	private System.ComponentModel.IContainer components = null;

	/// <summary>Clean up any resources being used.</summary>
	protected override void Dispose(bool disposing) {
		if (disposing && (components != null)) {
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	#region Windows Form Designer generated code

	/// <summary>
	/// Required method for Designer support - do not modify the contents of this
	/// method with the code editor.
	/// </summary>
	private void InitializeComponent() {
		_menuStrip = new MenuStrip();
		_fileMenuItem = new ToolStripMenuItem();
		_openMenuItem = new ToolStripMenuItem();
		_saveAsMenuItem = new ToolStripMenuItem();
		_fileMenuSeparator = new ToolStripSeparator();
		_closeMenuItem = new ToolStripMenuItem();
		_resourcesGroupBox = new GroupBox();
		_salvageLabel = new Label();
		_salvageInput = new NumericUpDown();
		_workshopSlot1Label = new Label();
		_workshopSlot1Combo = new ComboBox();
		_workshopSlot2Label = new Label();
		_workshopSlot2Combo = new ComboBox();
		_workshopSlot3Label = new Label();
		_workshopSlot3Combo = new ComboBox();
		_workshopSlot4Label = new Label();
		_workshopSlot4Combo = new ComboBox();
		_workshopSlot5Label = new Label();
		_workshopSlot5Combo = new ComboBox();
		_statusStrip = new StatusStrip();
		_statusLabel = new ToolStripStatusLabel();
		_menuStrip.SuspendLayout();
		_resourcesGroupBox.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_salvageInput).BeginInit();
		_statusStrip.SuspendLayout();
		SuspendLayout();
		//
		// _menuStrip
		//
		_menuStrip.Items.AddRange(new ToolStripItem[] { _fileMenuItem });
		_menuStrip.Location = new Point(0, 0);
		_menuStrip.Name = "_menuStrip";
		_menuStrip.Size = new Size(500, 24);
		_menuStrip.TabIndex = 0;
		//
		// _fileMenuItem
		//
		_fileMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
			_openMenuItem, _saveAsMenuItem, _fileMenuSeparator, _closeMenuItem
		});
		_fileMenuItem.Name = "_fileMenuItem";
		_fileMenuItem.Text = "&File";
		//
		// _openMenuItem
		//
		_openMenuItem.Name = "_openMenuItem";
		_openMenuItem.Text = "&Open Save File...";
		_openMenuItem.Click += OnOpen;
		//
		// _saveAsMenuItem
		//
		_saveAsMenuItem.Name = "_saveAsMenuItem";
		_saveAsMenuItem.Text = "&Save As...";
		_saveAsMenuItem.Click += OnSaveAs;
		//
		// _fileMenuSeparator
		//
		_fileMenuSeparator.Name = "_fileMenuSeparator";
		//
		// _closeMenuItem
		//
		_closeMenuItem.Name = "_closeMenuItem";
		_closeMenuItem.Text = "&Close";
		_closeMenuItem.Click += OnClose;
		//
		// _resourcesGroupBox
		//
		_resourcesGroupBox.Controls.Add(_salvageLabel);
		_resourcesGroupBox.Controls.Add(_salvageInput);
		_resourcesGroupBox.Controls.Add(_workshopSlot1Label);
		_resourcesGroupBox.Controls.Add(_workshopSlot1Combo);
		_resourcesGroupBox.Controls.Add(_workshopSlot2Label);
		_resourcesGroupBox.Controls.Add(_workshopSlot2Combo);
		_resourcesGroupBox.Controls.Add(_workshopSlot3Label);
		_resourcesGroupBox.Controls.Add(_workshopSlot3Combo);
		_resourcesGroupBox.Controls.Add(_workshopSlot4Label);
		_resourcesGroupBox.Controls.Add(_workshopSlot4Combo);
		_resourcesGroupBox.Controls.Add(_workshopSlot5Label);
		_resourcesGroupBox.Controls.Add(_workshopSlot5Combo);
		_resourcesGroupBox.Location = new Point(12, 36);
		_resourcesGroupBox.Name = "_resourcesGroupBox";
		_resourcesGroupBox.Size = new Size(460, 260);
		_resourcesGroupBox.TabIndex = 1;
		_resourcesGroupBox.TabStop = false;
		_resourcesGroupBox.Text = "Campaign Resources";
		//
		// _salvageLabel
		//
		_salvageLabel.AutoSize = true;
		_salvageLabel.Location = new Point(16, 32);
		_salvageLabel.Name = "_salvageLabel";
		_salvageLabel.Size = new Size(100, 15);
		_salvageLabel.TabIndex = 0;
		_salvageLabel.Text = "Salvage Total:";
		//
		// _salvageInput
		//
		_salvageInput.Location = new Point(180, 30);
		_salvageInput.Maximum = new decimal(new int[] { int.MaxValue, 0, 0, 0 });
		_salvageInput.Minimum = new decimal(new int[] { 0, 0, 0, 0 });
		_salvageInput.Name = "_salvageInput";
		_salvageInput.Size = new Size(160, 23);
		_salvageInput.TabIndex = 1;
		//
		// _workshopSlot1Label
		//
		_workshopSlot1Label.AutoSize = true;
		_workshopSlot1Label.Location = new Point(16, 74);
		_workshopSlot1Label.Name = "_workshopSlot1Label";
		_workshopSlot1Label.Size = new Size(100, 15);
		_workshopSlot1Label.TabIndex = 2;
		_workshopSlot1Label.Text = "Workshop Slot 1:";
		//
		// _workshopSlot1Combo
		//
		_workshopSlot1Combo.DropDownStyle = ComboBoxStyle.DropDownList;
		_workshopSlot1Combo.Location = new Point(180, 71);
		_workshopSlot1Combo.Name = "_workshopSlot1Combo";
		_workshopSlot1Combo.Size = new Size(200, 23);
		_workshopSlot1Combo.TabIndex = 3;
		//
		// _workshopSlot2Label
		//
		_workshopSlot2Label.AutoSize = true;
		_workshopSlot2Label.Location = new Point(16, 111);
		_workshopSlot2Label.Name = "_workshopSlot2Label";
		_workshopSlot2Label.Size = new Size(100, 15);
		_workshopSlot2Label.TabIndex = 4;
		_workshopSlot2Label.Text = "Workshop Slot 2:";
		//
		// _workshopSlot2Combo
		//
		_workshopSlot2Combo.DropDownStyle = ComboBoxStyle.DropDownList;
		_workshopSlot2Combo.Location = new Point(180, 108);
		_workshopSlot2Combo.Name = "_workshopSlot2Combo";
		_workshopSlot2Combo.Size = new Size(200, 23);
		_workshopSlot2Combo.TabIndex = 5;
		//
		// _workshopSlot3Label
		//
		_workshopSlot3Label.AutoSize = true;
		_workshopSlot3Label.Location = new Point(16, 148);
		_workshopSlot3Label.Name = "_workshopSlot3Label";
		_workshopSlot3Label.Size = new Size(100, 15);
		_workshopSlot3Label.TabIndex = 6;
		_workshopSlot3Label.Text = "Workshop Slot 3:";
		//
		// _workshopSlot3Combo
		//
		_workshopSlot3Combo.DropDownStyle = ComboBoxStyle.DropDownList;
		_workshopSlot3Combo.Location = new Point(180, 145);
		_workshopSlot3Combo.Name = "_workshopSlot3Combo";
		_workshopSlot3Combo.Size = new Size(200, 23);
		_workshopSlot3Combo.TabIndex = 7;
		//
		// _workshopSlot4Label
		//
		_workshopSlot4Label.AutoSize = true;
		_workshopSlot4Label.Location = new Point(16, 185);
		_workshopSlot4Label.Name = "_workshopSlot4Label";
		_workshopSlot4Label.Size = new Size(100, 15);
		_workshopSlot4Label.TabIndex = 8;
		_workshopSlot4Label.Text = "Workshop Slot 4:";
		//
		// _workshopSlot4Combo
		//
		_workshopSlot4Combo.DropDownStyle = ComboBoxStyle.DropDownList;
		_workshopSlot4Combo.Location = new Point(180, 182);
		_workshopSlot4Combo.Name = "_workshopSlot4Combo";
		_workshopSlot4Combo.Size = new Size(200, 23);
		_workshopSlot4Combo.TabIndex = 9;
		//
		// _workshopSlot5Label
		//
		_workshopSlot5Label.AutoSize = true;
		_workshopSlot5Label.Location = new Point(16, 222);
		_workshopSlot5Label.Name = "_workshopSlot5Label";
		_workshopSlot5Label.Size = new Size(100, 15);
		_workshopSlot5Label.TabIndex = 10;
		_workshopSlot5Label.Text = "Workshop Slot 5:";
		//
		// _workshopSlot5Combo
		//
		_workshopSlot5Combo.DropDownStyle = ComboBoxStyle.DropDownList;
		_workshopSlot5Combo.Location = new Point(180, 219);
		_workshopSlot5Combo.Name = "_workshopSlot5Combo";
		_workshopSlot5Combo.Size = new Size(200, 23);
		_workshopSlot5Combo.TabIndex = 11;
		//
		// _statusStrip
		//
		_statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel });
		_statusStrip.Location = new Point(0, 378);
		_statusStrip.Name = "_statusStrip";
		_statusStrip.Size = new Size(500, 22);
		_statusStrip.TabIndex = 2;
		//
		// _statusLabel
		//
		_statusLabel.Name = "_statusLabel";
		_statusLabel.Text = "No file loaded.";
		//
		// CampaignResourcesForm
		//
		Size = new Size(500, 400);
		Controls.Add(_resourcesGroupBox);
		Controls.Add(_statusStrip);
		Controls.Add(_menuStrip);
		MainMenuStrip = _menuStrip;
		Name = "CampaignResourcesForm";
		Text = "Campaign Resources Editor — Player Save";
		_menuStrip.ResumeLayout(false);
		_menuStrip.PerformLayout();
		_resourcesGroupBox.ResumeLayout(false);
		_resourcesGroupBox.PerformLayout();
		((System.ComponentModel.ISupportInitialize)_salvageInput).EndInit();
		_statusStrip.ResumeLayout(false);
		_statusStrip.PerformLayout();
		ResumeLayout(false);
		PerformLayout();
	}

	#endregion

	private MenuStrip _menuStrip;
	private ToolStripMenuItem _fileMenuItem;
	private ToolStripMenuItem _openMenuItem;
	private ToolStripMenuItem _saveAsMenuItem;
	private ToolStripSeparator _fileMenuSeparator;
	private ToolStripMenuItem _closeMenuItem;
	private GroupBox _resourcesGroupBox;
	private Label _salvageLabel;
	private NumericUpDown _salvageInput;
	private Label _workshopSlot1Label;
	private ComboBox _workshopSlot1Combo;
	private Label _workshopSlot2Label;
	private ComboBox _workshopSlot2Combo;
	private Label _workshopSlot3Label;
	private ComboBox _workshopSlot3Combo;
	private Label _workshopSlot4Label;
	private ComboBox _workshopSlot4Combo;
	private Label _workshopSlot5Label;
	private ComboBox _workshopSlot5Combo;
	private StatusStrip _statusStrip;
	private ToolStripStatusLabel _statusLabel;
}
