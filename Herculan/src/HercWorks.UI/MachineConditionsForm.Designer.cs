namespace HercWorks.UI;

partial class MachineConditionsForm {
	private System.ComponentModel.IContainer components = null!;

	protected override void Dispose(bool disposing) {
		if (disposing && components != null) {
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent() {
		_tabs = new TabControl();
		_externalsTab = new TabPage();
		_externalsGrid = new DataGridView();
		_internalsTab = new TabPage();
		_internalsGrid = new DataGridView();
		_hardpointsTab = new TabPage();
		_hardpointsGrid = new DataGridView();
		_buttonPanel = new Panel();
		_okButton = new Button();
		_cancelButton = new Button();
		_tabs.SuspendLayout();
		_externalsTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_externalsGrid).BeginInit();
		_internalsTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_internalsGrid).BeginInit();
		_hardpointsTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_hardpointsGrid).BeginInit();
		_buttonPanel.SuspendLayout();
		SuspendLayout();
		//
		// _tabs
		//
		_tabs.Controls.Add(_externalsTab);
		_tabs.Controls.Add(_internalsTab);
		_tabs.Controls.Add(_hardpointsTab);
		_tabs.Dock = DockStyle.Fill;
		_tabs.Location = new Point(0, 0);
		_tabs.Name = "_tabs";
		_tabs.SelectedIndex = 0;
		_tabs.Size = new Size(484, 411);
		_tabs.TabIndex = 0;
		//
		// _externalsTab
		//
		_externalsTab.Controls.Add(_externalsGrid);
		_externalsTab.Name = "_externalsTab";
		_externalsTab.Padding = new Padding(3);
		_externalsTab.TabIndex = 0;
		_externalsTab.Text = "Externals";
		_externalsTab.UseVisualStyleBackColor = true;
		ConfigurePartGrid(_externalsGrid, "_externalsGrid", "Facet");
		//
		// _internalsTab
		//
		_internalsTab.Controls.Add(_internalsGrid);
		_internalsTab.Name = "_internalsTab";
		_internalsTab.Padding = new Padding(3);
		_internalsTab.TabIndex = 1;
		_internalsTab.Text = "Internals";
		_internalsTab.UseVisualStyleBackColor = true;
		ConfigurePartGrid(_internalsGrid, "_internalsGrid", "Component");
		//
		// _hardpointsTab
		//
		_hardpointsTab.Controls.Add(_hardpointsGrid);
		_hardpointsTab.Name = "_hardpointsTab";
		_hardpointsTab.Padding = new Padding(3);
		_hardpointsTab.TabIndex = 2;
		_hardpointsTab.Text = "Hardpoints";
		_hardpointsTab.UseVisualStyleBackColor = true;
		ConfigurePartGrid(_hardpointsGrid, "_hardpointsGrid", "Hardpoint");
		//
		// _buttonPanel
		//
		_buttonPanel.Controls.Add(_okButton);
		_buttonPanel.Controls.Add(_cancelButton);
		_buttonPanel.Dock = DockStyle.Bottom;
		_buttonPanel.Location = new Point(0, 411);
		_buttonPanel.Name = "_buttonPanel";
		_buttonPanel.Size = new Size(484, 44);
		_buttonPanel.TabIndex = 1;
		//
		// _okButton
		//
		_okButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		_okButton.Location = new Point(290, 8);
		_okButton.Name = "_okButton";
		_okButton.Size = new Size(88, 28);
		_okButton.TabIndex = 0;
		_okButton.Text = "OK";
		_okButton.UseVisualStyleBackColor = true;
		_okButton.Click += OnOk;
		//
		// _cancelButton
		//
		_cancelButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		_cancelButton.Location = new Point(384, 8);
		_cancelButton.Name = "_cancelButton";
		_cancelButton.Size = new Size(88, 28);
		_cancelButton.TabIndex = 1;
		_cancelButton.Text = "Cancel";
		_cancelButton.UseVisualStyleBackColor = true;
		_cancelButton.Click += OnCancel;
		//
		// MachineConditionsForm
		//
		AcceptButton = _okButton;
		AutoScaleDimensions = new SizeF(7F, 15F);
		AutoScaleMode = AutoScaleMode.Font;
		CancelButton = _cancelButton;
		ClientSize = new Size(484, 455);
		Controls.Add(_tabs);
		Controls.Add(_buttonPanel);
		MinimizeBox = false;
		Name = "MachineConditionsForm";
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.CenterParent;
		Text = "Conditions";
		_tabs.ResumeLayout(false);
		_externalsTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_externalsGrid).EndInit();
		_internalsTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_internalsGrid).EndInit();
		_hardpointsTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_hardpointsGrid).EndInit();
		_buttonPanel.ResumeLayout(false);
		ResumeLayout(false);
	}

	/// <summary>The three grids are the same shape: a read-only id and label, and an editable condition.</summary>
	private static void ConfigurePartGrid(DataGridView grid, string name, string labelHeader) {
		grid.AllowUserToAddRows = false;
		grid.AllowUserToDeleteRows = false;
		grid.AutoGenerateColumns = false;
		grid.Dock = DockStyle.Fill;
		grid.Name = name;
		grid.RowHeadersVisible = false;
		grid.Columns.AddRange(new DataGridViewColumn[] {
			new DataGridViewTextBoxColumn {
				DataPropertyName = nameof(HercPartRow.Id), HeaderText = "Index", ReadOnly = true, Width = 60
			},
			new DataGridViewTextBoxColumn {
				DataPropertyName = nameof(HercPartRow.Label), HeaderText = labelHeader, ReadOnly = true, Width = 220
			},
			new DataGridViewTextBoxColumn {
				DataPropertyName = nameof(HercPartRow.Health), HeaderText = "Condition (0-100)", Width = 130
			}
		});
	}

	private TabControl _tabs;
	private TabPage _externalsTab;
	private DataGridView _externalsGrid;
	private TabPage _internalsTab;
	private DataGridView _internalsGrid;
	private TabPage _hardpointsTab;
	private DataGridView _hardpointsGrid;
	private Panel _buttonPanel;
	private Button _okButton;
	private Button _cancelButton;
}
