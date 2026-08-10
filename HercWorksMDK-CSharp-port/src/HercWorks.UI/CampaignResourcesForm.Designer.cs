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
		_tabs = new TabControl();
		_resourcesTab = new TabPage();
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
		_hercUnlocksTab = new TabPage();
		_hercUnlocksGrid = new DataGridView();
		_hercUnlockIdColumn = new DataGridViewTextBoxColumn();
		_hercUnlockNameColumn = new DataGridViewTextBoxColumn();
		_hercUnlockUnlockedColumn = new DataGridViewCheckBoxColumn();
		_squadmatesTab = new TabPage();
		_squadmatesGrid = new DataGridView();
		_sqRoleColumn = new DataGridViewTextBoxColumn();
		_sqIdColumn = new DataGridViewTextBoxColumn();
		_sqNameColumn = new DataGridViewTextBoxColumn();
		_sqBayIdColumn = new DataGridViewTextBoxColumn();
		_sqActiveColumn = new DataGridViewTextBoxColumn();
		_sqRankColumn = new DataGridViewComboBoxColumn();
		_sqCrewRowColumn = new DataGridViewTextBoxColumn();
		_sqUnk2Column = new DataGridViewTextBoxColumn();
		_sqHealthColumn = new DataGridViewTextBoxColumn();
		_sqKillsHercsColumn = new DataGridViewTextBoxColumn();
		_sqKillsFlyersColumn = new DataGridViewTextBoxColumn();
		_sqKillsBuildingColumn = new DataGridViewTextBoxColumn();
		_sqTotalKillHercColumn = new DataGridViewTextBoxColumn();
		_sqTotalKillFlyerColumn = new DataGridViewTextBoxColumn();
		_sqTotalKillBldngColumn = new DataGridViewTextBoxColumn();
		_sqMissionCountColumn = new DataGridViewTextBoxColumn();
		_sqUnk5Column = new DataGridViewTextBoxColumn();
		_inventoryTab = new TabPage();
		_inventoryGrid = new DataGridView();
		_inventoryNameColumn = new DataGridViewTextBoxColumn();
		_inventoryBuildableColumn = new DataGridViewCheckBoxColumn();
		_inventoryQuantityColumn = new DataGridViewTextBoxColumn();
		_hercBayTab = new TabPage();
		_hercBayGrid = new DataGridView();
		_hercBayIdColumn = new DataGridViewTextBoxColumn();
		_hercBayHercColumn = new DataGridViewComboBoxColumn();
		_hercBayBuildPercentColumn = new DataGridViewTextBoxColumn();
		_hercBayBuildStepColumn = new DataGridViewTextBoxColumn();
		_hercBayHardpointMaxColumn = new DataGridViewTextBoxColumn();
		_hercBayActiveSocketsColumn = new DataGridViewTextBoxColumn();
		_hercBayEditColumn = new DataGridViewButtonColumn();
		_statusStrip = new StatusStrip();
		_statusLabel = new ToolStripStatusLabel();
		_menuStrip.SuspendLayout();
		_tabs.SuspendLayout();
		_resourcesTab.SuspendLayout();
		_resourcesGroupBox.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_salvageInput).BeginInit();
		_hercUnlocksTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_hercUnlocksGrid).BeginInit();
		_squadmatesTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_squadmatesGrid).BeginInit();
		_inventoryTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_inventoryGrid).BeginInit();
		_hercBayTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_hercBayGrid).BeginInit();
		_statusStrip.SuspendLayout();
		SuspendLayout();
		//
		// _menuStrip
		//
		_menuStrip.Items.AddRange(new ToolStripItem[] { _fileMenuItem });
		_menuStrip.Location = new Point(0, 0);
		_menuStrip.Name = "_menuStrip";
		_menuStrip.Size = new Size(980, 24);
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
		// _tabs
		//
		_tabs.Controls.Add(_resourcesTab);
		_tabs.Controls.Add(_hercUnlocksTab);
		_tabs.Controls.Add(_squadmatesTab);
		_tabs.Controls.Add(_inventoryTab);
		_tabs.Controls.Add(_hercBayTab);
		_tabs.Dock = DockStyle.Fill;
		_tabs.Location = new Point(0, 24);
		_tabs.Name = "_tabs";
		_tabs.SelectedIndex = 0;
		_tabs.Size = new Size(980, 554);
		_tabs.TabIndex = 1;
		//
		// _resourcesTab
		//
		_resourcesTab.Controls.Add(_resourcesGroupBox);
		_resourcesTab.Location = new Point(4, 24);
		_resourcesTab.Name = "_resourcesTab";
		_resourcesTab.Padding = new Padding(3);
		_resourcesTab.Size = new Size(972, 526);
		_resourcesTab.TabIndex = 0;
		_resourcesTab.Text = "Resources";
		_resourcesTab.UseVisualStyleBackColor = true;
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
		_resourcesGroupBox.Location = new Point(12, 12);
		_resourcesGroupBox.Name = "_resourcesGroupBox";
		_resourcesGroupBox.Size = new Size(460, 260);
		_resourcesGroupBox.TabIndex = 0;
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
		// _hercUnlocksTab
		//
		_hercUnlocksTab.Controls.Add(_hercUnlocksGrid);
		_hercUnlocksTab.Location = new Point(4, 24);
		_hercUnlocksTab.Name = "_hercUnlocksTab";
		_hercUnlocksTab.Padding = new Padding(3);
		_hercUnlocksTab.Size = new Size(972, 526);
		_hercUnlocksTab.TabIndex = 1;
		_hercUnlocksTab.Text = "Herc Unlocks";
		_hercUnlocksTab.UseVisualStyleBackColor = true;
		//
		// _hercUnlocksGrid
		//
		_hercUnlocksGrid.AllowUserToAddRows = false;
		_hercUnlocksGrid.AllowUserToDeleteRows = false;
		_hercUnlocksGrid.AutoGenerateColumns = false;
		_hercUnlocksGrid.Columns.AddRange(new DataGridViewColumn[] {
			_hercUnlockIdColumn, _hercUnlockNameColumn, _hercUnlockUnlockedColumn
		});
		_hercUnlocksGrid.Dock = DockStyle.Fill;
		_hercUnlocksGrid.Location = new Point(3, 3);
		_hercUnlocksGrid.Name = "_hercUnlocksGrid";
		_hercUnlocksGrid.RowHeadersVisible = false;
		_hercUnlocksGrid.Size = new Size(966, 520);
		_hercUnlocksGrid.TabIndex = 0;
		//
		// _hercUnlockIdColumn
		//
		_hercUnlockIdColumn.DataPropertyName = "HercId";
		_hercUnlockIdColumn.HeaderText = "Herc Id";
		_hercUnlockIdColumn.Name = "_hercUnlockIdColumn";
		_hercUnlockIdColumn.ReadOnly = true;
		_hercUnlockIdColumn.Width = 60;
		//
		// _hercUnlockNameColumn
		//
		_hercUnlockNameColumn.DataPropertyName = "HercName";
		_hercUnlockNameColumn.HeaderText = "Herc Name";
		_hercUnlockNameColumn.Name = "_hercUnlockNameColumn";
		_hercUnlockNameColumn.ReadOnly = true;
		_hercUnlockNameColumn.Width = 150;
		//
		// _hercUnlockUnlockedColumn
		//
		_hercUnlockUnlockedColumn.DataPropertyName = "Unlocked";
		_hercUnlockUnlockedColumn.HeaderText = "Unlocked";
		_hercUnlockUnlockedColumn.Name = "_hercUnlockUnlockedColumn";
		_hercUnlockUnlockedColumn.Width = 80;
		//
		// _squadmatesTab
		//
		_squadmatesTab.Controls.Add(_squadmatesGrid);
		_squadmatesTab.Location = new Point(4, 24);
		_squadmatesTab.Name = "_squadmatesTab";
		_squadmatesTab.Padding = new Padding(3);
		_squadmatesTab.Size = new Size(972, 526);
		_squadmatesTab.TabIndex = 2;
		_squadmatesTab.Text = "Squadmates";
		_squadmatesTab.UseVisualStyleBackColor = true;
		//
		// _squadmatesGrid
		//
		_squadmatesGrid.AllowUserToAddRows = false;
		_squadmatesGrid.AllowUserToDeleteRows = false;
		_squadmatesGrid.AutoGenerateColumns = false;
		_squadmatesGrid.Columns.AddRange(new DataGridViewColumn[] {
			_sqRoleColumn, _sqIdColumn, _sqNameColumn, _sqBayIdColumn, _sqActiveColumn, _sqRankColumn,
			_sqCrewRowColumn, _sqUnk2Column, _sqHealthColumn, _sqKillsHercsColumn, _sqKillsFlyersColumn,
			_sqKillsBuildingColumn, _sqTotalKillHercColumn, _sqTotalKillFlyerColumn, _sqTotalKillBldngColumn,
			_sqMissionCountColumn, _sqUnk5Column
		});
		_squadmatesGrid.Dock = DockStyle.Fill;
		_squadmatesGrid.Location = new Point(3, 3);
		_squadmatesGrid.Name = "_squadmatesGrid";
		_squadmatesGrid.RowHeadersVisible = false;
		_squadmatesGrid.Size = new Size(966, 520);
		_squadmatesGrid.TabIndex = 0;
		//
		// _sqRoleColumn
		//
		_sqRoleColumn.DataPropertyName = "Role";
		_sqRoleColumn.HeaderText = "Role";
		_sqRoleColumn.Name = "_sqRoleColumn";
		_sqRoleColumn.ReadOnly = true;
		_sqRoleColumn.Width = 70;
		//
		// _sqIdColumn
		//
		_sqIdColumn.DataPropertyName = "SquadmateId";
		_sqIdColumn.HeaderText = "Id";
		_sqIdColumn.Name = "_sqIdColumn";
		_sqIdColumn.ReadOnly = true;
		_sqIdColumn.Width = 40;
		//
		// _sqNameColumn
		//
		_sqNameColumn.DataPropertyName = "Name";
		_sqNameColumn.HeaderText = "Name";
		_sqNameColumn.Name = "_sqNameColumn";
		_sqNameColumn.Width = 120;
		//
		// _sqBayIdColumn
		//
		_sqBayIdColumn.DataPropertyName = "BayId";
		_sqBayIdColumn.HeaderText = "Bay Id";
		_sqBayIdColumn.Name = "_sqBayIdColumn";
		_sqBayIdColumn.Width = 60;
		//
		// _sqActiveColumn
		//
		_sqActiveColumn.DataPropertyName = "Active";
		_sqActiveColumn.HeaderText = "Active Flag";
		_sqActiveColumn.Name = "_sqActiveColumn";
		_sqActiveColumn.Width = 70;
		//
		// _sqRankColumn
		//
		_sqRankColumn.DataPropertyName = "RankLabel";
		_sqRankColumn.HeaderText = "Rank";
		_sqRankColumn.Name = "_sqRankColumn";
		_sqRankColumn.Width = 100;
		//
		// _sqCrewRowColumn
		//
		_sqCrewRowColumn.DataPropertyName = "CrewRowNum";
		_sqCrewRowColumn.HeaderText = "Crew Row";
		_sqCrewRowColumn.Name = "_sqCrewRowColumn";
		_sqCrewRowColumn.Width = 70;
		//
		// _sqUnk2Column
		//
		_sqUnk2Column.DataPropertyName = "Unk2Uint16";
		_sqUnk2Column.HeaderText = "Unk2";
		_sqUnk2Column.Name = "_sqUnk2Column";
		_sqUnk2Column.Width = 60;
		//
		// _sqHealthColumn
		//
		_sqHealthColumn.DataPropertyName = "ProbablyHealth";
		_sqHealthColumn.HeaderText = "Health?";
		_sqHealthColumn.Name = "_sqHealthColumn";
		_sqHealthColumn.Width = 70;
		//
		// _sqKillsHercsColumn
		//
		_sqKillsHercsColumn.DataPropertyName = "KillsHercs";
		_sqKillsHercsColumn.HeaderText = "Kills (Hercs)";
		_sqKillsHercsColumn.Name = "_sqKillsHercsColumn";
		_sqKillsHercsColumn.Width = 90;
		//
		// _sqKillsFlyersColumn
		//
		_sqKillsFlyersColumn.DataPropertyName = "KillsFlyers";
		_sqKillsFlyersColumn.HeaderText = "Kills (Flyers)";
		_sqKillsFlyersColumn.Name = "_sqKillsFlyersColumn";
		_sqKillsFlyersColumn.Width = 90;
		//
		// _sqKillsBuildingColumn
		//
		_sqKillsBuildingColumn.DataPropertyName = "KillsBuilding";
		_sqKillsBuildingColumn.HeaderText = "Kills (Buildings)";
		_sqKillsBuildingColumn.Name = "_sqKillsBuildingColumn";
		_sqKillsBuildingColumn.Width = 100;
		//
		// _sqTotalKillHercColumn
		//
		_sqTotalKillHercColumn.DataPropertyName = "TotalKillHerc";
		_sqTotalKillHercColumn.HeaderText = "Total Kills (Hercs)";
		_sqTotalKillHercColumn.Name = "_sqTotalKillHercColumn";
		_sqTotalKillHercColumn.Width = 110;
		//
		// _sqTotalKillFlyerColumn
		//
		_sqTotalKillFlyerColumn.DataPropertyName = "TotalKillFlyer";
		_sqTotalKillFlyerColumn.HeaderText = "Total Kills (Flyers)";
		_sqTotalKillFlyerColumn.Name = "_sqTotalKillFlyerColumn";
		_sqTotalKillFlyerColumn.Width = 110;
		//
		// _sqTotalKillBldngColumn
		//
		_sqTotalKillBldngColumn.DataPropertyName = "TotalKillBldng";
		_sqTotalKillBldngColumn.HeaderText = "Total Kills (Buildings)";
		_sqTotalKillBldngColumn.Name = "_sqTotalKillBldngColumn";
		_sqTotalKillBldngColumn.Width = 130;
		//
		// _sqMissionCountColumn
		//
		_sqMissionCountColumn.DataPropertyName = "MissionCount";
		_sqMissionCountColumn.HeaderText = "Missions";
		_sqMissionCountColumn.Name = "_sqMissionCountColumn";
		_sqMissionCountColumn.Width = 70;
		//
		// _sqUnk5Column
		//
		_sqUnk5Column.DataPropertyName = "Unk5Uint16";
		_sqUnk5Column.HeaderText = "Unk5";
		_sqUnk5Column.Name = "_sqUnk5Column";
		_sqUnk5Column.Width = 60;
		//
		// _inventoryTab
		//
		_inventoryTab.Controls.Add(_inventoryGrid);
		_inventoryTab.Location = new Point(4, 24);
		_inventoryTab.Name = "_inventoryTab";
		_inventoryTab.Padding = new Padding(3);
		_inventoryTab.Size = new Size(972, 526);
		_inventoryTab.TabIndex = 3;
		_inventoryTab.Text = "Weapon Inventory";
		_inventoryTab.UseVisualStyleBackColor = true;
		//
		// _inventoryGrid
		//
		_inventoryGrid.AllowUserToAddRows = false;
		_inventoryGrid.AllowUserToDeleteRows = false;
		_inventoryGrid.AutoGenerateColumns = false;
		_inventoryGrid.Columns.AddRange(new DataGridViewColumn[] {
			_inventoryNameColumn, _inventoryBuildableColumn, _inventoryQuantityColumn
		});
		_inventoryGrid.Dock = DockStyle.Fill;
		_inventoryGrid.Location = new Point(3, 3);
		_inventoryGrid.Name = "_inventoryGrid";
		_inventoryGrid.RowHeadersVisible = false;
		_inventoryGrid.Size = new Size(966, 520);
		_inventoryGrid.TabIndex = 0;
		//
		// _inventoryNameColumn
		//
		_inventoryNameColumn.DataPropertyName = "WeaponName";
		_inventoryNameColumn.HeaderText = "Weapon";
		_inventoryNameColumn.Name = "_inventoryNameColumn";
		_inventoryNameColumn.ReadOnly = true;
		_inventoryNameColumn.Width = 150;
		//
		// _inventoryBuildableColumn
		//
		_inventoryBuildableColumn.DataPropertyName = "Buildable";
		_inventoryBuildableColumn.HeaderText = "Buildable";
		_inventoryBuildableColumn.Name = "_inventoryBuildableColumn";
		_inventoryBuildableColumn.Width = 80;
		//
		// _inventoryQuantityColumn
		//
		_inventoryQuantityColumn.DataPropertyName = "Quantity";
		_inventoryQuantityColumn.HeaderText = "Quantity";
		_inventoryQuantityColumn.Name = "_inventoryQuantityColumn";
		_inventoryQuantityColumn.Width = 80;
		//
		// _hercBayTab
		//
		_hercBayTab.Controls.Add(_hercBayGrid);
		_hercBayTab.Location = new Point(4, 24);
		_hercBayTab.Name = "_hercBayTab";
		_hercBayTab.Padding = new Padding(3);
		_hercBayTab.Size = new Size(972, 526);
		_hercBayTab.TabIndex = 4;
		_hercBayTab.Text = "Herc Bay";
		_hercBayTab.UseVisualStyleBackColor = true;
		//
		// _hercBayGrid
		//
		_hercBayGrid.AllowUserToAddRows = false;
		_hercBayGrid.AllowUserToDeleteRows = false;
		_hercBayGrid.AutoGenerateColumns = false;
		_hercBayGrid.Columns.AddRange(new DataGridViewColumn[] {
			_hercBayIdColumn, _hercBayHercColumn, _hercBayBuildPercentColumn, _hercBayBuildStepColumn,
			_hercBayHardpointMaxColumn, _hercBayActiveSocketsColumn, _hercBayEditColumn
		});
		_hercBayGrid.Dock = DockStyle.Fill;
		_hercBayGrid.Location = new Point(3, 3);
		_hercBayGrid.Name = "_hercBayGrid";
		_hercBayGrid.RowHeadersVisible = false;
		_hercBayGrid.Size = new Size(966, 520);
		_hercBayGrid.TabIndex = 0;
		_hercBayGrid.CellClick += OnHercBayCellClick;
		//
		// _hercBayIdColumn
		//
		_hercBayIdColumn.DataPropertyName = "BayId";
		_hercBayIdColumn.HeaderText = "Bay Id";
		_hercBayIdColumn.Name = "_hercBayIdColumn";
		_hercBayIdColumn.ReadOnly = true;
		_hercBayIdColumn.Width = 60;
		//
		// _hercBayHercColumn
		//
		_hercBayHercColumn.DataPropertyName = "Herc";
		_hercBayHercColumn.HeaderText = "Herc";
		_hercBayHercColumn.Name = "_hercBayHercColumn";
		_hercBayHercColumn.Width = 130;
		//
		// _hercBayBuildPercentColumn
		//
		_hercBayBuildPercentColumn.DataPropertyName = "BuildPercent";
		_hercBayBuildPercentColumn.HeaderText = "Build %";
		_hercBayBuildPercentColumn.Name = "_hercBayBuildPercentColumn";
		_hercBayBuildPercentColumn.Width = 70;
		//
		// _hercBayBuildStepColumn
		//
		_hercBayBuildStepColumn.DataPropertyName = "BuildStepNum";
		_hercBayBuildStepColumn.HeaderText = "Build Step";
		_hercBayBuildStepColumn.Name = "_hercBayBuildStepColumn";
		_hercBayBuildStepColumn.Width = 80;
		//
		// _hercBayHardpointMaxColumn
		//
		_hercBayHardpointMaxColumn.DataPropertyName = "HardpointMax";
		_hercBayHardpointMaxColumn.HeaderText = "Hardpoint Max";
		_hercBayHardpointMaxColumn.Name = "_hercBayHardpointMaxColumn";
		_hercBayHardpointMaxColumn.Width = 100;
		//
		// _hercBayActiveSocketsColumn
		//
		_hercBayActiveSocketsColumn.DataPropertyName = "ActiveSocketCount";
		_hercBayActiveSocketsColumn.HeaderText = "Equipped Weapons";
		_hercBayActiveSocketsColumn.Name = "_hercBayActiveSocketsColumn";
		_hercBayActiveSocketsColumn.ReadOnly = true;
		_hercBayActiveSocketsColumn.Width = 120;
		//
		// _hercBayEditColumn
		//
		_hercBayEditColumn.HeaderText = "";
		_hercBayEditColumn.Name = "_hercBayEditColumn";
		_hercBayEditColumn.Text = "Edit Health / Weapons...";
		_hercBayEditColumn.UseColumnTextForButtonValue = true;
		_hercBayEditColumn.Width = 160;
		//
		// _statusStrip
		//
		_statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel });
		_statusStrip.Location = new Point(0, 578);
		_statusStrip.Name = "_statusStrip";
		_statusStrip.Size = new Size(980, 22);
		_statusStrip.TabIndex = 2;
		//
		// _statusLabel
		//
		_statusLabel.Name = "_statusLabel";
		_statusLabel.Text = "No file loaded.";
		//
		// CampaignResourcesForm
		//
		Size = new Size(980, 600);
		Controls.Add(_tabs);
		Controls.Add(_statusStrip);
		Controls.Add(_menuStrip);
		MainMenuStrip = _menuStrip;
		Name = "CampaignResourcesForm";
		Text = "Campaign Resources Editor — Player Save";
		_menuStrip.ResumeLayout(false);
		_menuStrip.PerformLayout();
		_tabs.ResumeLayout(false);
		_resourcesTab.ResumeLayout(false);
		_resourcesGroupBox.ResumeLayout(false);
		_resourcesGroupBox.PerformLayout();
		((System.ComponentModel.ISupportInitialize)_salvageInput).EndInit();
		_hercUnlocksTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_hercUnlocksGrid).EndInit();
		_squadmatesTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_squadmatesGrid).EndInit();
		_inventoryTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_inventoryGrid).EndInit();
		_hercBayTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_hercBayGrid).EndInit();
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
	private TabControl _tabs;
	private TabPage _resourcesTab;
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
	private TabPage _hercUnlocksTab;
	private DataGridView _hercUnlocksGrid;
	private DataGridViewTextBoxColumn _hercUnlockIdColumn;
	private DataGridViewTextBoxColumn _hercUnlockNameColumn;
	private DataGridViewCheckBoxColumn _hercUnlockUnlockedColumn;
	private TabPage _squadmatesTab;
	private DataGridView _squadmatesGrid;
	private DataGridViewTextBoxColumn _sqRoleColumn;
	private DataGridViewTextBoxColumn _sqIdColumn;
	private DataGridViewTextBoxColumn _sqNameColumn;
	private DataGridViewTextBoxColumn _sqBayIdColumn;
	private DataGridViewTextBoxColumn _sqActiveColumn;
	private DataGridViewComboBoxColumn _sqRankColumn;
	private DataGridViewTextBoxColumn _sqCrewRowColumn;
	private DataGridViewTextBoxColumn _sqUnk2Column;
	private DataGridViewTextBoxColumn _sqHealthColumn;
	private DataGridViewTextBoxColumn _sqKillsHercsColumn;
	private DataGridViewTextBoxColumn _sqKillsFlyersColumn;
	private DataGridViewTextBoxColumn _sqKillsBuildingColumn;
	private DataGridViewTextBoxColumn _sqTotalKillHercColumn;
	private DataGridViewTextBoxColumn _sqTotalKillFlyerColumn;
	private DataGridViewTextBoxColumn _sqTotalKillBldngColumn;
	private DataGridViewTextBoxColumn _sqMissionCountColumn;
	private DataGridViewTextBoxColumn _sqUnk5Column;
	private TabPage _inventoryTab;
	private DataGridView _inventoryGrid;
	private DataGridViewTextBoxColumn _inventoryNameColumn;
	private DataGridViewCheckBoxColumn _inventoryBuildableColumn;
	private DataGridViewTextBoxColumn _inventoryQuantityColumn;
	private TabPage _hercBayTab;
	private DataGridView _hercBayGrid;
	private DataGridViewTextBoxColumn _hercBayIdColumn;
	private DataGridViewComboBoxColumn _hercBayHercColumn;
	private DataGridViewTextBoxColumn _hercBayBuildPercentColumn;
	private DataGridViewTextBoxColumn _hercBayBuildStepColumn;
	private DataGridViewTextBoxColumn _hercBayHardpointMaxColumn;
	private DataGridViewTextBoxColumn _hercBayActiveSocketsColumn;
	private DataGridViewButtonColumn _hercBayEditColumn;
	private StatusStrip _statusStrip;
	private ToolStripStatusLabel _statusLabel;
}
