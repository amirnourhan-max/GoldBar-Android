using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using GoldBar.Core;

namespace GoldBar.Windows;

public sealed class DesktopMainForm : Form
{
    private static readonly Color Bg = Color.FromArgb(7, 9, 12);
    private static readonly Color Panel = Color.FromArgb(13, 16, 22);
    private static readonly Color Card = Color.FromArgb(18, 22, 29);
    private static readonly Color Card2 = Color.FromArgb(24, 29, 38);
    private static readonly Color Border = Color.FromArgb(48, 55, 68);
    private static readonly Color Gold = Color.FromArgb(247, 211, 112);
    private static readonly Color GoldDark = Color.FromArgb(184, 130, 23);
    private static readonly Color TextMain = Color.FromArgb(246, 244, 237);
    private static readonly Color Muted = Color.FromArgb(151, 160, 176);
    private static readonly Color Danger = Color.FromArgb(255, 105, 105);
    private static readonly Color Success = Color.FromArgb(102, 220, 150);

    private readonly ScaleReader _scale = new();
    private AppSettings _settings = AppSettings.Load();
    private readonly List<GoldEntry> _entries = new();
    private int _editingIndex = -1;

    private readonly Panel _workspace = new() { Dock = DockStyle.Fill, BackColor = Bg };
    private readonly Label _pageTitle = LabelBase("داشبورد", 22, TextMain, true);
    private readonly Label _pageSubtitle = LabelBase("مرکز کنترل محاسبات Gold Bar", 10, Muted, false);
    private readonly Label _scaleHeaderStatus = LabelBase("ترازو: آماده", 9, Muted, true);
    private readonly Dictionary<string, Button> _nav = new();

    private readonly Label _totalWeight = MetricValue();
    private readonly Label _avgAssay = MetricValue();
    private readonly Label _count = MetricValue();
    private readonly Label _afterAlloy = MetricValue();

    private readonly TextBox _weight = Input();
    private readonly TextBox _assay = Input();
    private readonly Button _saveEntry = Primary("ثبت آبشده");
    private readonly Label _entryScaleHint = LabelBase("↑ دریافت از ترازو", 9, Muted, true);
    private readonly DataGridView _grid = new();

    private readonly TextBox _raiseTarget = Input("747");
    private readonly TextBox _barAssay = Input("995");
    private readonly Label _raiseDiff = MetricValue();
    private readonly Label _raiseNeed = MetricValue();
    private readonly Label _raiseState = Status();

    private readonly TextBox _lowerTarget = Input("746");
    private readonly TextBox _silver = Input("32");
    private readonly Label _alloy = MetricValue();
    private readonly Label _silverNeed = MetricValue();
    private readonly Label _otherAlloy = MetricValue();
    private readonly Label _lowerAfter = MetricValue();
    private readonly Label _lowerState = Status();

    private readonly TextBox _splitBase = Input("800");
    private readonly Label _splitA = MetricValue();
    private readonly Label _splitB = MetricValue();
    private readonly TextBox _corrWeight = Input("250");
    private readonly TextBox _corrTarget = Input("750");
    private readonly TextBox _corrDrop = Input("1");
    private readonly Label _corrAdd = MetricValue();
    private readonly Label _corrTotal = MetricValue();

    private static string DataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GoldBar", "entries.json");

    public DesktopMainForm()
    {
        Text = "Gold Bar (by:Amirnourhan)";
        MinimumSize = new Size(1180, 760);
        Size = new Size(1440, 900);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg;
        ForeColor = TextMain;
        Font = new Font("Segoe UI", 10f);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildShell();
        ConfigureGrid();
        BindEvents();
        LoadEntries();
        ApplyScaleSettings();
        ShowPage("dashboard");
        RefreshAll();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _scale.Dispose();
        base.OnFormClosed(e);
    }

