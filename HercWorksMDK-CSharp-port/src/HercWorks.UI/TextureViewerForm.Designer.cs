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
		_helpMenuItem = new ToolStripMenuItem();
		_keyboardShortcutsMenuItem = new ToolStripMenuItem();
		_statusStrip = new StatusStrip();
		_statusLabel = new ToolStripStatusLabel();
		_resolutionStatusLabel = new ToolStripStatusLabel();
		_contentPanel = new Panel();
		_frameLabel = new Label();
		_frameSelector = new NumericUpDown();
		_paletteLabel = new Label();
		_paletteSelector = new ComboBox();
		_previewPanel = new Panel();
		_preview = new PictureBox();
		_menuStrip.SuspendLayout();
		_statusStrip.SuspendLayout();
		_contentPanel.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_frameSelector).BeginInit();
		_previewPanel.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_preview).BeginInit();
		SuspendLayout();
		//
		// _menuStrip
		//
		_menuStrip.Dock = DockStyle.Top;
		_menuStrip.Items.AddRange(new ToolStripItem[] { _fileMenuItem, _helpMenuItem });
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
		// _helpMenuItem
		//
		_helpMenuItem.DropDownItems.AddRange(new ToolStripItem[] { _keyboardShortcutsMenuItem });
		_helpMenuItem.Name = "_helpMenuItem";
		_helpMenuItem.Text = "&Help";
		//
		// _keyboardShortcutsMenuItem
		//
		_keyboardShortcutsMenuItem.Name = "_keyboardShortcutsMenuItem";
		_keyboardShortcutsMenuItem.Text = "&Keyboard Shortcuts...";
		_keyboardShortcutsMenuItem.Click += OnShowKeyboardShortcuts;
		//
		// _statusStrip
		//
		_statusStrip.Dock = DockStyle.Bottom;
		_statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel, _resolutionStatusLabel });
		_statusStrip.Location = new Point(0, 538);
		_statusStrip.Name = "_statusStrip";
		_statusStrip.Size = new Size(620, 22);
		_statusStrip.TabIndex = 6;
		//
		// _statusLabel
		//
		_statusLabel.Name = "_statusLabel";
		_statusLabel.Spring = true;
		_statusLabel.Text = "No texture loaded.";
		_statusLabel.TextAlign = ContentAlignment.MiddleLeft;
		//
		// _resolutionStatusLabel
		//
		_resolutionStatusLabel.Name = "_resolutionStatusLabel";
		_resolutionStatusLabel.Text = "";
		//
		// _contentPanel
		//
		// Fills whatever's left between the docked _menuStrip and _statusStrip — everything inside
		// is positioned relative to this panel's own bounds, so _previewPanel's Anchor never has to
		// reconcile itself against the Form's docked siblings directly (that mismatch is what caused
		// _previewPanel to visually run under _statusStrip when the earlier version anchored straight
		// off the Form).
		//
		_contentPanel.Controls.Add(_frameLabel);
		_contentPanel.Controls.Add(_frameSelector);
		_contentPanel.Controls.Add(_paletteLabel);
		_contentPanel.Controls.Add(_paletteSelector);
		_contentPanel.Controls.Add(_previewPanel);
		_contentPanel.Dock = DockStyle.Fill;
		_contentPanel.Location = new Point(0, 24);
		_contentPanel.Name = "_contentPanel";
		_contentPanel.Size = new Size(620, 514);
		_contentPanel.TabIndex = 7;
		//
		// _frameLabel
		//
		_frameLabel.AutoSize = true;
		_frameLabel.Location = new Point(16, 10);
		_frameLabel.Name = "_frameLabel";
		_frameLabel.Size = new Size(45, 15);
		_frameLabel.TabIndex = 1;
		_frameLabel.Text = "Frame:";
		//
		// _frameSelector
		//
		_frameSelector.Location = new Point(75, 6);
		_frameSelector.Name = "_frameSelector";
		_frameSelector.Size = new Size(80, 23);
		_frameSelector.TabIndex = 2;
		_frameSelector.ValueChanged += OnFrameChanged;
		//
		// _paletteLabel
		//
		_paletteLabel.AutoSize = true;
		_paletteLabel.Location = new Point(16, 40);
		_paletteLabel.Name = "_paletteLabel";
		_paletteLabel.Size = new Size(50, 15);
		_paletteLabel.TabIndex = 3;
		_paletteLabel.Text = "Palette:";
		//
		// _paletteSelector
		//
		_paletteSelector.DropDownStyle = ComboBoxStyle.DropDownList;
		_paletteSelector.Location = new Point(75, 36);
		_paletteSelector.Name = "_paletteSelector";
		_paletteSelector.Size = new Size(240, 23);
		_paletteSelector.TabIndex = 4;
		_paletteSelector.SelectedIndexChanged += OnPaletteSelectionChanged;
		//
		// _previewPanel
		//
		_previewPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
		_previewPanel.AutoScroll = true;
		_previewPanel.BackColor = Color.DimGray;
		_previewPanel.BorderStyle = BorderStyle.FixedSingle;
		_previewPanel.Controls.Add(_preview);
		_previewPanel.Location = new Point(16, 70);
		_previewPanel.Name = "_previewPanel";
		_previewPanel.Size = new Size(580, 420);
		_previewPanel.TabIndex = 5;
		//
		// _preview
		//
		_preview.Location = new Point(0, 0);
		_preview.Name = "_preview";
		_preview.Size = new Size(0, 0);
		_preview.SizeMode = PictureBoxSizeMode.Normal;
		_preview.TabIndex = 0;
		_preview.TabStop = false;
		//
		// TextureViewerForm
		//
		Size = new Size(620, 560);
		MinimumSize = new Size(620, 560);
		Controls.Add(_contentPanel);
		Controls.Add(_statusStrip);
		Controls.Add(_menuStrip);
		MainMenuStrip = _menuStrip;
		Name = "TextureViewerForm";
		Text = "Texture Viewer — DBA / DBM";
		_menuStrip.ResumeLayout(false);
		_menuStrip.PerformLayout();
		_statusStrip.ResumeLayout(false);
		_statusStrip.PerformLayout();
		_contentPanel.ResumeLayout(false);
		_contentPanel.PerformLayout();
		((System.ComponentModel.ISupportInitialize)_frameSelector).EndInit();
		_previewPanel.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_preview).EndInit();
		ResumeLayout(false);
	}

	#endregion

	private MenuStrip _menuStrip;
	private ToolStripMenuItem _fileMenuItem;
	private ToolStripMenuItem _openImageMenuItem;
	private ToolStripMenuItem _openPaletteMenuItem;
	private ToolStripSeparator _fileMenuSeparator;
	private ToolStripMenuItem _closeMenuItem;
	private ToolStripMenuItem _helpMenuItem;
	private ToolStripMenuItem _keyboardShortcutsMenuItem;
	private StatusStrip _statusStrip;
	private ToolStripStatusLabel _statusLabel;
	private ToolStripStatusLabel _resolutionStatusLabel;
	private Panel _contentPanel;
	private Label _frameLabel;
	private NumericUpDown _frameSelector;
	private Label _paletteLabel;
	private ComboBox _paletteSelector;
	private Panel _previewPanel;
	private PictureBox _preview;
}
