namespace HercWorks.UI;

partial class HercBayEditorForm {
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
		_tabs = new TabControl();
		_externalsTab = new TabPage();
		_externalsGrid = new DataGridView();
		_externalsIdColumn = new DataGridViewTextBoxColumn();
		_externalsLabelColumn = new DataGridViewTextBoxColumn();
		_externalsHealthColumn = new DataGridViewTextBoxColumn();
		_internalsTab = new TabPage();
		_internalsGrid = new DataGridView();
		_internalsIdColumn = new DataGridViewTextBoxColumn();
		_internalsLabelColumn = new DataGridViewTextBoxColumn();
		_internalsHealthColumn = new DataGridViewTextBoxColumn();
		_hardpointsTab = new TabPage();
		_hardpointsGrid = new DataGridView();
		_hardpointsIdColumn = new DataGridViewTextBoxColumn();
		_hardpointsLabelColumn = new DataGridViewTextBoxColumn();
		_hardpointsHealthColumn = new DataGridViewTextBoxColumn();
		_weaponsTab = new TabPage();
		_weaponsGrid = new DataGridView();
		_weaponSocketColumn = new DataGridViewTextBoxColumn();
		_weaponIdColumn = new DataGridViewComboBoxColumn();
		_weaponNameIdColumn = new DataGridViewTextBoxColumn();
		_weaponArmorColumn = new DataGridViewTextBoxColumn();
		_weaponInternalColumn = new DataGridViewTextBoxColumn();
		_weaponMissileColumn = new DataGridViewComboBoxColumn();
		_addWeaponButton = new Button();
		_removeWeaponButton = new Button();
		_okButton = new Button();
		_cancelButton = new Button();
		_tabs.SuspendLayout();
		_externalsTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_externalsGrid).BeginInit();
		_internalsTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_internalsGrid).BeginInit();
		_hardpointsTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_hardpointsGrid).BeginInit();
		_weaponsTab.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)_weaponsGrid).BeginInit();
		SuspendLayout();
		//
		// _tabs
		//
		_tabs.Controls.Add(_externalsTab);
		_tabs.Controls.Add(_internalsTab);
		_tabs.Controls.Add(_hardpointsTab);
		_tabs.Controls.Add(_weaponsTab);
		_tabs.Dock = DockStyle.Top;
		_tabs.Location = new Point(0, 0);
		_tabs.Name = "_tabs";
		_tabs.SelectedIndex = 0;
		_tabs.Size = new Size(700, 420);
		_tabs.TabIndex = 0;
		//
		// _externalsTab
		//
		_externalsTab.Controls.Add(_externalsGrid);
		_externalsTab.Location = new Point(4, 24);
		_externalsTab.Name = "_externalsTab";
		_externalsTab.Padding = new Padding(3);
		_externalsTab.Size = new Size(692, 392);
		_externalsTab.TabIndex = 0;
		_externalsTab.Text = "Externals";
		_externalsTab.UseVisualStyleBackColor = true;
		//
		// _externalsGrid
		//
		_externalsGrid.AllowUserToAddRows = false;
		_externalsGrid.AllowUserToDeleteRows = false;
		_externalsGrid.AutoGenerateColumns = false;
		_externalsGrid.Columns.AddRange(new DataGridViewColumn[] {
			_externalsIdColumn, _externalsLabelColumn, _externalsHealthColumn
		});
		_externalsGrid.Dock = DockStyle.Fill;
		_externalsGrid.Location = new Point(3, 3);
		_externalsGrid.Name = "_externalsGrid";
		_externalsGrid.RowHeadersVisible = false;
		_externalsGrid.Size = new Size(686, 386);
		_externalsGrid.TabIndex = 0;
		//
		// _externalsIdColumn
		//
		_externalsIdColumn.DataPropertyName = "Id";
		_externalsIdColumn.HeaderText = "Id";
		_externalsIdColumn.Name = "_externalsIdColumn";
		_externalsIdColumn.ReadOnly = true;
		_externalsIdColumn.Width = 40;
		//
		// _externalsLabelColumn
		//
		_externalsLabelColumn.DataPropertyName = "Label";
		_externalsLabelColumn.HeaderText = "Component";
		_externalsLabelColumn.Name = "_externalsLabelColumn";
		_externalsLabelColumn.ReadOnly = true;
		_externalsLabelColumn.Width = 200;
		//
		// _externalsHealthColumn
		//
		_externalsHealthColumn.DataPropertyName = "Health";
		_externalsHealthColumn.HeaderText = "Health";
		_externalsHealthColumn.Name = "_externalsHealthColumn";
		_externalsHealthColumn.Width = 80;
		//
		// _internalsTab
		//
		_internalsTab.Controls.Add(_internalsGrid);
		_internalsTab.Location = new Point(4, 24);
		_internalsTab.Name = "_internalsTab";
		_internalsTab.Padding = new Padding(3);
		_internalsTab.Size = new Size(692, 392);
		_internalsTab.TabIndex = 1;
		_internalsTab.Text = "Internals";
		_internalsTab.UseVisualStyleBackColor = true;
		//
		// _internalsGrid
		//
		_internalsGrid.AllowUserToAddRows = false;
		_internalsGrid.AllowUserToDeleteRows = false;
		_internalsGrid.AutoGenerateColumns = false;
		_internalsGrid.Columns.AddRange(new DataGridViewColumn[] {
			_internalsIdColumn, _internalsLabelColumn, _internalsHealthColumn
		});
		_internalsGrid.Dock = DockStyle.Fill;
		_internalsGrid.Location = new Point(3, 3);
		_internalsGrid.Name = "_internalsGrid";
		_internalsGrid.RowHeadersVisible = false;
		_internalsGrid.Size = new Size(686, 386);
		_internalsGrid.TabIndex = 0;
		//
		// _internalsIdColumn
		//
		_internalsIdColumn.DataPropertyName = "Id";
		_internalsIdColumn.HeaderText = "Id";
		_internalsIdColumn.Name = "_internalsIdColumn";
		_internalsIdColumn.ReadOnly = true;
		_internalsIdColumn.Width = 40;
		//
		// _internalsLabelColumn
		//
		_internalsLabelColumn.DataPropertyName = "Label";
		_internalsLabelColumn.HeaderText = "Component";
		_internalsLabelColumn.Name = "_internalsLabelColumn";
		_internalsLabelColumn.ReadOnly = true;
		_internalsLabelColumn.Width = 200;
		//
		// _internalsHealthColumn
		//
		_internalsHealthColumn.DataPropertyName = "Health";
		_internalsHealthColumn.HeaderText = "Health";
		_internalsHealthColumn.Name = "_internalsHealthColumn";
		_internalsHealthColumn.Width = 80;
		//
		// _hardpointsTab
		//
		_hardpointsTab.Controls.Add(_hardpointsGrid);
		_hardpointsTab.Location = new Point(4, 24);
		_hardpointsTab.Name = "_hardpointsTab";
		_hardpointsTab.Padding = new Padding(3);
		_hardpointsTab.Size = new Size(692, 392);
		_hardpointsTab.TabIndex = 2;
		_hardpointsTab.Text = "Hardpoints";
		_hardpointsTab.UseVisualStyleBackColor = true;
		//
		// _hardpointsGrid
		//
		_hardpointsGrid.AllowUserToAddRows = false;
		_hardpointsGrid.AllowUserToDeleteRows = false;
		_hardpointsGrid.AutoGenerateColumns = false;
		_hardpointsGrid.Columns.AddRange(new DataGridViewColumn[] {
			_hardpointsIdColumn, _hardpointsLabelColumn, _hardpointsHealthColumn
		});
		_hardpointsGrid.Dock = DockStyle.Fill;
		_hardpointsGrid.Location = new Point(3, 3);
		_hardpointsGrid.Name = "_hardpointsGrid";
		_hardpointsGrid.RowHeadersVisible = false;
		_hardpointsGrid.Size = new Size(686, 386);
		_hardpointsGrid.TabIndex = 0;
		//
		// _hardpointsIdColumn
		//
		_hardpointsIdColumn.DataPropertyName = "Id";
		_hardpointsIdColumn.HeaderText = "Id";
		_hardpointsIdColumn.Name = "_hardpointsIdColumn";
		_hardpointsIdColumn.ReadOnly = true;
		_hardpointsIdColumn.Width = 40;
		//
		// _hardpointsLabelColumn
		//
		_hardpointsLabelColumn.DataPropertyName = "Label";
		_hardpointsLabelColumn.HeaderText = "Hardpoint";
		_hardpointsLabelColumn.Name = "_hardpointsLabelColumn";
		_hardpointsLabelColumn.ReadOnly = true;
		_hardpointsLabelColumn.Width = 200;
		//
		// _hardpointsHealthColumn
		//
		_hardpointsHealthColumn.DataPropertyName = "Health";
		_hardpointsHealthColumn.HeaderText = "Health";
		_hardpointsHealthColumn.Name = "_hardpointsHealthColumn";
		_hardpointsHealthColumn.Width = 80;
		//
		// _weaponsTab
		//
		_weaponsTab.Controls.Add(_weaponsGrid);
		_weaponsTab.Controls.Add(_addWeaponButton);
		_weaponsTab.Controls.Add(_removeWeaponButton);
		_weaponsTab.Location = new Point(4, 24);
		_weaponsTab.Name = "_weaponsTab";
		_weaponsTab.Padding = new Padding(3);
		_weaponsTab.Size = new Size(692, 392);
		_weaponsTab.TabIndex = 3;
		_weaponsTab.Text = "Weapons";
		_weaponsTab.UseVisualStyleBackColor = true;
		//
		// _weaponsGrid
		//
		_weaponsGrid.AllowUserToAddRows = false;
		_weaponsGrid.AllowUserToDeleteRows = false;
		_weaponsGrid.AutoGenerateColumns = false;
		_weaponsGrid.Columns.AddRange(new DataGridViewColumn[] {
			_weaponSocketColumn, _weaponIdColumn, _weaponNameIdColumn, _weaponArmorColumn,
			_weaponInternalColumn, _weaponMissileColumn
		});
		_weaponsGrid.Location = new Point(6, 6);
		_weaponsGrid.Name = "_weaponsGrid";
		_weaponsGrid.RowHeadersVisible = false;
		_weaponsGrid.Size = new Size(680, 340);
		_weaponsGrid.TabIndex = 0;
		//
		// _weaponSocketColumn
		//
		_weaponSocketColumn.HeaderText = "Socket";
		_weaponSocketColumn.Name = "_weaponSocketColumn";
		_weaponSocketColumn.Width = 60;
		//
		// _weaponIdColumn
		//
		_weaponIdColumn.HeaderText = "Weapon";
		_weaponIdColumn.Name = "_weaponIdColumn";
		_weaponIdColumn.Width = 150;
		//
		// _weaponNameIdColumn
		//
		_weaponNameIdColumn.DataPropertyName = "NameId";
		_weaponNameIdColumn.HeaderText = "Name Id";
		_weaponNameIdColumn.Name = "_weaponNameIdColumn";
		_weaponNameIdColumn.Width = 70;
		//
		// _weaponArmorColumn
		//
		_weaponArmorColumn.DataPropertyName = "HealthArmor";
		_weaponArmorColumn.HeaderText = "Armor";
		_weaponArmorColumn.Name = "_weaponArmorColumn";
		_weaponArmorColumn.Width = 70;
		//
		// _weaponInternalColumn
		//
		_weaponInternalColumn.DataPropertyName = "HealthInternal";
		_weaponInternalColumn.HeaderText = "Internal";
		_weaponInternalColumn.Name = "_weaponInternalColumn";
		_weaponInternalColumn.Width = 70;
		//
		// _weaponMissileColumn
		//
		_weaponMissileColumn.HeaderText = "Missile Type";
		_weaponMissileColumn.Name = "_weaponMissileColumn";
		_weaponMissileColumn.Width = 140;
		//
		// _addWeaponButton
		//
		_addWeaponButton.Location = new Point(6, 352);
		_addWeaponButton.Name = "_addWeaponButton";
		_addWeaponButton.Size = new Size(120, 28);
		_addWeaponButton.TabIndex = 1;
		_addWeaponButton.Text = "Add Weapon";
		_addWeaponButton.UseVisualStyleBackColor = true;
		_addWeaponButton.Click += OnAddWeapon;
		//
		// _removeWeaponButton
		//
		_removeWeaponButton.Location = new Point(132, 352);
		_removeWeaponButton.Name = "_removeWeaponButton";
		_removeWeaponButton.Size = new Size(120, 28);
		_removeWeaponButton.TabIndex = 2;
		_removeWeaponButton.Text = "Remove Weapon";
		_removeWeaponButton.UseVisualStyleBackColor = true;
		_removeWeaponButton.Click += OnRemoveWeapon;
		//
		// _okButton
		//
		_okButton.Location = new Point(524, 430);
		_okButton.Name = "_okButton";
		_okButton.Size = new Size(80, 28);
		_okButton.TabIndex = 1;
		_okButton.Text = "OK";
		_okButton.UseVisualStyleBackColor = true;
		_okButton.Click += OnOk;
		//
		// _cancelButton
		//
		_cancelButton.Location = new Point(610, 430);
		_cancelButton.Name = "_cancelButton";
		_cancelButton.Size = new Size(80, 28);
		_cancelButton.TabIndex = 2;
		_cancelButton.Text = "Cancel";
		_cancelButton.UseVisualStyleBackColor = true;
		_cancelButton.Click += OnCancel;
		//
		// HercBayEditorForm
		//
		AcceptButton = _okButton;
		CancelButton = _cancelButton;
		ClientSize = new Size(700, 470);
		Controls.Add(_tabs);
		Controls.Add(_okButton);
		Controls.Add(_cancelButton);
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		Name = "HercBayEditorForm";
		StartPosition = FormStartPosition.CenterParent;
		Text = "Edit Herc Bay";
		_tabs.ResumeLayout(false);
		_externalsTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_externalsGrid).EndInit();
		_internalsTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_internalsGrid).EndInit();
		_hardpointsTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_hardpointsGrid).EndInit();
		_weaponsTab.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)_weaponsGrid).EndInit();
		ResumeLayout(false);
	}

	#endregion

	private TabControl _tabs;
	private TabPage _externalsTab;
	private DataGridView _externalsGrid;
	private DataGridViewTextBoxColumn _externalsIdColumn;
	private DataGridViewTextBoxColumn _externalsLabelColumn;
	private DataGridViewTextBoxColumn _externalsHealthColumn;
	private TabPage _internalsTab;
	private DataGridView _internalsGrid;
	private DataGridViewTextBoxColumn _internalsIdColumn;
	private DataGridViewTextBoxColumn _internalsLabelColumn;
	private DataGridViewTextBoxColumn _internalsHealthColumn;
	private TabPage _hardpointsTab;
	private DataGridView _hardpointsGrid;
	private DataGridViewTextBoxColumn _hardpointsIdColumn;
	private DataGridViewTextBoxColumn _hardpointsLabelColumn;
	private DataGridViewTextBoxColumn _hardpointsHealthColumn;
	private TabPage _weaponsTab;
	private DataGridView _weaponsGrid;
	private DataGridViewTextBoxColumn _weaponSocketColumn;
	private DataGridViewComboBoxColumn _weaponIdColumn;
	private DataGridViewTextBoxColumn _weaponNameIdColumn;
	private DataGridViewTextBoxColumn _weaponArmorColumn;
	private DataGridViewTextBoxColumn _weaponInternalColumn;
	private DataGridViewComboBoxColumn _weaponMissileColumn;
	private Button _addWeaponButton;
	private Button _removeWeaponButton;
	private Button _okButton;
	private Button _cancelButton;
}
