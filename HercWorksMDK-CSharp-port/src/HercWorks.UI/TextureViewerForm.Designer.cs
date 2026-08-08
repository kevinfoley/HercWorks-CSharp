namespace HercWorks.UI;

partial class TextureViewerForm {
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
		_frameLabel = new Label();
		_frameSelector = new NumericUpDown();
		_paletteLabel = new Label();
		_paletteSelector = new ComboBox();
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
		_menuStrip.Items.AddRange(new ToolStripItem[] { _fileMenuItem });
		_menuStrip.Location = new Point(0, 0);
		_menuStrip.Name = "_menuStrip";
		_menuStrip.Size = new Size(620, 24);
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
		_frameSelector.Location = new Point(75, 30);
		_frameSelector.Name = "_frameSelector";
		_frameSelector.Size = new Size(80, 23);
		_frameSelector.TabIndex = 2;
		_frameSelector.ValueChanged += OnFrameChanged;
		//
		// _paletteLabel
		//
		_paletteLabel.AutoSize = true;
		_paletteLabel.Location = new Point(16, 64);
		_paletteLabel.Name = "_paletteLabel";
		_paletteLabel.Size = new Size(50, 15);
		_paletteLabel.TabIndex = 3;
		_paletteLabel.Text = "Palette:";
		//
		// _paletteSelector
		//
		_paletteSelector.DropDownStyle = ComboBoxStyle.DropDownList;
		_paletteSelector.Location = new Point(75, 60);
		_paletteSelector.Name = "_paletteSelector";
		_paletteSelector.Size = new Size(240, 23);
		_paletteSelector.TabIndex = 4;
		_paletteSelector.SelectedIndexChanged += OnPaletteSelectionChanged;
		//
		// _preview
		//
		_preview.BackColor = Color.DimGray;
		_preview.BorderStyle = BorderStyle.FixedSingle;
		_preview.Location = new Point(16, 94);
		_preview.Name = "_preview";
		_preview.Size = new Size(580, 420);
		_preview.SizeMode = PictureBoxSizeMode.Zoom;
		_preview.TabIndex = 5;
		_preview.TabStop = false;
		//
		// _statusStrip
		//
		_statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel });
		_statusStrip.Location = new Point(0, 538);
		_statusStrip.Name = "_statusStrip";
		_statusStrip.Size = new Size(620, 22);
		_statusStrip.TabIndex = 6;
		//
		// _statusLabel
		//
		_statusLabel.Name = "_statusLabel";
		_statusLabel.Text = "No texture loaded.";
		//
		// TextureViewerForm
		//
		Size = new Size(620, 560);
		Controls.Add(_preview);
		Controls.Add(_paletteSelector);
		Controls.Add(_paletteLabel);
		Controls.Add(_frameSelector);
		Controls.Add(_frameLabel);
		Controls.Add(_statusStrip);
		Controls.Add(_menuStrip);
		MainMenuStrip = _menuStrip;
		Name = "TextureViewerForm";
		Text = "Texture Viewer — DBA / DBM";
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
	private Label _frameLabel;
	private NumericUpDown _frameSelector;
	private Label _paletteLabel;
	private ComboBox _paletteSelector;
	private PictureBox _preview;
	private StatusStrip _statusStrip;
	private ToolStripStatusLabel _statusLabel;
}
