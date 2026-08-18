using System.IO.Ports;

namespace GoldBar.Windows;

public sealed class SettingsForm : Form
{
    private static readonly Color Bg = Color.FromArgb(8, 9, 11);
    private static readonly Color Card = Color.FromArgb(18, 20, 25);
    private static readonly Color Card2 = Color.FromArgb(27, 30, 36);
    private static readonly Color Gold = Color.FromArgb(247, 211, 112);
    private static readonly Color TextMain = Color.FromArgb(246, 244, 237);
    private static readonly Color Muted = Color.FromArgb(155, 161, 173);
    private static readonly Color Danger = Color.FromArgb(255, 105, 105);

    private readonly AppSettings _settings;
    private readonly TextBox _reportFolder = Input();
    private readonly ComboBox _model = Combo();
    private readonly ComboBox _port = Combo();
    private readonly ComboBox _baud = Combo();
    private readonly ComboBox _dataBits = Combo();
    private readonly ComboBox _parity = Combo();
    private readonly ComboBox _stopBits = Combo();
    private readonly ComboBox _handshake = Combo();
    private readonly TextBox _decimal = Input();
    private readonly NumericUpDown _before = Number(0, 20);
    private readonly NumericUpDown _after = Number(0, 20);
    private readonly NumericUpDown _minAfter = Number(0, 20);
    private readonly CheckBox _receivePrint = Check("دریافت وزن با فشردن کلید PRINT روی ترازو");
    private readonly CheckBox _autoRead = Check("فعال‌سازی خواندن خودکار وزن");
    private readonly CheckBox _upArrow = Check("خواندن وزن با کلید جهت بالا ↑");
    private readonly CheckBox _showRaw = Check("نمایش متن خام دریافتی از ترازو در وضعیت اتصال");
    private readonly CheckBox _sendQuery = Check("هنگام ↑ فرمان درخواست وزن برای ترازو ارسال شود");
    private readonly TextBox _query = Input();
    private readonly ComboBox _ending = Combo();
    private readonly NumericUpDown _timeout = Number(500, 10000);
    private readonly Label _status = new();

