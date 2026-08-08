namespace HercWorks.UI;

partial class MainForm {
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
		_openVolMenuItem = new ToolStripMenuItem();
		_unpackVolMenuItem = new ToolStripMenuItem();
		_fileMenuSeparator = new ToolStripSeparator();
		_exitMenuItem = new ToolStripMenuItem();
		_editMenuItem = new ToolStripMenuItem();
		_hercStatsMenuItem = new ToolStripMenuItem();
		_itemStatsMenuItem = new ToolStripMenuItem();
		_campaignResourcesMenuItem = new ToolStripMenuItem();
		_missionFilesMenuItem = new ToolStripMenuItem();
		_toolsMenuItem = new ToolStripMenuItem();
		_imageExportMenuItem = new ToolStripMenuItem();
		_volTree = new TreeView();
		_fileDetails = new ListView();
		_fileDetailsPropertyColumn = new ColumnHeader();
		_fileDetailsValueColumn = new ColumnHeader();
		_contentTree = new TreeView();
		_statusStrip = new StatusStrip();
		_statusLabel = new ToolStripStatusLabel();
		_menuStrip.SuspendLayout();
		_statusStrip.SuspendLayout();
		SuspendLayout();
		//
		// _menuStrip
		//
		_menuStrip.Items.AddRange(new ToolStripItem[] { _fileMenuItem, _editMenuItem, _toolsMenuItem });
		_menuStrip.Location = new Point(0, 0);
		_menuStrip.Name = "_menuStrip";
		_menuStrip.Size = new Size(1000, 24);
		_menuStrip.TabIndex = 0;
		_menuStrip.Text = "menuStrip";
		//
		// _fileMenuItem
		//
		_fileMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
			_openVolMenuItem, _unpackVolMenuItem, _fileMenuSeparator, _exitMenuItem
		});
		_fileMenuItem.Name = "_fileMenuItem";
		_fileMenuItem.Text = "&File";
		//
		// _openVolMenuItem
		//
		_openVolMenuItem.Name = "_openVolMenuItem";
		_openVolMenuItem.Text = "&Open VOL...";
		_openVolMenuItem.Click += OnOpenVol;
		//
		// _unpackVolMenuItem
		//
		_unpackVolMenuItem.Name = "_unpackVolMenuItem";
		_unpackVolMenuItem.Text = "&Unpack VOL To Folder...";
		_unpackVolMenuItem.Click += OnUnpackVol;
		//
		// _fileMenuSeparator
		//
		_fileMenuSeparator.Name = "_fileMenuSeparator";
		//
		// _exitMenuItem
		//
		_exitMenuItem.Name = "_exitMenuItem";
		_exitMenuItem.Text = "E&xit";
		_exitMenuItem.Click += OnExit;
		//
		// _editMenuItem
		//
		_editMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
			_hercStatsMenuItem, _itemStatsMenuItem, _campaignResourcesMenuItem, _missionFilesMenuItem
		});
		_editMenuItem.Name = "_editMenuItem";
		_editMenuItem.Text = "&Edit";
		//
		// _hercStatsMenuItem
		//
		_hercStatsMenuItem.Name = "_hercStatsMenuItem";
		_hercStatsMenuItem.Text = "Herc Stats...";
		_hercStatsMenuItem.Click += OnOpenHercStats;
		//
		// _itemStatsMenuItem
		//
		_itemStatsMenuItem.Name = "_itemStatsMenuItem";
		_itemStatsMenuItem.Text = "Item Stats...";
		_itemStatsMenuItem.Click += OnOpenItemStats;
		//
		// _campaignResourcesMenuItem
		//
		_campaignResourcesMenuItem.Name = "_campaignResourcesMenuItem";
		_campaignResourcesMenuItem.Text = "Campaign Resources...";
		_campaignResourcesMenuItem.Click += OnOpenCampaignResources;
		//
		// _missionFilesMenuItem
		//
		_missionFilesMenuItem.Enabled = false;
		_missionFilesMenuItem.Name = "_missionFilesMenuItem";
		_missionFilesMenuItem.Text = "Mission Files...";
		//
		// _toolsMenuItem
		//
		_toolsMenuItem.DropDownItems.AddRange(new ToolStripItem[] { _imageExportMenuItem });
		_toolsMenuItem.Name = "_toolsMenuItem";
		_toolsMenuItem.Text = "&Tools";
		//
		// _imageExportMenuItem
		//
		_imageExportMenuItem.Name = "_imageExportMenuItem";
		_imageExportMenuItem.Text = "Image Export (DBA/DBM/DPL)...";
		_imageExportMenuItem.Click += OnOpenImageExport;
		//
		// _volTree
		//
		_volTree.Dock = DockStyle.Left;
		_volTree.Location = new Point(0, 24);
		_volTree.Name = "_volTree";
		_volTree.Size = new Size(320, 604);
		_volTree.TabIndex = 1;
		_volTree.AfterSelect += OnTreeSelect;
		//
		// _fileDetails
		//
		// Fixed-height panel docked to the top of the right-hand area: the metadata list is
		// always exactly 7 rows plus a header, so a scrollable/growing area isn't needed here —
		// leaves the rest of the vertical space for _contentTree below it.
		_fileDetails.Columns.AddRange(new ColumnHeader[] { _fileDetailsPropertyColumn, _fileDetailsValueColumn });
		_fileDetails.Dock = DockStyle.Top;
		_fileDetails.FullRowSelect = true;
		_fileDetails.Height = 180;
		_fileDetails.Location = new Point(320, 24);
		_fileDetails.Name = "_fileDetails";
		_fileDetails.Size = new Size(680, 180);
		_fileDetails.TabIndex = 2;
		_fileDetails.UseCompatibleStateImageBehavior = false;
		_fileDetails.View = View.Details;
		//
		// _fileDetailsPropertyColumn
		//
		_fileDetailsPropertyColumn.Text = "Property";
		_fileDetailsPropertyColumn.Width = 160;
		//
		// _fileDetailsValueColumn
		//
		_fileDetailsValueColumn.Text = "Value";
		_fileDetailsValueColumn.Width = 480;
		//
		// _contentTree
		//
		_contentTree.Dock = DockStyle.Fill;
		_contentTree.Location = new Point(320, 204);
		_contentTree.Name = "_contentTree";
		_contentTree.Size = new Size(680, 424);
		_contentTree.TabIndex = 3;
		//
		// _statusStrip
		//
		_statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel });
		_statusStrip.Location = new Point(0, 628);
		_statusStrip.Name = "_statusStrip";
		_statusStrip.Size = new Size(1000, 22);
		_statusStrip.TabIndex = 4;
		//
		// _statusLabel
		//
		_statusLabel.Name = "_statusLabel";
		_statusLabel.Text = "No VOL file loaded.";
		//
		// MainForm
		//
		Size = new Size(1000, 650);
		Controls.Add(_contentTree);
		Controls.Add(_fileDetails);
		Controls.Add(_volTree);
		Controls.Add(_statusStrip);
		Controls.Add(_menuStrip);
		MainMenuStrip = _menuStrip;
		Name = "MainForm";
		Text = "HercWorks MDK";
		_menuStrip.ResumeLayout(false);
		_menuStrip.PerformLayout();
		_statusStrip.ResumeLayout(false);
		_statusStrip.PerformLayout();
		ResumeLayout(false);
		PerformLayout();
	}

	#endregion

	private MenuStrip _menuStrip;
	private ToolStripMenuItem _fileMenuItem;
	private ToolStripMenuItem _openVolMenuItem;
	private ToolStripMenuItem _unpackVolMenuItem;
	private ToolStripSeparator _fileMenuSeparator;
	private ToolStripMenuItem _exitMenuItem;
	private ToolStripMenuItem _editMenuItem;
	private ToolStripMenuItem _hercStatsMenuItem;
	private ToolStripMenuItem _itemStatsMenuItem;
	private ToolStripMenuItem _campaignResourcesMenuItem;
	private ToolStripMenuItem _missionFilesMenuItem;
	private ToolStripMenuItem _toolsMenuItem;
	private ToolStripMenuItem _imageExportMenuItem;
	private TreeView _volTree;
	private ListView _fileDetails;
	private ColumnHeader _fileDetailsPropertyColumn;
	private ColumnHeader _fileDetailsValueColumn;
	private TreeView _contentTree;
	private StatusStrip _statusStrip;
	private ToolStripStatusLabel _statusLabel;
}
