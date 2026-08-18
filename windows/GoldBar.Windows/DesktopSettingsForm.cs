using System.IO.Ports;

namespace GoldBar.Windows;

public sealed class DesktopSettingsForm : Form
{
    private static readonly Color Bg = Color.FromArgb(7, 9, 12);
    private static readonly Color Panel = Color.FromArgb(13, 16, 22);
    private static readonly Color Card = Color.FromArgb(18, 22, 29);
    private static readonly Color Card2 = Color.FromArgb(24, 29, 38);
    private static readonly Color Border = Color.FromArgb(48, 55, 68);
    private static readonly Color Gold = Color.FromArgb(247, 211, 112);
    private static readonly Color TextMain = Color.FromArgb(246, 244, 237);
    private static readonly Color Muted = Color.FromArgb(151, 160, 176);
    private static readonly Color Danger = Color.FromArgb(255, 105, 105);
    private static readonly Color Success = Color.FromArgb(102, 220, 150);

    private readonly AppSettings _source;
    private readonly Panel _page = new() { Dock = DockStyle.Fill, BackColor = Bg };
    private readonly Button _navReport = Nav("گزارش");
    private readonly Button _navScale = Nav("ترازو / RS-232");

    private readonly TextBox _reportFolder = Input();
    private readonly ComboBox _model = Combo();
    private readonly ComboBox _port = Combo();
    private readonly ComboBox _baud = Combo();
    private readonly ComboBox _dataBits = Combo();
    private readonly ComboBox _parity = Combo();
    private readonly ComboBox _stopBits = Combo();
    private readonly ComboBox _flow = Combo();
    private readonly TextBox _decimal = Input();
    private readonly NumericUpDown _before = Number(0, 20);
    private readonly NumericUpDown _after = Number(0, 20);
    private readonly NumericUpDown _minAfter = Number(0, 20);
    private readonly CheckBox _receivePrint = Check("دریافت وزن با کلید PRINT روی ترازو");
    private readonly CheckBox _autoRead = Check("خواندن خودکار وزن");
    private readonly CheckBox _up = Check("دریافت وزن با کلید ↑ در فیلد وزن");
    private readonly CheckBox _raw = Check("نمایش متن خام دریافتی");
    private readonly CheckBox _sendQuery = Check("هنگام ↑ فرمان درخواست وزن ارسال شود");
    private readonly TextBox _query = Input();
    private readonly ComboBox _ending = Combo();
    private readonly NumericUpDown _timeout = Number(500, 10000);
    private readonly Label _testStatus = LabelX("آماده تست", 9.5f, Muted, true);

    public AppSettings ResultSettings { get; private set; }

    public DesktopSettingsForm(AppSettings settings)
    {
        _source = settings;
        ResultSettings = settings;
        Text = "Gold Bar — Settings";
        Size = new Size(1080, 760);
        MinimumSize = new Size(920, 650);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Bg;
        ForeColor = TextMain;
        Font = new Font("Segoe UI", 10f);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildShell();
        PopulateOptions();
        LoadValues();
        ShowReport();
    }

    private void BuildShell()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, ColumnCount = 2, RowCount = 2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        root.Controls.Add(_page, 0, 0);
        root.Controls.Add(BuildNav(), 1, 0);
        root.SetColumnSpan(BuildBottomPlaceholder(), 2);

