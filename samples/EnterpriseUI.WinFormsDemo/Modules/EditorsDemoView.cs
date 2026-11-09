using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Editors;
using EnterpriseUI.Validation;
using EnterpriseUI.WinForms;
using EnterpriseUI.WinFormsDemo.Controls;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class EditorsDemoModel
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal AnnualBudget { get; set; }
    public DateTime EffectiveDate { get; set; } = DateTime.Today;
    public string Department { get; set; } = "Engineering";
    public bool EnableNotifications { get; set; } = true;
}

public class EditorsDemoView : UserControl
{

    private static readonly string[] CachedValues3 = new[] { "North America", "EMEA", "APAC", "LATAM" };

    private static readonly string[] CachedValues2 = new[] { "CSharp", "WinForms", "DataGrid", "EnterpriseVirtualGrid", "EnterpriseRibbonBar", "EnterpriseScheduler", "EnterpriseGanttView", "KanbanBoard" };

    private static readonly object[] CachedValues1 = new object[] { "Executive", "Engineering", "Finance", "Legal", "Quality Assurance", "Product Management" };
    private readonly BaseDemoView _baseView=new BaseDemoView();
    private readonly EnterpriseTextBoxControl _txtFullName=new EnterpriseTextBoxControl();
    private readonly EnterpriseTextBoxControl _txtEmail=new EnterpriseTextBoxControl();
    private readonly EnterpriseButtonTextBoxControl _txtBrowsePath=new EnterpriseButtonTextBoxControl();
    private readonly NumericUpDown _numBudget=new NumericUpDown();
    private readonly DateTimePicker _dtpEffectiveDate=new DateTimePicker();
    private readonly EnterpriseFontDropDownControl _cmbFont=new EnterpriseFontDropDownControl();
    private readonly System.Windows.Forms.ComboBox _cmbDept=new System.Windows.Forms.ComboBox();
    private readonly EnterpriseToggleSwitchControl _tglNotifications=new EnterpriseToggleSwitchControl();
    private readonly Label _lblValidationSummary=new Label();

