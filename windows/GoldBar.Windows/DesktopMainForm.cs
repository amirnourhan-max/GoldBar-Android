using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using GoldBar.Core;

namespace GoldBar.Windows;

public sealed class DesktopMainForm : Form
{
    private static readonly Color Bg = Color.FromArgb(7, 9, 13);
    private static readonly Color Sidebar = Color.FromArgb(12, 15, 21);
    private static readonly Color Card = Color.FromArgb(17, 21, 28);
    private static readonly Color Card2 = Color.FromArgb(23, 28, 37);
    private static readonly Color Border = Color.FromArgb(47, 54, 68);
    private static readonly Color Gold = Color.FromArgb(247, 211, 112);
    private static readonly Color GoldDark = Color.FromArgb(188, 139, 39);
    private static readonly Color TextMain = Color.FromArgb(246, 244, 237);
    private static readonly Color Muted = Color.FromArgb(151, 160, 176);
    private static readonly Color Success = Color.FromArgb(103, 221, 151);
    private static readonly Color Danger = Color.FromArgb(255, 108, 108);

    private readonly ScaleReader _scale = new();
    private AppSettings _settings = AppSettings.Load();
    private readonly List<GoldEntry> _entries = new();
    private int _editingIndex = -1;

    private readonly Panel _workspace = new() { Dock = DockStyle.Fill, BackColor = Bg };
    private readonly Label _pageTitle = UiLabel("داشبورد", 22f, TextMain, true);
    private readonly Label _pageSubtitle = UiLabel("نمای کلی وزن، عیار و عملیات پرکاربرد", 9.5f, Muted, false);
    private readonly Label _scaleHeaderStatus = UiLabel("ترازو: آماده", 9.2f, Muted, true);
    private readonly Label _liveScaleWeight = UiLabel("— g", 26f, Gold, true);
    private readonly Dictionary<string, RoundButton> _nav = new();

    private readonly Label _totalWeight = MetricValue();
    private readonly Label _avgAssay = MetricValue();
    private readonly Label _count = MetricValue();
    private readonly Label _afterAlloy = MetricValue();

    private readonly TextBox _weight = Input();
    private readonly TextBox _assay = Input();
    private readonly RoundButton _saveEntry = Primary("ثبت آبشده");
    private readonly Label _entryScaleHint = UiLabel("↑  دریافت وزن از ترازو", 9.2f, Muted, true);
    private readonly DataGridView _grid = new();

    private readonly TextBox _raiseTarget = Input("747");
    private readonly TextBox _barAssay = Input("995");
    private readonly Label _raiseDiff = MetricValue(16f);
    private readonly Label _raiseNeed = MetricValue(16f);
    private readonly Label _raiseState = StatusLabel();

    private readonly TextBox _lowerTarget = Input("746");
    private readonly TextBox _silver = Input("32");
    private readonly Label _alloy = MetricValue(16f);
    private readonly Label _silverNeed = MetricValue(16f);
    private readonly Label _otherAlloy = MetricValue(16f);
    private readonly Label _lowerAfter = MetricValue(16f);
    private readonly Label _lowerState = StatusLabel();

    private readonly TextBox _splitBase = Input("800");
    private readonly Label _splitA = MetricValue(20f);
    private readonly Label _splitB = MetricValue(20f);
    private readonly TextBox _corrWeight = Input("250");
    private readonly TextBox _corrTarget = Input("750");
    private readonly TextBox _corrDrop = Input("1");
    private readonly Label _corrAdd = MetricValue(18f);
    private readonly Label _corrTotal = MetricValue(18f);

    private static string DataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GoldBar", "entries.json");

