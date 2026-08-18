using System.Globalization;
using System.Text.Json;
using GoldBar.Core;

namespace GoldBar.Windows;

public sealed class MainForm : Form
{
    private static readonly Color Bg = Color.FromArgb(8, 9, 11);
    private static readonly Color Card = Color.FromArgb(18, 20, 25);
    private static readonly Color Card2 = Color.FromArgb(27, 30, 36);
    private static readonly Color Gold = Color.FromArgb(247, 211, 112);
    private static readonly Color TextMain = Color.FromArgb(246, 244, 237);
    private static readonly Color Muted = Color.FromArgb(155, 161, 173);
    private static readonly Color Danger = Color.FromArgb(255, 105, 105);

    private readonly List<GoldEntry> _entries = new();
    private readonly FlowLayoutPanel _content = new();
    private readonly DataGridView _grid = new();
    private int _editingIndex = -1;

    private readonly TextBox _weightInput = NewInput();
    private readonly TextBox _assayInput = NewInput();
    private readonly Button _saveEntryButton = NewButton("ثبت آبشده + بعدی", true);

    private readonly Label _totalWeight = NewValueLabel();
    private readonly Label _averageAssay = NewValueLabel();
    private readonly Label _entryCount = NewValueLabel();

    private readonly TextBox _raiseTarget = NewInput("747");
    private readonly TextBox _highBarAssay = NewInput("995");
    private readonly Label _raiseDifference = NewValueLabel();
    private readonly Label _requiredHighBar = NewValueLabel();
    private readonly Label _raiseState = NewStatusLabel();

    private readonly TextBox _lowerTarget = NewInput("746");
    private readonly TextBox _silverPercent = NewInput("32");
    private readonly Label _totalAlloy = NewValueLabel();
    private readonly Label _silverNeed = NewValueLabel();
    private readonly Label _nonSilverNeed = NewValueLabel();
    private readonly Label _fourPerThousand = NewValueLabel();
    private readonly Label _finalOther = NewValueLabel();
    private readonly Label _totalAfter = NewValueLabel();
    private readonly Label _lowerState = NewStatusLabel();

    private readonly TextBox _splitBase = NewInput("800");
    private readonly Label _split3679 = NewValueLabel();
    private readonly Label _split6321 = NewValueLabel();

    private readonly TextBox _correctionWeight = NewInput("250");
    private readonly TextBox _correctionTarget = NewInput("750");
    private readonly TextBox _correctionDrop = NewInput("1");
    private readonly Label _correctionAdd = NewValueLabel();
    private readonly Label _correctionTotal = NewValueLabel();

