namespace HercWorks.UI;

partial class PlayerSquadForm {
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
		_topPanel = new Panel();
		_squadGroupBox = new GroupBox();
		_playerSlotLabel = new Label();
		_playerSlotInput = new NumericUpDown();
		_noteLabel = new Label();
		_squadGrid = new DataGridView();
		_indexColumn = new DataGridViewTextBoxColumn();
		_hercTypeColumn = new DataGridViewComboBoxColumn();
		_slotsColumn = new DataGridViewTextBoxColumn();
		_weaponRefsColumn = new DataGridViewTextBoxColumn();
		_weaponAmmoTypesColumn = new DataGridViewTextBoxColumn();
		_unk00Column = new DataGridViewTextBoxColumn();
		_unk02Column = new DataGridViewTextBoxColumn();
		_unk3AColumn = new DataGridViewTextBoxColumn();
		_buttonPanel = new Panel();
		_addEntryButton = new Button();
		_removeEntryButton = new Button();
		_statusStrip = new StatusStrip();
		_statusLabel = new ToolStripStatusLabel();
		_menuStrip.SuspendLayout();
		_topPanel.SuspendLayout();
		_squadGroupBox.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_playerSlotInput).BeginInit();
		((System.ComponentModel.ISupportInitialize)_squadGrid).BeginInit();
		_buttonPanel.SuspendLayout();
		_statusStrip.SuspendLayout();
		SuspendLayout();
		//
		// _menuStrip
		//
		_menuStrip.Items.AddRange(new ToolStripItem[] { _fileMenuItem });
		_menuStrip.Location = new Point(0, 0);
		_menuStrip.Name = "_menuStrip";
		_menuStrip.Size = new Size(900, 24);
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
		_openMenuItem.Text = "&Open player.mec...";
		_openMenuItem.Click += OnOpen;
		//
		// _saveAsMenuItem
		//
		_saveAsMenuItem.Name = "_saveAsMenuItem";
		_saveAsMenuItem.Text = "Save &As...";
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
		// _topPanel
		//
		_topPanel.Controls.Add(_squadGroupBox);
		_topPanel.Dock = DockStyle.Top;
		_topPanel.Location = new Point(0, 24);
		_topPanel.Name = "_topPanel";
		_topPanel.Padding = new Padding(8);
		_topPanel.Size = new Size(900, 130);
		_topPanel.TabIndex = 1;
		//
		// _squadGroupBox
		//
		_squadGroupBox.Controls.Add(_playerSlotLabel);
		_squadGroupBox.Controls.Add(_playerSlotInput);
		_squadGroupBox.Controls.Add(_noteLabel);
		_squadGroupBox.Dock = DockStyle.Fill;
		_squadGroupBox.Location = new Point(8, 8);
		_squadGroupBox.Name = "_squadGroupBox";
		_squadGroupBox.Size = new Size(884, 114);
		_squadGroupBox.TabIndex = 0;
		_squadGroupBox.TabStop = false;
		_squadGroupBox.Text = "Squad";
		//
		// _playerSlotLabel
		//
		_playerSlotLabel.AutoSize = true;
		_playerSlotLabel.Location = new Point(18, 32);
		_playerSlotLabel.Name = "_playerSlotLabel";
		_playerSlotLabel.Text = "Player pilots entry #:";
		//
		// _playerSlotInput
		//
		_playerSlotInput.Location = new Point(160, 30);
		_playerSlotInput.Name = "_playerSlotInput";
		_playerSlotInput.Size = new Size(80, 23);
		_playerSlotInput.TabIndex = 0;
		//
		// _noteLabel
		//
		_noteLabel.AutoSize = true;
		_noteLabel.Location = new Point(18, 64);
		_noteLabel.Name = "_noteLabel";
		_noteLabel.Text =
			"Weapon ids and their paired values are one list per weapon slot — both lists must stay the same length.\r\n" +
			"The squad spawns at the point script.dat block 11's record 0 carries; nothing else about it comes from script.dat.";
		//
		// _squadGrid
		//
		_squadGrid.AllowUserToAddRows = false;
		_squadGrid.AllowUserToDeleteRows = false;
		_squadGrid.AutoGenerateColumns = false;
		_squadGrid.Columns.AddRange(new DataGridViewColumn[] {
			_indexColumn, _hercTypeColumn, _slotsColumn, _weaponRefsColumn, _weaponAmmoTypesColumn,
			_unk00Column, _unk02Column, _unk3AColumn
		});
		_squadGrid.Dock = DockStyle.Fill;
		_squadGrid.Location = new Point(0, 154);
		_squadGrid.Name = "_squadGrid";
		_squadGrid.RowHeadersVisible = false;
		_squadGrid.Size = new Size(900, 422);
		_squadGrid.TabIndex = 2;
		_squadGrid.DataError += OnGridDataError;
		_squadGrid.CellValueChanged += OnSquadCellChanged;
		//
		// _indexColumn
		//
		_indexColumn.DataPropertyName = "Index";
		_indexColumn.HeaderText = "#";
		_indexColumn.Name = "_indexColumn";
		_indexColumn.ReadOnly = true;
		_indexColumn.Width = 50;
		//
		// _hercTypeColumn
		//
		// Items are filled per load (see PlayerSquadForm.BindHercTypes) so that a type the file
		// carries but MECHS.NAM has no name for still has an entry to select.
		_hercTypeColumn.DataPropertyName = "HercType";
		_hercTypeColumn.DisplayMember = "Label";
		_hercTypeColumn.HeaderText = "Herc Type";
		_hercTypeColumn.Name = "_hercTypeColumn";
		_hercTypeColumn.ValueMember = "Id";
		_hercTypeColumn.Width = 170;
		//
		// _slotsColumn
		//
		_slotsColumn.DataPropertyName = "Slots";
		_slotsColumn.HeaderText = "Slots";
		_slotsColumn.Name = "_slotsColumn";
		_slotsColumn.ReadOnly = true;
		_slotsColumn.Width = 60;
		//
		// _weaponRefsColumn
		//
		_weaponRefsColumn.DataPropertyName = "WeaponRefs";
		_weaponRefsColumn.HeaderText = "Weapon ids (0 = empty slot)";
		_weaponRefsColumn.Name = "_weaponRefsColumn";
		_weaponRefsColumn.Width = 260;
		//
		// _weaponAmmoTypesColumn
		//
		_weaponAmmoTypesColumn.DataPropertyName = "WeaponAmmoTypes";
		_weaponAmmoTypesColumn.HeaderText = "Ammunition type (one per slot)";
		_weaponAmmoTypesColumn.Name = "_weaponAmmoTypesColumn";
		_weaponAmmoTypesColumn.Width = 260;
		//
		// _unk00Column
		//
		_unk00Column.DataPropertyName = "Unk00";
		_unk00Column.HeaderText = "Unk 00";
		_unk00Column.Name = "_unk00Column";
		_unk00Column.Width = 70;
		//
		// _unk02Column
		//
		_unk02Column.DataPropertyName = "Unk02";
		_unk02Column.HeaderText = "Unk 02";
		_unk02Column.Name = "_unk02Column";
		_unk02Column.Width = 70;
		//
		// _unk3AColumn
		//
		_unk3AColumn.DataPropertyName = "Unk3A";
		_unk3AColumn.HeaderText = "Unk 3A";
		_unk3AColumn.Name = "_unk3AColumn";
		_unk3AColumn.Width = 70;
		//
		// _buttonPanel
		//
		_buttonPanel.Controls.Add(_addEntryButton);
		_buttonPanel.Controls.Add(_removeEntryButton);
		_buttonPanel.Dock = DockStyle.Bottom;
		_buttonPanel.Location = new Point(0, 576);
		_buttonPanel.Name = "_buttonPanel";
		_buttonPanel.Padding = new Padding(6);
		_buttonPanel.Size = new Size(900, 52);
		_buttonPanel.TabIndex = 3;
		//
		// _addEntryButton
		//
		_addEntryButton.Location = new Point(8, 10);
		_addEntryButton.Name = "_addEntryButton";
		_addEntryButton.Size = new Size(140, 28);
		_addEntryButton.TabIndex = 0;
		_addEntryButton.Text = "Add Wingman";
		_addEntryButton.UseVisualStyleBackColor = true;
		_addEntryButton.Click += OnAddEntry;
		//
		// _removeEntryButton
		//
		_removeEntryButton.Location = new Point(154, 10);
		_removeEntryButton.Name = "_removeEntryButton";
		_removeEntryButton.Size = new Size(140, 28);
		_removeEntryButton.TabIndex = 1;
		_removeEntryButton.Text = "Remove Entry";
		_removeEntryButton.UseVisualStyleBackColor = true;
		_removeEntryButton.Click += OnRemoveEntry;
		//
		// _statusStrip
		//
		_statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel });
		_statusStrip.Location = new Point(0, 628);
		_statusStrip.Name = "_statusStrip";
		_statusStrip.Size = new Size(900, 22);
		_statusStrip.TabIndex = 4;
		//
		// _statusLabel
		//
		_statusLabel.Name = "_statusLabel";
		_statusLabel.Text = "No player.mec loaded.";
		//
		// PlayerSquadForm
		//
		Size = new Size(900, 650);
		Controls.Add(_squadGrid);
		Controls.Add(_buttonPanel);
		Controls.Add(_topPanel);
		Controls.Add(_statusStrip);
		Controls.Add(_menuStrip);
		MainMenuStrip = _menuStrip;
		Name = "PlayerSquadForm";
		Text = "Player Squad Editor — data\\player.mec";
		_menuStrip.ResumeLayout(false);
		_menuStrip.PerformLayout();
		_topPanel.ResumeLayout(false);
		_squadGroupBox.ResumeLayout(false);
		_squadGroupBox.PerformLayout();
		((System.ComponentModel.ISupportInitialize)_playerSlotInput).EndInit();
		((System.ComponentModel.ISupportInitialize)_squadGrid).EndInit();
		_buttonPanel.ResumeLayout(false);
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
	private Panel _topPanel;
	private GroupBox _squadGroupBox;
	private Label _playerSlotLabel;
	private NumericUpDown _playerSlotInput;
	private Label _noteLabel;
	private DataGridView _squadGrid;
	private DataGridViewTextBoxColumn _indexColumn;
	private DataGridViewComboBoxColumn _hercTypeColumn;
	private DataGridViewTextBoxColumn _slotsColumn;
	private DataGridViewTextBoxColumn _weaponRefsColumn;
	private DataGridViewTextBoxColumn _weaponAmmoTypesColumn;
	private DataGridViewTextBoxColumn _unk00Column;
	private DataGridViewTextBoxColumn _unk02Column;
	private DataGridViewTextBoxColumn _unk3AColumn;
	private Panel _buttonPanel;
	private Button _addEntryButton;
	private Button _removeEntryButton;
	private StatusStrip _statusStrip;
	private ToolStripStatusLabel _statusLabel;
}