    public DesktopMainForm()
    {
        Text = "Gold Bar (by:Amirnourhan)";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1460, 900);
        MinimumSize = new Size(1180, 760);
        BackColor = Bg;
        ForeColor = TextMain;
        Font = new Font("Segoe UI", 10f);
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        RightToLeft = RightToLeft.No;
        RightToLeftLayout = false;

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
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 242));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.Controls.Add(BuildSidebar(), 0, 0);
        shell.Controls.Add(BuildMainArea(), 1, 0);
        Controls.Add(shell);
    }

    private Control BuildSidebar()
    {
        var side = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Sidebar,
            Padding = new Padding(15, 18, 15, 16)
        };

        var brand = new RoundedPanel
        {
            Dock = DockStyle.Top,
            Height = 116,
            Radius = 18,
            BackColor = Card,
            BorderColor = Border,
            Padding = new Padding(14)
        };

        var brandLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Card,
            Margin = Padding.Empty
        };
        brandLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        brandLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));

        var brandText = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Card,
            Padding = new Padding(6, 8, 0, 2)
        };
        brandText.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        brandText.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        brandText.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var brandTitle = UiLabel("GOLD BAR", 18f, TextMain, true);
        brandTitle.RightToLeft = RightToLeft.No;
        brandTitle.TextAlign = ContentAlignment.MiddleLeft;
        brandTitle.Dock = DockStyle.Fill;
        var by = new LinkLabel
        {
            Text = "by: Amirnourhan",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            RightToLeft = RightToLeft.No,
            LinkColor = Gold,
            ActiveLinkColor = Gold,
            VisitedLinkColor = Gold,
            Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        by.LinkClicked += (_, _) => OpenInstagram();
        var edition = UiLabel("Windows Desktop", 8.3f, Muted, false);
        edition.RightToLeft = RightToLeft.No;
        edition.TextAlign = ContentAlignment.MiddleLeft;
        edition.Dock = DockStyle.Fill;
        brandText.Controls.Add(brandTitle, 0, 0);
        brandText.Controls.Add(by, 0, 1);
        brandText.Controls.Add(edition, 0, 2);

        var au = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 12, 0, 12),
            BackColor = Gold,
            BorderColor = Gold,
            Radius = 15
        };
        var auLabel = UiLabel("Au", 19f, Color.FromArgb(22, 16, 3), true);
        auLabel.Dock = DockStyle.Fill;
        auLabel.TextAlign = ContentAlignment.MiddleCenter;
        auLabel.RightToLeft = RightToLeft.No;
        au.Controls.Add(auLabel);

        brandLayout.Controls.Add(brandText, 0, 0);
        brandLayout.Controls.Add(au, 1, 0);
        brand.Controls.Add(brandLayout);
        side.Controls.Add(brand);

        var navHost = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 425,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Sidebar,
            Padding = new Padding(0, 22, 0, 0),
            Margin = Padding.Empty
        };
        AddNav(navHost, "dashboard", "داشبورد", "▦");
        AddNav(navHost, "entries", "آبشده‌ها", "◆");
        AddNav(navHost, "calculations", "محاسبات عیار", "∑");
        AddNav(navHost, "quick", "محاسبه سریع", "⚡");
        AddNav(navHost, "reports", "گزارش", "▤");
        AddNav(navHost, "settings", "تنظیمات", "⚙");
        side.Controls.Add(navHost);

        var footer = UiLabel("GOLD BAR  •  v1.4.0", 8.4f, Muted, false);
        footer.Dock = DockStyle.Bottom;
        footer.Height = 32;
        footer.TextAlign = ContentAlignment.MiddleCenter;
        footer.RightToLeft = RightToLeft.No;
        side.Controls.Add(footer);
        return side;
    }

    private void AddNav(Control host, string key, string title, string icon)
    {
        var b = new RoundButton
        {
            Text = $"{icon}     {title}",
            Width = 210,
            Height = 50,
            Radius = 12,
            FlatStyle = FlatStyle.Flat,
            BackColor = Sidebar,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 10.3f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(10, 0, 14, 0),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 7),
            RightToLeft = RightToLeft.Yes
        };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (_, _) =>
        {
            if (key == "settings") OpenSettings();
            else ShowPage(key);
        };
        _nav[key] = b;
        host.Controls.Add(b);
    }

    private Control BuildMainArea()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Bg,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
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
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(24, 14, 24, 10)
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var statusChip = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 6, 10, 6),
            Radius = 14,
            BackColor = Card,
            BorderColor = Border,
            Padding = new Padding(12)
        };
        _scaleHeaderStatus.Dock = DockStyle.Fill;
        _scaleHeaderStatus.TextAlign = ContentAlignment.MiddleCenter;
        _scaleHeaderStatus.RightToLeft = RightToLeft.Yes;
        statusChip.Controls.Add(_scaleHeaderStatus);

        var titles = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Bg,
            Margin = Padding.Empty
        };
        titles.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        titles.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        _pageTitle.Dock = DockStyle.Fill;
        _pageTitle.TextAlign = ContentAlignment.MiddleRight;
        _pageSubtitle.Dock = DockStyle.Fill;
        _pageSubtitle.TextAlign = ContentAlignment.MiddleRight;
        titles.Controls.Add(_pageTitle, 0, 0);
        titles.Controls.Add(_pageSubtitle, 0, 1);

        bar.Controls.Add(statusChip, 0, 0);
        bar.Controls.Add(titles, 1, 0);
        return bar;
    }

    private void ShowPage(string key)
    {
        foreach (var pair in _nav)
        {
            var active = pair.Key == key;
            pair.Value.BackColor = active ? Card2 : Sidebar;
            pair.Value.ForeColor = active ? Gold : Muted;
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
                key = "dashboard";
                break;
        }

        page.Dock = DockStyle.Fill;
        _workspace.Controls.Add(page);
        _workspace.ResumeLayout(true);
        RefreshAll();
    }

    private Control BuildDashboardPage()
    {
        var root = PageGrid(3);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 270));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(BuildMetricsStrip(), 0, 0);

        var mid = TwoColumns(68, 32);
        mid.Controls.Add(BuildEntryCard(), 0, 0);
        mid.Controls.Add(BuildScaleCard(), 1, 0);
        root.Controls.Add(mid, 0, 1);

        var bottom = TwoColumns(50, 50);
        bottom.Controls.Add(BuildRaiseCard(), 0, 0);
        bottom.Controls.Add(BuildLowerCard(), 1, 0);
        root.Controls.Add(bottom, 0, 2);
        return root;
    }

    private Control BuildEntriesPage()
    {
        var root = PageGrid(2);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 270));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildEntryCard(), 0, 0);
        root.Controls.Add(BuildGridCard(), 0, 1);
        return root;
    }

    private Control BuildCalculationsPage()
    {
        var root = PageGrid(2);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildMetricsStrip(), 0, 0);
        var split = TwoColumns(50, 50);
        split.Controls.Add(BuildRaiseCard(), 0, 0);
        split.Controls.Add(BuildLowerCard(), 1, 0);
        root.Controls.Add(split, 0, 1);
        return root;
    }

    private Control BuildQuickPage()
    {
        var root = PageGrid(1);
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var split = TwoColumns(50, 50);
        split.Controls.Add(BuildSplitCard(), 0, 0);
        split.Controls.Add(BuildCorrectionCard(), 1, 0);
        root.Controls.Add(split, 0, 0);
        return root;
    }

    private Control BuildReportsPage()
    {
        var root = PageGrid(1);
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Card,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(4, 8, 4, 4)
        };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var pathTitle = UiLabel("مسیر ذخیره فعلی", 9.4f, Muted, true);
        pathTitle.Dock = DockStyle.Fill;
        pathTitle.TextAlign = ContentAlignment.BottomRight;
        var path = UiLabel(string.IsNullOrWhiteSpace(_settings.ReportFolder) ? "تعیین نشده" : _settings.ReportFolder, 11f, TextMain, true);
        path.Dock = DockStyle.Fill;
        path.TextAlign = ContentAlignment.TopRight;
        path.AutoEllipsis = true;
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Card
        };
        var save = Primary("ذخیره گزارش");
        save.Width = 190;
        save.Click += (_, _) => SaveReport();
        var settings = Secondary("تغییر مسیر");
        settings.Width = 155;
        settings.Click += (_, _) => OpenSettings();
        actions.Controls.Add(save);
        actions.Controls.Add(settings);
        var note = UiLabel("فایل متنی شامل آبشده‌ها و نتیجه‌های نهایی است؛ فرمول محاسبات داخل گزارش نوشته نمی‌شود.", 9.3f, Muted, false);
        note.Dock = DockStyle.Fill;
        note.TextAlign = ContentAlignment.TopRight;

        body.Controls.Add(pathTitle, 0, 0);
        body.Controls.Add(path, 0, 1);
        body.Controls.Add(actions, 0, 2);
        body.Controls.Add(note, 0, 3);

        var card = CardWithHeader("گزارش کامل", "خروجی مرتب و تاریخ‌دار", body);
        card.MaximumSize = new Size(850, 420);
        card.MinimumSize = new Size(650, 330);

        var center = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Bg,
            ColumnCount = 3,
            RowCount = 3
        };
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
        center.RowStyles.Add(new RowStyle(SizeType.Percent, 15));
        center.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
        center.RowStyles.Add(new RowStyle(SizeType.Percent, 15));
        center.Controls.Add(card, 1, 1);
        root.Controls.Add(center, 0, 0);
        return root;
    }

    private Control BuildMetricsStrip()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Bg,
            ColumnCount = 4,
            RowCount = 1,
            Margin = Padding.Empty
        };
        for (var i = 0; i < 4; i++) row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        row.Controls.Add(MetricCard("وزن کل", _totalWeight, "g"), 0, 0);
        row.Controls.Add(MetricCard("عیار میانگین", _avgAssay, "‰"), 1, 0);
        row.Controls.Add(MetricCard("تعداد آبشده", _count, "ردیف"), 2, 0);
        row.Controls.Add(MetricCard("وزن پس از بار", _afterAlloy, "g"), 3, 0);
        return row;
    }

    private Control BuildEntryCard()
    {
        var fields = TwoColumns(50, 50, Card);
        fields.Margin = new Padding(0, 0, 0, 8);
        fields.Controls.Add(Field("وزن آبشده (g)", _weight), 0, 0);
        fields.Controls.Add(Field("عیار آبشده", _assay), 1, 0);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Card,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(0)
        };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        body.Controls.Add(fields, 0, 0);
        _entryScaleHint.Dock = DockStyle.Fill;
        _entryScaleHint.TextAlign = ContentAlignment.MiddleRight;
        body.Controls.Add(_entryScaleHint, 0, 1);

        var actions = TwoColumns(78, 22, Card);
        _saveEntry.Dock = DockStyle.Fill;
        var clear = Secondary("پاک‌کردن همه");
        clear.ForeColor = Danger;
        clear.Dock = DockStyle.Fill;
        clear.Click += (_, _) => ClearAll();
        actions.Controls.Add(_saveEntry, 0, 0);
        actions.Controls.Add(clear, 1, 0);
        body.Controls.Add(actions, 0, 2);

        return CardWithHeader("ثبت سریع آبشده", "وزن را دستی وارد کن یا داخل فیلد وزن کلید ↑ را بزن.", body);
    }

    private Control BuildScaleCard()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Card,
            ColumnCount = 1,
            RowCount = 4,
            Padding = Padding.Empty
        };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        _liveScaleWeight.Dock = DockStyle.Fill;
        _liveScaleWeight.TextAlign = ContentAlignment.MiddleCenter;
        _liveScaleWeight.RightToLeft = RightToLeft.No;
        body.Controls.Add(_liveScaleWeight, 0, 0);

        var config = UiLabel($"{_settings.PortName}  •  {_settings.BaudRate} baud  •  {_settings.DataBits} bit  •  {_settings.Parity}", 9f, Muted, false);
        config.Dock = DockStyle.Fill;
        config.RightToLeft = RightToLeft.No;
        config.TextAlign = ContentAlignment.MiddleCenter;
        body.Controls.Add(config, 0, 1);

        var read = Primary("دریافت وزن با کلید ↑");
        read.Dock = DockStyle.Fill;
        read.Click += async (_, _) => await ReadScaleIntoWeightAsync();
        body.Controls.Add(read, 0, 2);
        var settings = Secondary("تنظیمات ترازو");
        settings.Dock = DockStyle.Fill;
        settings.Click += (_, _) => OpenSettings();
        body.Controls.Add(settings, 0, 3);

        return CardWithHeader("ترازو", "دریافت سریع وزن از RS-232 / COM", body);
    }

    private Control BuildGridCard()
    {
        _grid.Dock = DockStyle.Fill;
        return CardWithHeader("لیست آبشده‌ها", "برای ویرایش یا حذف از ستون عملیات استفاده کن.", _grid);
    }

    private Control BuildRaiseCard()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Card,
            ColumnCount = 1,
            RowCount = 3,
            Padding = Padding.Empty
        };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var fields = TwoColumns(50, 50, Card);
        fields.Controls.Add(Field("عیار هدف", _raiseTarget), 0, 0);
        fields.Controls.Add(Field("عیار شمش", _barAssay), 1, 0);
        body.Controls.Add(fields, 0, 0);

        var metrics = TwoColumns(50, 50, Card);
        metrics.Controls.Add(MiniMetric("اختلاف تا هدف", _raiseDiff), 0, 0);
        metrics.Controls.Add(MiniMetric("شمش مورد نیاز (g)", _raiseNeed), 1, 0);
        body.Controls.Add(metrics, 0, 1);
        _raiseState.Dock = DockStyle.Fill;
        body.Controls.Add(_raiseState, 0, 2);
        return CardWithHeader("بالا بردن عیار", "در صورت پایین‌تر بودن عیار میانگین از هدف", body);
    }

    private Control BuildLowerCard()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Card,
            ColumnCount = 1,
            RowCount = 4,
            Padding = Padding.Empty
        };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var fields = TwoColumns(50, 50, Card);
        fields.Controls.Add(Field("عیار هدف", _lowerTarget), 0, 0);
        fields.Controls.Add(Field("درصد نقره", _silver), 1, 0);
        body.Controls.Add(fields, 0, 0);

        var metrics1 = TwoColumns(50, 50, Card);
        metrics1.Controls.Add(MiniMetric("کل بار (g)", _alloy), 0, 0);
        metrics1.Controls.Add(MiniMetric("نقره (g)", _silverNeed), 1, 0);
        body.Controls.Add(metrics1, 0, 1);

        var metrics2 = TwoColumns(50, 50, Card);
        metrics2.Controls.Add(MiniMetric("بار بدون نقره (g)", _otherAlloy), 0, 0);
        metrics2.Controls.Add(MiniMetric("وزن پس از بار (g)", _lowerAfter), 1, 0);
        body.Controls.Add(metrics2, 0, 2);
        _lowerState.Dock = DockStyle.Fill;
        body.Controls.Add(_lowerState, 0, 3);
        return CardWithHeader("پایین آوردن عیار", "در صورت بالاتر بودن عیار میانگین از هدف", body);
    }

    private Control BuildSplitCard()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Card,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0, 6, 0, 0)
        };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.Controls.Add(Field("عدد پایه", _splitBase), 0, 0);
        var metrics = TwoColumns(50, 50, Card);
        metrics.Controls.Add(MiniMetric("۳۶.۷۹٪", _splitA), 0, 0);
        metrics.Controls.Add(MiniMetric("۶۳.۲۱٪", _splitB), 1, 0);
        body.Controls.Add(metrics, 0, 1);
        return CardWithHeader("تقسیم ۳۶.۷۹٪ / ۶۳.۲۱٪", "عدد پایه را وارد کن؛ هر دو خروجی لحظه‌ای محاسبه می‌شوند.", body);
    }

    private Control BuildCorrectionCard()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Card,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(0, 6, 0, 0)
        };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var top = TwoColumns(50, 50, Card);
        top.Controls.Add(Field("وزن پایه", _corrWeight), 0, 0);
        top.Controls.Add(Field("عیار هدف", _corrTarget), 1, 0);
        body.Controls.Add(top, 0, 0);
        body.Controls.Add(Field("مقدار افت عیار", _corrDrop), 0, 1);
        var metrics = TwoColumns(50, 50, Card);
        metrics.Controls.Add(MiniMetric("بار افزوده (g)", _corrAdd), 0, 0);
        metrics.Controls.Add(MiniMetric("جمع وزن (g)", _corrTotal), 1, 0);
        body.Controls.Add(metrics, 0, 2);
        return CardWithHeader("اصلاح وزن برای افت عیار", "وزن پایه، عیار هدف و مقدار افت را وارد کن.", body);
    }

    private static TableLayoutPanel PageGrid(int rows)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Bg,
            ColumnCount = 1,
            RowCount = rows,
            Padding = new Padding(22, 8, 22, 22),
            Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return root;
    }

    private static TableLayoutPanel TwoColumns(float first, float second, Color? background = null)
    {
        var t = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = background ?? Bg,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, first));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, second));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        return t;
    }

    private static Control CardWithHeader(string title, string subtitle, Control body)
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(6),
            Padding = new Padding(16),
            Radius = 18,
            BackColor = Card,
            BorderColor = Border
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Card,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var h = UiLabel(title, 13.5f, TextMain, true);
        h.Dock = DockStyle.Fill;
        h.TextAlign = ContentAlignment.MiddleRight;
        var s = UiLabel(subtitle, 9f, Muted, false);
        s.Dock = DockStyle.Fill;
        s.TextAlign = ContentAlignment.TopRight;
        body.Dock = DockStyle.Fill;
        layout.Controls.Add(h, 0, 0);
        layout.Controls.Add(s, 0, 1);
        layout.Controls.Add(body, 0, 2);
        card.Controls.Add(layout);
        return card;
    }

    private static Control MetricCard(string title, Label value, string unit)
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(6),
            Padding = new Padding(15),
            Radius = 16,
            BackColor = Card,
            BorderColor = Border
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Card
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        var t = UiLabel(title, 9.2f, Muted, false);
        t.Dock = DockStyle.Fill;
        t.TextAlign = ContentAlignment.MiddleRight;
        value.Dock = DockStyle.Fill;
        value.TextAlign = ContentAlignment.MiddleRight;
        var u = UiLabel(unit, 8.3f, Muted, false);
        u.Dock = DockStyle.Fill;
        u.TextAlign = ContentAlignment.MiddleRight;
        layout.Controls.Add(t, 0, 0);
        layout.Controls.Add(value, 0, 1);
        layout.Controls.Add(u, 0, 2);
        card.Controls.Add(layout);
        return card;
    }

    private static Control MiniMetric(string title, Label value)
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(5),
            Padding = new Padding(10),
            Radius = 13,
            BackColor = Card2,
            BorderColor = Border
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Card2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var t = UiLabel(title, 8.7f, Muted, false);
        t.Dock = DockStyle.Fill;
        t.TextAlign = ContentAlignment.MiddleCenter;
        value.Dock = DockStyle.Fill;
        value.TextAlign = ContentAlignment.MiddleCenter;
        layout.Controls.Add(t, 0, 0);
        layout.Controls.Add(value, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private static Control Field(string title, TextBox box)
    {
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Card,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(5, 2, 5, 2),
            Padding = Padding.Empty
        };
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var label = UiLabel(title, 8.9f, Muted, false);
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleRight;
        var inputHost = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 0, 0),
            Padding = new Padding(12, 9, 12, 7),
            Radius = 12,
            BackColor = Card2,
            BorderColor = Border
        };
        box.Dock = DockStyle.Fill;
        inputHost.Controls.Add(box);
        host.Controls.Add(label, 0, 0);
        host.Controls.Add(inputHost, 0, 1);
        return host;
    }

    private static TextBox Input(string? text = null) => new()
    {
        Text = text ?? "",
        BorderStyle = BorderStyle.None,
        BackColor = Card2,
        ForeColor = TextMain,
        Font = new Font("Segoe UI", 11.5f),
        TextAlign = HorizontalAlignment.Right,
        RightToLeft = RightToLeft.No,
        Margin = Padding.Empty
    };

    private static RoundButton Primary(string text) => MakeButton(text, Gold, Color.FromArgb(22, 16, 3), GoldDark);
    private static RoundButton Secondary(string text) => MakeButton(text, Card2, Gold, Border);

    private static RoundButton MakeButton(string text, Color bg, Color fg, Color border)
    {
        var b = new RoundButton
        {
            Text = text,
            Height = 44,
            Radius = 12,
            FlatStyle = FlatStyle.Flat,
            BackColor = bg,
            ForeColor = fg,
            Font = new Font("Segoe UI", 9.8f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4),
            Padding = new Padding(8, 2, 8, 2),
            RightToLeft = RightToLeft.Yes
        };
        b.FlatAppearance.BorderColor = border;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.MouseOverBackColor = bg == Gold ? Color.FromArgb(255, 222, 131) : Color.FromArgb(30, 36, 47);
        return b;
    }

    private static Label UiLabel(string text, float size, Color color, bool bold) => new()
    {
        Text = text,
        AutoSize = false,
        ForeColor = color,
        Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
        RightToLeft = RightToLeft.Yes,
        TextAlign = ContentAlignment.MiddleRight
    };

    private static Label MetricValue(float size = 18f) => new()
    {
        Text = "—",
        AutoSize = false,
        ForeColor = Gold,
        Font = new Font("Segoe UI", size, FontStyle.Bold),
        RightToLeft = RightToLeft.No,
        TextAlign = ContentAlignment.MiddleRight
    };

    private static Label StatusLabel() => new()
    {
        Text = "—",
        AutoSize = false,
        ForeColor = Muted,
        BackColor = Card2,
        Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
        RightToLeft = RightToLeft.Yes,
        TextAlign = ContentAlignment.MiddleCenter,
        Padding = new Padding(10),
        AutoEllipsis = true
    };

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

        foreach (var box in new[] { _raiseTarget, _barAssay, _lowerTarget, _silver, _splitBase, _corrWeight, _corrTarget, _corrDrop })
        {
            box.TextChanged += (_, _) => Recalculate();
            box.Enter += (_, _) => box.SelectAll();
        }

        _scale.WeightReceived += value => Ui(() =>
        {
            _liveScaleWeight.Text = Num(value) + " g";
            _scaleHeaderStatus.Text = "●  ترازو متصل  •  " + Num(value) + " g";
            _scaleHeaderStatus.ForeColor = Success;
            _entryScaleHint.Text = "●  وزن دریافتی: " + Num(value) + " g";
            _entryScaleHint.ForeColor = Success;
            if (_settings.AutoRead && _weight.Focused)
            {
                _weight.Text = Num(value);
                _weight.SelectAll();
            }
        });
        _scale.StatusChanged += (text, ok) => Ui(() =>
        {
            _scaleHeaderStatus.Text = ok ? "●  " + text : text;
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
        _scaleHeaderStatus.Text = _scale.IsOpen ? "●  ترازو متصل  •  " + _settings.PortName : "ترازو  •  " + _settings.PortName;
        _scaleHeaderStatus.ForeColor = _scale.IsOpen ? Success : Muted;
        _liveScaleWeight.Text = _scale.LastWeight.HasValue ? Num(_scale.LastWeight.Value) + " g" : "— g";
    }

    private async Task ReadScaleIntoWeightAsync()
    {
        try
        {
            _entryScaleHint.Text = "●  در حال دریافت وزن…";
            _entryScaleHint.ForeColor = Gold;
            var w = await _scale.ReadNowAsync();
            _weight.Text = Num(w);
            _weight.Focus();
            _weight.SelectAll();
            _entryScaleHint.Text = "●  وزن دریافتی: " + Num(w) + " g";
            _entryScaleHint.ForeColor = Success;
        }
        catch (Exception ex)
        {
            _entryScaleHint.Text = "●  دریافت وزن ناموفق";
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
        ShowPage("dashboard");
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

    private void EditEntry(int index)
    {
        if (index < 0 || index >= _entries.Count) return;
        ShowPage("entries");
        _editingIndex = index;
        _weight.Text = Num(_entries[index].Weight);
        _assay.Text = Num(_entries[index].Assay);
        _saveEntry.Text = "ذخیره تغییرات";
        _weight.Focus();
        _weight.SelectAll();
    }

    private void DeleteEntry(int index)
    {
        if (index < 0 || index >= _entries.Count) return;
        _entries.RemoveAt(index);
        if (_editingIndex == index) _editingIndex = -1;
        else if (_editingIndex > index) _editingIndex--;
        PersistEntries();
        RefreshAll();
    }

    private void ClearAll()
    {
        if (_entries.Count == 0) return;
        var answer = MessageBox.Show(this, "همه آبشده‌ها حذف شوند؟", "پاک‌کردن همه", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;
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

        var raiseTarget = Parse(_raiseTarget.Text, 747);
        var high = Parse(_barAssay.Text, 995);
        var raise = GoldCalculator.RequiredHighAssayBar(s, raiseTarget, high);
        _raiseDiff.Text = Num(raise.DifferenceNeeded);
        _raiseNeed.Text = Num(raise.RequiredHighBar);
        _raiseState.Text = !double.IsFinite(raise.RequiredHighBar)
            ? "ابتدا آبشده معتبر ثبت کن."
            : raise.RequiredHighBar > 0
                ? $"برای رسیدن به {Num(raiseTarget)}، مقدار {Num(raise.RequiredHighBar)} g شمش {Num(high)} نیاز است."
                : "افزایش عیار لازم نیست.";
        _raiseState.ForeColor = double.IsFinite(raise.RequiredHighBar) ? Gold : Muted;

        var lowerTarget = Parse(_lowerTarget.Text, 746);
        var silver = Parse(_silver.Text, 32);
        var lower = GoldCalculator.RequiredAlloy(s, lowerTarget, silver, s.Weight);
        _alloy.Text = Num(lower.TotalAlloyRequired);
        _silverNeed.Text = Num(lower.SilverRequired);
        _otherAlloy.Text = Num(lower.NonSilverRequired);
        _lowerAfter.Text = Num(lower.TotalAfterAlloy);
        _afterAlloy.Text = Num(lower.TotalAfterAlloy);
        _lowerState.Text = !double.IsFinite(lower.TotalAlloyRequired)
            ? "ابتدا آبشده معتبر ثبت کن."
            : lower.TotalAlloyRequired > 0
                ? $"برای کاهش تا {Num(lowerTarget)}، مقدار {Num(lower.TotalAlloyRequired)} g بار ریخته‌گری نیاز است."
                : "کاهش عیار لازم نیست.";
        _lowerState.ForeColor = double.IsFinite(lower.TotalAlloyRequired) ? Gold : Muted;

        var baseValue = Parse(_splitBase.Text, 800);
        var p = GoldCalculator.Split3679(baseValue);
        _splitA.Text = Num(p);
        _splitB.Text = Num(baseValue - p);

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
        for (var i = 0; i < _entries.Count; i++)
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
            for (var i = 0; i < _entries.Count; i++)
                b.AppendLine($"{i + 1}) وزن {Num(_entries[i].Weight)} g | عیار {Num(_entries[i].Assay)}");
            b.AppendLine();
            b.AppendLine($"وزن کل: {Num(s.Weight)} g | عیار میانگین: {Num(s.AverageAssay)} | تعداد: {s.Count}");
            b.AppendLine($"وزن پس از بار: {Num(lower.TotalAfterAlloy)} g");
            b.AppendLine($"شمش عیار بالا مورد نیاز: {Num(raise.RequiredHighBar)} g");
            b.AppendLine($"بار ریخته‌گری مورد نیاز: {Num(lower.TotalAlloyRequired)} g | نقره: {Num(lower.SilverRequired)} g | بار بدون نقره: {Num(lower.NonSilverRequired)} g");
            var split = GoldCalculator.Split3679(Parse(_splitBase.Text, 800));
            b.AppendLine($"محاسبه سریع: 36.79% = {Num(split)} | 63.21% = {Num(Parse(_splitBase.Text, 800) - split)}");
            b.AppendLine($"اصلاح افت عیار: بار افزوده {_corrAdd.Text} g | جمع وزن {_corrTotal.Text} g");

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
        _grid.ColumnHeadersHeight = 44;
        _grid.DefaultCellStyle.BackColor = Card;
        _grid.DefaultCellStyle.ForeColor = TextMain;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(62, 51, 27);
        _grid.DefaultCellStyle.SelectionForeColor = TextMain;
        _grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.RowTemplate.Height = 44;
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
            else if (name == "Delete") DeleteEntry(e.RowIndex);
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

    private static string Num(double v) => !double.IsFinite(v)
        ? "—"
        : (Math.Abs(v) < 1e-7 ? 0 : v).ToString("0.###", CultureInfo.InvariantCulture);

    private static void OpenInstagram()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://www.instagram.com/4mirnourhan/") { UseShellExecute = true });
        }
        catch { }
    }
}

public class RoundedPanel : Panel
{
    public int Radius { get; set; } = 16;
    public Color BorderColor { get; set; } = Color.Transparent;

    public RoundedPanel()
    {
        DoubleBuffered = true;
        Resize += (_, _) => UpdateRegion();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var rect = ClientRectangle;
        rect.Width -= 1;
        rect.Height -= 1;
        if (rect.Width <= 1 || rect.Height <= 1) return;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var path = RoundPath(rect, Radius);
        using var pen = new Pen(BorderColor, 1f);
        e.Graphics.DrawPath(pen, path);
    }

    private void UpdateRegion()
    {
        var rect = ClientRectangle;
        if (rect.Width <= 2 || rect.Height <= 2) return;
        using var path = RoundPath(rect, Radius);
        Region?.Dispose();
        Region = new Region(path);
    }

    internal static System.Drawing.Drawing2D.GraphicsPath RoundPath(Rectangle rect, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        var d = Math.Max(2, radius * 2);
        path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public sealed class RoundButton : Button
{
    public int Radius { get; set; } = 12;

    public RoundButton()
    {
        Resize += (_, _) => UpdateRegion();
    }

    private void UpdateRegion()
    {
        var rect = ClientRectangle;
        if (rect.Width <= 2 || rect.Height <= 2) return;
        using var path = RoundedPanel.RoundPath(rect, Radius);
        Region?.Dispose();
        Region = new Region(path);
    }
}