        var bottom = BuildBottom();
        root.Controls.Add(bottom, 0, 1);
        root.SetColumnSpan(bottom, 2);
        Controls.Add(root);
    }

    private Control BuildBottomPlaceholder() => new Panel { Height = 1 };

    private Control BuildNav()
    {
        var nav = new Panel { Dock = DockStyle.Fill, BackColor = Panel, Padding = new Padding(14, 20, 14, 20) };
        var title = LabelX("تنظیمات", 18, TextMain, true);
        title.Dock = DockStyle.Top;
        title.Height = 54;
        title.TextAlign = ContentAlignment.MiddleRight;
        nav.Controls.Add(title);

        var host = new FlowLayoutPanel { Dock = DockStyle.Top, Top = 70, Height = 150, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Panel };
        _navReport.Width = 190; _navScale.Width = 190;
        _navReport.Click += (_, _) => ShowReport();
        _navScale.Click += (_, _) => ShowScale();
        host.Controls.Add(_navReport);
        host.Controls.Add(_navScale);
        nav.Controls.Add(host);
        return nav;
    }

    private Control BuildBottom()
    {
        var p = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Panel, ColumnCount = 3, Padding = new Padding(18, 12, 18, 12) };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        var hint = LabelX("تنظیمات روی همین ویندوز ذخیره می‌شوند.", 9, Muted, false);
        hint.Dock = DockStyle.Fill;
        hint.TextAlign = ContentAlignment.MiddleRight;
        var cancel = Secondary("انصراف"); cancel.DialogResult = DialogResult.Cancel; cancel.Dock = DockStyle.Fill;
        var save = Primary("ذخیره تنظیمات"); save.Dock = DockStyle.Fill; save.Click += (_, _) => SaveAndClose();
        p.Controls.Add(hint, 0, 0); p.Controls.Add(cancel, 1, 0); p.Controls.Add(save, 2, 0);
        CancelButton = cancel;
        return p;
    }

    private void ShowReport()
    {
        SetNav(_navReport);
        _page.Controls.Clear();
        var host = ContentHost("گزارش", "مسیر ذخیره گزارش را یک‌بار انتخاب کن. ذخیره تنظیمات فقط مسیر را ثبت می‌کند و دیگر پوشه را اجباری ایجاد نمی‌کند.");
        var card = Section("محل ذخیره گزارش");
        var row = new TableLayoutPanel { Dock = DockStyle.Top, Height = 54, ColumnCount = 2, BackColor = Card };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 76)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        _reportFolder.Dock = DockStyle.Fill;
        var browse = Secondary("انتخاب پوشه…"); browse.Dock = DockStyle.Fill;
        browse.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { Description = "پوشه ذخیره گزارش‌های Gold Bar را انتخاب کن", UseDescriptionForTitle = true };
            if (Directory.Exists(_reportFolder.Text)) dlg.SelectedPath = _reportFolder.Text;
            if (dlg.ShowDialog(this) == DialogResult.OK) _reportFolder.Text = dlg.SelectedPath;
        };
        row.Controls.Add(_reportFolder, 0, 0); row.Controls.Add(browse, 1, 0);
        card.Controls.Add(row);
        var note = LabelX("اگر مسیر بعداً حذف یا جابه‌جا شود، هنگام ذخیره گزارش پیام خطای واضح نمایش داده می‌شود و تنظیمات برنامه از بین نمی‌رود.", 9, Muted, false);
        note.Dock = DockStyle.Top; note.Height = 54; card.Controls.Add(note);
        host.Controls.Add(card);
        _page.Controls.Add(host);
    }

    private void ShowScale()
    {
        SetNav(_navScale);
        _page.Controls.Clear();
        var host = ContentHost("ترازو / RS-232", "پارامترهای ارتباط سریال ترازو را تنظیم کن. پیش‌فرض A&D مطابق عکس مرجع است.");

        var connection = Section("ارتباط سریال");
        connection.Controls.Add(FieldGrid(
            ("مدل ترازو", _model), ("Port", _port), ("Baud Rate", _baud), ("Data Bits", _dataBits),
            ("Parity", _parity), ("Stop Bits", _stopBits), ("Flow Control", _flow)));
        host.Controls.Add(connection);

        var format = Section("قالب وزن دریافتی");
        format.Controls.Add(FieldGrid(("ممیز", _decimal), ("قبل ممیز", _before), ("بعد ممیز", _after), ("حداقل بعد ممیز", _minAfter)));
        host.Controls.Add(format);

        var behavior = Section("رفتار دریافت");
        behavior.Controls.Add(_receivePrint); behavior.Controls.Add(_autoRead); behavior.Controls.Add(_up); behavior.Controls.Add(_raw); behavior.Controls.Add(_sendQuery);
        behavior.Controls.Add(FieldGrid(("فرمان درخواست وزن", _query), ("پایان فرمان", _ending), ("مهلت دریافت (ms)", _timeout)));
        _testStatus.Dock = DockStyle.Top; _testStatus.Height = 36; behavior.Controls.Add(_testStatus);
        var test = Secondary("تست اتصال و دریافت وزن"); test.Dock = DockStyle.Top; test.Height = 46; test.Click += async (_, _) => await TestScale(test);
        behavior.Controls.Add(test);
        host.Controls.Add(behavior);
        _page.Controls.Add(host);
    }

    private async Task TestScale(Button button)
    {
        button.Enabled = false;
        _testStatus.Text = "در حال اتصال…"; _testStatus.ForeColor = Gold;
        using var reader = new ScaleReader();
        try
        {
            var cfg = BuildSettings();
            reader.ApplySettings(cfg, false);
            reader.Start();
            _testStatus.Text = $"پورت {cfg.PortName} باز شد؛ منتظر وزن…";
            try
            {
                var w = await reader.ReadNowAsync();
                _testStatus.Text = "وزن دریافتی: " + w.ToString("0.###") + " g";
                _testStatus.ForeColor = Success;
            }
            catch (TimeoutException)
            {
                _testStatus.Text = "پورت باز شد ولی وزن در مهلت تعیین‌شده دریافت نشد.";
                _testStatus.ForeColor = Gold;
            }
        }
        catch (Exception ex)
        {
            _testStatus.Text = "خطا: " + ex.Message;
            _testStatus.ForeColor = Danger;
        }
        finally { button.Enabled = true; }
    }

    private void SaveAndClose()
    {
        var next = BuildSettings();
        if (string.IsNullOrWhiteSpace(next.ReportFolder))
        {
            MessageBox.Show(this, "یک مسیر برای گزارش انتخاب کن.", "مسیر گزارش", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ShowReport();
            return;
        }
        try
        {
            // Important: do not create/validate the report directory while saving settings.
            // The selected path is persisted as-is. Directory access is checked only when a report is written.
            next.Save();
            ResultSettings = next;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "ذخیره فایل تنظیمات انجام نشد:\n" + ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private AppSettings BuildSettings() => new()
    {
        ReportFolder = _reportFolder.Text.Trim(),
        ScaleModel = string.IsNullOrWhiteSpace(_model.Text) ? "A&D" : _model.Text.Trim(),
        PortName = string.IsNullOrWhiteSpace(_port.Text) ? "COM1" : _port.Text.Trim(),
        BaudRate = int.TryParse(_baud.Text, out var baud) ? baud : 2400,
        DataBits = int.TryParse(_dataBits.Text, out var bits) ? bits : 7,
        Parity = string.IsNullOrWhiteSpace(_parity.Text) ? nameof(Parity.Even) : _parity.Text,
        StopBits = string.IsNullOrWhiteSpace(_stopBits.Text) ? nameof(StopBits.Two) : _stopBits.Text,
        Handshake = string.IsNullOrWhiteSpace(_flow.Text) ? nameof(Handshake.None) : _flow.Text,
        DecimalSeparator = string.IsNullOrEmpty(_decimal.Text) ? "." : _decimal.Text,
        CharactersBeforeDecimal = (int)_before.Value,
        CharactersAfterDecimal = (int)_after.Value,
        MinimumAfterDecimal = (int)_minAfter.Value,
        ReceivePrintKey = _receivePrint.Checked,
        AutoRead = _autoRead.Checked,
        ReadOnUpArrow = _up.Checked,
        ShowRawText = _raw.Checked,
        SendQueryOnUpArrow = _sendQuery.Checked,
        QueryCommand = _query.Text,
        QueryLineEnding = string.IsNullOrWhiteSpace(_ending.Text) ? "CRLF" : _ending.Text,
        ReadTimeoutMs = (int)_timeout.Value
    };

    private void PopulateOptions()
    {
        _model.Items.AddRange(new object[] { "A&D", "Custom / Generic" });
        var ports = SerialPort.GetPortNames().OrderBy(x => x).ToArray();
        _port.Items.AddRange(ports); if (!_port.Items.Contains("COM1")) _port.Items.Add("COM1");
        _baud.Items.AddRange(new object[] { "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200" });
        _dataBits.Items.AddRange(new object[] { "7", "8" });
        _parity.Items.AddRange(Enum.GetNames<Parity>());
        _stopBits.Items.AddRange(new object[] { nameof(StopBits.One), nameof(StopBits.OnePointFive), nameof(StopBits.Two) });
        _flow.Items.AddRange(Enum.GetNames<Handshake>());
        _ending.Items.AddRange(new object[] { "CRLF", "CR", "LF", "None" });
    }

    private void LoadValues()
    {
        _reportFolder.Text = _source.ReportFolder;
        Select(_model, _source.ScaleModel); Select(_port, _source.PortName); Select(_baud, _source.BaudRate.ToString()); Select(_dataBits, _source.DataBits.ToString());
        Select(_parity, _source.Parity); Select(_stopBits, _source.StopBits); Select(_flow, _source.Handshake);
        _decimal.Text = _source.DecimalSeparator;
        _before.Value = Math.Clamp(_source.CharactersBeforeDecimal, (int)_before.Minimum, (int)_before.Maximum);
        _after.Value = Math.Clamp(_source.CharactersAfterDecimal, (int)_after.Minimum, (int)_after.Maximum);
        _minAfter.Value = Math.Clamp(_source.MinimumAfterDecimal, (int)_minAfter.Minimum, (int)_minAfter.Maximum);
        _receivePrint.Checked = _source.ReceivePrintKey; _autoRead.Checked = _source.AutoRead; _up.Checked = _source.ReadOnUpArrow; _raw.Checked = _source.ShowRawText; _sendQuery.Checked = _source.SendQueryOnUpArrow;
        _query.Text = _source.QueryCommand; Select(_ending, _source.QueryLineEnding); _timeout.Value = Math.Clamp(_source.ReadTimeoutMs, (int)_timeout.Minimum, (int)_timeout.Maximum);
    }

    private void SetNav(Button active)
    {
        foreach (var b in new[] { _navReport, _navScale }) { b.BackColor = b == active ? Card2 : Panel; b.ForeColor = b == active ? Gold : Muted; }
    }

    private static FlowLayoutPanel ContentHost(string title, string subtitle)
    {
        var h = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Bg, Padding = new Padding(28, 22, 28, 24), RightToLeft = RightToLeft.Yes };
        var t = LabelX(title, 21, TextMain, true); t.Margin = new Padding(0, 0, 0, 3); h.Controls.Add(t);
        var s = LabelX(subtitle, 9.5f, Muted, false); s.Margin = new Padding(0, 0, 0, 16); h.Controls.Add(s);
        h.SizeChanged += (_, _) => { foreach (Control c in h.Controls) if (c is RoundedPanel) c.Width = Math.Max(600, h.ClientSize.Width - 64); };
        return h;
    }

    private static RoundedPanel Section(string title)
    {
        var p = new RoundedPanel { Width = 760, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Card, BorderColor = Border, Radius = 17, Padding = new Padding(16), Margin = new Padding(0, 0, 0, 14) };
        var t = LabelX(title, 13, TextMain, true); t.Dock = DockStyle.Top; t.Height = 34; p.Controls.Add(t); return p;
    }

    private static TableLayoutPanel FieldGrid(params (string Label, Control C)[] fields)
    {
        var cols = Math.Min(4, Math.Max(1, fields.Length));
        var g = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = cols, BackColor = Card, RightToLeft = RightToLeft.Yes, Margin = new Padding(0, 4, 0, 10) };
        for (int i = 0; i < cols; i++) g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cols));
        foreach (var f in fields)
        {
            var host = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, BackColor = Card, Margin = new Padding(5) };
            var l = LabelX(f.Label, 8.7f, Muted, false); l.Dock = DockStyle.Top; l.Height = 24; host.Controls.Add(l); f.C.Dock = DockStyle.Top; host.Controls.Add(f.C); g.Controls.Add(host);
        }
        return g;
    }

    private static TextBox Input() => new() { BackColor = Card2, ForeColor = TextMain, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10.5f), RightToLeft = RightToLeft.No, Height = 36 };
    private static ComboBox Combo() => new() { BackColor = Card2, ForeColor = TextMain, FlatStyle = FlatStyle.Flat, DropDownStyle = ComboBoxStyle.DropDown, Font = new Font("Segoe UI", 10f), RightToLeft = RightToLeft.No, Height = 36 };
    private static NumericUpDown Number(int min, int max) => new() { Minimum = min, Maximum = max, BackColor = Card2, ForeColor = TextMain, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10f), TextAlign = HorizontalAlignment.Center, Height = 36 };
    private static CheckBox Check(string text) => new() { Text = text, ForeColor = TextMain, AutoSize = true, Padding = new Padding(4, 7, 4, 7), Font = new Font("Segoe UI", 9.5f) };
    private static Button Nav(string text) { var b = Secondary(text); b.Height = 48; b.TextAlign = ContentAlignment.MiddleRight; return b; }
    private static Button Primary(string text) => ButtonX(text, Gold, Color.FromArgb(22, 16, 3));
    private static Button Secondary(string text) => ButtonX(text, Card2, Gold);
    private static Button ButtonX(string text, Color bg, Color fg) { var b = new Button { Text = text, FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = fg, Font = new Font("Segoe UI", 10f, FontStyle.Bold), Cursor = Cursors.Hand, Margin = new Padding(4) }; b.FlatAppearance.BorderColor = bg == Gold ? Gold : Border; return b; }
    private static Label LabelX(string text, float size, Color color, bool bold) => new() { Text = text, ForeColor = color, AutoSize = true, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), RightToLeft = RightToLeft.Yes };
    private static void Select(ComboBox c, string value) { var i = c.FindStringExact(value); if (i >= 0) c.SelectedIndex = i; else c.Text = value; }
}
