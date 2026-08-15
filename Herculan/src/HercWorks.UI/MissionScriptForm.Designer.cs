namespace HercWorks.UI;

partial class MissionScriptForm {
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

		_headerTab = new TabPage();
		_headerGroupBox = new GroupBox();
		_theaterLabel = new Label();
		_theaterInput = new NumericUpDown();
		_zoneLabel = new Label();
		_zoneInput = new NumericUpDown();
		_variantLabel = new Label();
		_variantInput = new NumericUpDown();
		_worldLabel = new Label();
		_worldValueLabel = new Label();
		_headerRawLabel = new Label();
		_headerRawText = new TextBox();
		_headerNoteLabel = new Label();

		_pointsTab = new TabPage();
		_pointsGrid = new DataGridView();
		_ptIndexColumn = new DataGridViewTextBoxColumn();
		_ptXColumn = new DataGridViewTextBoxColumn();
		_ptYColumn = new DataGridViewTextBoxColumn();
		_ptZColumn = new DataGridViewTextBoxColumn();

		_headingsTab = new TabPage();
		_headingsGrid = new DataGridView();
		_hdIndexColumn = new DataGridViewTextBoxColumn();
		_hdDegreesColumn = new DataGridViewTextBoxColumn();
		_hdBamColumn = new DataGridViewTextBoxColumn();

		_routesTab = new TabPage();
		_routesGrid = new DataGridView();
		_rtIndexColumn = new DataGridViewTextBoxColumn();
		_rtCountColumn = new DataGridViewTextBoxColumn();
		_rtWaypointsColumn = new DataGridViewTextBoxColumn();

		_linksTab = new TabPage();
		_linksGrid = new DataGridView();
		_lrIndexColumn = new DataGridViewTextBoxColumn();
		_lrTypeColumn = new DataGridViewTextBoxColumn();
		_lrRefAColumn = new DataGridViewTextBoxColumn();
		_lrRefBColumn = new DataGridViewTextBoxColumn();

		_actionsTab = new TabPage();
		_actionsGrid = new DataGridView();
		_acIndexColumn = new DataGridViewTextBoxColumn();
		_acTypeColumn = new DataGridViewTextBoxColumn();
		_acVerbColumn = new DataGridViewTextBoxColumn();
		_acSecondaryColumn = new DataGridViewTextBoxColumn();
		_acTargetColumn = new DataGridViewTextBoxColumn();
		_acRefsColumn = new DataGridViewTextBoxColumn();
		_acLutRefsColumn = new DataGridViewTextBoxColumn();
		_acArrayAColumn = new DataGridViewTextBoxColumn();
		_acArrayBColumn = new DataGridViewTextBoxColumn();

		_actionPairsTab = new TabPage();
		_actionPairsGrid = new DataGridView();
		_apIndexColumn = new DataGridViewTextBoxColumn();
		_apPrimaryColumn = new DataGridViewTextBoxColumn();
		_apTimerColumn = new DataGridViewTextBoxColumn();
		_apSequenceColumn = new DataGridViewTextBoxColumn();

		_mechsTab = new TabPage();
		_mechsGrid = new DataGridView();
		_mechIndexColumn = new DataGridViewTextBoxColumn();
		_mechTypeColumn = new DataGridViewComboBoxColumn();
		_mechPositionColumn = new DataGridViewTextBoxColumn();
		_mechHeadingColumn = new DataGridViewTextBoxColumn();
		_mechWeaponsColumn = new DataGridViewTextBoxColumn();

		_flyersTab = new TabPage();
		_flyersGrid = new DataGridView();
		_flyIndexColumn = new DataGridViewTextBoxColumn();
		_flyTypeColumn = new DataGridViewTextBoxColumn();
		_flyPositionColumn = new DataGridViewTextBoxColumn();
		_flyHeadingColumn = new DataGridViewTextBoxColumn();

		_basesTab = new TabPage();
		_basesGrid = new DataGridView();
		_baseIndexColumn = new DataGridViewTextBoxColumn();
		_baseTypeColumn = new DataGridViewTextBoxColumn();
		_basePositionColumn = new DataGridViewTextBoxColumn();
		_baseHeadingColumn = new DataGridViewTextBoxColumn();

		_routeLinksTab = new TabPage();
		_routeLinksGrid = new DataGridView();
		_rlIndexColumn = new DataGridViewTextBoxColumn();
		_rlSmall1Column = new DataGridViewTextBoxColumn();
		_rlSmall2Column = new DataGridViewTextBoxColumn();
		_rlPointColumn = new DataGridViewTextBoxColumn();
		_rlRouteColumn = new DataGridViewTextBoxColumn();
		_rlDiscriminatorColumn = new DataGridViewTextBoxColumn();
		_rlDiscriminatedRefColumn = new DataGridViewTextBoxColumn();
		_rlActionColumn = new DataGridViewTextBoxColumn();

		_groupsTab = new TabPage();
		_groupsGrid = new DataGridView();
		_grpIndexColumn = new DataGridViewTextBoxColumn();
		_grpRosterColumn = new DataGridViewTextBoxColumn();
		_grpFormationColumn = new DataGridViewTextBoxColumn();
		_grpPointColumn = new DataGridViewTextBoxColumn();
		_grpHeadingColumn = new DataGridViewTextBoxColumn();
		_grpRouteColumn = new DataGridViewTextBoxColumn();
		_grpMembersColumn = new DataGridViewTextBoxColumn();
		_grpRouteLinksColumn = new DataGridViewTextBoxColumn();
		_grpBinaryFlagColumn = new DataGridViewTextBoxColumn();
		_grpTriStateColumn = new DataGridViewTextBoxColumn();
		_grpActionColumn = new DataGridViewTextBoxColumn();

		_entityLinksTab = new TabPage();
		_entityLinksGrid = new DataGridView();
		_elIndexColumn = new DataGridViewTextBoxColumn();
		_elUnk02Column = new DataGridViewTextBoxColumn();
		_elUnk04Column = new DataGridViewTextBoxColumn();
		_elDiscriminatorColumn = new DataGridViewTextBoxColumn();
		_elDiscriminatedRefColumn = new DataGridViewTextBoxColumn();
		_elPointColumn = new DataGridViewTextBoxColumn();
		_elRouteColumn = new DataGridViewTextBoxColumn();
		_elLutRefColumn = new DataGridViewTextBoxColumn();
		_elPairRefsColumn = new DataGridViewTextBoxColumn();
		_elPairTagsColumn = new DataGridViewTextBoxColumn();

		_unlocksTab = new TabPage();
		_unlocksGrid = new DataGridView();
		_unlockValueColumn = new DataGridViewTextBoxColumn();
		_unlocksButtonPanel = new Panel();
		_addUnlockButton = new Button();
		_removeUnlockButton = new Button();

		_statusStrip = new StatusStrip();
		_statusLabel = new ToolStripStatusLabel();