    private void BuildShell()
    {
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Bg,
            ColumnCount = 2,
            RowCount = 1,
            Padding = Padding.Empty
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 248));
        shell.Controls.Add(BuildMainArea(), 0, 0);
        shell.Controls.Add(BuildSidebar(), 1, 0);
        Controls.Add(shell);
    }

    private Control BuildMainArea()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Bg,
            ColumnCount = 1,
            RowCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildTopbar(), 0, 0);
        root.Controls.Add(_workspace, 0, 1);
        return root;
    }

    private Control BuildTopbar()
    {
        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Bg,
            Padding = new Padding(26, 16, 26, 8),
            ColumnCount = 2
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));

        var titles = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Bg,
            Padding = Padding.Empty,
            RightToLeft = RightToLeft.Yes
        };
        titles.Controls.Add(_pageTitle);
        titles.Controls.Add(_pageSubtitle);
        bar.Controls.Add(titles, 0, 0);

        var status = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 4, 0, 4),
            Padding = new Padding(12),
            BackColor = Panel,
            BorderColor = Border,
            Radius = 14
        };
        _scaleHeaderStatus.Dock = DockStyle.Fill;
        _scaleHeaderStatus.TextAlign = ContentAlignment.MiddleCenter;
        status.Controls.Add(_scaleHeaderStatus);
        bar.Controls.Add(status, 1, 0);
        return bar;
    }

    private Control BuildSidebar()
    {
        var side = new Panel { Dock = DockStyle.Fill, BackColor = Panel, Padding = new Padding(16, 18, 16, 18) };

        var brand = new RoundedPanel
        {
            Dock = DockStyle.Top,
            Height = 104,
            BackColor = Card,
            BorderColor = Border,
            Radius = 18,
            Padding = new Padding(14)
        };
        var au = new Label
        {
            Text = "Au",
            Dock = DockStyle.Right,
            Width = 58,
            BackColor = Gold,
            ForeColor = Color.FromArgb(22, 16, 3),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 18, FontStyle.Bold)
        };
        var brandText = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Card,
            Padding = new Padding(8, 6, 0, 0)
        };
        brandText.Controls.Add(LabelBase("GOLD BAR", 19, TextMain, true));
        var by = new LinkLabel
        {
            Text = "by: Amirnourhan",
            AutoSize = true,
            LinkColor = Gold,
            ActiveLinkColor = Gold,
            VisitedLinkColor = Gold,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        by.LinkClicked += (_, _) => OpenInstagram();
        brandText.Controls.Add(by);
        brand.Controls.Add(brandText);
        brand.Controls.Add(au);
        side.Controls.Add(brand);

        var navHost = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 430,
            Top = 124,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Panel,
            Padding = new Padding(0, 18, 0, 0)
        };
        AddNav(navHost, "dashboard", "▦   داشبورد");
        AddNav(navHost, "entries", "◆   آبشده‌ها");
        AddNav(navHost, "calculations", "∑   محاسبات عیار");
        AddNav(navHost, "quick", "⚡   محاسبه سریع");
        AddNav(navHost, "reports", "▤   گزارش");
        AddNav(navHost, "settings", "⚙   تنظیمات");
        side.Controls.Add(navHost);

        var footer = LabelBase("Windows Desktop Edition", 8.5f, Muted, false);
        footer.Dock = DockStyle.Bottom;
        footer.Height = 28;
        footer.TextAlign = ContentAlignment.MiddleCenter;
        side.Controls.Add(footer);
        return side;
    }

    private void AddNav(Control host, string key, string text)
    {
        var b = new Button
        {
            Text = text,
            Width = 212,
            Height = 50,
            FlatStyle = FlatStyle.Flat,
            BackColor = Panel,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(10, 0, 16, 0),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 7)
        };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (_, _) => key == "settings" ? OpenSettings() : ShowPage(key);
        _nav[key] = b;
        host.Controls.Add(b);
    }

    private void ShowPage(string key)
    {
        foreach (var pair in _nav)
        {
            pair.Value.BackColor = pair.Key == key ? Card2 : Panel;
            pair.Value.ForeColor = pair.Key == key ? Gold : Muted;
        }

        _workspace.SuspendLayout();
        _workspace.Controls.Clear();
        Control page;
        switch (key)
        {
            case "entries":
                _pageTitle.Text = "آبشده‌ها";
                _pageSubtitle.Text = "ثبت دستی یا دریافت وزن از ترازو و مدیریت لیست";
                page = BuildEntriesPage();
                break;
            case "calculations":
                _pageTitle.Text = "محاسبات عیار";
                _pageSubtitle.Text = "افزایش و کاهش عیار با دو فرمول مستقل";
                page = BuildCalculationsPage();
                break;
            case "quick":
                _pageTitle.Text = "محاسبه سریع";
                _pageSubtitle.Text = "تقسیم ۳۶.۷۹٪ و اصلاح وزن برای افت عیار";
                page = BuildQuickPage();
                break;
            case "reports":
                _pageTitle.Text = "گزارش";
                _pageSubtitle.Text = "ذخیره خروجی کامل در مسیر ثابت انتخاب‌شده";
                page = BuildReportsPage();
                break;
            default:
                _pageTitle.Text = "داشبورد";
                _pageSubtitle.Text = "نمای کلی وزن، عیار و عملیات پرکاربرد";
                page = BuildDashboardPage();
                break;
        }
        page.Dock = DockStyle.Fill;
        _workspace.Controls.Add(page);
        _workspace.ResumeLayout();
        RefreshAll();
    }

    private Control BuildDashboardPage()
    {
        var page = NewScroll();
        var stack = Stack(page);
        stack.Controls.Add(BuildMetricsStrip());

        var row = TwoColumn();
        row.Controls.Add(BuildEntryCard(true), 0, 0);
        row.Controls.Add(BuildScaleCard(), 1, 0);
        stack.Controls.Add(row);

        var calcRow = TwoColumn();
        calcRow.Controls.Add(BuildRaiseCard(compact: true), 0, 0);
        calcRow.Controls.Add(BuildLowerCard(compact: true), 1, 0);
        stack.Controls.Add(calcRow);
        return page;
    }

    private Control BuildEntriesPage()
    {
        var page = NewScroll();
        var stack = Stack(page);
        stack.Controls.Add(BuildEntryCard(false));
        stack.Controls.Add(BuildGridCard());
        return page;
    }

    private Control BuildCalculationsPage()
    {
        var page = NewScroll();
        var stack = Stack(page);
        stack.Controls.Add(BuildMetricsStrip());
        var row = TwoColumn();
        row.Controls.Add(BuildRaiseCard(false), 0, 0);
        row.Controls.Add(BuildLowerCard(false), 1, 0);
        stack.Controls.Add(row);
        return page;
    }

    private Control BuildQuickPage()
    {
        var page = NewScroll();
        var stack = Stack(page);
        var row = TwoColumn();
        row.Controls.Add(BuildSplitCard(), 0, 0);
        row.Controls.Add(BuildCorrectionCard(), 1, 0);
        stack.Controls.Add(row);
        return page;
    }

    private Control BuildReportsPage()
    {
        var page = NewScroll();
        var stack = Stack(page);
        var card = CardPanel("ذخیره گزارش کامل", "گزارش شامل آبشده‌ها و نتیجه‌های نهایی است؛ فرمول محاسبه داخل فایل نوشته نمی‌شود.");
        var path = LabelBase("مسیر فعلی: " + (_settings.ReportFolder.Length == 0 ? "تعیین نشده" : _settings.ReportFolder), 10, Muted, false);
        path.AutoEllipsis = true;
        path.Height = 34;
        path.Dock = DockStyle.Top;
        card.Controls.Add(path);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 56, FlowDirection = FlowDirection.RightToLeft, BackColor = Card };
        var save = Primary("ذخیره گزارش");
        save.Width = 190;
        save.Click += (_, _) => SaveReport();
        var settings = Secondary("تغییر مسیر");
        settings.Width = 150;
        settings.Click += (_, _) => OpenSettings();
        actions.Controls.Add(save);
        actions.Controls.Add(settings);
        card.Controls.Add(actions);
        stack.Controls.Add(card);
        return page;
    }

    private Control BuildMetricsStrip()
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Top, Height = 126, ColumnCount = 4, BackColor = Bg, Margin = new Padding(0, 0, 0, 14) };
        for (int i = 0; i < 4; i++) row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        row.Controls.Add(MetricCard("وزن کل", _totalWeight, "g"), 0, 0);
        row.Controls.Add(MetricCard("عیار میانگین", _avgAssay, "‰"), 1, 0);
        row.Controls.Add(MetricCard("تعداد آبشده", _count, "ردیف"), 2, 0);
        row.Controls.Add(MetricCard("وزن پس از بار", _afterAlloy, "g"), 3, 0);
        return row;
    }

    private Control BuildEntryCard(bool compact)
    {
        var card = CardPanel("ثبت سریع آبشده", "وزن را دستی وارد کن یا وقتی فیلد وزن فعال است کلید ↑ را برای دریافت از ترازو بزن.");
        var fields = TwoColumn();
        fields.Controls.Add(Field("وزن آبشده (g)", _weight), 0, 0);
        fields.Controls.Add(Field("عیار آبشده", _assay), 1, 0);
        card.Controls.Add(fields);
        _entryScaleHint.Dock = DockStyle.Top;
        _entryScaleHint.Height = 28;
        card.Controls.Add(_entryScaleHint);

        var actions = new TableLayoutPanel { Dock = DockStyle.Top, Height = 54, ColumnCount = 2, BackColor = Card, Margin = new Padding(0, 8, 0, 0) };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 76));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        _saveEntry.Dock = DockStyle.Fill;
        var clear = Secondary("پاک‌کردن همه");
        clear.ForeColor = Danger;
        clear.Dock = DockStyle.Fill;
        clear.Click += (_, _) => ClearAll();
        actions.Controls.Add(_saveEntry, 0, 0);
        actions.Controls.Add(clear, 1, 0);
        card.Controls.Add(actions);
        if (compact) card.Height = 300;
        return card;
    }

    private Control BuildScaleCard()
    {
        var card = CardPanel("ترازو", "وضعیت اتصال و میانبر دریافت وزن");
        var status = LabelBase("پورت: " + _settings.PortName, 12, TextMain, true);
        status.Dock = DockStyle.Top;
        status.Height = 44;
        card.Controls.Add(status);
        var info = LabelBase($"{_settings.BaudRate} baud  •  {_settings.DataBits} data bits  •  {_settings.Parity}  •  {_settings.StopBits}", 9.5f, Muted, false);
        info.Dock = DockStyle.Top;
        info.Height = 36;
        info.RightToLeft = RightToLeft.No;
        card.Controls.Add(info);
        var read = Primary("دریافت وزن با ↑");
        read.Dock = DockStyle.Top;
        read.Height = 46;
        read.Click += async (_, _) => await ReadScaleIntoWeightAsync();
        card.Controls.Add(read);
        var settings = Secondary("تنظیمات ترازو");
        settings.Dock = DockStyle.Top;
        settings.Height = 42;
        settings.Click += (_, _) => OpenSettings();
        card.Controls.Add(settings);
        return card;
    }

    private Control BuildGridCard()
    {
        var card = CardPanel("لیست آبشده‌ها", "برای ویرایش یا حذف از ستون عملیات استفاده کن.");
        _grid.Dock = DockStyle.Top;
        _grid.Height = 430;
        card.Controls.Add(_grid);
        return card;
    }

    private Control BuildRaiseCard(bool compact)
    {
        var card = CardPanel("بالا بردن عیار", "در صورت پایین‌تر بودن عیار میانگین از هدف، مقدار شمش عیار بالا محاسبه می‌شود.");
        var fields = TwoColumn();
        fields.Controls.Add(Field("عیار هدف", _raiseTarget), 0, 0);
        fields.Controls.Add(Field("عیار شمش", _barAssay), 1, 0);
        card.Controls.Add(fields);
        var metrics = TwoColumn();
        metrics.Controls.Add(MiniMetric("اختلاف تا هدف", _raiseDiff), 0, 0);
        metrics.Controls.Add(MiniMetric("شمش مورد نیاز (g)", _raiseNeed), 1, 0);
        card.Controls.Add(metrics);
        _raiseState.Dock = DockStyle.Top;
        _raiseState.Height = compact ? 58 : 78;
        card.Controls.Add(_raiseState);
        return card;
    }

    private Control BuildLowerCard(bool compact)
    {
        var card = CardPanel("پایین آوردن عیار", "در صورت بالاتر بودن عیار میانگین از هدف، بار ریخته‌گری مورد نیاز محاسبه می‌شود.");
        var fields = TwoColumn();
        fields.Controls.Add(Field("عیار هدف", _lowerTarget), 0, 0);
        fields.Controls.Add(Field("درصد نقره", _silver), 1, 0);
        card.Controls.Add(fields);
        var metrics = new TableLayoutPanel { Dock = DockStyle.Top, Height = 138, ColumnCount = 2, RowCount = 2, BackColor = Card };
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        metrics.Controls.Add(MiniMetric("کل بار (g)", _alloy), 0, 0);
        metrics.Controls.Add(MiniMetric("نقره (g)", _silverNeed), 1, 0);
        metrics.Controls.Add(MiniMetric("بار بدون نقره (g)", _otherAlloy), 0, 1);
        metrics.Controls.Add(MiniMetric("وزن پس از بار (g)", _lowerAfter), 1, 1);
        card.Controls.Add(metrics);
        _lowerState.Dock = DockStyle.Top;
        _lowerState.Height = compact ? 58 : 78;
        card.Controls.Add(_lowerState);
        return card;
    }

    private Control BuildSplitCard()
    {
        var card = CardPanel("تقسیم ۳۶.۷۹٪ / ۶۳.۲۱٪", "عدد پایه را وارد کن؛ هر دو خروجی بلافاصله محاسبه می‌شوند.");
        card.Controls.Add(Field("عدد پایه", _splitBase));
        var row = TwoColumn();
        row.Controls.Add(MiniMetric("۳۶.۷۹٪", _splitA), 0, 0);
        row.Controls.Add(MiniMetric("۶۳.۲۱٪", _splitB), 1, 0);
        card.Controls.Add(row);
        return card;
    }

    private Control BuildCorrectionCard()
    {
        var card = CardPanel("اصلاح وزن برای افت عیار", "وزن پایه، عیار هدف و مقدار افت را وارد کن.");
        var top = TwoColumn();
        top.Controls.Add(Field("وزن پایه", _corrWeight), 0, 0);
        top.Controls.Add(Field("عیار هدف", _corrTarget), 1, 0);
        card.Controls.Add(top);
        card.Controls.Add(Field("مقدار افت عیار", _corrDrop));
        var row = TwoColumn();
        row.Controls.Add(MiniMetric("بار افزوده (g)", _corrAdd), 0, 0);
        row.Controls.Add(MiniMetric("جمع وزن (g)", _corrTotal), 1, 0);
        card.Controls.Add(row);
        return card;
    }

    private void BindEvents()
    {
        _saveEntry.Click += (_, _) => SaveEntry();
        _weight.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Up && _settings.ReadOnUpArrow)
            {
                e.SuppressKeyPress = true;
                await ReadScaleIntoWeightAsync();
            }
            else if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                _assay.Focus();
                _assay.SelectAll();
            }
        };
        _assay.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SaveEntry();
            }
        };

        foreach (var b in new[] { _raiseTarget, _barAssay, _lowerTarget, _silver, _splitBase, _corrWeight, _corrTarget, _corrDrop })
        {
            b.TextChanged += (_, _) => Recalculate();
            b.Enter += (_, _) => b.SelectAll();
        }

        _scale.WeightReceived += value => Ui(() =>
        {
            _scaleHeaderStatus.Text = "ترازو: " + Num(value) + " g";
            _scaleHeaderStatus.ForeColor = Success;
            _entryScaleHint.Text = "● وزن دریافتی: " + Num(value) + " g";
            _entryScaleHint.ForeColor = Success;
            if (_settings.AutoRead && _weight.Focused) _weight.Text = Num(value);
        });
        _scale.StatusChanged += (text, ok) => Ui(() =>
        {
            _scaleHeaderStatus.Text = text;
            _scaleHeaderStatus.ForeColor = ok ? Success : Muted;
        });
    }

    private void Ui(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(action); } catch { }
    }

    private void ApplyScaleSettings()
    {
        _scale.ApplySettings(_settings, _settings.AutoRead);
        _scaleHeaderStatus.Text = _scale.IsOpen ? "ترازو: متصل " + _settings.PortName : "ترازو: " + _settings.PortName;
        _scaleHeaderStatus.ForeColor = _scale.IsOpen ? Success : Muted;
    }

    private async Task ReadScaleIntoWeightAsync()
    {
        try
        {
            _entryScaleHint.Text = "● در حال دریافت وزن…";
            _entryScaleHint.ForeColor = Gold;
            var w = await _scale.ReadNowAsync();
            _weight.Text = Num(w);
            _weight.Focus();
            _weight.SelectAll();
        }
        catch (Exception ex)
        {
            _entryScaleHint.Text = "● دریافت وزن ناموفق";
            _entryScaleHint.ForeColor = Danger;
            MessageBox.Show(this, ex.Message + "\n\nتنظیمات Port و Baud Rate را بررسی کن.", "ترازو", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(_settings);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _settings = dialog.ResultSettings;
        ApplyScaleSettings();
        if (_nav.ContainsKey("dashboard")) ShowPage("dashboard");
    }

    private void SaveEntry()
    {
        var w = Parse(_weight.Text, -1);
        var a = Parse(_assay.Text, -1);
        if (w <= 0 || a <= 0 || a > 1000)
        {
            MessageBox.Show(this, "وزن و عیار را صحیح وارد کن.", "ورودی نامعتبر", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var item = new GoldEntry(w, a);
        if (_editingIndex >= 0 && _editingIndex < _entries.Count) _entries[_editingIndex] = item;
        else _entries.Add(item);
        _editingIndex = -1;
        _saveEntry.Text = "ثبت آبشده";
        _weight.Clear();
        _assay.Clear();
        PersistEntries();
        RefreshAll();
        _weight.Focus();
    }

    private void EditEntry(int i)
    {
        if (i < 0 || i >= _entries.Count) return;
        _editingIndex = i;
        _weight.Text = Num(_entries[i].Weight);
        _assay.Text = Num(_entries[i].Assay);
        _saveEntry.Text = "ذخیره تغییرات";
        _weight.Focus();
        _weight.SelectAll();
        ShowPage("entries");
    }

    private void DeleteEntry(int i)
    {
        if (i < 0 || i >= _entries.Count) return;
        _entries.RemoveAt(i);
        if (_editingIndex == i) _editingIndex = -1;
        if (_editingIndex > i) _editingIndex--;
        PersistEntries();
        RefreshAll();
    }

    private void ClearAll()
    {
        if (_entries.Count == 0) return;
        if (MessageBox.Show(this, "همه آبشده‌ها حذف شوند؟", "پاک‌کردن همه", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        _entries.Clear();
        _editingIndex = -1;
        _weight.Clear();
        _assay.Clear();
        _saveEntry.Text = "ثبت آبشده";
        PersistEntries();
        RefreshAll();
    }

    private void Recalculate()
    {
        var s = GoldCalculator.Summarize(_entries);
        _totalWeight.Text = Num(s.Weight);
        _avgAssay.Text = Num(s.AverageAssay);
        _count.Text = s.Count.ToString(CultureInfo.InvariantCulture);

        var raiseT = Parse(_raiseTarget.Text, 747);
        var high = Parse(_barAssay.Text, 995);
        var raise = GoldCalculator.RequiredHighAssayBar(s, raiseT, high);
        _raiseDiff.Text = Num(raise.DifferenceNeeded);
        _raiseNeed.Text = Num(raise.RequiredHighBar);
        _raiseState.Text = !double.IsFinite(raise.RequiredHighBar) ? "ابتدا آبشده معتبر ثبت کن." : raise.RequiredHighBar > 0 ? $"برای رسیدن به {Num(raiseT)}، مقدار {Num(raise.RequiredHighBar)} g شمش {Num(high)} نیاز است." : "افزایش عیار لازم نیست.";
        _raiseState.ForeColor = double.IsFinite(raise.RequiredHighBar) ? Gold : Muted;

        var lowerT = Parse(_lowerTarget.Text, 746);
        var silver = Parse(_silver.Text, 32);
        var lower = GoldCalculator.RequiredAlloy(s, lowerT, silver, s.Weight);
        _alloy.Text = Num(lower.TotalAlloyRequired);
        _silverNeed.Text = Num(lower.SilverRequired);
        _otherAlloy.Text = Num(lower.NonSilverRequired);
        _lowerAfter.Text = Num(lower.TotalAfterAlloy);
        _afterAlloy.Text = Num(lower.TotalAfterAlloy);
        _lowerState.Text = !double.IsFinite(lower.TotalAlloyRequired) ? "ابتدا آبشده معتبر ثبت کن." : lower.TotalAlloyRequired > 0 ? $"برای کاهش تا {Num(lowerT)}، مقدار {Num(lower.TotalAlloyRequired)} g بار ریخته‌گری نیاز است." : "کاهش عیار لازم نیست.";
        _lowerState.ForeColor = double.IsFinite(lower.TotalAlloyRequired) ? Gold : Muted;

        var baseVal = Parse(_splitBase.Text, 800);
        var p = GoldCalculator.Split3679(baseVal);
        _splitA.Text = Num(p);
        _splitB.Text = Num(baseVal - p);

        var cw = Parse(_corrWeight.Text, 250);
        var ct = Parse(_corrTarget.Text, 750);
        var cd = Parse(_corrDrop.Text, 1);
        var add = GoldCalculator.CorrectionAddition(cw, ct, cd);
        _corrAdd.Text = Num(add);
        _corrTotal.Text = Num(cw + add);
    }

    private void RefreshAll()
    {
        Recalculate();
        if (_grid.Columns.Count == 0) return;
        _grid.Rows.Clear();
        for (int i = 0; i < _entries.Count; i++)
            _grid.Rows.Add(i + 1, Num(_entries[i].Weight), Num(_entries[i].Assay), "ویرایش", "حذف");
    }

    private void SaveReport()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_settings.ReportFolder))
            {
                MessageBox.Show(this, "ابتدا از تنظیمات مسیر گزارش را انتخاب کن.", "گزارش", MessageBoxButtons.OK, MessageBoxIcon.Information);
                OpenSettings();
                return;
            }
            if (!Directory.Exists(_settings.ReportFolder)) Directory.CreateDirectory(_settings.ReportFolder);
            var s = GoldCalculator.Summarize(_entries);
            var raise = GoldCalculator.RequiredHighAssayBar(s, Parse(_raiseTarget.Text, 747), Parse(_barAssay.Text, 995));
            var lower = GoldCalculator.RequiredAlloy(s, Parse(_lowerTarget.Text, 746), Parse(_silver.Text, 32), s.Weight);
            var b = new StringBuilder();
            b.AppendLine("GOLD BAR (by:Amirnourhan)");
            b.AppendLine("تاریخ و ساعت: " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
            b.AppendLine();
            b.AppendLine("آبشده‌ها:");
            for (int i = 0; i < _entries.Count; i++) b.AppendLine($"{i + 1}) وزن {Num(_entries[i].Weight)} g | عیار {Num(_entries[i].Assay)}");
            b.AppendLine();
            b.AppendLine($"وزن کل: {Num(s.Weight)} g | عیار میانگین: {Num(s.AverageAssay)} | تعداد: {s.Count}");
            b.AppendLine($"وزن پس از بار: {Num(lower.TotalAfterAlloy)} g");
            b.AppendLine($"شمش عیار بالا مورد نیاز: {Num(raise.RequiredHighBar)} g");
            b.AppendLine($"بار ریخته‌گری مورد نیاز: {Num(lower.TotalAlloyRequired)} g | نقره: {Num(lower.SilverRequired)} g | بار بدون نقره: {Num(lower.NonSilverRequired)} g");
            var split = GoldCalculator.Split3679(Parse(_splitBase.Text, 800));
            b.AppendLine($"محاسبه سریع: 36.79% = {Num(split)} | 63.21% = {Num(Parse(_splitBase.Text, 800) - split)}");
            b.AppendLine($"اصلاح افت عیار: بار افزوده {Num(_corrAdd.Text.Length == 0 ? 0 : Parse(_corrAdd.Text, 0))} g | جمع وزن { _corrTotal.Text }");
            var path = Path.Combine(_settings.ReportFolder, "GoldBar_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt");
            File.WriteAllText(path, b.ToString(), Encoding.UTF8);
            MessageBox.Show(this, "گزارش ذخیره شد:\n" + path, "گزارش", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "ذخیره گزارش انجام نشد:\n" + ex.Message + "\n\nاز تنظیمات یک پوشه موجود و قابل‌نوشتن انتخاب کن.", "خطای گزارش", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ConfigureGrid()
    {
        _grid.BackgroundColor = Card;
        _grid.BorderStyle = BorderStyle.None;
        _grid.GridColor = Border;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Card2;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = TextMain;
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Card2;
        _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.ColumnHeadersHeight = 42;
        _grid.DefaultCellStyle.BackColor = Card;
        _grid.DefaultCellStyle.ForeColor = TextMain;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(59, 50, 28);
        _grid.DefaultCellStyle.SelectionForeColor = TextMain;
        _grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.RowTemplate.Height = 42;
        _grid.RowHeadersVisible = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.ReadOnly = true;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RightToLeft = RightToLeft.Yes;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "No", HeaderText = "#", FillWeight = 10 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Weight", HeaderText = "وزن (g)", FillWeight = 28 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Assay", HeaderText = "عیار", FillWeight = 24 });
        _grid.Columns.Add(new DataGridViewButtonColumn { Name = "Edit", HeaderText = "", Text = "ویرایش", UseColumnTextForButtonValue = true, FillWeight = 19, FlatStyle = FlatStyle.Flat });
        _grid.Columns.Add(new DataGridViewButtonColumn { Name = "Delete", HeaderText = "", Text = "حذف", UseColumnTextForButtonValue = true, FillWeight = 19, FlatStyle = FlatStyle.Flat });
        _grid.CellContentClick += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            var name = _grid.Columns[e.ColumnIndex].Name;
            if (name == "Edit") EditEntry(e.RowIndex);
            if (name == "Delete") DeleteEntry(e.RowIndex);
        };
    }

    private void LoadEntries()
    {
        try
        {
            if (!File.Exists(DataPath)) return;
            var loaded = JsonSerializer.Deserialize<List<GoldEntry>>(File.ReadAllText(DataPath));
            if (loaded is null) return;
            _entries.AddRange(loaded.Where(x => x.Weight > 0 && x.Assay > 0 && x.Assay <= 1000));
        }
        catch { }
    }

    private void PersistEntries()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!);
            File.WriteAllText(DataPath, JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "ذخیره اطلاعات داخلی انجام نشد:\n" + ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static Panel NewScroll() => new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Bg, Padding = new Padding(24, 8, 24, 26) };

    private static FlowLayoutPanel Stack(Panel parent)
    {
        var stack = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Bg, Padding = new Padding(0), RightToLeft = RightToLeft.Yes };
        parent.Controls.Add(stack);
        parent.SizeChanged += (_, _) => { foreach (Control c in stack.Controls) c.Width = Math.Max(760, parent.ClientSize.Width - 56); };
        return stack;
    }

    private static TableLayoutPanel TwoColumn()
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, BackColor = Bg, Margin = new Padding(0, 0, 0, 14), RightToLeft = RightToLeft.Yes };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        return t;
    }

    private static RoundedPanel CardPanel(string title, string? subtitle = null)
    {
        var card = new RoundedPanel { BackColor = Card, BorderColor = Border, Radius = 18, Padding = new Padding(18), Margin = new Padding(6), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var stack = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Card, RightToLeft = RightToLeft.Yes };
        var h = LabelBase(title, 14, TextMain, true); h.Margin = new Padding(0, 0, 0, 4); stack.Controls.Add(h);
        if (!string.IsNullOrWhiteSpace(subtitle)) { var s = LabelBase(subtitle, 9.5f, Muted, false); s.MaximumSize = new Size(620, 0); s.Margin = new Padding(0, 0, 0, 12); stack.Controls.Add(s); }
        card.Controls.Add(stack);
        card.ControlAdded += (_, _) => { };
        return card;
    }

    private static Control MetricCard(string title, Label value, string unit)
    {
        var c = new RoundedPanel { Dock = DockStyle.Fill, Margin = new Padding(6), Padding = new Padding(14), BackColor = Card, BorderColor = Border, Radius = 16 };
        var titleL = LabelBase(title, 9.5f, Muted, false); titleL.Dock = DockStyle.Top; titleL.Height = 24;
        value.Dock = DockStyle.Top; value.Height = 46; value.TextAlign = ContentAlignment.MiddleRight;
        var u = LabelBase(unit, 8.5f, Muted, false); u.Dock = DockStyle.Bottom; u.Height = 18;
        c.Controls.Add(u); c.Controls.Add(value); c.Controls.Add(titleL); return c;
    }

    private static Control MiniMetric(string title, Label value)
    {
        var c = new RoundedPanel { Dock = DockStyle.Fill, Margin = new Padding(5), Padding = new Padding(10), BackColor = Card2, BorderColor = Border, Radius = 13 };
        var t = LabelBase(title, 8.8f, Muted, false); t.Dock = DockStyle.Top; t.Height = 22;
        value.Dock = DockStyle.Fill; value.TextAlign = ContentAlignment.MiddleCenter;
        c.Controls.Add(value); c.Controls.Add(t); return c;
    }

    private static Control Field(string title, TextBox box)
    {
        var h = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 2, BackColor = Card, Margin = new Padding(5), Padding = new Padding(0, 0, 0, 6) };
        var t = LabelBase(title, 9, Muted, false); t.Dock = DockStyle.Top; t.Height = 24;
        box.Dock = DockStyle.Top; box.Height = 40;
        h.Controls.Add(t, 0, 0); h.Controls.Add(box, 0, 1); return h;
    }

    private static TextBox Input(string? text = null) => new()
    {
        Text = text ?? "",
        BackColor = Card2,
        ForeColor = TextMain,
        BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Segoe UI", 11.5f),
        TextAlign = HorizontalAlignment.Right,
        RightToLeft = RightToLeft.No,
        Margin = new Padding(3)
    };

    private static Button Primary(string text) => MakeButton(text, Gold, Color.FromArgb(22, 16, 3));
    private static Button Secondary(string text) => MakeButton(text, Card2, Gold);
    private static Button MakeButton(string text, Color bg, Color fg)
    {
        var b = new Button { Text = text, Height = 44, FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = fg, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand, Margin = new Padding(4), Padding = new Padding(8, 3, 8, 3) };
        b.FlatAppearance.BorderColor = bg == Gold ? GoldDark : Border;
        b.FlatAppearance.BorderSize = 1;
        return b;
    }

    private static Label LabelBase(string text, float size, Color color, bool bold) => new() { Text = text, AutoSize = true, ForeColor = color, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), RightToLeft = RightToLeft.Yes };
    private static Label MetricValue() => new() { Text = "—", AutoSize = false, ForeColor = Gold, Font = new Font("Segoe UI", 18, FontStyle.Bold), RightToLeft = RightToLeft.No };
    private static Label Status() => new() { Text = "—", ForeColor = Muted, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, BackColor = Card2, AutoEllipsis = true, Padding = new Padding(10) };

    private static double Parse(string raw, double fallback)
    {
        try
        {
            var s = NormalizeDigits(raw).Trim().Replace('٫', '.').Replace(',', '.');
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }
        catch { return fallback; }
    }

    private static string NormalizeDigits(string raw)
    {
        const string fa = "۰۱۲۳۴۵۶۷۸۹", ar = "٠١٢٣٤٥٦٧٨٩";
        var chars = raw.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var p = fa.IndexOf(chars[i]); if (p < 0) p = ar.IndexOf(chars[i]); if (p >= 0) chars[i] = (char)('0' + p);
        }
        return new string(chars);
    }

    private static string Num(double v) => !double.IsFinite(v) ? "—" : (Math.Abs(v) < 1e-7 ? 0 : v).ToString("0.###", CultureInfo.InvariantCulture);

    private static void OpenInstagram()
    {
        try { Process.Start(new ProcessStartInfo("https://www.instagram.com/4mirnourhan/") { UseShellExecute = true }); } catch { }
    }
}

public sealed class RoundedPanel : Panel
{
    public int Radius { get; set; } = 16;
    public Color BorderColor { get; set; } = Color.Transparent;

    public RoundedPanel()
    {
        DoubleBuffered = true;
        Resize += (_, _) => Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var r = ClientRectangle;
        r.Width -= 1; r.Height -= 1;
        if (r.Width <= 1 || r.Height <= 1) return;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var path = Rounded(r, Radius);
        using var pen = new Pen(BorderColor, 1);
        e.Graphics.DrawPath(pen, path);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        var r = ClientRectangle;
        if (r.Width <= 1 || r.Height <= 1) return;
        using var path = Rounded(r, Radius);
        Region = new Region(path);
    }

    private static System.Drawing.Drawing2D.GraphicsPath Rounded(Rectangle r, int radius)
    {
        var p = new System.Drawing.Drawing2D.GraphicsPath();
        var d = Math.Max(2, radius * 2);
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