    private static string DataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GoldBar",
        "entries.json");

    public MainForm()
    {
        Text = "Gold Bar";
        Width = 1040;
        Height = 900;
        MinimumSize = new Size(840, 650);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg;
        ForeColor = TextMain;
        Font = new Font("Segoe UI", 10f);
        RightToLeft = RightToLeft.Yes;
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildUi();
        BindEvents();
        LoadEntries();
        RefreshAll();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Bg,
            ColumnCount = 1,
            RowCount = 2,
            Padding = Padding.Empty,
            RightToLeft = RightToLeft.Yes
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(BuildHeader(), 0, 0);

        _content.Dock = DockStyle.Fill;
        _content.AutoScroll = true;
        _content.WrapContents = false;
        _content.FlowDirection = FlowDirection.TopDown;
        _content.BackColor = Bg;
        _content.Padding = new Padding(14, 0, 14, 28);
        _content.RightToLeft = RightToLeft.Yes;
        _content.SizeChanged += (_, _) => ResizeCards();

        _content.Controls.Add(BuildSummaryCard());
        _content.Controls.Add(BuildEntryCard());
        _content.Controls.Add(BuildRaiseCard());
        _content.Controls.Add(BuildLowerCard());
        _content.Controls.Add(BuildListCard());
        _content.Controls.Add(BuildToolsCard());

        root.Controls.Add(_content, 0, 1);
        Controls.Add(root);
    }

    private Control BuildHeader()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(22, 14, 22, 10) };

        var badge = new Label
        {
            Text = "Au",
            Width = 60,
            Height = 60,
            BackColor = Gold,
            ForeColor = Color.FromArgb(22, 16, 3),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 17f, FontStyle.Bold),
            Dock = DockStyle.Right,
            Margin = new Padding(0, 0, 12, 0)
        };

        var titlePanel = new Panel { Dock = DockStyle.Fill, BackColor = Bg };
        var title = new Label
        {
            Text = "GOLD BAR",
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 23f, FontStyle.Bold),
            AutoSize = true,
            RightToLeft = RightToLeft.No,
            Location = new Point(0, 4)
        };
        var subtitle = new Label
        {
            Text = "محاسبه عیار، شمش و بار ریخته‌گری — نسخه ویندوز",
            ForeColor = Muted,
            Font = new Font("Segoe UI", 10f),
            AutoSize = true,
            Location = new Point(0, 46)
        };
        titlePanel.Controls.Add(title);
        titlePanel.Controls.Add(subtitle);

        panel.Controls.Add(titlePanel);
        panel.Controls.Add(badge);
        return panel;
    }

    private Control BuildSummaryCard()
    {
        var card = NewCard("خلاصه آبشده‌ها");
        AddMetricRow(card, ("کل وزن آبشده (g)", _totalWeight), ("عیار میانگین", _averageAssay), ("تعداد آبشده", _entryCount));
        return card;
    }

    private Control BuildEntryCard()
    {
        var card = NewCard("ثبت سریع آبشده");
        AddHint(card, "وزن → Enter → عیار → Enter. بعد از ثبت، فیلد وزن برای آبشده بعدی آماده می‌شود.");
        AddFieldRow(card, ("وزن آبشده (g)", _weightInput), ("عیار آبشده", _assayInput));
        _saveEntryButton.Height = 44;
        _saveEntryButton.Dock = DockStyle.Top;
        card.Controls.Add(_saveEntryButton);
        return card;
    }

    private Control BuildRaiseCard()
    {
        var card = NewCard("بالا بردن عیار با شمش ۹۹۵");
        AddHint(card, "این بخش فقط وقتی عیار میانگین کمتر از عیار هدف باشد محاسبه می‌شود.");
        AddFieldRow(card, ("عیار هدف افزایش", _raiseTarget), ("عیار شمش", _highBarAssay));
        AddMetricRow(card, ("اختلاف تا هدف", _raiseDifference), ("شمش مورد نیاز (g)", _requiredHighBar));
        card.Controls.Add(WrapStatus(_raiseState));
        return card;
    }

    private Control BuildLowerCard()
    {
        var card = NewCard("پایین آوردن عیار با بار ریخته‌گری");
        AddHint(card, "این بخش فرمول مستقل دارد و فقط وقتی عیار میانگین بالاتر از عیار هدف کاهش باشد اجرا می‌شود.");
        AddFieldRow(card, ("عیار هدف کاهش", _lowerTarget), ("درصد نقره از بار", _silverPercent));
        AddMetricRow(card, ("کل بار مورد نیاز (g)", _totalAlloy), ("نقره مورد نیاز (g)", _silverNeed));
        AddMetricRow(card, ("بار بدون نقره (g)", _nonSilverNeed), ("۰.۴٪ کل وزن (g)", _fourPerThousand));
        AddMetricRow(card, ("بار نهایی دیگر (g)", _finalOther), ("وزن پس از بار (g)", _totalAfter));
        card.Controls.Add(WrapStatus(_lowerState));
        return card;
    }

    private Control BuildListCard()
    {
        var card = NewCard("لیست آبشده‌ها");

        var clear = NewButton("پاک‌کردن همه", false);
        clear.ForeColor = Danger;
        clear.Width = 140;
        clear.Height = 38;
        clear.Click += (_, _) => ClearAll();
        var clearHost = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Card,
            Padding = new Padding(0, 0, 0, 8)
        };
        clearHost.Controls.Add(clear);
        card.Controls.Add(clearHost);

        ConfigureGrid();
        card.Controls.Add(_grid);
        return card;
    }

    private Control BuildToolsCard()
    {
        var card = NewCard("ابزارهای سریع اکسل");
        AddFieldRow(card, ("عدد پایه تقسیم", _splitBase));
        AddMetricRow(card, ("۳۶.۷۹٪", _split3679), ("۶۳.۲۱٪", _split6321));

        var sep = new Label
        {
            Text = "اصلاح وزن برای افت عیار",
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            AutoSize = true,
            Padding = new Padding(0, 12, 0, 6)
        };
        card.Controls.Add(sep);

        AddFieldRow(card, ("وزن پایه", _correctionWeight), ("عیار هدف", _correctionTarget), ("مقدار افت عیار", _correctionDrop));
        AddMetricRow(card, ("بار افزوده (g)", _correctionAdd), ("جمع وزن (g)", _correctionTotal));
        return card;
    }

    private void BindEvents()
    {
        _saveEntryButton.Click += (_, _) => SaveEntry();

        _weightInput.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                _assayInput.Focus();
                _assayInput.SelectAll();
            }
        };
        _assayInput.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SaveEntry();
            }
        };

        foreach (var box in new[]
                 {
                     _raiseTarget, _highBarAssay, _lowerTarget, _silverPercent,
                     _splitBase, _correctionWeight, _correctionTarget, _correctionDrop
                 })
        {
            box.TextChanged += (_, _) => Recalculate();
            box.Enter += (_, _) => box.SelectAll();
        }
    }

    private void SaveEntry()
    {
        var weight = Parse(_weightInput.Text, -1);
        var assay = Parse(_assayInput.Text, -1);
        if (weight <= 0 || assay <= 0 || assay > 1000)
        {
            MessageBox.Show(this, "وزن و عیار را صحیح وارد کن. عیار باید بین ۱ تا ۱۰۰۰ باشد.", "ورودی نامعتبر",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var item = new GoldEntry(weight, assay);
        if (_editingIndex >= 0 && _editingIndex < _entries.Count)
            _entries[_editingIndex] = item;
        else
            _entries.Add(item);

        PersistEntries();
        _editingIndex = -1;
        _saveEntryButton.Text = "ثبت آبشده + بعدی";
        _weightInput.Clear();
        _assayInput.Clear();
        RefreshAll();
        _weightInput.Focus();
    }

    private void EditEntry(int index)
    {
        if (index < 0 || index >= _entries.Count) return;
        var item = _entries[index];
        _editingIndex = index;
        _weightInput.Text = Num(item.Weight);
        _assayInput.Text = Num(item.Assay);
        _saveEntryButton.Text = "ذخیره تغییرات";
        _weightInput.Focus();
        _weightInput.SelectAll();
    }

    private void DeleteEntry(int index)
    {
        if (index < 0 || index >= _entries.Count) return;
        _entries.RemoveAt(index);
        if (_editingIndex == index)
        {
            _editingIndex = -1;
            _saveEntryButton.Text = "ثبت آبشده + بعدی";
            _weightInput.Clear();
            _assayInput.Clear();
        }
        else if (_editingIndex > index)
        {
            _editingIndex--;
        }
        PersistEntries();
        RefreshAll();
    }

    private void ClearAll()
    {
        if (_entries.Count == 0) return;
        var result = MessageBox.Show(this, "همه آبشده‌های ثبت‌شده حذف شوند؟", "پاک‌کردن اطلاعات",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (result != DialogResult.Yes) return;
        _entries.Clear();
        _editingIndex = -1;
        _weightInput.Clear();
        _assayInput.Clear();
        _saveEntryButton.Text = "ثبت آبشده + بعدی";
        PersistEntries();
        RefreshAll();
    }

    private void RefreshAll()
    {
        RefreshGrid();
        Recalculate();
    }

    private void Recalculate()
    {
        var summary = GoldCalculator.Summarize(_entries);
        _totalWeight.Text = Num(summary.Weight);
        _averageAssay.Text = Num(summary.AverageAssay);
        _entryCount.Text = summary.Count.ToString(CultureInfo.InvariantCulture);

        var raiseTarget = Parse(_raiseTarget.Text, 747);
        var highBar = Parse(_highBarAssay.Text, 995);
        var raise = GoldCalculator.RequiredHighAssayBar(summary, raiseTarget, highBar);
        _raiseDifference.Text = Num(raise.DifferenceNeeded);
        _requiredHighBar.Text = Num(raise.RequiredHighBar);
        if (!double.IsFinite(raise.RequiredHighBar))
        {
            _raiseState.Text = "برای محاسبه افزایش عیار، ابتدا آبشده معتبر ثبت کن.";
            _raiseState.ForeColor = Muted;
        }
        else if (raise.RequiredHighBar > 0)
        {
            _raiseState.Text = $"برای رسیدن به عیار {Num(raiseTarget)} باید {Num(raise.RequiredHighBar)} g شمش عیار {Num(highBar)} اضافه شود.";
            _raiseState.ForeColor = Gold;
        }
        else
        {
            _raiseState.Text = "بالا بردن عیار لازم نیست؛ شمش عیار بالا = ۰ g";
            _raiseState.ForeColor = Gold;
        }

        var lowerTarget = Parse(_lowerTarget.Text, 746);
        var silver = Parse(_silverPercent.Text, 32);
        var lower = GoldCalculator.RequiredAlloy(summary, lowerTarget, silver, summary.Weight);
        _totalAlloy.Text = Num(lower.TotalAlloyRequired);
        _silverNeed.Text = Num(lower.SilverRequired);
        _nonSilverNeed.Text = Num(lower.NonSilverRequired);
        _fourPerThousand.Text = Num(lower.FourPerThousand);
        _finalOther.Text = Num(lower.FinalOtherAlloy);
        _totalAfter.Text = Num(lower.TotalAfterAlloy);
        if (!double.IsFinite(lower.TotalAlloyRequired))
        {
            _lowerState.Text = "برای محاسبه کاهش عیار، ابتدا آبشده معتبر ثبت کن.";
            _lowerState.ForeColor = Muted;
        }
        else if (lower.TotalAlloyRequired > 0)
        {
            _lowerState.Text = $"برای کاهش عیار تا {Num(lowerTarget)} باید {Num(lower.TotalAlloyRequired)} g بار ریخته‌گری اضافه شود.";
            _lowerState.ForeColor = Gold;
        }
        else
        {
            _lowerState.Text = "پایین آوردن عیار لازم نیست؛ بار ریخته‌گری = ۰ g";
            _lowerState.ForeColor = Gold;
        }

        var split = Parse(_splitBase.Text, 800);
        var part = GoldCalculator.Split3679(split);
        _split3679.Text = Num(part);
        _split6321.Text = Num(split - part);

        var cw = Parse(_correctionWeight.Text, 250);
        var ct = Parse(_correctionTarget.Text, 750);
        var cd = Parse(_correctionDrop.Text, 1);
        var add = GoldCalculator.CorrectionAddition(cw, ct, cd);
        _correctionAdd.Text = Num(add);
        _correctionTotal.Text = Num(cw + add);
    }

    private void ConfigureGrid()
    {
        _grid.Height = 250;
        _grid.Dock = DockStyle.Top;
        _grid.BackgroundColor = Card2;
        _grid.BorderStyle = BorderStyle.None;
        _grid.GridColor = Color.FromArgb(55, 58, 66);
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(35, 38, 45);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = TextMain;
        _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.DefaultCellStyle.BackColor = Card2;
        _grid.DefaultCellStyle.ForeColor = TextMain;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(55, 48, 30);
        _grid.DefaultCellStyle.SelectionForeColor = TextMain;
        _grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.RowHeadersVisible = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.ReadOnly = true;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RightToLeft = RightToLeft.Yes;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Weight", HeaderText = "وزن (g)", FillWeight = 32 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Assay", HeaderText = "عیار", FillWeight = 26 });
        _grid.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "Edit",
            HeaderText = "",
            Text = "ویرایش",
            UseColumnTextForButtonValue = true,
            FillWeight = 21,
            FlatStyle = FlatStyle.Flat
        });
        _grid.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "Delete",
            HeaderText = "",
            Text = "حذف",
            UseColumnTextForButtonValue = true,
            FillWeight = 21,
            FlatStyle = FlatStyle.Flat
        });

        _grid.CellContentClick += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            if (_grid.Columns[e.ColumnIndex].Name == "Edit") EditEntry(e.RowIndex);
            if (_grid.Columns[e.ColumnIndex].Name == "Delete") DeleteEntry(e.RowIndex);
        };
    }

    private void RefreshGrid()
    {
        _grid.Rows.Clear();
        foreach (var e in _entries)
            _grid.Rows.Add(Num(e.Weight), Num(e.Assay), "ویرایش", "حذف");
    }

    private void LoadEntries()
    {
        try
        {
            if (!File.Exists(DataPath)) return;
            var json = File.ReadAllText(DataPath);
            var loaded = JsonSerializer.Deserialize<List<GoldEntry>>(json);
            if (loaded is null) return;
            _entries.Clear();
            _entries.AddRange(loaded.Where(e => e.Weight > 0 && e.Assay > 0 && e.Assay <= 1000));
        }
        catch
        {
            // Keep the app usable even if a previous local file is malformed.
        }
    }

    private void PersistEntries()
    {
        try
        {
            var dir = Path.GetDirectoryName(DataPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(DataPath, JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "ذخیره اطلاعات روی ویندوز انجام نشد:\n" + ex.Message, "خطای ذخیره",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ResizeCards()
    {
        var width = Math.Max(400, _content.ClientSize.Width - 32);
        foreach (Control c in _content.Controls)
            c.Width = width;
    }

    private static TableLayoutPanel NewCard(string title)
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 1,
            BackColor = Card,
            ForeColor = TextMain,
            Padding = new Padding(16),
            Margin = new Padding(0, 10, 0, 0),
            RightToLeft = RightToLeft.Yes
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label
        {
            Text = title,
            ForeColor = TextMain,
            AutoSize = true,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 9)
        });
        return panel;
    }

    private static void AddHint(TableLayoutPanel card, string text)
    {
        card.Controls.Add(new Label
        {
            Text = text,
            ForeColor = Muted,
            AutoSize = true,
            MaximumSize = new Size(900, 0),
            Padding = new Padding(0, 0, 0, 8)
        });
    }

    private static void AddFieldRow(TableLayoutPanel card, params (string Label, TextBox Box)[] fields)
    {
        var row = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = fields.Length,
            RowCount = 1,
            BackColor = Card,
            Margin = new Padding(0, 2, 0, 8),
            RightToLeft = RightToLeft.Yes
        };
        for (var i = 0; i < fields.Length; i++) row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / fields.Length));
        foreach (var field in fields)
            row.Controls.Add(LabeledInput(field.Label, field.Box));
        card.Controls.Add(row);
    }

    private static Control LabeledInput(string label, TextBox box)
    {
        var host = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Card,
            Margin = new Padding(4)
        };
        host.Controls.Add(new Label
        {
            Text = label,
            ForeColor = Muted,
            AutoSize = true,
            Padding = new Padding(2, 0, 2, 4)
        }, 0, 0);
        box.Dock = DockStyle.Top;
        host.Controls.Add(box, 0, 1);
        return host;
    }

    private static void AddMetricRow(TableLayoutPanel card, params (string Label, Label Value)[] metrics)
    {
        var row = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = metrics.Length,
            RowCount = 1,
            BackColor = Card,
            Margin = new Padding(0, 2, 0, 8),
            RightToLeft = RightToLeft.Yes
        };
        for (var i = 0; i < metrics.Length; i++) row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / metrics.Length));
        foreach (var metric in metrics)
            row.Controls.Add(MetricBox(metric.Label, metric.Value));
        card.Controls.Add(row);
    }

    private static Control MetricBox(string label, Label value)
    {
        var p = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            BackColor = Card2,
            Padding = new Padding(10),
            Margin = new Padding(4),
            ColumnCount = 1,
            RowCount = 2
        };
        var l = new Label
        {
            Text = label,
            ForeColor = Muted,
            AutoSize = true,
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.MiddleCenter
        };
        value.Dock = DockStyle.Top;
        value.TextAlign = ContentAlignment.MiddleCenter;
        p.Controls.Add(l, 0, 0);
        p.Controls.Add(value, 0, 1);
        return p;
    }

    private static Control WrapStatus(Label label)
    {
        var host = new Panel { Height = 48, Dock = DockStyle.Top, BackColor = Card2, Padding = new Padding(10), Margin = new Padding(4, 3, 4, 3) };
        label.Dock = DockStyle.Fill;
        host.Controls.Add(label);
        return host;
    }

    private static TextBox NewInput(string? value = null)
    {
        return new TextBox
        {
            Text = value ?? string.Empty,
            BackColor = Card2,
            ForeColor = TextMain,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 11f),
            TextAlign = HorizontalAlignment.Right,
            RightToLeft = RightToLeft.No,
            Height = 34,
            Margin = new Padding(4)
        };
    }

    private static Button NewButton(string text, bool filled)
    {
        var button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = filled ? Gold : Card2,
            ForeColor = filled ? Color.FromArgb(22, 16, 3) : Gold,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4),
            Padding = new Padding(8, 3, 8, 3)
        };
        button.FlatAppearance.BorderColor = filled ? Gold : Color.FromArgb(60, 63, 71);
        return button;
    }

    private static Label NewValueLabel() => new()
    {
        Text = "—",
        ForeColor = Gold,
        AutoSize = true,
        Font = new Font("Segoe UI", 15f, FontStyle.Bold),
        RightToLeft = RightToLeft.No
    };

    private static Label NewStatusLabel() => new()
    {
        Text = "—",
        ForeColor = Muted,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
        AutoEllipsis = true
    };

    private static double Parse(string raw, double fallback)
    {
        try
        {
            var s = NormalizeDigits(raw).Trim().Replace('٫', '.').Replace(',', '.');
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static string NormalizeDigits(string raw)
    {
        const string fa = "۰۱۲۳۴۵۶۷۸۹";
        const string ar = "٠١٢٣٤٥٦٧٨٩";
        var chars = raw.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var p = fa.IndexOf(chars[i]);
            if (p < 0) p = ar.IndexOf(chars[i]);
            if (p >= 0) chars[i] = (char)('0' + p);
        }
        return new string(chars);
    }

    private static string Num(double value)
    {
        if (!double.IsFinite(value)) return "—";
        if (Math.Abs(value) < 0.0000001) value = 0;
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