		_menuStrip.SuspendLayout();
		_tabs.SuspendLayout();
		_headerTab.SuspendLayout();
		_headerGroupBox.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_theaterInput).BeginInit();
		((System.ComponentModel.ISupportInitialize)_zoneInput).BeginInit();
		((System.ComponentModel.ISupportInitialize)_variantInput).BeginInit();
		_pointsTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_pointsGrid).BeginInit();
		_headingsTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_headingsGrid).BeginInit();
		_routesTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_routesGrid).BeginInit();
		_linksTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_linksGrid).BeginInit();
		_actionsTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_actionsGrid).BeginInit();
		_actionPairsTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_actionPairsGrid).BeginInit();
		_mechsTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_mechsGrid).BeginInit();
		_flyersTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_flyersGrid).BeginInit();
		_basesTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_basesGrid).BeginInit();
		_routeLinksTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_routeLinksGrid).BeginInit();
		_groupsTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_groupsGrid).BeginInit();
		_entityLinksTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_entityLinksGrid).BeginInit();
		_unlocksTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_unlocksGrid).BeginInit();
		_unlocksButtonPanel.SuspendLayout();
		_statusStrip.SuspendLayout();
		SuspendLayout();
		//
		// _menuStrip
		//
		_menuStrip.Items.AddRange(new ToolStripItem[] { _fileMenuItem });
		_menuStrip.Location = new Point(0, 0);
		_menuStrip.Name = "_menuStrip";
		_menuStrip.Size = new Size(1060, 24);
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
		_openMenuItem.Text = "&Open script.dat...";
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
		// _tabs
		//
		_tabs.Controls.Add(_headerTab);
		_tabs.Controls.Add(_pointsTab);
		_tabs.Controls.Add(_headingsTab);
		_tabs.Controls.Add(_routesTab);
		_tabs.Controls.Add(_linksTab);
		_tabs.Controls.Add(_actionsTab);
		_tabs.Controls.Add(_actionPairsTab);
		_tabs.Controls.Add(_mechsTab);
		_tabs.Controls.Add(_flyersTab);
		_tabs.Controls.Add(_basesTab);
		_tabs.Controls.Add(_routeLinksTab);
		_tabs.Controls.Add(_groupsTab);
		_tabs.Controls.Add(_entityLinksTab);
		_tabs.Controls.Add(_unlocksTab);
		_tabs.Dock = DockStyle.Fill;
		_tabs.Location = new Point(0, 24);
		_tabs.Multiline = true;
		_tabs.Name = "_tabs";
		_tabs.SelectedIndex = 0;
		_tabs.Size = new Size(1060, 604);
		_tabs.TabIndex = 1;
		//
		// _headerTab
		//
		_headerTab.Controls.Add(_headerGroupBox);
		_headerTab.Location = new Point(4, 44);
		_headerTab.Name = "_headerTab";
		_headerTab.Padding = new Padding(3);
		_headerTab.Size = new Size(1052, 556);
		_headerTab.TabIndex = 0;
		_headerTab.Text = "Header";
		_headerTab.UseVisualStyleBackColor = true;
		//
		// _headerGroupBox
		//
		_headerGroupBox.Controls.Add(_theaterLabel);
		_headerGroupBox.Controls.Add(_theaterInput);
		_headerGroupBox.Controls.Add(_zoneLabel);
		_headerGroupBox.Controls.Add(_zoneInput);
		_headerGroupBox.Controls.Add(_variantLabel);
		_headerGroupBox.Controls.Add(_variantInput);
		_headerGroupBox.Controls.Add(_worldLabel);
		_headerGroupBox.Controls.Add(_worldValueLabel);
		_headerGroupBox.Controls.Add(_headerRawLabel);
		_headerGroupBox.Controls.Add(_headerRawText);
		_headerGroupBox.Controls.Add(_headerNoteLabel);
		_headerGroupBox.Location = new Point(12, 12);
		_headerGroupBox.Name = "_headerGroupBox";
		_headerGroupBox.Size = new Size(680, 260);
		_headerGroupBox.TabIndex = 0;
		_headerGroupBox.TabStop = false;
		_headerGroupBox.Text = "Mission Setup";
		//
		// _theaterLabel
		//
		_theaterLabel.AutoSize = true;
		_theaterLabel.Location = new Point(18, 34);
		_theaterLabel.Name = "_theaterLabel";
		_theaterLabel.Text = "Theater (0-4):";
		//
		// _theaterInput
		//
		_theaterInput.Location = new Point(160, 32);
		_theaterInput.Maximum = 4;
		_theaterInput.Name = "_theaterInput";
		_theaterInput.Size = new Size(80, 23);
		_theaterInput.TabIndex = 0;
		_theaterInput.ValueChanged += OnWorldSelectionChanged;
		//
		// _zoneLabel
		//
		_zoneLabel.AutoSize = true;
		_zoneLabel.Location = new Point(18, 68);
		_zoneLabel.Name = "_zoneLabel";
		_zoneLabel.Text = "Zone:";
		//
		// _zoneInput
		//
		_zoneInput.Location = new Point(160, 66);
		_zoneInput.Maximum = short.MaxValue;
		_zoneInput.Name = "_zoneInput";
		_zoneInput.Size = new Size(80, 23);
		_zoneInput.TabIndex = 1;
		//
		// _variantLabel
		//
		_variantLabel.AutoSize = true;
		_variantLabel.Location = new Point(18, 102);
		_variantLabel.Name = "_variantLabel";
		_variantLabel.Text = "Theater variant (0-1):";
		//
		// _variantInput
		//
		_variantInput.Location = new Point(160, 100);
		_variantInput.Maximum = 1;
		_variantInput.Name = "_variantInput";
		_variantInput.Size = new Size(80, 23);
		_variantInput.TabIndex = 2;
		_variantInput.ValueChanged += OnWorldSelectionChanged;
		//
		// _worldLabel
		//
		_worldLabel.AutoSize = true;
		_worldLabel.Location = new Point(18, 136);
		_worldLabel.Name = "_worldLabel";
		_worldLabel.Text = "Resolved world file:";
		//
		// _worldValueLabel
		//
		_worldValueLabel.AutoSize = true;
		_worldValueLabel.Location = new Point(160, 136);
		_worldValueLabel.Name = "_worldValueLabel";
		_worldValueLabel.Text = "wld\\world0.wld";
		//
		// _headerRawLabel
		//
		_headerRawLabel.AutoSize = true;
		_headerRawLabel.Location = new Point(18, 170);
		_headerRawLabel.Name = "_headerRawLabel";
		_headerRawLabel.Text = "Raw header:";
		//
		// _headerRawText
		//
		// Read-only: the remaining header shorts are constant across every real file and nothing is
		// known about them, so they are shown for reference and round-tripped untouched.
		_headerRawText.Location = new Point(160, 168);
		_headerRawText.Name = "_headerRawText";
		_headerRawText.ReadOnly = true;
		_headerRawText.Size = new Size(490, 23);
		_headerRawText.TabIndex = 3;
		//
		// _headerNoteLabel
		//
		_headerNoteLabel.AutoSize = true;
		_headerNoteLabel.Location = new Point(18, 206);
		_headerNoteLabel.Name = "_headerNoteLabel";
		_headerNoteLabel.Text =
			"Records are edited in place — refs between blocks are indexes, so rows cannot be added or removed\r\n" +
			"except on the Unlocks tab, whose block nothing references.";
		//
		// _pointsTab
		//
		_pointsTab.Controls.Add(_pointsGrid);
		_pointsTab.Location = new Point(4, 44);
		_pointsTab.Name = "_pointsTab";
		_pointsTab.Padding = new Padding(3);
		_pointsTab.Size = new Size(1052, 556);
		_pointsTab.TabIndex = 1;
		_pointsTab.Text = "Points";
		_pointsTab.UseVisualStyleBackColor = true;
		//
		// _pointsGrid
		//
		_pointsGrid.AllowUserToAddRows = false;
		_pointsGrid.AllowUserToDeleteRows = false;
		_pointsGrid.AutoGenerateColumns = false;
		_pointsGrid.Columns.AddRange(new DataGridViewColumn[] {
			_ptIndexColumn, _ptXColumn, _ptYColumn, _ptZColumn
		});
		_pointsGrid.Dock = DockStyle.Fill;
		_pointsGrid.Location = new Point(3, 3);
		_pointsGrid.Name = "_pointsGrid";
		_pointsGrid.RowHeadersVisible = false;
		_pointsGrid.Size = new Size(1046, 550);
		_pointsGrid.TabIndex = 0;
		_pointsGrid.DataError += OnGridDataError;
		//
		// _ptIndexColumn
		//
		_ptIndexColumn.DataPropertyName = "Index";
		_ptIndexColumn.HeaderText = "#";
		_ptIndexColumn.Name = "_ptIndexColumn";
		_ptIndexColumn.ReadOnly = true;
		_ptIndexColumn.Width = 50;
		//
		// _ptXColumn
		//
		_ptXColumn.DataPropertyName = "X";
		_ptXColumn.HeaderText = "X";
		_ptXColumn.Name = "_ptXColumn";
		_ptXColumn.Width = 120;
		//
		// _ptYColumn
		//
		_ptYColumn.DataPropertyName = "Y";
		_ptYColumn.HeaderText = "Y";
		_ptYColumn.Name = "_ptYColumn";
		_ptYColumn.Width = 120;
		//
		// _ptZColumn
		//
		_ptZColumn.DataPropertyName = "Z";
		_ptZColumn.HeaderText = "Z";
		_ptZColumn.Name = "_ptZColumn";
		_ptZColumn.Width = 120;
		//
		// _headingsTab
		//
		_headingsTab.Controls.Add(_headingsGrid);
		_headingsTab.Location = new Point(4, 44);
		_headingsTab.Name = "_headingsTab";
		_headingsTab.Padding = new Padding(3);
		_headingsTab.Size = new Size(1052, 556);
		_headingsTab.TabIndex = 2;
		_headingsTab.Text = "Headings";
		_headingsTab.UseVisualStyleBackColor = true;
		//
		// _headingsGrid
		//
		_headingsGrid.AllowUserToAddRows = false;
		_headingsGrid.AllowUserToDeleteRows = false;
		_headingsGrid.AutoGenerateColumns = false;
		_headingsGrid.Columns.AddRange(new DataGridViewColumn[] {
			_hdIndexColumn, _hdDegreesColumn, _hdBamColumn
		});
		_headingsGrid.Dock = DockStyle.Fill;
		_headingsGrid.Location = new Point(3, 3);
		_headingsGrid.Name = "_headingsGrid";
		_headingsGrid.RowHeadersVisible = false;
		_headingsGrid.Size = new Size(1046, 550);
		_headingsGrid.TabIndex = 0;
		_headingsGrid.DataError += OnGridDataError;
		//
		// _hdIndexColumn
		//
		_hdIndexColumn.DataPropertyName = "Index";
		_hdIndexColumn.HeaderText = "#";
		_hdIndexColumn.Name = "_hdIndexColumn";
		_hdIndexColumn.ReadOnly = true;
		_hdIndexColumn.Width = 50;
		//
		// _hdDegreesColumn
		//
		_hdDegreesColumn.DataPropertyName = "Degrees";
		_hdDegreesColumn.HeaderText = "Degrees";
		_hdDegreesColumn.Name = "_hdDegreesColumn";
		_hdDegreesColumn.Width = 100;
		//
		// _hdBamColumn
		//
		_hdBamColumn.DataPropertyName = "Bam";
		_hdBamColumn.HeaderText = "BAM at load (× 182)";
		_hdBamColumn.Name = "_hdBamColumn";
		_hdBamColumn.ReadOnly = true;
		_hdBamColumn.Width = 160;
		//
		// _routesTab
		//
		_routesTab.Controls.Add(_routesGrid);
		_routesTab.Location = new Point(4, 44);
		_routesTab.Name = "_routesTab";
		_routesTab.Padding = new Padding(3);
		_routesTab.Size = new Size(1052, 556);
		_routesTab.TabIndex = 3;
		_routesTab.Text = "Routes";
		_routesTab.UseVisualStyleBackColor = true;
		//
		// _routesGrid
		//
		_routesGrid.AllowUserToAddRows = false;
		_routesGrid.AllowUserToDeleteRows = false;
		_routesGrid.AutoGenerateColumns = false;
		_routesGrid.Columns.AddRange(new DataGridViewColumn[] {
			_rtIndexColumn, _rtCountColumn, _rtWaypointsColumn
		});
		_routesGrid.Dock = DockStyle.Fill;
		_routesGrid.Location = new Point(3, 3);
		_routesGrid.Name = "_routesGrid";
		_routesGrid.RowHeadersVisible = false;
		_routesGrid.Size = new Size(1046, 550);
		_routesGrid.TabIndex = 0;
		_routesGrid.DataError += OnGridDataError;
		_routesGrid.CellValueChanged += OnRouteCellChanged;
		//
		// _rtIndexColumn
		//
		_rtIndexColumn.DataPropertyName = "Index";
		_rtIndexColumn.HeaderText = "#";
		_rtIndexColumn.Name = "_rtIndexColumn";
		_rtIndexColumn.ReadOnly = true;
		_rtIndexColumn.Width = 50;
		//
		// _rtCountColumn
		//
		_rtCountColumn.DataPropertyName = "Count";
		_rtCountColumn.HeaderText = "Waypoints";
		_rtCountColumn.Name = "_rtCountColumn";
		_rtCountColumn.ReadOnly = true;
		_rtCountColumn.Width = 80;
		//
		// _rtWaypointsColumn
		//
		_rtWaypointsColumn.DataPropertyName = "Waypoints";
		_rtWaypointsColumn.HeaderText = "Point refs (in order, any length)";
		_rtWaypointsColumn.Name = "_rtWaypointsColumn";
		_rtWaypointsColumn.Width = 700;
		//
		// _linksTab
		//
		_linksTab.Controls.Add(_linksGrid);
		_linksTab.Location = new Point(4, 44);
		_linksTab.Name = "_linksTab";
		_linksTab.Padding = new Padding(3);
		_linksTab.Size = new Size(1052, 556);
		_linksTab.TabIndex = 4;
		_linksTab.Text = "Links / Rewards";
		_linksTab.UseVisualStyleBackColor = true;
		//
		// _linksGrid
		//
		_linksGrid.AllowUserToAddRows = false;
		_linksGrid.AllowUserToDeleteRows = false;
		_linksGrid.AutoGenerateColumns = false;
		_linksGrid.Columns.AddRange(new DataGridViewColumn[] {
			_lrIndexColumn, _lrTypeColumn, _lrRefAColumn, _lrRefBColumn
		});
		_linksGrid.Dock = DockStyle.Fill;
		_linksGrid.Location = new Point(3, 3);
		_linksGrid.Name = "_linksGrid";
		_linksGrid.RowHeadersVisible = false;
		_linksGrid.Size = new Size(1046, 550);
		_linksGrid.TabIndex = 0;
		_linksGrid.DataError += OnGridDataError;
		//
		// _lrIndexColumn
		//
		_lrIndexColumn.DataPropertyName = "Index";
		_lrIndexColumn.HeaderText = "#";
		_lrIndexColumn.Name = "_lrIndexColumn";
		_lrIndexColumn.ReadOnly = true;
		_lrIndexColumn.Width = 50;
		//
		// _lrTypeColumn
		//
		_lrTypeColumn.DataPropertyName = "TypeFlag";
		_lrTypeColumn.HeaderText = "Type";
		_lrTypeColumn.Name = "_lrTypeColumn";
		_lrTypeColumn.Width = 80;
		//
		// _lrRefAColumn
		//
		_lrRefAColumn.DataPropertyName = "RefA";
		_lrRefAColumn.HeaderText = "Ref A";
		_lrRefAColumn.Name = "_lrRefAColumn";
		_lrRefAColumn.Width = 100;
		//
		// _lrRefBColumn
		//
		_lrRefBColumn.DataPropertyName = "RefBOrLiteral";
		_lrRefBColumn.HeaderText = "Ref B / literal";
		_lrRefBColumn.Name = "_lrRefBColumn";
		_lrRefBColumn.Width = 120;
		//
		// _actionsTab
		//
		_actionsTab.Controls.Add(_actionsGrid);
		_actionsTab.Location = new Point(4, 44);
		_actionsTab.Name = "_actionsTab";
		_actionsTab.Padding = new Padding(3);
		_actionsTab.Size = new Size(1052, 556);
		_actionsTab.TabIndex = 5;
		_actionsTab.Text = "Actions";
		_actionsTab.UseVisualStyleBackColor = true;
		//
		// _actionsGrid
		//
		_actionsGrid.AllowUserToAddRows = false;
		_actionsGrid.AllowUserToDeleteRows = false;
		_actionsGrid.AutoGenerateColumns = false;
		_actionsGrid.Columns.AddRange(new DataGridViewColumn[] {
			_acIndexColumn, _acTypeColumn, _acVerbColumn, _acSecondaryColumn, _acTargetColumn,
			_acRefsColumn, _acLutRefsColumn, _acArrayAColumn, _acArrayBColumn
		});
		_actionsGrid.Dock = DockStyle.Fill;
		_actionsGrid.Location = new Point(3, 3);
		_actionsGrid.Name = "_actionsGrid";
		_actionsGrid.RowHeadersVisible = false;
		_actionsGrid.Size = new Size(1046, 550);
		_actionsGrid.TabIndex = 0;
		_actionsGrid.DataError += OnGridDataError;
		//
		// _acIndexColumn
		//
		_acIndexColumn.DataPropertyName = "Index";
		_acIndexColumn.HeaderText = "#";
		_acIndexColumn.Name = "_acIndexColumn";
		_acIndexColumn.ReadOnly = true;
		_acIndexColumn.Width = 50;
		//
		// _acTypeColumn
		//
		_acTypeColumn.DataPropertyName = "Type";
		_acTypeColumn.HeaderText = "Type";
		_acTypeColumn.Name = "_acTypeColumn";
		_acTypeColumn.Width = 60;
		//
		// _acVerbColumn
		//
		_acVerbColumn.DataPropertyName = "Verb";
		_acVerbColumn.HeaderText = "Verb";
		_acVerbColumn.Name = "_acVerbColumn";
		_acVerbColumn.Width = 60;
		//
		// _acSecondaryColumn
		//
		_acSecondaryColumn.DataPropertyName = "SecondaryValue";
		_acSecondaryColumn.HeaderText = "Secondary";
		_acSecondaryColumn.Name = "_acSecondaryColumn";
		_acSecondaryColumn.Width = 80;
		//
		// _acTargetColumn
		//
		_acTargetColumn.DataPropertyName = "Target";
		_acTargetColumn.HeaderText = "Target";
		_acTargetColumn.Name = "_acTargetColumn";
		_acTargetColumn.Width = 70;
		//
		// _acRefsColumn
		//
		_acRefsColumn.DataPropertyName = "RefsRow9";
		_acRefsColumn.HeaderText = "Link refs (8)";
		_acRefsColumn.Name = "_acRefsColumn";
		_acRefsColumn.Width = 220;
		//
		// _acLutRefsColumn
		//
		_acLutRefsColumn.DataPropertyName = "LutRefs";
		_acLutRefsColumn.HeaderText = "Herc LUT refs (5)";
		_acLutRefsColumn.Name = "_acLutRefsColumn";
		_acLutRefsColumn.Width = 160;
		//
		// _acArrayAColumn
		//
		_acArrayAColumn.DataPropertyName = "ArrayA";
		_acArrayAColumn.HeaderText = "Constant span A";
		_acArrayAColumn.Name = "_acArrayAColumn";
		_acArrayAColumn.ReadOnly = true;
		_acArrayAColumn.Width = 200;
		//
		// _acArrayBColumn
		//
		_acArrayBColumn.DataPropertyName = "ArrayB";
		_acArrayBColumn.HeaderText = "Constant span B";
		_acArrayBColumn.Name = "_acArrayBColumn";
		_acArrayBColumn.ReadOnly = true;
		_acArrayBColumn.Width = 200;
		//
		// _actionPairsTab
		//
		_actionPairsTab.Controls.Add(_actionPairsGrid);
		_actionPairsTab.Location = new Point(4, 44);
		_actionPairsTab.Name = "_actionPairsTab";
		_actionPairsTab.Padding = new Padding(3);
		_actionPairsTab.Size = new Size(1052, 556);
		_actionPairsTab.TabIndex = 6;
		_actionPairsTab.Text = "Action Pairs";
		_actionPairsTab.UseVisualStyleBackColor = true;
		//
		// _actionPairsGrid
		//
		_actionPairsGrid.AllowUserToAddRows = false;
		_actionPairsGrid.AllowUserToDeleteRows = false;
		_actionPairsGrid.AutoGenerateColumns = false;
		_actionPairsGrid.Columns.AddRange(new DataGridViewColumn[] {
			_apIndexColumn, _apPrimaryColumn, _apTimerColumn, _apSequenceColumn
		});
		_actionPairsGrid.Dock = DockStyle.Fill;
		_actionPairsGrid.Location = new Point(3, 3);
		_actionPairsGrid.Name = "_actionPairsGrid";
		_actionPairsGrid.RowHeadersVisible = false;
		_actionPairsGrid.Size = new Size(1046, 550);
		_actionPairsGrid.TabIndex = 0;
		_actionPairsGrid.DataError += OnGridDataError;
		//
		// _apIndexColumn
		//
		_apIndexColumn.DataPropertyName = "Index";
		_apIndexColumn.HeaderText = "#";
		_apIndexColumn.Name = "_apIndexColumn";
		_apIndexColumn.ReadOnly = true;
		_apIndexColumn.Width = 50;
		//
		// _apPrimaryColumn
		//
		_apPrimaryColumn.DataPropertyName = "PrimaryActionRef";
		_apPrimaryColumn.HeaderText = "Action ref";
		_apPrimaryColumn.Name = "_apPrimaryColumn";
		_apPrimaryColumn.Width = 90;
		//
		// _apTimerColumn
		//
		_apTimerColumn.DataPropertyName = "TimerValue";
		_apTimerColumn.HeaderText = "Type / timer";
		_apTimerColumn.Name = "_apTimerColumn";
		_apTimerColumn.Width = 100;
		//
		// _apSequenceColumn
		//
		_apSequenceColumn.DataPropertyName = "SequenceRefs";
		_apSequenceColumn.HeaderText = "Sequence action refs (10)";
		_apSequenceColumn.Name = "_apSequenceColumn";
		_apSequenceColumn.Width = 500;
		//
		// _mechsTab
		//
		_mechsTab.Controls.Add(_mechsGrid);
		_mechsTab.Location = new Point(4, 44);
		_mechsTab.Name = "_mechsTab";
		_mechsTab.Padding = new Padding(3);
		_mechsTab.Size = new Size(1052, 556);
		_mechsTab.TabIndex = 7;
		_mechsTab.Text = "Hercs";
		_mechsTab.UseVisualStyleBackColor = true;
		//
		// _mechsGrid
		//
		_mechsGrid.AllowUserToAddRows = false;
		_mechsGrid.AllowUserToDeleteRows = false;
		_mechsGrid.AutoGenerateColumns = false;
		_mechsGrid.Columns.AddRange(new DataGridViewColumn[] {
			_mechIndexColumn, _mechTypeColumn, _mechPositionColumn, _mechHeadingColumn, _mechWeaponsColumn
		});
		_mechsGrid.Dock = DockStyle.Fill;
		_mechsGrid.Location = new Point(3, 3);
		_mechsGrid.Name = "_mechsGrid";
		_mechsGrid.RowHeadersVisible = false;
		_mechsGrid.Size = new Size(1046, 550);
		_mechsGrid.TabIndex = 0;
		_mechsGrid.DataError += OnGridDataError;
		//
		// _mechIndexColumn
		//
		_mechIndexColumn.DataPropertyName = "Index";
		_mechIndexColumn.HeaderText = "#";
		_mechIndexColumn.Name = "_mechIndexColumn";
		_mechIndexColumn.ReadOnly = true;
		_mechIndexColumn.Width = 50;
		//
		// _mechTypeColumn
		//
		// Items are filled per load (see MissionScriptForm.BindHercTypes) so that a type the file
		// carries but MECHS.NAM has no name for still has an entry to select.
		_mechTypeColumn.DataPropertyName = "HercType";
		_mechTypeColumn.DisplayMember = "Label";
		_mechTypeColumn.HeaderText = "Herc Type";
		_mechTypeColumn.Name = "_mechTypeColumn";
		_mechTypeColumn.ValueMember = "Id";
		_mechTypeColumn.Width = 170;
		//
		// _mechPositionColumn
		//
		_mechPositionColumn.DataPropertyName = "PositionRef";
		_mechPositionColumn.HeaderText = "Point ref";
		_mechPositionColumn.Name = "_mechPositionColumn";
		_mechPositionColumn.Width = 90;
		//
		// _mechHeadingColumn
		//
		_mechHeadingColumn.DataPropertyName = "HeadingRef";
		_mechHeadingColumn.HeaderText = "Heading ref";
		_mechHeadingColumn.Name = "_mechHeadingColumn";
		_mechHeadingColumn.Width = 90;
		//
		// _mechWeaponsColumn
		//
		_mechWeaponsColumn.DataPropertyName = "WeaponRefs";
		_mechWeaponsColumn.HeaderText = "Weapon fit (10 slots, -1 = empty)";
		_mechWeaponsColumn.Name = "_mechWeaponsColumn";
		_mechWeaponsColumn.Width = 420;
		//
		// _flyersTab
		//
		_flyersTab.Controls.Add(_flyersGrid);
		_flyersTab.Location = new Point(4, 44);
		_flyersTab.Name = "_flyersTab";
		_flyersTab.Padding = new Padding(3);
		_flyersTab.Size = new Size(1052, 556);
		_flyersTab.TabIndex = 8;
		_flyersTab.Text = "Flyers";
		_flyersTab.UseVisualStyleBackColor = true;
		//
		// _flyersGrid
		//
		_flyersGrid.AllowUserToAddRows = false;
		_flyersGrid.AllowUserToDeleteRows = false;
		_flyersGrid.AutoGenerateColumns = false;
		_flyersGrid.Columns.AddRange(new DataGridViewColumn[] {
			_flyIndexColumn, _flyTypeColumn, _flyPositionColumn, _flyHeadingColumn
		});
		_flyersGrid.Dock = DockStyle.Fill;
		_flyersGrid.Location = new Point(3, 3);
		_flyersGrid.Name = "_flyersGrid";
		_flyersGrid.RowHeadersVisible = false;
		_flyersGrid.Size = new Size(1046, 550);
		_flyersGrid.TabIndex = 0;
		_flyersGrid.DataError += OnGridDataError;
		//
		// _flyIndexColumn
		//
		_flyIndexColumn.DataPropertyName = "Index";
		_flyIndexColumn.HeaderText = "#";
		_flyIndexColumn.Name = "_flyIndexColumn";
		_flyIndexColumn.ReadOnly = true;
		_flyIndexColumn.Width = 50;
		//
		// _flyTypeColumn
		//
		_flyTypeColumn.DataPropertyName = "FlyerType";
		_flyTypeColumn.HeaderText = "Flyer type (FLYERS.NAM)";
		_flyTypeColumn.Name = "_flyTypeColumn";
		_flyTypeColumn.Width = 170;
		//
		// _flyPositionColumn
		//
		_flyPositionColumn.DataPropertyName = "PositionRef";
		_flyPositionColumn.HeaderText = "Point ref";
		_flyPositionColumn.Name = "_flyPositionColumn";
		_flyPositionColumn.Width = 90;
		//
		// _flyHeadingColumn
		//
		_flyHeadingColumn.DataPropertyName = "HeadingRef";
		_flyHeadingColumn.HeaderText = "Heading ref";
		_flyHeadingColumn.Name = "_flyHeadingColumn";
		_flyHeadingColumn.Width = 90;
		//
		// _basesTab
		//
		_basesTab.Controls.Add(_basesGrid);
		_basesTab.Location = new Point(4, 44);
		_basesTab.Name = "_basesTab";
		_basesTab.Padding = new Padding(3);
		_basesTab.Size = new Size(1052, 556);
		_basesTab.TabIndex = 9;
		_basesTab.Text = "Bases";
		_basesTab.UseVisualStyleBackColor = true;
		//
		// _basesGrid
		//
		_basesGrid.AllowUserToAddRows = false;
		_basesGrid.AllowUserToDeleteRows = false;
		_basesGrid.AutoGenerateColumns = false;
		_basesGrid.Columns.AddRange(new DataGridViewColumn[] {
			_baseIndexColumn, _baseTypeColumn, _basePositionColumn, _baseHeadingColumn
		});
		_basesGrid.Dock = DockStyle.Fill;
		_basesGrid.Location = new Point(3, 3);
		_basesGrid.Name = "_basesGrid";
		_basesGrid.RowHeadersVisible = false;
		_basesGrid.Size = new Size(1046, 550);
		_basesGrid.TabIndex = 0;
		_basesGrid.DataError += OnGridDataError;
		//
		// _baseIndexColumn
		//
		_baseIndexColumn.DataPropertyName = "Index";
		_baseIndexColumn.HeaderText = "#";
		_baseIndexColumn.Name = "_baseIndexColumn";
		_baseIndexColumn.ReadOnly = true;
		_baseIndexColumn.Width = 50;
		//
		// _baseTypeColumn
		//
		_baseTypeColumn.DataPropertyName = "BaseType";
		_baseTypeColumn.HeaderText = "Base type (BASES.DAT)";
		_baseTypeColumn.Name = "_baseTypeColumn";
		_baseTypeColumn.Width = 170;
		//
		// _basePositionColumn
		//
		_basePositionColumn.DataPropertyName = "PositionRef";
		_basePositionColumn.HeaderText = "Point ref";
		_basePositionColumn.Name = "_basePositionColumn";
		_basePositionColumn.Width = 90;
		//
		// _baseHeadingColumn
		//
		_baseHeadingColumn.DataPropertyName = "HeadingRef";
		_baseHeadingColumn.HeaderText = "Heading ref";
		_baseHeadingColumn.Name = "_baseHeadingColumn";
		_baseHeadingColumn.Width = 90;
		//
		// _routeLinksTab
		//
		_routeLinksTab.Controls.Add(_routeLinksGrid);
		_routeLinksTab.Location = new Point(4, 44);
		_routeLinksTab.Name = "_routeLinksTab";
		_routeLinksTab.Padding = new Padding(3);
		_routeLinksTab.Size = new Size(1052, 556);
		_routeLinksTab.TabIndex = 10;
		_routeLinksTab.Text = "Route Links";
		_routeLinksTab.UseVisualStyleBackColor = true;
		//
		// _routeLinksGrid
		//
		_routeLinksGrid.AllowUserToAddRows = false;
		_routeLinksGrid.AllowUserToDeleteRows = false;
		_routeLinksGrid.AutoGenerateColumns = false;
		_routeLinksGrid.Columns.AddRange(new DataGridViewColumn[] {
			_rlIndexColumn, _rlSmall1Column, _rlSmall2Column, _rlPointColumn, _rlRouteColumn,
			_rlDiscriminatorColumn, _rlDiscriminatedRefColumn, _rlActionColumn
		});
		_routeLinksGrid.Dock = DockStyle.Fill;
		_routeLinksGrid.Location = new Point(3, 3);
		_routeLinksGrid.Name = "_routeLinksGrid";
		_routeLinksGrid.RowHeadersVisible = false;
		_routeLinksGrid.Size = new Size(1046, 550);
		_routeLinksGrid.TabIndex = 0;
		_routeLinksGrid.DataError += OnGridDataError;
		//
		// _rlIndexColumn
		//
		_rlIndexColumn.DataPropertyName = "Index";
		_rlIndexColumn.HeaderText = "#";
		_rlIndexColumn.Name = "_rlIndexColumn";
		_rlIndexColumn.ReadOnly = true;
		_rlIndexColumn.Width = 50;
		//
		// _rlSmall1Column
		//
		_rlSmall1Column.DataPropertyName = "SmallInt1";
		_rlSmall1Column.HeaderText = "Small 1";
		_rlSmall1Column.Name = "_rlSmall1Column";
		_rlSmall1Column.Width = 80;
		//
		// _rlSmall2Column
		//
		_rlSmall2Column.DataPropertyName = "SmallInt2";
		_rlSmall2Column.HeaderText = "Small 2";
		_rlSmall2Column.Name = "_rlSmall2Column";
		_rlSmall2Column.Width = 80;
		//
		// _rlPointColumn
		//
		_rlPointColumn.DataPropertyName = "PointRef";
		_rlPointColumn.HeaderText = "Point ref";
		_rlPointColumn.Name = "_rlPointColumn";
		_rlPointColumn.Width = 90;
		//
		// _rlRouteColumn
		//
		_rlRouteColumn.DataPropertyName = "RouteRef";
		_rlRouteColumn.HeaderText = "Route ref";
		_rlRouteColumn.Name = "_rlRouteColumn";
		_rlRouteColumn.Width = 90;
		//
		// _rlDiscriminatorColumn
		//
		_rlDiscriminatorColumn.DataPropertyName = "DiscriminatorType";
		_rlDiscriminatorColumn.HeaderText = "Entity kind";
		_rlDiscriminatorColumn.Name = "_rlDiscriminatorColumn";
		_rlDiscriminatorColumn.Width = 90;
		//
		// _rlDiscriminatedRefColumn
		//
		_rlDiscriminatedRefColumn.DataPropertyName = "DiscriminatedRef";
		_rlDiscriminatedRefColumn.HeaderText = "Entity ref";
		_rlDiscriminatedRefColumn.Name = "_rlDiscriminatedRefColumn";
		_rlDiscriminatedRefColumn.Width = 90;
		//
		// _rlActionColumn
		//
		_rlActionColumn.DataPropertyName = "ActionRef";
		_rlActionColumn.HeaderText = "Action ref";
		_rlActionColumn.Name = "_rlActionColumn";
		_rlActionColumn.Width = 90;
		//
		// _groupsTab
		//
		_groupsTab.Controls.Add(_groupsGrid);
		_groupsTab.Location = new Point(4, 44);
		_groupsTab.Name = "_groupsTab";
		_groupsTab.Padding = new Padding(3);
		_groupsTab.Size = new Size(1052, 556);
		_groupsTab.TabIndex = 11;
		_groupsTab.Text = "Groups";
		_groupsTab.UseVisualStyleBackColor = true;
		//
		// _groupsGrid
		//
		_groupsGrid.AllowUserToAddRows = false;
		_groupsGrid.AllowUserToDeleteRows = false;
		_groupsGrid.AutoGenerateColumns = false;
		_groupsGrid.Columns.AddRange(new DataGridViewColumn[] {
			_grpIndexColumn, _grpRosterColumn, _grpFormationColumn, _grpPointColumn, _grpHeadingColumn,
			_grpRouteColumn, _grpMembersColumn, _grpRouteLinksColumn, _grpBinaryFlagColumn,
			_grpTriStateColumn, _grpActionColumn
		});
		_groupsGrid.Dock = DockStyle.Fill;
		_groupsGrid.Location = new Point(3, 3);
		_groupsGrid.Name = "_groupsGrid";
		_groupsGrid.RowHeadersVisible = false;
		_groupsGrid.Size = new Size(1046, 550);
		_groupsGrid.TabIndex = 0;
		_groupsGrid.DataError += OnGridDataError;
		//
		// _grpIndexColumn
		//
		_grpIndexColumn.DataPropertyName = "Index";
		_grpIndexColumn.HeaderText = "#";
		_grpIndexColumn.Name = "_grpIndexColumn";
		_grpIndexColumn.ReadOnly = true;
		_grpIndexColumn.Width = 50;
		//
		// _grpRosterColumn
		//
		_grpRosterColumn.DataPropertyName = "Roster";
		_grpRosterColumn.HeaderText = "Roster (0 herc / 1 flyer / 2 base)";
		_grpRosterColumn.Name = "_grpRosterColumn";
		_grpRosterColumn.Width = 200;
		//
		// _grpFormationColumn
		//
		_grpFormationColumn.DataPropertyName = "Formation";
		_grpFormationColumn.HeaderText = "Formation";
		_grpFormationColumn.Name = "_grpFormationColumn";
		_grpFormationColumn.Width = 80;
		//
		// _grpPointColumn
		//
		_grpPointColumn.DataPropertyName = "PointRef";
		_grpPointColumn.HeaderText = "Point ref";
		_grpPointColumn.Name = "_grpPointColumn";
		_grpPointColumn.Width = 80;
		//
		// _grpHeadingColumn
		//
		_grpHeadingColumn.DataPropertyName = "HeadingRef";
		_grpHeadingColumn.HeaderText = "Heading ref";
		_grpHeadingColumn.Name = "_grpHeadingColumn";
		_grpHeadingColumn.Width = 90;
		//
		// _grpRouteColumn
		//
		_grpRouteColumn.DataPropertyName = "RouteRef";
		_grpRouteColumn.HeaderText = "Route ref";
		_grpRouteColumn.Name = "_grpRouteColumn";
		_grpRouteColumn.Width = 80;
		//
		// _grpMembersColumn
		//
		_grpMembersColumn.DataPropertyName = "MemberRefs";
		_grpMembersColumn.HeaderText = "Member slots (20, -1 = empty)";
		_grpMembersColumn.Name = "_grpMembersColumn";
		_grpMembersColumn.Width = 460;
		//
		// _grpRouteLinksColumn
		//
		_grpRouteLinksColumn.DataPropertyName = "RouteLinkRefs";
		_grpRouteLinksColumn.HeaderText = "Route link refs (10)";
		_grpRouteLinksColumn.Name = "_grpRouteLinksColumn";
		_grpRouteLinksColumn.Width = 260;
		//
		// _grpBinaryFlagColumn
		//
		_grpBinaryFlagColumn.DataPropertyName = "BinaryFlag";
		_grpBinaryFlagColumn.HeaderText = "Grid-snap flag";
		_grpBinaryFlagColumn.Name = "_grpBinaryFlagColumn";
		_grpBinaryFlagColumn.Width = 100;
		//
		// _grpTriStateColumn
		//
		_grpTriStateColumn.DataPropertyName = "TriStateFlag";
		_grpTriStateColumn.HeaderText = "Tri-state";
		_grpTriStateColumn.Name = "_grpTriStateColumn";
		_grpTriStateColumn.Width = 80;
		//
		// _grpActionColumn
		//
		_grpActionColumn.DataPropertyName = "ActionRef";
		_grpActionColumn.HeaderText = "Action ref";
		_grpActionColumn.Name = "_grpActionColumn";
		_grpActionColumn.Width = 90;
		//
		// _entityLinksTab
		//
		_entityLinksTab.Controls.Add(_entityLinksGrid);
		_entityLinksTab.Location = new Point(4, 44);
		_entityLinksTab.Name = "_entityLinksTab";
		_entityLinksTab.Padding = new Padding(3);
		_entityLinksTab.Size = new Size(1052, 556);
		_entityLinksTab.TabIndex = 12;
		_entityLinksTab.Text = "Entity Links";
		_entityLinksTab.UseVisualStyleBackColor = true;
		//
		// _entityLinksGrid
		//
		_entityLinksGrid.AllowUserToAddRows = false;
		_entityLinksGrid.AllowUserToDeleteRows = false;
		_entityLinksGrid.AutoGenerateColumns = false;
		_entityLinksGrid.Columns.AddRange(new DataGridViewColumn[] {
			_elIndexColumn, _elUnk02Column, _elUnk04Column, _elDiscriminatorColumn,
			_elDiscriminatedRefColumn, _elPointColumn, _elRouteColumn, _elLutRefColumn,
			_elPairRefsColumn, _elPairTagsColumn
		});
		_entityLinksGrid.Dock = DockStyle.Fill;
		_entityLinksGrid.Location = new Point(3, 3);
		_entityLinksGrid.Name = "_entityLinksGrid";
		_entityLinksGrid.RowHeadersVisible = false;
		_entityLinksGrid.Size = new Size(1046, 550);
		_entityLinksGrid.TabIndex = 0;
		_entityLinksGrid.DataError += OnGridDataError;
		//
		// _elIndexColumn
		//
		_elIndexColumn.DataPropertyName = "Index";
		_elIndexColumn.HeaderText = "#";
		_elIndexColumn.Name = "_elIndexColumn";
		_elIndexColumn.ReadOnly = true;
		_elIndexColumn.Width = 50;
		//
		// _elUnk02Column
		//
		_elUnk02Column.DataPropertyName = "Unk02";
		_elUnk02Column.HeaderText = "Unk 02";
		_elUnk02Column.Name = "_elUnk02Column";
		_elUnk02Column.Width = 80;
		//
		// _elUnk04Column
		//
		_elUnk04Column.DataPropertyName = "Unk04";
		_elUnk04Column.HeaderText = "Unk 04";
		_elUnk04Column.Name = "_elUnk04Column";
		_elUnk04Column.Width = 80;
		//
		// _elDiscriminatorColumn
		//
		_elDiscriminatorColumn.DataPropertyName = "Discriminator";
		_elDiscriminatorColumn.HeaderText = "Entity kind";
		_elDiscriminatorColumn.Name = "_elDiscriminatorColumn";
		_elDiscriminatorColumn.Width = 90;
		//
		// _elDiscriminatedRefColumn
		//
		_elDiscriminatedRefColumn.DataPropertyName = "DiscriminatedRef";
		_elDiscriminatedRefColumn.HeaderText = "Entity ref";
		_elDiscriminatedRefColumn.Name = "_elDiscriminatedRefColumn";
		_elDiscriminatedRefColumn.Width = 90;
		//
		// _elPointColumn
		//
		_elPointColumn.DataPropertyName = "PointRef";
		_elPointColumn.HeaderText = "Point ref";
		_elPointColumn.Name = "_elPointColumn";
		_elPointColumn.Width = 90;
		//
		// _elRouteColumn
		//
		_elRouteColumn.DataPropertyName = "RouteRef";
		_elRouteColumn.HeaderText = "Route ref";
		_elRouteColumn.Name = "_elRouteColumn";
		_elRouteColumn.Width = 90;
		//
		// _elLutRefColumn
		//
		_elLutRefColumn.DataPropertyName = "LutRef";
		_elLutRefColumn.HeaderText = "LUT ref";
		_elLutRefColumn.Name = "_elLutRefColumn";
		_elLutRefColumn.Width = 90;
		//
		// _elPairRefsColumn
		//
		_elPairRefsColumn.DataPropertyName = "PairRefs";
		_elPairRefsColumn.HeaderText = "Pair refs (10)";
		_elPairRefsColumn.Name = "_elPairRefsColumn";
		_elPairRefsColumn.Width = 240;
		//
		// _elPairTagsColumn
		//
		_elPairTagsColumn.DataPropertyName = "PairTags";
		_elPairTagsColumn.HeaderText = "Pair tags (10)";
		_elPairTagsColumn.Name = "_elPairTagsColumn";
		_elPairTagsColumn.Width = 240;
		//
		// _unlocksTab
		//
		_unlocksTab.Controls.Add(_unlocksGrid);
		_unlocksTab.Controls.Add(_unlocksButtonPanel);
		_unlocksTab.Location = new Point(4, 44);
		_unlocksTab.Name = "_unlocksTab";
		_unlocksTab.Padding = new Padding(3);
		_unlocksTab.Size = new Size(1052, 556);
		_unlocksTab.TabIndex = 13;
		_unlocksTab.Text = "Unlocks";
		_unlocksTab.UseVisualStyleBackColor = true;
		//
		// _unlocksGrid
		//
		_unlocksGrid.AllowUserToAddRows = false;
		_unlocksGrid.AllowUserToDeleteRows = false;
		_unlocksGrid.AutoGenerateColumns = false;
		_unlocksGrid.Columns.AddRange(new DataGridViewColumn[] { _unlockValueColumn });
		_unlocksGrid.Dock = DockStyle.Fill;
		_unlocksGrid.Location = new Point(3, 3);
		_unlocksGrid.Name = "_unlocksGrid";
		_unlocksGrid.RowHeadersVisible = false;
		_unlocksGrid.Size = new Size(1046, 498);
		_unlocksGrid.TabIndex = 0;
		_unlocksGrid.DataError += OnGridDataError;
		//
		// _unlockValueColumn
		//
		_unlockValueColumn.DataPropertyName = "Value";
		_unlockValueColumn.HeaderText = "Herc/weapon LUT ref";
		_unlockValueColumn.Name = "_unlockValueColumn";
		_unlockValueColumn.Width = 200;
		//
		// _unlocksButtonPanel
		//
		_unlocksButtonPanel.Controls.Add(_addUnlockButton);
		_unlocksButtonPanel.Controls.Add(_removeUnlockButton);
		_unlocksButtonPanel.Dock = DockStyle.Bottom;
		_unlocksButtonPanel.Location = new Point(3, 501);
		_unlocksButtonPanel.Name = "_unlocksButtonPanel";
		_unlocksButtonPanel.Padding = new Padding(6);
		_unlocksButtonPanel.Size = new Size(1046, 52);
		_unlocksButtonPanel.TabIndex = 1;
		//
		// _addUnlockButton
		//
		_addUnlockButton.Location = new Point(6, 10);
		_addUnlockButton.Name = "_addUnlockButton";
		_addUnlockButton.Size = new Size(120, 28);
		_addUnlockButton.TabIndex = 0;
		_addUnlockButton.Text = "Add Unlock";
		_addUnlockButton.UseVisualStyleBackColor = true;
		_addUnlockButton.Click += OnAddUnlock;
		//
		// _removeUnlockButton
		//
		_removeUnlockButton.Location = new Point(132, 10);
		_removeUnlockButton.Name = "_removeUnlockButton";
		_removeUnlockButton.Size = new Size(120, 28);
		_removeUnlockButton.TabIndex = 1;
		_removeUnlockButton.Text = "Remove Unlock";
		_removeUnlockButton.UseVisualStyleBackColor = true;
		_removeUnlockButton.Click += OnRemoveUnlock;
		//
		// _statusStrip
		//
		_statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel });
		_statusStrip.Location = new Point(0, 628);
		_statusStrip.Name = "_statusStrip";
		_statusStrip.Size = new Size(1060, 22);
		_statusStrip.TabIndex = 2;
		//
		// _statusLabel
		//
		_statusLabel.Name = "_statusLabel";
		_statusLabel.Text = "No script.dat loaded.";
		//
		// MissionScriptForm
		//
		Size = new Size(1060, 650);
		Controls.Add(_tabs);
		Controls.Add(_statusStrip);
		Controls.Add(_menuStrip);
		MainMenuStrip = _menuStrip;
		Name = "MissionScriptForm";
		Text = "Mission Script Editor — data\\script.dat";
		_menuStrip.ResumeLayout(false);
		_menuStrip.PerformLayout();
		_tabs.ResumeLayout(false);
		_headerTab.ResumeLayout(false);
		_headerGroupBox.ResumeLayout(false);
		_headerGroupBox.PerformLayout();
		((System.ComponentModel.ISupportInitialize)_theaterInput).EndInit();
		((System.ComponentModel.ISupportInitialize)_zoneInput).EndInit();
		((System.ComponentModel.ISupportInitialize)_variantInput).EndInit();
		_pointsTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_pointsGrid).EndInit();
		_headingsTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_headingsGrid).EndInit();
		_routesTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_routesGrid).EndInit();
		_linksTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_linksGrid).EndInit();
		_actionsTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_actionsGrid).EndInit();
		_actionPairsTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_actionPairsGrid).EndInit();
		_mechsTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_mechsGrid).EndInit();
		_flyersTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_flyersGrid).EndInit();
		_basesTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_basesGrid).EndInit();
		_routeLinksTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_routeLinksGrid).EndInit();
		_groupsTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_groupsGrid).EndInit();
		_entityLinksTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_entityLinksGrid).EndInit();
		_unlocksTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_unlocksGrid).EndInit();
		_unlocksButtonPanel.ResumeLayout(false);
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

	private TabPage _headerTab;
	private GroupBox _headerGroupBox;
	private Label _theaterLabel;
	private NumericUpDown _theaterInput;
	private Label _zoneLabel;
	private NumericUpDown _zoneInput;
	private Label _variantLabel;
	private NumericUpDown _variantInput;
	private Label _worldLabel;
	private Label _worldValueLabel;
	private Label _headerRawLabel;
	private TextBox _headerRawText;
	private Label _headerNoteLabel;

	private TabPage _pointsTab;
	private DataGridView _pointsGrid;
	private DataGridViewTextBoxColumn _ptIndexColumn;
	private DataGridViewTextBoxColumn _ptXColumn;
	private DataGridViewTextBoxColumn _ptYColumn;
	private DataGridViewTextBoxColumn _ptZColumn;

	private TabPage _headingsTab;
	private DataGridView _headingsGrid;
	private DataGridViewTextBoxColumn _hdIndexColumn;
	private DataGridViewTextBoxColumn _hdDegreesColumn;
	private DataGridViewTextBoxColumn _hdBamColumn;

	private TabPage _routesTab;
	private DataGridView _routesGrid;
	private DataGridViewTextBoxColumn _rtIndexColumn;
	private DataGridViewTextBoxColumn _rtCountColumn;
	private DataGridViewTextBoxColumn _rtWaypointsColumn;

	private TabPage _linksTab;
	private DataGridView _linksGrid;
	private DataGridViewTextBoxColumn _lrIndexColumn;
	private DataGridViewTextBoxColumn _lrTypeColumn;
	private DataGridViewTextBoxColumn _lrRefAColumn;
	private DataGridViewTextBoxColumn _lrRefBColumn;

	private TabPage _actionsTab;
	private DataGridView _actionsGrid;
	private DataGridViewTextBoxColumn _acIndexColumn;
	private DataGridViewTextBoxColumn _acTypeColumn;
	private DataGridViewTextBoxColumn _acVerbColumn;
	private DataGridViewTextBoxColumn _acSecondaryColumn;
	private DataGridViewTextBoxColumn _acTargetColumn;
	private DataGridViewTextBoxColumn _acRefsColumn;
	private DataGridViewTextBoxColumn _acLutRefsColumn;
	private DataGridViewTextBoxColumn _acArrayAColumn;
	private DataGridViewTextBoxColumn _acArrayBColumn;

	private TabPage _actionPairsTab;
	private DataGridView _actionPairsGrid;
	private DataGridViewTextBoxColumn _apIndexColumn;
	private DataGridViewTextBoxColumn _apPrimaryColumn;
	private DataGridViewTextBoxColumn _apTimerColumn;
	private DataGridViewTextBoxColumn _apSequenceColumn;

	private TabPage _mechsTab;
	private DataGridView _mechsGrid;
	private DataGridViewTextBoxColumn _mechIndexColumn;
	private DataGridViewComboBoxColumn _mechTypeColumn;
	private DataGridViewTextBoxColumn _mechPositionColumn;
	private DataGridViewTextBoxColumn _mechHeadingColumn;
	private DataGridViewTextBoxColumn _mechWeaponsColumn;

	private TabPage _flyersTab;
	private DataGridView _flyersGrid;
	private DataGridViewTextBoxColumn _flyIndexColumn;
	private DataGridViewTextBoxColumn _flyTypeColumn;
	private DataGridViewTextBoxColumn _flyPositionColumn;
	private DataGridViewTextBoxColumn _flyHeadingColumn;

	private TabPage _basesTab;
	private DataGridView _basesGrid;
	private DataGridViewTextBoxColumn _baseIndexColumn;
	private DataGridViewTextBoxColumn _baseTypeColumn;
	private DataGridViewTextBoxColumn _basePositionColumn;
	private DataGridViewTextBoxColumn _baseHeadingColumn;

	private TabPage _routeLinksTab;
	private DataGridView _routeLinksGrid;
	private DataGridViewTextBoxColumn _rlIndexColumn;
	private DataGridViewTextBoxColumn _rlSmall1Column;
	private DataGridViewTextBoxColumn _rlSmall2Column;
	private DataGridViewTextBoxColumn _rlPointColumn;
	private DataGridViewTextBoxColumn _rlRouteColumn;
	private DataGridViewTextBoxColumn _rlDiscriminatorColumn;
	private DataGridViewTextBoxColumn _rlDiscriminatedRefColumn;
	private DataGridViewTextBoxColumn _rlActionColumn;

	private TabPage _groupsTab;
	private DataGridView _groupsGrid;
	private DataGridViewTextBoxColumn _grpIndexColumn;
	private DataGridViewTextBoxColumn _grpRosterColumn;
	private DataGridViewTextBoxColumn _grpFormationColumn;
	private DataGridViewTextBoxColumn _grpPointColumn;
	private DataGridViewTextBoxColumn _grpHeadingColumn;
	private DataGridViewTextBoxColumn _grpRouteColumn;
	private DataGridViewTextBoxColumn _grpMembersColumn;
	private DataGridViewTextBoxColumn _grpRouteLinksColumn;
	private DataGridViewTextBoxColumn _grpBinaryFlagColumn;
	private DataGridViewTextBoxColumn _grpTriStateColumn;
	private DataGridViewTextBoxColumn _grpActionColumn;

	private TabPage _entityLinksTab;
	private DataGridView _entityLinksGrid;
	private DataGridViewTextBoxColumn _elIndexColumn;
	private DataGridViewTextBoxColumn _elUnk02Column;
	private DataGridViewTextBoxColumn _elUnk04Column;
	private DataGridViewTextBoxColumn _elDiscriminatorColumn;
	private DataGridViewTextBoxColumn _elDiscriminatedRefColumn;
	private DataGridViewTextBoxColumn _elPointColumn;
	private DataGridViewTextBoxColumn _elRouteColumn;
	private DataGridViewTextBoxColumn _elLutRefColumn;
	private DataGridViewTextBoxColumn _elPairRefsColumn;
	private DataGridViewTextBoxColumn _elPairTagsColumn;

	private TabPage _unlocksTab;
	private DataGridView _unlocksGrid;
	private DataGridViewTextBoxColumn _unlockValueColumn;
	private Panel _unlocksButtonPanel;
	private Button _addUnlockButton;
	private Button _removeUnlockButton;

	private StatusStrip _statusStrip;
	private ToolStripStatusLabel _statusLabel;
}