    public AppSettings ResultSettings { get; private set; }

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;
        ResultSettings = settings;
        Text = "تنظیمات Gold Bar";
        Width = 900;
        Height = 820;
        MinimumSize = new Size(760, 620);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Bg;
        ForeColor = TextMain;
        Font = new Font("Segoe UI", 10f);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildUi();
        LoadValues();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Bg,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));

        var title = new Label
        {
            Text = "تنظیمات",
            Dock = DockStyle.Fill,
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 20f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        root.Controls.Add(title, 0, 0);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Bg };
        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Bg,
            Padding = new Padding(0, 0, 8, 20),
            RightToLeft = RightToLeft.Yes
        };
        stack.SizeChanged += (_, _) =>
        {
            foreach (Control c in stack.Controls) c.Width = Math.Max(600, scroll.ClientSize.Width - 30);
        };

        stack.Controls.Add(BuildReportCard());
        stack.Controls.Add(BuildScaleCard());
        scroll.Controls.Add(stack);
        root.Controls.Add(scroll, 0, 1);

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Bg,
            Padding = new Padding(0, 8, 0, 0)
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var cancel = Button("انصراف", false);
        cancel.DialogResult = DialogResult.Cancel;
        var save = Button("ذخیره تنظیمات", true);
        save.Click += (_, _) => SaveAndClose();
        actions.Controls.Add(cancel, 0, 0);
        actions.Controls.Add(save, 1, 0);
        root.Controls.Add(actions, 0, 2);

        Controls.Add(root);
        CancelButton = cancel;
    }

    private Control BuildReportCard()
    {
        var card = CardPanel("گزارش");
        Hint(card, "مسیر را فقط یک‌بار انتخاب کن؛ از این به بعد دکمه «ذخیره گزارش» فایل را مستقیم در همین پوشه ذخیره می‌کند.");

        var row = new TableLayoutPanel { Dock = DockStyle.Top, Height = 48, ColumnCount = 2, BackColor = Card };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 78));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        _reportFolder.Dock = DockStyle.Fill;
        var browse = Button("انتخاب پوشه…", false);
        browse.Dock = DockStyle.Fill;
        browse.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "پوشه ذخیره گزارش‌های Gold Bar را انتخاب کن",
                SelectedPath = Directory.Exists(_reportFolder.Text) ? _reportFolder.Text : string.Empty,
                UseDescriptionForTitle = true
            };
            if (dlg.ShowDialog(this) == DialogResult.OK) _reportFolder.Text = dlg.SelectedPath;
        };
        row.Controls.Add(_reportFolder, 0, 0);
        row.Controls.Add(browse, 1, 0);
        card.Controls.Add(row);
        return card;
    }

    private Control BuildScaleCard()
    {
        var card = CardPanel("تنظیمات ترازو / RS-232");
        Hint(card, "پیش‌فرض A&D مطابق تصویر مرجع: COM1، 2400، 7 Data Bits، Even Parity، 2 Stop Bits و Flow Control=None.");

        _model.Items.AddRange(new object[] { "A&D", "Custom / Generic" });
        var ports = SerialPort.GetPortNames().OrderBy(x => x).Cast<object>().ToArray();
        if (ports.Length > 0) _port.Items.AddRange(ports);
        if (!_port.Items.Contains("COM1")) _port.Items.Add("COM1");
        _baud.Items.AddRange(new object[] { "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200" });
        _dataBits.Items.AddRange(new object[] { "7", "8" });
        _parity.Items.AddRange(Enum.GetNames<Parity>());
        _stopBits.Items.AddRange(new object[] { nameof(StopBits.One), nameof(StopBits.OnePointFive), nameof(StopBits.Two) });
        _handshake.Items.AddRange(Enum.GetNames<Handshake>());
        _ending.Items.AddRange(new object[] { "CRLF", "CR", "LF", "None" });

        AddFields(card,
            ("مدل ترازو", _model), ("Port", _port), ("Baud Rate", _baud),
            ("Data Bits", _dataBits), ("Parity", _parity), ("Stop Bits", _stopBits), ("Flow Control", _handshake));

        AddFields(card,
            ("ممیز", _decimal), ("قبل ممیز", _before), ("بعد ممیز", _after), ("حداقل بعد ممیز", _minAfter));

        card.Controls.Add(_receivePrint);
        card.Controls.Add(_autoRead);
        card.Controls.Add(_upArrow);
        card.Controls.Add(_showRaw);
        card.Controls.Add(_sendQuery);

        AddFields(card,
            ("فرمان درخواست وزن", _query), ("پایان فرمان", _ending), ("مهلت دریافت (ms)", _timeout));

        _status.Text = "● آماده تست";
        _status.ForeColor = Muted;
        _status.AutoSize = true;
        _status.Padding = new Padding(4, 10, 4, 10);
        card.Controls.Add(_status);

        var test = Button("تست اتصال و دریافت وزن", false);
        test.Height = 44;
        test.Dock = DockStyle.Top;
        test.Click += async (_, _) => await TestScaleAsync(test);
        card.Controls.Add(test);
        return card;
    }

    private async Task TestScaleAsync(Button button)
    {
        button.Enabled = false;
        _status.Text = "● در حال اتصال…";
        _status.ForeColor = Gold;
        using var reader = new ScaleReader();
        try
        {
            var temp = BuildFromControls();
            string raw = string.Empty;
            reader.RawReceived += s => raw += s;
            reader.ApplySettings(temp, false);
            reader.Start();
            _status.Text = $"● اتصال برقرار شد: {temp.PortName} — منتظر وزن…";
            _status.ForeColor = Gold;
            try
            {
                var weight = await reader.ReadNowAsync();
                _status.Text = "● وزن دریافتی: " + weight.ToString("0.###") + " g" +
                               (temp.ShowRawText && raw.Length > 0 ? "   |   " + raw.Trim() : string.Empty);
                _status.ForeColor = Color.FromArgb(102, 220, 150);
            }
            catch (TimeoutException)
            {
                _status.Text = "● پورت باز شد، اما در مهلت تعیین‌شده وزن دریافت نشد.";
                _status.ForeColor = Gold;
            }
        }
        catch (Exception ex)
        {
            _status.Text = "● خطا: " + ex.Message;
            _status.ForeColor = Danger;
        }
        finally
        {
            button.Enabled = true;
        }
    }

    private void LoadValues()
    {
        _reportFolder.Text = _settings.ReportFolder;
        Select(_model, _settings.ScaleModel);
        Select(_port, _settings.PortName);
        Select(_baud, _settings.BaudRate.ToString());
        Select(_dataBits, _settings.DataBits.ToString());
        Select(_parity, _settings.Parity);
        Select(_stopBits, _settings.StopBits);
        Select(_handshake, _settings.Handshake);
        _decimal.Text = _settings.DecimalSeparator;
        _before.Value = Math.Clamp(_settings.CharactersBeforeDecimal, (int)_before.Minimum, (int)_before.Maximum);
        _after.Value = Math.Clamp(_settings.CharactersAfterDecimal, (int)_after.Minimum, (int)_after.Maximum);
        _minAfter.Value = Math.Clamp(_settings.MinimumAfterDecimal, (int)_minAfter.Minimum, (int)_minAfter.Maximum);
        _receivePrint.Checked = _settings.ReceivePrintKey;
        _autoRead.Checked = _settings.AutoRead;
        _upArrow.Checked = _settings.ReadOnUpArrow;
        _showRaw.Checked = _settings.ShowRawText;
        _sendQuery.Checked = _settings.SendQueryOnUpArrow;
        _query.Text = _settings.QueryCommand;
        Select(_ending, _settings.QueryLineEnding);
        _timeout.Value = Math.Clamp(_settings.ReadTimeoutMs, (int)_timeout.Minimum, (int)_timeout.Maximum);
    }

    private AppSettings BuildFromControls() => new()
    {
        ReportFolder = _reportFolder.Text.Trim(),
        ScaleModel = _model.Text.Trim().Length == 0 ? "A&D" : _model.Text.Trim(),
        PortName = _port.Text.Trim().Length == 0 ? "COM1" : _port.Text.Trim(),
        BaudRate = int.TryParse(_baud.Text, out var baud) ? baud : 2400,
        DataBits = int.TryParse(_dataBits.Text, out var bits) ? bits : 7,
        Parity = _parity.Text.Trim().Length == 0 ? nameof(System.IO.Ports.Parity.Even) : _parity.Text,
        StopBits = _stopBits.Text.Trim().Length == 0 ? nameof(System.IO.Ports.StopBits.Two) : _stopBits.Text,
        Handshake = _handshake.Text.Trim().Length == 0 ? nameof(System.IO.Ports.Handshake.None) : _handshake.Text,
        DecimalSeparator = string.IsNullOrEmpty(_decimal.Text) ? "." : _decimal.Text,
        CharactersBeforeDecimal = (int)_before.Value,
        CharactersAfterDecimal = (int)_after.Value,
        MinimumAfterDecimal = (int)_minAfter.Value,
        ReceivePrintKey = _receivePrint.Checked,
        AutoRead = _autoRead.Checked,
        ReadOnUpArrow = _upArrow.Checked,
        ShowRawText = _showRaw.Checked,
        SendQueryOnUpArrow = _sendQuery.Checked,
        QueryCommand = _query.Text,
        QueryLineEnding = _ending.Text.Length == 0 ? "CRLF" : _ending.Text,
        ReadTimeoutMs = (int)_timeout.Value
    };

    private void SaveAndClose()
    {
        var next = BuildFromControls();
        if (string.IsNullOrWhiteSpace(next.ReportFolder))
        {
            MessageBox.Show(this, "مسیر ذخیره گزارش را انتخاب کن.", "مسیر گزارش", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            Directory.CreateDirectory(next.ReportFolder);
            next.Save();
            ResultSettings = next;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "ذخیره تنظیمات انجام نشد:\n" + ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static TableLayoutPanel CardPanel(string title)
    {
        var p = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Card,
            ForeColor = TextMain,
            Padding = new Padding(16),
            Margin = new Padding(0, 8, 0, 8)
        };
        p.Controls.Add(new Label
        {
            Text = title,
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 8)
        });
        return p;
    }

    private static void Hint(TableLayoutPanel card, string text) => card.Controls.Add(new Label
    {
        Text = text,
        ForeColor = Muted,
        AutoSize = true,
        MaximumSize = new Size(820, 0),
        Padding = new Padding(0, 0, 0, 10)
    });

    private static void AddFields(TableLayoutPanel card, params (string Label, Control Control)[] fields)
    {
        var cols = Math.Min(4, Math.Max(1, fields.Length));
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = cols,
            BackColor = Card,
            RightToLeft = RightToLeft.Yes,
            Margin = new Padding(0, 2, 0, 8)
        };
        for (var i = 0; i < cols; i++) row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cols));
        foreach (var f in fields)
        {
            var host = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, BackColor = Card, Padding = new Padding(4), ColumnCount = 1 };
            host.Controls.Add(new Label { Text = f.Label, ForeColor = Muted, AutoSize = true, Padding = new Padding(0, 0, 0, 4) });
            f.Control.Dock = DockStyle.Top;
            host.Controls.Add(f.Control);
            row.Controls.Add(host);
        }
        card.Controls.Add(row);
    }

    private static TextBox Input() => new()
    {
        BackColor = Card2,
        ForeColor = TextMain,
        BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Segoe UI", 10.5f),
        Height = 34,
        RightToLeft = RightToLeft.No
    };

    private static ComboBox Combo() => new()
    {
        BackColor = Card2,
        ForeColor = TextMain,
        FlatStyle = FlatStyle.Flat,
        DropDownStyle = ComboBoxStyle.DropDown,
        Font = new Font("Segoe UI", 10f),
        Height = 34,
        RightToLeft = RightToLeft.No
    };

    private static NumericUpDown Number(int min, int max) => new()
    {
        Minimum = min,
        Maximum = max,
        BackColor = Card2,
        ForeColor = TextMain,
        BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Segoe UI", 10f),
        Height = 34,
        TextAlign = HorizontalAlignment.Center
    };

    private static CheckBox Check(string text) => new()
    {
        Text = text,
        ForeColor = TextMain,
        AutoSize = true,
        Padding = new Padding(4, 5, 4, 5)
    };

    private static Button Button(string text, bool filled)
    {
        var b = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = filled ? Gold : Card2,
            ForeColor = filled ? Color.FromArgb(22, 16, 3) : Gold,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(5)
        };
        b.FlatAppearance.BorderColor = filled ? Gold : Color.FromArgb(60, 63, 71);
        return b;
    }

    private static void Select(ComboBox combo, string value)
    {
        var index = combo.FindStringExact(value);
        if (index >= 0) combo.SelectedIndex = index;
        else combo.Text = value;
    }
}
