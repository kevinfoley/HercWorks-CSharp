namespace HercWorks.UI;

partial class ImageExportForm {
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
		_openImageMenuItem = new ToolStripMenuItem();
		_openPaletteMenuItem = new ToolStripMenuItem();
		_fileMenuSeparator = new ToolStripSeparator();
		_closeMenuItem = new ToolStripMenuItem();
		_exportMenuItem = new ToolStripMenuItem();
		_exportCurrentFrameMenuItem = new ToolStripMenuItem();
		_exportAllFramesMenuItem = new ToolStripMenuItem();
		_unpackToDbmMenuItem = new ToolStripMenuItem();
		_exportMenuSeparator = new ToolStripSeparator();
		_exportPaletteGridMenuItem = new ToolStripMenuItem();
		_frameLabel = new Label();
		_frameSelector = new NumericUpDown();
		_preview = new PictureBox();
		_statusStrip = new StatusStrip();
		_statusLabel = new ToolStripStatusLabel();
		_menuStrip.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_frameSelector).BeginInit();
		((System.ComponentModel.ISupportInitialize)_preview).BeginInit();
		_statusStrip.SuspendLayout();
		SuspendLayout();
		//
		// _menuStrip
		//
		_menuStrip.Items.AddRange(new ToolStripItem[] { _fileMenuItem, _exportMenuItem });
		_menuStrip.Location = new Point(0, 0);
		_menuStrip.Name = "_menuStrip";
		_menuStrip.Size = new Size(600, 24);
		_menuStrip.TabIndex = 0;
		//
		// _fileMenuItem
		//
		_fileMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
			_openImageMenuItem, _openPaletteMenuItem, _fileMenuSeparator, _closeMenuItem
		});
		_fileMenuItem.Name = "_fileMenuItem";
		_fileMenuItem.Text = "&File";
		//
		// _openImageMenuItem
		//
		_openImageMenuItem.Name = "_openImageMenuItem";
		_openImageMenuItem.Text = "&Open Image (DBA/DBM)...";
		_openImageMenuItem.Click += OnOpenImage;
		//
		// _openPaletteMenuItem
		//
		_openPaletteMenuItem.Name = "_openPaletteMenuItem";
		_openPaletteMenuItem.Text = "Open &Palette (DPL)...";
		_openPaletteMenuItem.Click += OnOpenPalette;
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
		// _exportMenuItem
		//
		_exportMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
			_exportCurrentFrameMenuItem, _exportAllFramesMenuItem, _unpackToDbmMenuItem,
			_exportMenuSeparator, _exportPaletteGridMenuItem
		});
		_exportMenuItem.Name = "_exportMenuItem";
		_exportMenuItem.Text = "&Export";
		//
		// _exportCurrentFrameMenuItem
		//
		_exportCurrentFrameMenuItem.Name = "_exportCurrentFrameMenuItem";
		_exportCurrentFrameMenuItem.Text = "Export Current Frame as PNG...";
		_exportCurrentFrameMenuItem.Click += OnExportCurrentFrame;
		//
		// _exportAllFramesMenuItem
		//
		_exportAllFramesMenuItem.Name = "_exportAllFramesMenuItem";
		_exportAllFramesMenuItem.Text = "Export All Frames as PNG (Folder)...";
		_exportAllFramesMenuItem.Click += OnExportAllFrames;
		//
		// _unpackToDbmMenuItem
		//
		_unpackToDbmMenuItem.Name = "_unpackToDbmMenuItem";
		_unpackToDbmMenuItem.Text = "Unpack to Separate DBM Files (Folder)...";
		_unpackToDbmMenuItem.Click += OnUnpackToDbm;
		//
		// _exportMenuSeparator
		//
		_exportMenuSeparator.Name = "_exportMenuSeparator";
		//
		// _exportPaletteGridMenuItem
		//
		_exportPaletteGridMenuItem.Name = "_exportPaletteGridMenuItem";
		_exportPaletteGridMenuItem.Text = "Export Palette as Grid PNG...";
		_exportPaletteGridMenuItem.Click += OnExportPaletteGrid;
		//
		// _frameLabel
		//
		_frameLabel.AutoSize = true;
		_frameLabel.Location = new Point(16, 34);
		_frameLabel.Name = "_frameLabel";
		_frameLabel.Size = new Size(45, 15);
		_frameLabel.TabIndex = 1;
		_frameLabel.Text = "Frame:";
		//
		// _frameSelector
		//
		_frameSelector.Location = new Point(70, 30);
		_frameSelector.Name = "_frameSelector";
		_frameSelector.Size = new Size(80, 23);
		_frameSelector.TabIndex = 2;
		_frameSelector.ValueChanged += OnFrameChanged;
		//
		// _preview
		//
		_preview.BackColor = Color.DimGray;
		_preview.BorderStyle = BorderStyle.FixedSingle;
		_preview.Location = new Point(16, 60);
		_preview.Name = "_preview";
		_preview.Size = new Size(560, 380);
		_preview.SizeMode = PictureBoxSizeMode.Zoom;
		_preview.TabIndex = 3;
		_preview.TabStop = false;
		//
		// _statusStrip
		//
		_statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel });
		_statusStrip.Location = new Point(0, 478);
		_statusStrip.Name = "_statusStrip";
		_statusStrip.Size = new Size(600, 22);
		_statusStrip.TabIndex = 4;
		//
		// _statusLabel
		//
		_statusLabel.Name = "_statusLabel";
		_statusLabel.Text = "No image loaded.";
		//
		// ImageExportForm
		//
		Size = new Size(600, 500);
		Controls.Add(_preview);
		Controls.Add(_frameSelector);
		Controls.Add(_frameLabel);
		Controls.Add(_statusStrip);
		Controls.Add(_menuStrip);
		MainMenuStrip = _menuStrip;
		Name = "ImageExportForm";
		Text = "Image Export — DBA / DBM / DPL";
		_menuStrip.ResumeLayout(false);
		_menuStrip.PerformLayout();
		((System.ComponentModel.ISupportInitialize)_frameSelector).EndInit();
		((System.ComponentModel.ISupportInitialize)_preview).EndInit();
		_statusStrip.ResumeLayout(false);
		_statusStrip.PerformLayout();
		ResumeLayout(false);
		PerformLayout();
	}

	#endregion

	private MenuStrip _menuStrip;
	private ToolStripMenuItem _fileMenuItem;
	private ToolStripMenuItem _openImageMenuItem;
	private ToolStripMenuItem _openPaletteMenuItem;
	private ToolStripSeparator _fileMenuSeparator;
	private ToolStripMenuItem _closeMenuItem;
	private ToolStripMenuItem _exportMenuItem;
	private ToolStripMenuItem _exportCurrentFrameMenuItem;
	private ToolStripMenuItem _exportAllFramesMenuItem;
	private ToolStripMenuItem _unpackToDbmMenuItem;
	private ToolStripSeparator _exportMenuSeparator;
	private ToolStripMenuItem _exportPaletteGridMenuItem;
	private Label _frameLabel;
	private NumericUpDown _frameSelector;
	private PictureBox _preview;
	private StatusStrip _statusStrip;
	private ToolStripStatusLabel _statusLabel;
}