    public EditorsDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _baseView.Dock = DockStyle.Fill ;
        _baseView.BindDemoMetadata(metadata);
        Controls.Add(_baseView);

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9f),
            Padding = new Point(8, 4)
        };

        // ====================================================================
        // Tab 1: Themed Form Controls (EnterpriseTextBox, EnterpriseButtonTextBox, EnterpriseBrowseEditor, EnterpriseToggleSwitch, EnterpriseFontDropDownList)
        // ====================================================================
        var tabCoreForm = new TabPage("📝 Form Controls (TextBox, BrowseEditor, ToggleSwitch)");
        var formPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16), AutoScroll = true };

        int y = 12;
        formPanel.Controls.Add(new Label { Text = "EnterpriseTextBox (Prefix + Clear ✕ + Focus Ring):", Location = new Point(16, y + 5), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
        _txtFullName.            Location = new Point(310, y);
        _txtFullName.            Width = 300;
        _txtFullName.            PrefixGlyph = "👤";
        _txtFullName.            PlaceholderText = "Enter full executive name...";
        _txtFullName.            Text = "Alexander Hamilton"
;
        _txtFullName.ValueChanged += (_, _) => _baseView.LogEvent("EnterpriseTextBox.Text", _txtFullName.Text);
        formPanel.Controls.Add(_txtFullName);
        y += 40;

        formPanel.Controls.Add(new Label { Text = "EnterpriseTextBox (Email Validation State):", Location = new Point(16, y + 5), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
        _txtEmail.            Location = new Point(310, y);
        _txtEmail.            Width = 300;
        _txtEmail.            PrefixGlyph = "✉";
        _txtEmail.            PlaceholderText = "name@company.com";
        _txtEmail.            Text = "a.hamilton@treasury.gov"
;
        _txtEmail.ValueChanged += (_, _) => _baseView.LogEvent("EmailTextBox.Text", _txtEmail.Text);
        formPanel.Controls.Add(_txtEmail);
        y += 40;

        formPanel.Controls.Add(new Label { Text = "EnterpriseButtonTextBox / EnterpriseBrowseEditor:", Location = new Point(16, y + 5), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
        _txtBrowsePath.            Location = new Point(310, y);
        _txtBrowsePath.            Width = 300;
        _txtBrowsePath.            EnableFileBrowseOnRightClick = true
;
        _txtBrowsePath.LeftButtonClick += (_, _) => _baseView.LogEvent("ButtonTextBox.LeftButtonClick", _txtBrowsePath.Text);
        _txtBrowsePath.RightButtonClick += (_, _) => _baseView.LogEvent("BrowseEditor.RightButtonClick", _txtBrowsePath.Text);
        formPanel.Controls.Add(_txtBrowsePath);
        y += 40;

        formPanel.Controls.Add(new Label { Text = "EnterpriseSpinEditor (Annual Budget $):", Location = new Point(16, y + 4), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
        _numBudget.            Location = new Point(310, y);
        _numBudget.            Width = 145;
        _numBudget.            Minimum = 0;
        _numBudget.            Maximum = 10000000;
        _numBudget.            Increment = 5000;
        _numBudget.            ThousandsSeparator = true;
        _numBudget.            Value = 750000
;
        _numBudget.ValueChanged += (_, _) => _baseView.LogEvent("SpinEditor.Value", _numBudget.Value.ToString("C0"));

        _dtpEffectiveDate.Location = new Point(465, y);

        _dtpEffectiveDate.Width = 145;

        _dtpEffectiveDate.Value = DateTime.Today ;
        _dtpEffectiveDate.ValueChanged += (_, _) => _baseView.LogEvent("DateTimePicker.Value", _dtpEffectiveDate.Value.ToShortDateString());
        formPanel.Controls.AddRange(new Control[] { _numBudget, _dtpEffectiveDate });
        y += 38;

        formPanel.Controls.Add(new Label { Text = "EnterpriseDropDownList & EnterpriseFontDropDownList:", Location = new Point(16, y + 4), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
        _cmbDept.Location = new Point(310, y);
        _cmbDept.Width = 145;
        _cmbDept.DropDownStyle = ComboBoxStyle.DropDownList ;
        _cmbDept.Items.AddRange(CachedValues1);
        _cmbDept.SelectedIndex = 1;
        _cmbDept.SelectedIndexChanged += (_, _) => _baseView.LogEvent("DropDownList.SelectedItem", _cmbDept.SelectedItem?.ToString() ?? "");

        _cmbFont.Location = new Point(465, y);

        _cmbFont.Width = 145 ;
        _cmbFont.SelectedIndexChanged += (_, _) => _baseView.LogEvent("FontDropDownList.SelectedFont", _cmbFont.SelectedItem?.ToString() ?? "");
        formPanel.Controls.AddRange(new Control[] { _cmbDept, _cmbFont });
        y += 38;

        formPanel.Controls.Add(new Label { Text = "EnterpriseToggleSwitch (Push Notifications):", Location = new Point(16, y + 5), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
        _tglNotifications.Location = new Point(310, y);
        _tglNotifications.IsOn = true ;
        var lblSwitchState = new Label { Text = "Enabled (ON)", Location = new Point(398, y + 6), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _tglNotifications.Toggled += isOn =>
        {
            lblSwitchState.Text = isOn ? "Enabled (ON)" : "Muted (OFF)";
            _baseView.LogEvent("EnterpriseToggleSwitch.IsOn", isOn.ToString());
        };
        formPanel.Controls.AddRange(new Control[] { _tglNotifications, lblSwitchState });
        y += 40;

        var btnValidate = new EnterpriseButtonControl
        {
            Text = "✓ Validate Form Controls",
            Location = new Point(16, y),
            Size = new Size(210, 34),
            Variant = EnterpriseButtonVariant.Primary
        };
        btnValidate.Click += (_, _) => RunValidation();
        formPanel.Controls.Add(btnValidate);

        _lblValidationSummary.            Location = new Point(240, y + 8);

        _lblValidationSummary.            AutoSize = true;

        _lblValidationSummary.            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);

        _lblValidationSummary.            ForeColor = Color.FromArgb(16, 124, 65);

        _lblValidationSummary.            Text = "Status: All form inputs valid and compliant."
;
        formPanel.Controls.Add(_lblValidationSummary);
        tabCoreForm.Controls.Add(formPanel);

        // ====================================================================
        // Tab 2: Buttons, SplitButton, DropDownButton, RepeatButton, CheckBox, RadioButton & EnterpriseCollapsiblePanel
        // ====================================================================
        var tabButtonsSuite = new TabPage("🔘 Buttons, SplitButton, RepeatButton & Panels");
        var btnPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16), AutoScroll = true };

        var lblBtnHeader = new Label
        {
            Text = "EnterpriseButton Variants (Primary, Success, Danger, Outline & Notification Badge)",
            Location = new Point(16, 10),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

        var btnPrimary = new EnterpriseButtonControl { Text = "🚀 Primary Button", Location = new Point(16, 38), Size = new Size(155, 36), Variant = EnterpriseButtonVariant.Primary, BadgeText = "4" };
        var btnSuccess = new EnterpriseButtonControl { Text = "✔ Approve Ledger", Location = new Point(182, 38), Size = new Size(155, 36), Variant = EnterpriseButtonVariant.Success };
        var btnDanger = new EnterpriseButtonControl { Text = "✖ Revoke Token", Location = new Point(348, 38), Size = new Size(155, 36), Variant = EnterpriseButtonVariant.Danger };
        var btnOutline = new EnterpriseButtonControl { Text = "⚙ Outline Action", Location = new Point(514, 38), Size = new Size(155, 36), Variant = EnterpriseButtonVariant.Outline };

        btnPrimary.Click += (_, _) => _baseView.LogEvent("EnterpriseButton.Click", "Primary Button Clicked");
        btnSuccess.Click += (_, _) => _baseView.LogEvent("EnterpriseButton.Click", "Approve Ledger Clicked");
        btnDanger.Click += (_, _) => _baseView.LogEvent("EnterpriseButton.Click", "Revoke Token Clicked");
        btnOutline.Click += (_, _) => _baseView.LogEvent("EnterpriseButton.Click", "Outline Action Clicked");

        var lblSplitHeader = new Label
        {
            Text = "EnterpriseSplitButton, EnterpriseDropDownButton & EnterpriseRepeatButton (Press-and-Hold)",
            Location = new Point(16, 88),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

        var splitBtn = new EnterpriseSplitButtonControl { Text = "Export Document", Location = new Point(16, 116), Size = new Size(175, 34), IsSplitMode = true };
        var dropDownBtn = new EnterpriseSplitButtonControl { Text = "Quick Actions ▾", Location = new Point(202, 116), Size = new Size(165, 34), IsSplitMode = false };
        var repeatBtn = new EnterpriseRepeatButtonControl { Location = new Point(378, 116), Size = new Size(185, 34) };
        var progressBar = new EnterpriseProgressBarControl { Location = new Point(575, 120), Size = new Size(220, 26), Value = 45 };

        splitBtn.PrimaryActionClicked += (_, _) => _baseView.LogEvent("EnterpriseSplitButton.PrimaryClick", "Default Export Invoked");
        splitBtn.MenuItemSelected += item => _baseView.LogEvent("EnterpriseSplitButton.MenuItemSelected", item);
        dropDownBtn.MenuItemSelected += item => _baseView.LogEvent("EnterpriseDropDownButton.Selected", item);
        repeatBtn.RepeatedClick += cnt =>
        {
            progressBar.Value = (cnt * 5) % 105;
            _baseView.LogEvent("EnterpriseRepeatButton.RepeatedClick", $"Tick #{cnt} -> Progress={progressBar.Value}%");
        };

        var collapsiblePanel = new EnterpriseCollapsiblePanelControl
        {
            Location = new Point(16, 166),
            Size = new Size(780, 125)
        };
        var chkTriState = new CheckBox { Text = "EnterpriseCheckBox (ThreeState = true)", Location = new Point(12, 12), ThreeState = true, CheckState = CheckState.Indeterminate, AutoSize = true };
        var rbOpt1 = new RadioButton { Text = "EnterpriseRadioButton: Real-Time Sync", Location = new Point(260, 12), Checked = true, AutoSize = true };
        var rbOpt2 = new RadioButton { Text = "EnterpriseRadioButton: Batch Snapshot", Location = new Point(490, 12), AutoSize = true };
        var trkSlider = new TrackBar { Location = new Point(12, 42), Size = new Size(320, 42), Minimum = 0, Maximum = 100, Value = 45, TickFrequency = 10 };
        trkSlider.Scroll += (_, _) =>
        {
            progressBar.Value = trkSlider.Value;
            _baseView.LogEvent("EnterpriseTrackBar.Value", trkSlider.Value.ToString());
        };
        collapsiblePanel.ContentPanel.Controls.AddRange(new Control[] { chkTriState, rbOpt1, rbOpt2, trkSlider });
        collapsiblePanel.ExpandedChanged += exp => _baseView.LogEvent("EnterpriseCollapsiblePanel.IsExpanded", exp.ToString());

        btnPanel.Controls.AddRange(new Control[]
        {
            lblBtnHeader, btnPrimary, btnSuccess, btnDanger, btnOutline,
            lblSplitHeader, splitBtn, dropDownBtn, repeatBtn, progressBar,
            collapsiblePanel
        });
        tabButtonsSuite.Controls.Add(btnPanel);

        // ====================================================================
        // Tab 3: Specialized Editors (Masked, AutoComplete, CheckedDropDown, Color, RangeSlider, Calculator)
        // ====================================================================
        var tabSpecialized = new TabPage("🎛️ Masked, AutoComplete, Color, Range & Calculator");
        var specPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16), AutoScroll = true };

        int sy = 12;
        specPanel.Controls.Add(new Label { Text = "EnterpriseMaskedEditBox (Phone):", Location = new Point(16, sy), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
        var txtRawPhone = new System.Windows.Forms.TextBox { Text = "4158902345", Location = new Point(210, sy - 2), Width = 140 };
        var lblMaskedOut = new Label { Text = "Formatted: (415) 890-2345", Location = new Point(365, sy), AutoSize = true, ForeColor = Color.FromArgb(0, 102, 180), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        txtRawPhone.TextChanged += (_, _) =>
        {
            var m = EnterpriseUI.Editors.EnterpriseMaskedTextBox.ApplyMask(txtRawPhone.Text, "(000) 000-0000", '_');
            lblMaskedOut.Text = $"Formatted: {m}";
            _baseView.LogEvent("EnterpriseMaskedTextBox.MaskedText", m);
        };
        specPanel.Controls.AddRange(new Control[] { txtRawPhone, lblMaskedOut });
        sy += 36;

        var autoComplete = new EnterpriseAutoCompleteBox();
        autoComplete.SuggestionsSource.AddRange(CachedValues2);
        autoComplete.AddToken("WinForms");
        autoComplete.AddToken("DataGrid");

        specPanel.Controls.Add(new Label { Text = "EnterpriseAutoCompleteBox Tokens:", Location = new Point(16, sy), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
        var txtTokenInput = new System.Windows.Forms.TextBox { Text = "EnterpriseGanttView", Location = new Point(210, sy - 2), Width = 140 };
        var btnAddToken = new Button { Text = "+ Add Token", Location = new Point(360, sy - 3), Size = new Size(95, 26), FlatStyle = FlatStyle.Flat };
        var lblTokens = new Label { Text = $"Tokens: [{string.Join("] [", autoComplete.SelectedTokens)}]", Location = new Point(465, sy), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        btnAddToken.Click += (_, _) =>
        {
            autoComplete.AddToken(txtTokenInput.Text);
            lblTokens.Text = $"Tokens: [{string.Join("] [", autoComplete.SelectedTokens)}]";
            _baseView.LogEvent("EnterpriseAutoCompleteBox.AddToken", txtTokenInput.Text);
        };
        specPanel.Controls.AddRange(new Control[] { txtTokenInput, btnAddToken, lblTokens });
        sy += 36;

        var checkedDrop = new EnterpriseCheckedDropDownList<string>();
        checkedDrop.Items.AddRange(CachedValues3);
        checkedDrop.CheckAll();

        specPanel.Controls.Add(new Label { Text = "EnterpriseCheckedDropDownList:", Location = new Point(16, sy), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
        var btnCheckAll = new Button { Text = "Check All (4)", Location = new Point(210, sy - 3), Size = new Size(105, 26), FlatStyle = FlatStyle.Flat };
        var btnUncheckAll = new Button { Text = "Uncheck All", Location = new Point(322, sy - 3), Size = new Size(105, 26), FlatStyle = FlatStyle.Flat };
        var lblCheckedState = new Label { Text = "Checked: North America, EMEA, APAC, LATAM", Location = new Point(440, sy), AutoSize = true };
        btnCheckAll.Click += (_, _) =>
        {
            checkedDrop.CheckAll();
            lblCheckedState.Text = $"Checked ({checkedDrop.CheckedItems.Count}): {string.Join(", ", checkedDrop.CheckedItems)}";
            _baseView.LogEvent("EnterpriseCheckedDropDownList.CheckAll", lblCheckedState.Text);
        };
        btnUncheckAll.Click += (_, _) =>
        {
            checkedDrop.UncheckAll();
            lblCheckedState.Text = "Checked (0): None";
            _baseView.LogEvent("EnterpriseCheckedDropDownList.UncheckAll", "0 items");
        };
        specPanel.Controls.AddRange(new Control[] { btnCheckAll, btnUncheckAll, lblCheckedState });
        sy += 36;

        var colorPickerModel = new ColorPicker();
        specPanel.Controls.Add(new Label { Text = "EnterpriseColorBox / ColorPicker:", Location = new Point(16, sy), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
        var pnlColorSwatch = new Panel { Location = new Point(210, sy - 2), Size = new Size(36, 24), BackColor = Color.FromArgb(0, 120, 212), BorderStyle = BorderStyle.FixedSingle };
        var btnPickColor = new Button { Text = "Choose Color...", Location = new Point(254, sy - 3), Size = new Size(120, 26), FlatStyle = FlatStyle.Flat };
        var lblColorHex = new Label { Text = colorPickerModel.SelectedColorHex, Location = new Point(384, sy), AutoSize = true, Font = new Font("Consolas", 9.5f, FontStyle.Bold) };
        btnPickColor.Click += (_, _) =>
        {
            using var cd = new ColorDialog { Color = pnlColorSwatch.BackColor, FullOpen = true };
            if (cd.ShowDialog() == DialogResult.OK)
            {
                pnlColorSwatch.BackColor = cd.Color;
                colorPickerModel.SelectedColorHex = $"#{cd.Color.R:X2}{cd.Color.G:X2}{cd.Color.B:X2}";
                lblColorHex.Text = colorPickerModel.SelectedColorHex;
                _baseView.LogEvent("ColorPicker.SelectedColorHex", colorPickerModel.SelectedColorHex);
            }
        };
        specPanel.Controls.AddRange(new Control[] { pnlColorSwatch, btnPickColor, lblColorHex });
        sy += 36;

        var rangeModel = new RangeSlider { Minimum = 0, Maximum = 100, SelectionStart = 25, SelectionEnd = 85 };
        var timeSpanModel = new EnterpriseTimeSpanPicker { Value = TimeSpan.FromMinutes(90) };
        specPanel.Controls.Add(new Label { Text = "RangeSlider & EnterpriseTimeSpanPicker:", Location = new Point(16, sy), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
        var btnStepRange = new Button { Text = "Expand Range (+5)", Location = new Point(210, sy - 3), Size = new Size(135, 26), FlatStyle = FlatStyle.Flat };
        var btnAddDur = new Button { Text = "+15m Duration", Location = new Point(352, sy - 3), Size = new Size(120, 26), FlatStyle = FlatStyle.Flat };
        var lblRangeDur = new Label { Text = $"Range: [{rangeModel.SelectionStart}..{rangeModel.SelectionEnd}] | Duration: {timeSpanModel.Value}", Location = new Point(482, sy), AutoSize = true };
        btnStepRange.Click += (_, _) =>
        {
            rangeModel.SelectionEnd = Math.Min(100, rangeModel.SelectionEnd + 5);
            lblRangeDur.Text = $"Range: [{rangeModel.SelectionStart}..{rangeModel.SelectionEnd}] | Duration: {timeSpanModel.Value}";
            _baseView.LogEvent("RangeSlider.SelectionEnd", rangeModel.SelectionEnd.ToString());
        };
        btnAddDur.Click += (_, _) =>
        {
            timeSpanModel.AddMinutes(15);
            lblRangeDur.Text = $"Range: [{rangeModel.SelectionStart}..{rangeModel.SelectionEnd}] | Duration: {timeSpanModel.Value}";
            _baseView.LogEvent("EnterpriseTimeSpanPicker.Value", timeSpanModel.Value.ToString());
        };
        specPanel.Controls.AddRange(new Control[] { btnStepRange, btnAddDur, lblRangeDur });
        sy += 36;

        var calc = new EnterpriseCalculatorDropDown();
        specPanel.Controls.Add(new Label { Text = "EnterpriseCalculatorDropDown:", Location = new Point(16, sy), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
        var txtCalcExpr = new System.Windows.Forms.TextBox { Text = "(450 * 12) + 1850.75", Location = new Point(210, sy - 2), Width = 160 };
        var btnEvalCalc = new Button { Text = "= Compute", Location = new Point(378, sy - 3), Size = new Size(90, 26), FlatStyle = FlatStyle.Flat };
        var lblCalcOut = new Label { Text = "Result: 7,250.75", Location = new Point(478, sy), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(16, 124, 65) };
        btnEvalCalc.Click += (_, _) =>
        {
            try
            {
                var r = calc.EvaluateExpression(txtCalcExpr.Text);
                lblCalcOut.Text = $"Result: {r:N2}";
                _baseView.LogEvent("EnterpriseCalculatorDropDown.Evaluate", $"{txtCalcExpr.Text} = {r:N2}");
            }
            catch (Exception ex)
            {
                lblCalcOut.Text = $"Error: {ex.Message}";
            }
        };
        specPanel.Controls.AddRange(new Control[] { txtCalcExpr, btnEvalCalc, lblCalcOut });
        tabSpecialized.Controls.Add(specPanel);

        // ====================================================================
        // Tab 4: EnterpriseSyntaxEditor & SpellChecker
        // ====================================================================
        var tabSyntax = new TabPage("💻 EnterpriseSyntaxEditor & SpellChecker");
        var syntaxTop = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(6), BackColor = Color.FromArgb(245, 247, 250) };
        var syntaxCtrl = new EnterpriseSyntaxEditorControl { Dock = DockStyle.Fill };
        var btnTok = new Button { Text = "Tokenize C# Document", AutoSize = true, FlatStyle = FlatStyle.Flat };
        var btnSpell = new Button { Text = "SpellCheck ' virtualization / enterprize '", AutoSize = true, FlatStyle = FlatStyle.Flat };
        var lblSynRes = new Label { Text = "Lexical & SpellEngine Ready", AutoSize = true, Padding = new Padding(8, 5, 0, 0), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };

        btnTok.Click += (_, _) =>
        {
            var tokens = syntaxCtrl.Tokenize();
            lblSynRes.Text = $"Classified {tokens.Count} C# tokens";
            _baseView.LogEvent("EnterpriseSyntaxEditor.Tokenize", $"{tokens.Count} tokens");
        };
        btnSpell.Click += (_, _) =>
        {
            var sc = new SpellChecker();
            var sug = sc.GetSuggestions("enterprize");
            lblSynRes.Text = $"Suggestions for 'enterprize': {string.Join(", ", sug)}";
            _baseView.LogEvent("SpellChecker.GetSuggestions", string.Join(", ", sug));
        };
        syntaxTop.Controls.AddRange(new Control[] { btnTok, btnSpell, lblSynRes });
        tabSyntax.Controls.Add(syntaxCtrl);
        tabSyntax.Controls.Add(syntaxTop);

        // ====================================================================
        // Tab 5: SignaturePad & VirtualKeyboard
        // ====================================================================
        var tabTouchAndButtons = new TabPage("✍️ SignaturePad & VirtualKeyboard");
        var tbSplit = new System.Windows.Forms.SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 150 };

        var sigPanel = new Panel { Dock = DockStyle.Fill };
        var btnBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(6), BackColor = Color.FromArgb(245, 247, 250) };
        var actionModel = new EnterpriseActionButton { Text = "Toggle Split Action", IsToggleMode = true };
        var btnToggleDemo = new Button { Text = "⚡ ToggleButton (OFF)", AutoSize = true, FlatStyle = FlatStyle.Flat };
        var btnClearInk = new Button { Text = "Clear Signature Pad", AutoSize = true, FlatStyle = FlatStyle.Flat };
        var sigCtrl = new EnterpriseSignaturePadControl { Dock = DockStyle.Fill };

        btnToggleDemo.Click += (_, _) =>
        {
            actionModel.PerformClick();
            btnToggleDemo.Text = $"⚡ ToggleButton ({(actionModel.IsChecked ? "ON" : "OFF")})";
            _baseView.LogEvent("EnterpriseActionButton.IsChecked", actionModel.IsChecked.ToString());
        };
        btnClearInk.Click += (_, _) => sigCtrl.ClearSignature();
        sigCtrl.SignatureChanged += strokes => _baseView.LogEvent("SignaturePad.Strokes", strokes.ToString());

        btnBar.Controls.AddRange(new Control[] { btnToggleDemo, btnClearInk });
        sigPanel.Controls.Add(sigCtrl);
        sigPanel.Controls.Add(btnBar);

        var vkCtrl = new EnterpriseVirtualKeyboardControl { Dock = DockStyle.Fill };
        vkCtrl.KeyPressed += k => _baseView.LogEvent("VirtualKeyboard.KeyPressed", $"{k} -> '{vkCtrl.TypedBuffer}'");

        tbSplit.Panel1.Controls.Add(sigPanel);
        tbSplit.Panel2.Controls.Add(vkCtrl);
        tabTouchAndButtons.Controls.Add(tbSplit);

        tabs.TabPages.AddRange(new[] { tabCoreForm, tabButtonsSuite, tabSpecialized, tabSyntax, tabTouchAndButtons });
        _baseView.DemoContainer.Controls.Add(tabs);
    }

    private async void RunValidation()
    {
        var model = new EditorsDemoModel
        {
            FullName = _txtFullName.Text,
            Email = _txtEmail.Text,
            AnnualBudget = _numBudget.Value,
            EffectiveDate = _dtpEffectiveDate.Value,
            Department = _cmbDept.SelectedItem?.ToString() ?? string.Empty,
            EnableNotifications = _tglNotifications.IsOn
        };

        var validator = new Validator<EditorsDemoModel>()
            .RuleFor(m => m.FullName, s => !string.IsNullOrWhiteSpace(s) && s.Length >= 3, "Name must be at least 3 characters.")
            .RuleFor(m => m.Email, s => s.Contains('@') && s.Contains('.'), "A valid email format is required.")
            .RuleFor(m => m.AnnualBudget, b => b >= 1000, "Annual budget must be at least $1,000.");

        var res = await validator.ValidateAsync(model);
        _txtFullName.HasValidationError = string.IsNullOrWhiteSpace(_txtFullName.Text) || _txtFullName.Text.Length < 3;
        _txtEmail.HasValidationError = !_txtEmail.Text.Contains('@') || !_txtEmail.Text.Contains('.');
        _txtFullName.Invalidate();
        _txtEmail.Invalidate();

        if (res.IsValid)
        {
            _lblValidationSummary.Text = "Status: PASS - All themed form controls validated!";
            _lblValidationSummary.ForeColor = Color.FromArgb(16, 124, 65);
            _baseView.LogEvent("Validator.ValidateAsync", "PASS");
        }
        else
        {
            _lblValidationSummary.Text = $"Status: FAIL - {res.Errors[0].ErrorMessage}";
            _lblValidationSummary.ForeColor = Color.FromArgb(232, 17, 35);
            _baseView.LogEvent("Validator.ValidateAsync", $"FAIL: {res.Errors[0].ErrorMessage}");
        }
    }
}
