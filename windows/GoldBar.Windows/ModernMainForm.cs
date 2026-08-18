using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.Json;
using GoldBar.Core;

namespace GoldBar.Windows;

public sealed class ModernMainForm : Form
{
    private static readonly Color Bg = Color.FromArgb(6, 8, 12);
    private static readonly Color Sidebar = Color.FromArgb(10, 13, 18);
    private static readonly Color Card = Color.FromArgb(16, 20, 27);
    private static readonly Color Card2 = Color.FromArgb(22, 27, 36);
    private static readonly Color Border = Color.FromArgb(47, 54, 68);
    private static readonly Color Gold = Color.FromArgb(247, 211, 112);
    private static readonly Color GoldDark = Color.FromArgb(184, 132, 29);
    private static readonly Color TextMain = Color.FromArgb(246, 244, 237);
    private static readonly Color Muted = Color.FromArgb(150, 159, 176);
    private static readonly Color Success = Color.FromArgb(83, 218, 138);
    private static readonly Color Danger = Color.FromArgb(255, 106, 106);

    private readonly ScaleReader _scale = new();
    private AppSettings _settings = AppSettings.Load();
    private readonly List<GoldEntry> _entries = new();
    private int _editingIndex = -1;

    private readonly Panel _workspace = new() { Dock = DockStyle.Fill, BackColor = Bg };
    private readonly Label _pageTitle = L("داشبورد", 22, TextMain, true);
    private readonly Label _pageSubtitle = L("نمای کلی وزن، عیار و عملیات پرکاربرد", 9.6f, Muted, false);
    private readonly Label _scaleHeader = L("ترازو • آماده", 9.3f, Muted, true);
    private readonly Label _liveScaleWeight = L("— g", 29, Gold, true);
    private readonly Label _sidebarScaleWeight = L("— g", 22, TextMain, true);
    private readonly Dictionary<string, RoundButton> _nav = new();

    private readonly Label _totalWeight = MetricValue();
    private readonly Label _avgAssay = MetricValue();
    private readonly Label _count = MetricValue();
    private readonly Label _totalAlloyTop = MetricValue();
    private readonly Label _afterAlloy = MetricValue(16);

    private readonly TextBox _weight = Input();
    private readonly TextBox _assay = Input();
    private readonly RoundButton _saveEntry = Primary("ثبت آبشده");
    private readonly Label _entryScaleHint = L("↑  دریافت وزن از ترازو", 9.2f, Muted, true);
    private readonly DataGridView _grid = new();
    private FlowLayoutPanel? _recentHost;

    private readonly TextBox _raiseTarget = Input("747");
    private readonly TextBox _barAssay = Input("995");
    private readonly Label _raiseDiff = MetricValue(17);
    private readonly Label _raiseNeed = MetricValue(17);
    private readonly Label _raiseState = StatusLabel();

    private readonly TextBox _lowerTarget = Input("746");
    private readonly TextBox _silver = Input("32");
    private readonly Label _alloy = MetricValue(17);
    private readonly Label _silverNeed = MetricValue(17);
    private readonly Label _otherAlloy = MetricValue(17);
    private readonly Label _lowerAfter = MetricValue(17);
    private readonly Label _lowerState = StatusLabel();

    private readonly TextBox _splitBase = Input("800");
    private readonly Label _splitA = MetricValue(21);
    private readonly Label _splitB = MetricValue(21);
    private readonly TextBox _corrWeight = Input("250");
    private readonly TextBox _corrTarget = Input("750");
    private readonly TextBox _corrDrop = Input("1");
    private readonly Label _corrAdd = MetricValue(19);
    private readonly Label _corrTotal = MetricValue(19);

    private SplitContainer? _dashboardMainSplit;
    private SplitContainer? _dashboardTopSplit;
    private SplitContainer? _dashboardBottomLeft;
    private SplitContainer? _dashboardBottomRight;

    // Integrated settings drawer (the mockup shows settings inside the same shell).
    private readonly RoundedPanel _settingsDrawer = new();
    private readonly FlowLayoutPanel _settingsScroll = new();
    private readonly TextBox _setReport = DrawerInput();
    private readonly ComboBox _setModel = DrawerCombo();
    private readonly ComboBox _setPort = DrawerCombo();
    private readonly ComboBox _setBaud = DrawerCombo();
    private readonly ComboBox _setData = DrawerCombo();
    private readonly ComboBox _setParity = DrawerCombo();
    private readonly ComboBox _setStop = DrawerCombo();
    private readonly ComboBox _setFlow = DrawerCombo();
    private readonly CheckBox _setAuto = DrawerCheck("خواندن خودکار پایدار");
    private readonly CheckBox _setUp = DrawerCheck("پاسخ‌دهی ترازو با کلید ↑");
    private readonly CheckBox _setPrint = DrawerCheck("دریافت با PRINT ترازو");
    private readonly CheckBox _setQueryOnUp = DrawerCheck("هنگام ↑ فرمان درخواست وزن ارسال شود");
    private readonly NumericUpDown _setStableSamples = DrawerNumber(2, 10, 3);
    private readonly NumericUpDown _setTolerance = DrawerDecimal(0.001m, 5m, 0.02m, 3);
    private readonly TextBox _setQuery = DrawerInput();
    private readonly ComboBox _setEnding = DrawerCombo();
    private readonly NumericUpDown _setTimeout = DrawerNumber(500, 10000, 1800);
    private readonly Label _settingsTestStatus = L("آماده تست", 9, Muted, true);

    private static string DataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GoldBar", "entries.json");

    public ModernMainForm()
    {
        Text = "GOLD BAR (by:Amirnourhan)";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1540, 940);
        MinimumSize = new Size(1180, 760);
        BackColor = Bg;
        ForeColor = TextMain;
        Font = new Font("Segoe UI", 10f);
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        RightToLeft = RightToLeft.No;
        RightToLeftLayout = false;
        DoubleBuffered = true;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        BuildShell();
        BuildSettingsDrawer();
        ConfigureGrid();
        BindEvents();
        LoadEntries();
        ApplyScaleSettings();
        ShowPage("dashboard");
        RefreshAll();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveSplitterPreferences();
        try { _settings.Save(); } catch { }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _scale.Dispose();
        base.OnFormClosed(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        PositionSettingsDrawer();
    }

    public void ShowPageForTest(string page)
    {
        if (page.Equals("settings", StringComparison.OrdinalIgnoreCase)) OpenSettingsDrawer();
        else ShowPage(page);
    }

    private void BuildShell()
    {
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Bg,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 264));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.Controls.Add(BuildSidebar(), 0, 0);
        shell.Controls.Add(BuildMainArea(), 1, 0);
        Controls.Add(shell);
    }

    private Control BuildSidebar()
    {
        var side = new Panel { Dock = DockStyle.Fill, BackColor = Sidebar, Padding = new Padding(16, 18, 16, 16) };

        var brand = new RoundedPanel
        {
            Dock = DockStyle.Top,
            Height = 214,
            BackColor = Card,
            BorderColor = Border,
            Radius = 20,
            Padding = new Padding(16)
        };
        var brandLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Card };
        brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        brandLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var picture = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Card, Margin = new Padding(20, 2, 20, 4) };
        try
        {
            var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (icon is not null) picture.Image = icon.ToBitmap();
        }
        catch { }
        var brandTitle = L("GOLD BAR", 20, Gold, true);
        brandTitle.Dock = DockStyle.Fill;
        brandTitle.TextAlign = ContentAlignment.MiddleCenter;
        brandTitle.RightToLeft = RightToLeft.No;
        var by = new LinkLabel
        {
            Text = "by: Amirnourhan",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            RightToLeft = RightToLeft.No,
            LinkColor = TextMain,
            ActiveLinkColor = Gold,
            VisitedLinkColor = TextMain,
            Font = new Font("Segoe UI", 9.7f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        by.LinkClicked += (_, _) => OpenInstagram();
        var edition = L("Windows Desktop", 8.2f, Muted, false);
        edition.Dock = DockStyle.Fill;
        edition.TextAlign = ContentAlignment.MiddleCenter;
        edition.RightToLeft = RightToLeft.No;
        brandLayout.Controls.Add(picture, 0, 0);
        brandLayout.Controls.Add(brandTitle, 0, 1);
        brandLayout.Controls.Add(by, 0, 2);
        brandLayout.Controls.Add(edition, 0, 3);
        brand.Controls.Add(brandLayout);
        side.Controls.Add(brand);

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 358,
            Top = 230,
            BackColor = Sidebar,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 18, 0, 0)
        };
        AddNav(nav, "dashboard", "داشبورد", "▦");
        AddNav(nav, "entries", "آبشده‌ها", "◆");
        AddNav(nav, "calculations", "محاسبات عیار", "∑");
        AddNav(nav, "quick", "محاسبه سریع", "⚡");
        AddNav(nav, "reports", "گزارش", "▤");
        AddNav(nav, "settings", "تنظیمات", "⚙");
        side.Controls.Add(nav);

        var scaleMini = new RoundedPanel
        {
            Dock = DockStyle.Bottom,
            Height = 150,
            Radius = 16,
            BackColor = Card,
            BorderColor = Border,
            Padding = new Padding(14),
            Margin = new Padding(0, 0, 0, 12)
        };
        var scaleTitle = L("ترازو", 10.2f, Gold, true);
        scaleTitle.Dock = DockStyle.Top;
        scaleTitle.Height = 28;
        _sidebarScaleWeight.Dock = DockStyle.Top;
        _sidebarScaleWeight.Height = 50;
        _sidebarScaleWeight.TextAlign = ContentAlignment.MiddleCenter;
        _sidebarScaleWeight.RightToLeft = RightToLeft.No;
        var scaleOpen = Secondary("دریافت وزن ↑");
        scaleOpen.Dock = DockStyle.Bottom;
        scaleOpen.Height = 40;
        scaleOpen.Click += async (_, _) => await ReadScaleIntoWeightAsync();
        scaleMini.Controls.Add(scaleOpen);
        scaleMini.Controls.Add(_sidebarScaleWeight);
        scaleMini.Controls.Add(scaleTitle);
        side.Controls.Add(scaleMini);

        var footer = L("GOLD BAR • v1.5.0", 8.2f, Muted, false);
        footer.Dock = DockStyle.Bottom;
        footer.Height = 28;
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
            Width = 226,
            Height = 48,
            Radius = 13,
            FlatStyle = FlatStyle.Flat,
            BackColor = Sidebar,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 10.3f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(10, 0, 15, 0),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 7),
            RightToLeft = RightToLeft.Yes
        };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (_, _) => key == "settings" ? OpenSettingsDrawer() : ShowPage(key);
        _nav[key] = b;
        host.Controls.Add(b);
    }

    private Control BuildMainArea()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Bg };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildTopbar(), 0, 0);
        root.Controls.Add(_workspace, 0, 1);
        return root;
    }

    private Control BuildTopbar()
    {
        var bar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Bg, Padding = new Padding(24, 13, 24, 9) };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var scaleChip = new RoundedPanel { Dock = DockStyle.Fill, Radius = 14, BackColor = Card, BorderColor = Border, Padding = new Padding(12), Margin = new Padding(0, 7, 12, 7) };
        _scaleHeader.Dock = DockStyle.Fill;
        _scaleHeader.TextAlign = ContentAlignment.MiddleCenter;
        scaleChip.Controls.Add(_scaleHeader);

        var titles = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Bg };
        titles.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        titles.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        _pageTitle.Dock = DockStyle.Fill;
        _pageTitle.TextAlign = ContentAlignment.MiddleRight;
        _pageSubtitle.Dock = DockStyle.Fill;
        _pageSubtitle.TextAlign = ContentAlignment.MiddleRight;
        titles.Controls.Add(_pageTitle, 0, 0);
        titles.Controls.Add(_pageSubtitle, 0, 1);
        bar.Controls.Add(scaleChip, 0, 0);
        bar.Controls.Add(titles, 1, 0);
        return bar;
    }

    private void ShowPage(string key)
    {
        HideSettingsDrawer();
        foreach (var p in _nav)
        {
            var active = p.Key == key;
            p.Value.BackColor = active ? Card2 : Sidebar;
            p.Value.ForeColor = active ? Gold : Muted;
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
                key = "dashboard";
                _pageTitle.Text = "داشبورد";
                _pageSubtitle.Text = "نمای کلی وزن، عیار و عملیات پرکاربرد";
                page = BuildDashboardPage();
                break;
        }
        page.Dock = DockStyle.Fill;
        _workspace.Controls.Add(page);
        _workspace.ResumeLayout(true);
        RefreshAll();
    }

    private Control BuildDashboardPage()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Bg, Padding = new Padding(22, 7, 22, 20) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildMetricsStrip(), 0, 0);

        _dashboardMainSplit = Split(Orientation.Horizontal, 250, 250);
        _dashboardTopSplit = Split(Orientation.Vertical, 470, 270);
        _dashboardBottomLeft = Split(Orientation.Vertical, 260, 480);
        _dashboardBottomRight = Split(Orientation.Vertical, 300, 300);

        _dashboardTopSplit.Panel1.Controls.Add(BuildEntryCard());
        _dashboardTopSplit.Panel2.Controls.Add(BuildScaleCard());
        _dashboardMainSplit.Panel1.Controls.Add(_dashboardTopSplit);

        _dashboardBottomLeft.Panel1.Controls.Add(BuildRaiseCard());
        _dashboardBottomLeft.Panel2.Controls.Add(_dashboardBottomRight);
        _dashboardBottomRight.Panel1.Controls.Add(BuildLowerCard());
        _dashboardBottomRight.Panel2.Controls.Add(BuildRecentCard());
        _dashboardMainSplit.Panel2.Controls.Add(_dashboardBottomLeft);

        root.Controls.Add(_dashboardMainSplit, 0, 1);
        BeginInvoke((Action)ApplySavedSplitters);
        return root;
    }

    private Control BuildEntriesPage()
    {
        var split = Split(Orientation.Horizontal, 250, 260);
        split.Padding = new Padding(22, 8, 22, 20);
        split.Panel1.Controls.Add(BuildEntryCard());
        split.Panel2.Controls.Add(BuildGridCard());
        return split;
    }

    private Control BuildCalculationsPage()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Bg, Padding = new Padding(22, 7, 22, 20) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildMetricsStrip(), 0, 0);
        var split = Split(Orientation.Vertical, 400, 400);
        split.Panel1.Controls.Add(BuildRaiseCard());
        split.Panel2.Controls.Add(BuildLowerCard());
        root.Controls.Add(split, 0, 1);
        return root;
    }

    private Control BuildQuickPage()
    {
        var root = new Panel { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(22, 8, 22, 20) };
        var split = Split(Orientation.Vertical, 400, 400);
        split.Panel1.Controls.Add(BuildSplitCard());
        split.Panel2.Controls.Add(BuildCorrectionCard());
        root.Controls.Add(split);
        return root;
    }

    private Control BuildReportsPage()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(60) };
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, BackColor = Card, Padding = new Padding(8) };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var t = L("مسیر ذخیره فعلی", 10, Muted, true); t.Dock = DockStyle.Fill;
        var p = L(string.IsNullOrWhiteSpace(_settings.ReportFolder) ? "تعیین نشده" : _settings.ReportFolder, 11.2f, TextMain, true); p.Dock = DockStyle.Fill; p.AutoEllipsis = true;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = Card };
        var save = Primary("ذخیره گزارش"); save.Width = 190; save.Click += (_, _) => SaveReport();
        var settings = Secondary("تغییر مسیر"); settings.Width = 150; settings.Click += (_, _) => OpenSettingsDrawer();
        buttons.Controls.Add(save); buttons.Controls.Add(settings);
        var note = L("گزارش یک فایل متنی تاریخ‌دار است و فقط داده‌ها و نتیجه‌های نهایی را ثبت می‌کند.", 9.4f, Muted, false); note.Dock = DockStyle.Fill;
        body.Controls.Add(t, 0, 0); body.Controls.Add(p, 0, 1); body.Controls.Add(buttons, 0, 2); body.Controls.Add(note, 0, 3);
        var card = CardWithHeader("گزارش کامل", "ذخیره مستقیم در مسیر انتخاب‌شده", body);
        card.Dock = DockStyle.Fill;
        page.Controls.Add(card);
        return page;
    }

    private Control BuildMetricsStrip()
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Bg };
        for (var i = 0; i < 4; i++) row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        row.Controls.Add(MetricCard("وزن کل", _totalWeight, "g"), 0, 0);
        row.Controls.Add(MetricCard("عیار میانگین", _avgAssay, "‰"), 1, 0);
        row.Controls.Add(MetricCard("تعداد آبشده", _count, "ردیف"), 2, 0);
        row.Controls.Add(MetricCard("کل بار مورد نیاز", _totalAlloyTop, "g"), 3, 0);
        return row;
    }

    private Control BuildEntryCard()
    {
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, BackColor = Card };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var fields = TwoColumns(50, 50, Card);
        fields.Controls.Add(Field("وزن آبشده (g)", _weight), 0, 0);
        fields.Controls.Add(Field("عیار آبشده", _assay), 1, 0);
        body.Controls.Add(fields, 0, 0);
        _entryScaleHint.Dock = DockStyle.Fill;
        _entryScaleHint.TextAlign = ContentAlignment.MiddleRight;
        body.Controls.Add(_entryScaleHint, 0, 1);

        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = Card };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21));
        _saveEntry.Dock = DockStyle.Fill;
        var reset = Secondary("ثبت جدید"); reset.Dock = DockStyle.Fill; reset.Click += (_, _) => ResetEntryForm();
        var clear = Secondary("پاک کردن"); clear.Dock = DockStyle.Fill; clear.ForeColor = Danger; clear.Click += (_, _) => ClearAll();
        actions.Controls.Add(_saveEntry, 0, 0); actions.Controls.Add(reset, 1, 0); actions.Controls.Add(clear, 2, 0);
        body.Controls.Add(actions, 0, 2);
        var tip = L("وزن را دستی بنویس یا وقتی فیلد وزن فعال است کلید ↑ را بزن.", 8.8f, Muted, false); tip.Dock = DockStyle.Fill; tip.TextAlign = ContentAlignment.TopRight;
        body.Controls.Add(tip, 0, 3);
        return CardWithHeader("ثبت سریع آبشده", "ثبت سریع و پیوسته بدون خروج دکمه‌ها از کادر", body);
    }

    private Control BuildScaleCard()
    {
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1, BackColor = Card };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _liveScaleWeight.Dock = DockStyle.Fill; _liveScaleWeight.TextAlign = ContentAlignment.MiddleCenter; _liveScaleWeight.RightToLeft = RightToLeft.No;
        var cfg = L($"{_settings.PortName}  •  {_settings.BaudRate} baud", 9, Muted, false); cfg.Dock = DockStyle.Fill; cfg.TextAlign = ContentAlignment.MiddleCenter; cfg.RightToLeft = RightToLeft.No;
        var read = Primary("دریافت وزن با ↑"); read.Dock = DockStyle.Fill; read.Click += async (_, _) => await ReadScaleIntoWeightAsync();
        var settings = Secondary("تنظیمات ترازو"); settings.Dock = DockStyle.Fill; settings.Click += (_, _) => OpenSettingsDrawer();
        var note = L(_settings.AutoRead ? "Auto Read: روشن • فقط وزن پایدار" : "Auto Read: خاموش • خواندن با کلید ↑", 8.7f, _settings.AutoRead ? Success : Muted, true); note.Dock = DockStyle.Fill; note.TextAlign = ContentAlignment.MiddleCenter;
        body.Controls.Add(_liveScaleWeight, 0, 0); body.Controls.Add(cfg, 0, 1); body.Controls.Add(read, 0, 2); body.Controls.Add(settings, 0, 3); body.Controls.Add(note, 0, 4);
        return CardWithHeader("ترازو", "RS-232 / COM • سریع و بدون قفل کردن رابط", body);
    }

    private Control BuildRaiseCard()
    {
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = Card };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 76)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 88)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var fields = TwoColumns(50, 50, Card); fields.Controls.Add(Field("عیار هدف", _raiseTarget), 0, 0); fields.Controls.Add(Field("عیار شمش", _barAssay), 1, 0);
        var metrics = TwoColumns(50, 50, Card); metrics.Controls.Add(MiniMetric("اختلاف تا هدف", _raiseDiff), 0, 0); metrics.Controls.Add(MiniMetric("شمش مورد نیاز (g)", _raiseNeed), 1, 0);
        _raiseState.Dock = DockStyle.Fill;
        body.Controls.Add(fields, 0, 0); body.Controls.Add(metrics, 0, 1); body.Controls.Add(_raiseState, 0, 2);
        return CardWithHeader("بالا بردن عیار", "افزایش عیار با شمش عیار بالا", body);
    }

    private Control BuildLowerCard()
    {
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, BackColor = Card };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 76)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 76)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 76)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var fields = TwoColumns(50, 50, Card); fields.Controls.Add(Field("عیار هدف", _lowerTarget), 0, 0); fields.Controls.Add(Field("درصد نقره", _silver), 1, 0);
        var m1 = TwoColumns(50, 50, Card); m1.Controls.Add(MiniMetric("کل بار (g)", _alloy), 0, 0); m1.Controls.Add(MiniMetric("نقره (g)", _silverNeed), 1, 0);
        var m2 = TwoColumns(50, 50, Card); m2.Controls.Add(MiniMetric("بار بدون نقره (g)", _otherAlloy), 0, 0); m2.Controls.Add(MiniMetric("وزن پس از بار (g)", _lowerAfter), 1, 0);
        _lowerState.Dock = DockStyle.Fill;
        body.Controls.Add(fields, 0, 0); body.Controls.Add(m1, 0, 1); body.Controls.Add(m2, 0, 2); body.Controls.Add(_lowerState, 0, 3);
        return CardWithHeader("پایین آوردن عیار", "محاسبه بار ریخته‌گری مورد نیاز", body);
    }

    private Control BuildRecentCard()
    {
        _recentHost = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Card, Padding = new Padding(0, 4, 0, 0) };
        return CardWithHeader("آخرین آبشده‌ها", "آخرین ثبت‌های انجام‌شده", _recentHost);
    }

    private Control BuildSplitCard()
    {
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Card };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.Controls.Add(Field("عدد پایه", _splitBase), 0, 0);
        var m = TwoColumns(50, 50, Card); m.Controls.Add(MiniMetric("۳۶.۷۹٪", _splitA), 0, 0); m.Controls.Add(MiniMetric("۶۳.۲۱٪", _splitB), 1, 0); body.Controls.Add(m, 0, 1);
        return CardWithHeader("تقسیم ۳۶.۷۹٪ / ۶۳.۲۱٪", "محاسبه سریع", body);
    }

    private Control BuildCorrectionCard()
    {
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = Card };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 92)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 92)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var top = TwoColumns(50, 50, Card); top.Controls.Add(Field("وزن پایه", _corrWeight), 0, 0); top.Controls.Add(Field("عیار هدف", _corrTarget), 1, 0);
        body.Controls.Add(top, 0, 0); body.Controls.Add(Field("مقدار افت عیار", _corrDrop), 0, 1);
        var m = TwoColumns(50, 50, Card); m.Controls.Add(MiniMetric("بار افزوده (g)", _corrAdd), 0, 0); m.Controls.Add(MiniMetric("جمع وزن (g)", _corrTotal), 1, 0); body.Controls.Add(m, 0, 2);
        return CardWithHeader("اصلاح وزن برای افت عیار", "محاسبه سریع", body);
    }

    private Control BuildGridCard()
    {
        _grid.Dock = DockStyle.Fill;
        return CardWithHeader("لیست آبشده‌ها", "ویرایش و حذف مستقیم", _grid);
    }

    private static SplitContainer Split(Orientation orientation, int min1, int min2)
    {
        var s = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = orientation,
            SplitterWidth = 7,
            BackColor = Border,
            BorderStyle = BorderStyle.None,
            Panel1MinSize = min1,
            Panel2MinSize = min2,
            IsSplitterFixed = false
        };
        s.Panel1.BackColor = Bg;
        s.Panel2.BackColor = Bg;
        s.Panel1.Padding = new Padding(0);
        s.Panel2.Padding = new Padding(0);
        return s;
    }

    private void ApplySavedSplitters()
    {
        ApplyPercent(_dashboardMainSplit, _settings.DashboardUpperPercent);
        ApplyPercent(_dashboardTopSplit, _settings.DashboardEntryPercent);
        ApplyPercent(_dashboardBottomLeft, _settings.DashboardRaisePercent);
        ApplyPercent(_dashboardBottomRight, _settings.DashboardLowerPercent);
    }

    private static void ApplyPercent(SplitContainer? s, int percent)
    {
        if (s is null || s.IsDisposed) return;
        try
        {
            var total = s.Orientation == Orientation.Vertical ? s.ClientSize.Width : s.ClientSize.Height;
            var max = total - s.SplitterWidth;
            if (max <= 0) return;
            var distance = (int)(max * Math.Clamp(percent, 10, 90) / 100.0);
            distance = Math.Clamp(distance, s.Panel1MinSize, Math.Max(s.Panel1MinSize, max - s.Panel2MinSize));
            s.SplitterDistance = distance;
        }
        catch { }
    }

    private void SaveSplitterPreferences()
    {
        _settings.DashboardUpperPercent = SplitPercent(_dashboardMainSplit, _settings.DashboardUpperPercent);
        _settings.DashboardEntryPercent = SplitPercent(_dashboardTopSplit, _settings.DashboardEntryPercent);
        _settings.DashboardRaisePercent = SplitPercent(_dashboardBottomLeft, _settings.DashboardRaisePercent);
        _settings.DashboardLowerPercent = SplitPercent(_dashboardBottomRight, _settings.DashboardLowerPercent);
    }

    private static int SplitPercent(SplitContainer? s, int fallback)
    {
        if (s is null || s.IsDisposed) return fallback;
        var total = s.Orientation == Orientation.Vertical ? s.ClientSize.Width : s.ClientSize.Height;
        var max = total - s.SplitterWidth;
        if (max <= 0) return fallback;
        return Math.Clamp((int)Math.Round(s.SplitterDistance * 100.0 / max), 10, 90);
    }

    private void BuildSettingsDrawer()
    {
        _settingsDrawer.Visible = false;
        _settingsDrawer.Width = 438;
        _settingsDrawer.BackColor = Card;
        _settingsDrawer.BorderColor = GoldDark;
        _settingsDrawer.Radius = 20;
        _settingsDrawer.Padding = new Padding(18);
        _settingsDrawer.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;

        var header = new TableLayoutPanel { Dock = DockStyle.Top, Height = 58, ColumnCount = 2, BackColor = Card };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
        var title = L("⚙  تنظیمات ترازو", 16, TextMain, true); title.Dock = DockStyle.Fill; title.TextAlign = ContentAlignment.MiddleRight;
        var close = Secondary("×"); close.Dock = DockStyle.Fill; close.Font = new Font("Segoe UI", 16, FontStyle.Bold); close.Click += (_, _) => HideSettingsDrawer();
        header.Controls.Add(title, 0, 0); header.Controls.Add(close, 1, 0);

        _settingsScroll.Dock = DockStyle.Fill;
        _settingsScroll.AutoScroll = true;
        _settingsScroll.FlowDirection = FlowDirection.TopDown;
        _settingsScroll.WrapContents = false;
        _settingsScroll.BackColor = Card;
        _settingsScroll.Padding = new Padding(0, 10, 4, 10);

        _settingsScroll.Controls.Add(DrawerSection("گزارش", DrawerReportSection()));
        _settingsScroll.Controls.Add(DrawerSection("اتصال RS-232", DrawerConnectionSection()));
        _settingsScroll.Controls.Add(DrawerSection("خواندن وزن", DrawerReadSection()));
        _settingsScroll.Controls.Add(DrawerSection("فرمان درخواست وزن", DrawerQuerySection()));

        var testBox = new Panel { Height = 94, BackColor = Card, Margin = new Padding(0, 0, 0, 8) };
        _settingsTestStatus.Dock = DockStyle.Top; _settingsTestStatus.Height = 34;
        var test = Secondary("تست دریافت وزن"); test.Dock = DockStyle.Bottom; test.Height = 46; test.Click += async (_, _) => await TestScaleAsync(test);
        testBox.Controls.Add(test); testBox.Controls.Add(_settingsTestStatus);
        _settingsScroll.Controls.Add(testBox);

        var footer = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 58, ColumnCount = 2, BackColor = Card };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        var reset = Secondary("بازنشانی"); reset.Dock = DockStyle.Fill; reset.Click += (_, _) => LoadDrawerValues(new AppSettings());
        var save = Primary("ذخیره تنظیمات"); save.Dock = DockStyle.Fill; save.Click += (_, _) => SaveSettingsDrawer();
        footer.Controls.Add(reset, 0, 0); footer.Controls.Add(save, 1, 0);

        _settingsDrawer.Controls.Add(_settingsScroll);
        _settingsDrawer.Controls.Add(footer);
        _settingsDrawer.Controls.Add(header);
        Controls.Add(_settingsDrawer);
        _settingsDrawer.BringToFront();
        _settingsScroll.SizeChanged += (_, _) => ResizeDrawerSections();
        PopulateDrawerOptions();
        PositionSettingsDrawer();
    }

    private Control DrawerReportSection()
    {
        var host = new TableLayoutPanel { Dock = DockStyle.Top, Height = 96, RowCount = 2, ColumnCount = 1, BackColor = Card2 };
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); host.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        _setReport.Dock = DockStyle.Fill;
        var browse = Secondary("انتخاب پوشه گزارش"); browse.Dock = DockStyle.Fill;
        browse.Click += (_, _) =>
        {
            using var d = new FolderBrowserDialog { Description = "پوشه گزارش‌های Gold Bar را انتخاب کن", UseDescriptionForTitle = true };
            if (Directory.Exists(_setReport.Text)) d.SelectedPath = _setReport.Text;
            if (d.ShowDialog(this) == DialogResult.OK) _setReport.Text = d.SelectedPath;
        };
        host.Controls.Add(_setReport, 0, 0); host.Controls.Add(browse, 0, 1);
        return host;
    }

    private Control DrawerConnectionSection()
    {
        var grid = DrawerGrid();
        AddDrawerField(grid, "مدل ترازو", _setModel); AddDrawerField(grid, "COM Port", _setPort);
        AddDrawerField(grid, "Baud Rate", _setBaud); AddDrawerField(grid, "Data Bits", _setData);
        AddDrawerField(grid, "Parity", _setParity); AddDrawerField(grid, "Stop Bits", _setStop);
        AddDrawerField(grid, "Flow Control", _setFlow);
        return grid;
    }

    private Control DrawerReadSection()
    {
        var host = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Card2 };
        host.Controls.Add(_setAuto); host.Controls.Add(_setUp); host.Controls.Add(_setPrint); host.Controls.Add(_setQueryOnUp);
        var grid = DrawerGrid(); AddDrawerField(grid, "تعداد قرائت پایدار", _setStableSamples); AddDrawerField(grid, "حداکثر نوسان (g)", _setTolerance);
        host.Controls.Add(grid);
        return host;
    }

    private Control DrawerQuerySection()
    {
        var grid = DrawerGrid();
        AddDrawerField(grid, "فرمان", _setQuery); AddDrawerField(grid, "پایان فرمان", _setEnding); AddDrawerField(grid, "مهلت دریافت (ms)", _setTimeout);
        return grid;
    }

    private static TableLayoutPanel DrawerGrid()
    {
        var g = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, BackColor = Card2, RightToLeft = RightToLeft.Yes };
        g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        return g;
    }

    private static void AddDrawerField(TableLayoutPanel grid, string title, Control control)
    {
        var host = new TableLayoutPanel { Dock = DockStyle.Top, Height = 70, RowCount = 2, ColumnCount = 1, BackColor = Card2, Margin = new Padding(4) };
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); host.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        var l = L(title, 8.6f, Muted, false); l.Dock = DockStyle.Fill;
        control.Dock = DockStyle.Fill;
        host.Controls.Add(l, 0, 0); host.Controls.Add(control, 0, 1);
        grid.Controls.Add(host);
    }

    private static RoundedPanel DrawerSection(string title, Control body)
    {
        var p = new RoundedPanel { Width = 382, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Card2, BorderColor = Border, Radius = 15, Padding = new Padding(12), Margin = new Padding(0, 0, 0, 10) };
        var stack = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 2, BackColor = Card2 };
        var h = L(title, 11, Gold, true); h.Dock = DockStyle.Top; h.Height = 32;
        body.Dock = DockStyle.Top;
        stack.Controls.Add(h, 0, 0); stack.Controls.Add(body, 0, 1);
        p.Controls.Add(stack);
        return p;
    }

    private void ResizeDrawerSections()
    {
        var width = Math.Max(330, _settingsScroll.ClientSize.Width - 18);
        foreach (Control c in _settingsScroll.Controls) c.Width = width;
    }

    private void PositionSettingsDrawer()
    {
        if (_settingsDrawer.IsDisposed) return;
        var width = Math.Clamp((int)(ClientSize.Width * 0.31), 410, 500);
        _settingsDrawer.Width = width;
        _settingsDrawer.Height = Math.Max(500, ClientSize.Height - 22);
        _settingsDrawer.Location = new Point(Math.Max(8, ClientSize.Width - width - 12), 11);
    }

    private void OpenSettingsDrawer()
    {
        LoadDrawerValues(_settings);
        PositionSettingsDrawer();
        _settingsDrawer.Visible = true;
        _settingsDrawer.BringToFront();
        foreach (var p in _nav) { p.Value.BackColor = p.Key == "settings" ? Card2 : Sidebar; p.Value.ForeColor = p.Key == "settings" ? Gold : Muted; }
    }

    private void HideSettingsDrawer()
    {
        _settingsDrawer.Visible = false;
        if (_nav.ContainsKey("settings")) { _nav["settings"].BackColor = Sidebar; _nav["settings"].ForeColor = Muted; }
    }

    private void PopulateDrawerOptions()
    {
        _setModel.Items.AddRange(new object[] { "A&D", "Custom / Generic" });
        var ports = SerialPort.GetPortNames().OrderBy(x => x).Cast<object>().ToArray();
        _setPort.Items.AddRange(ports); if (!_setPort.Items.Contains("COM1")) _setPort.Items.Add("COM1");
        _setBaud.Items.AddRange(new object[] { "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200" });
        _setData.Items.AddRange(new object[] { "7", "8" });
        _setParity.Items.AddRange(Enum.GetNames<Parity>()); _setStop.Items.AddRange(new object[] { nameof(StopBits.One), nameof(StopBits.OnePointFive), nameof(StopBits.Two) });
        _setFlow.Items.AddRange(Enum.GetNames<Handshake>()); _setEnding.Items.AddRange(new object[] { "CRLF", "CR", "LF", "None" });
    }

    private void LoadDrawerValues(AppSettings s)
    {
        _setReport.Text = s.ReportFolder;
        SelectCombo(_setModel, s.ScaleModel); SelectCombo(_setPort, s.PortName); SelectCombo(_setBaud, s.BaudRate.ToString()); SelectCombo(_setData, s.DataBits.ToString());
        SelectCombo(_setParity, s.Parity); SelectCombo(_setStop, s.StopBits); SelectCombo(_setFlow, s.Handshake);
        _setAuto.Checked = s.AutoRead; _setUp.Checked = s.ReadOnUpArrow; _setPrint.Checked = s.ReceivePrintKey; _setQueryOnUp.Checked = s.SendQueryOnUpArrow;
        _setStableSamples.Value = Math.Clamp(s.StableSampleCount, (int)_setStableSamples.Minimum, (int)_setStableSamples.Maximum);
        _setTolerance.Value = Math.Clamp((decimal)s.StableToleranceGrams, _setTolerance.Minimum, _setTolerance.Maximum);
        _setQuery.Text = s.QueryCommand; SelectCombo(_setEnding, s.QueryLineEnding); _setTimeout.Value = Math.Clamp(s.ReadTimeoutMs, (int)_setTimeout.Minimum, (int)_setTimeout.Maximum);
        _settingsTestStatus.Text = "آماده تست"; _settingsTestStatus.ForeColor = Muted;
    }

    private AppSettings BuildDrawerSettings()
    {
        return new AppSettings
        {
            ReportFolder = _setReport.Text.Trim(),
            ScaleModel = string.IsNullOrWhiteSpace(_setModel.Text) ? "A&D" : _setModel.Text,
            PortName = string.IsNullOrWhiteSpace(_setPort.Text) ? "COM1" : _setPort.Text,
            BaudRate = int.TryParse(_setBaud.Text, out var baud) ? baud : 2400,
            DataBits = int.TryParse(_setData.Text, out var bits) ? bits : 7,
            Parity = string.IsNullOrWhiteSpace(_setParity.Text) ? nameof(Parity.Even) : _setParity.Text,
            StopBits = string.IsNullOrWhiteSpace(_setStop.Text) ? nameof(StopBits.Two) : _setStop.Text,
            Handshake = string.IsNullOrWhiteSpace(_setFlow.Text) ? nameof(Handshake.None) : _setFlow.Text,
            AutoRead = _setAuto.Checked,
            ReadOnUpArrow = _setUp.Checked,
            ReceivePrintKey = _setPrint.Checked,
            SendQueryOnUpArrow = _setQueryOnUp.Checked,
            StableAutoReadOnly = true,
            StableSampleCount = (int)_setStableSamples.Value,
            StableToleranceGrams = (double)_setTolerance.Value,
            QueryCommand = _setQuery.Text,
            QueryLineEnding = string.IsNullOrWhiteSpace(_setEnding.Text) ? "CRLF" : _setEnding.Text,
            ReadTimeoutMs = (int)_setTimeout.Value,
            CharactersBeforeDecimal = _settings.CharactersBeforeDecimal,
            CharactersAfterDecimal = _settings.CharactersAfterDecimal,
            MinimumAfterDecimal = _settings.MinimumAfterDecimal,
            DecimalSeparator = _settings.DecimalSeparator,
            ShowRawText = _settings.ShowRawText,
            DashboardUpperPercent = _settings.DashboardUpperPercent,
            DashboardEntryPercent = _settings.DashboardEntryPercent,
            DashboardRaisePercent = _settings.DashboardRaisePercent,
            DashboardLowerPercent = _settings.DashboardLowerPercent
        };
    }

    private void SaveSettingsDrawer()
    {
        try
        {
            SaveSplitterPreferences();
            var next = BuildDrawerSettings();
            next.Save();
            _settings = next;
            ApplyScaleSettings();
            HideSettingsDrawer();
            ShowPage("dashboard");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "ذخیره تنظیمات انجام نشد:\n" + ex.Message, "تنظیمات", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task TestScaleAsync(Button button)
    {
        button.Enabled = false;
        _settingsTestStatus.Text = "در حال اتصال…"; _settingsTestStatus.ForeColor = Gold;
        using var reader = new ScaleReader();
        try
        {
            var cfg = BuildDrawerSettings(); cfg.AutoRead = false;
            reader.ApplySettings(cfg, false);
            var w = await reader.ReadNowAsync();
            _settingsTestStatus.Text = "وزن دریافتی: " + Num(w) + " g"; _settingsTestStatus.ForeColor = Success;
        }
        catch (Exception ex)
        {
            _settingsTestStatus.Text = "خطا: " + ex.Message; _settingsTestStatus.ForeColor = Danger;
        }
        finally { button.Enabled = true; }
    }

    private void BindEvents()
    {
        _saveEntry.Click += (_, _) => SaveEntry();
        _weight.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Up && _settings.ReadOnUpArrow) { e.SuppressKeyPress = true; await ReadScaleIntoWeightAsync(); }
            else if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _assay.Focus(); _assay.SelectAll(); }
        };
        _assay.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SaveEntry(); } };
        foreach (var b in new[] { _raiseTarget, _barAssay, _lowerTarget, _silver, _splitBase, _corrWeight, _corrTarget, _corrDrop })
        {
            b.TextChanged += (_, _) => Recalculate(); b.Enter += (_, _) => b.SelectAll();
        }

        _scale.WeightReceived += value => Ui(() =>
        {
            _liveScaleWeight.Text = Num(value) + " g";
            _sidebarScaleWeight.Text = Num(value) + " g";
            _scaleHeader.Text = "●  ترازو متصل • " + Num(value) + " g"; _scaleHeader.ForeColor = Success;
            _entryScaleHint.Text = "●  وزن پایدار: " + Num(value) + " g"; _entryScaleHint.ForeColor = Success;
            if (_settings.AutoRead && _weight.Focused) { _weight.Text = Num(value); _weight.SelectAll(); }
        });
        _scale.StatusChanged += (text, ok) => Ui(() => { _scaleHeader.Text = ok ? "●  " + text : text; _scaleHeader.ForeColor = ok ? Success : Muted; });
    }

    private void ApplyScaleSettings()
    {
        _scale.ApplySettings(_settings, _settings.AutoRead);
        _scaleHeader.Text = _settings.AutoRead ? "ترازو • Auto Read پایدار" : "ترازو • " + _settings.PortName;
        _scaleHeader.ForeColor = _settings.AutoRead ? Gold : Muted;
        var last = _scale.LastWeight.HasValue ? Num(_scale.LastWeight.Value) + " g" : "— g";
        _liveScaleWeight.Text = last; _sidebarScaleWeight.Text = last;
    }

    private async Task ReadScaleIntoWeightAsync()
    {
        try
        {
            _entryScaleHint.Text = "●  در حال دریافت وزن…"; _entryScaleHint.ForeColor = Gold;
            var w = await _scale.ReadNowAsync();
            _weight.Text = Num(w); _weight.Focus(); _weight.SelectAll();
            _liveScaleWeight.Text = Num(w) + " g"; _sidebarScaleWeight.Text = Num(w) + " g";
            _entryScaleHint.Text = "●  وزن دریافتی: " + Num(w) + " g"; _entryScaleHint.ForeColor = Success;
        }
        catch (Exception ex)
        {
            _entryScaleHint.Text = "●  دریافت وزن ناموفق"; _entryScaleHint.ForeColor = Danger;
            MessageBox.Show(this, ex.Message + "\n\nتنظیمات COM و Baud Rate را بررسی کن.", "ترازو", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Ui(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(action); } catch { }
    }

    private void SaveEntry()
    {
        var w = Parse(_weight.Text, -1); var a = Parse(_assay.Text, -1);
        if (w <= 0 || a <= 0 || a > 1000) { MessageBox.Show(this, "وزن و عیار را صحیح وارد کن.", "ورودی نامعتبر", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        var item = new GoldEntry(w, a);
        if (_editingIndex >= 0 && _editingIndex < _entries.Count) _entries[_editingIndex] = item; else _entries.Add(item);
        _editingIndex = -1; _saveEntry.Text = "ثبت آبشده"; _weight.Clear(); _assay.Clear(); PersistEntries(); RefreshAll(); _weight.Focus();
    }

    private void ResetEntryForm()
    {
        _editingIndex = -1; _saveEntry.Text = "ثبت آبشده"; _weight.Clear(); _assay.Clear(); _weight.Focus();
    }

    private void EditEntry(int i)
    {
        if (i < 0 || i >= _entries.Count) return;
        ShowPage("entries"); _editingIndex = i; _weight.Text = Num(_entries[i].Weight); _assay.Text = Num(_entries[i].Assay); _saveEntry.Text = "ذخیره تغییرات"; _weight.Focus(); _weight.SelectAll();
    }

    private void DeleteEntry(int i)
    {
        if (i < 0 || i >= _entries.Count) return;
        _entries.RemoveAt(i); if (_editingIndex == i) _editingIndex = -1; else if (_editingIndex > i) _editingIndex--; PersistEntries(); RefreshAll();
    }

    private void ClearAll()
    {
        if (_entries.Count == 0) return;
        if (MessageBox.Show(this, "همه آبشده‌ها حذف شوند؟", "پاک کردن همه", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        _entries.Clear(); ResetEntryForm(); PersistEntries(); RefreshAll();
    }

    private void Recalculate()
    {
        var s = GoldCalculator.Summarize(_entries);
        _totalWeight.Text = Num(s.Weight); _avgAssay.Text = Num(s.AverageAssay); _count.Text = s.Count.ToString(CultureInfo.InvariantCulture);
        var raiseT = Parse(_raiseTarget.Text, 747); var high = Parse(_barAssay.Text, 995); var raise = GoldCalculator.RequiredHighAssayBar(s, raiseT, high);
        _raiseDiff.Text = Num(raise.DifferenceNeeded); _raiseNeed.Text = Num(raise.RequiredHighBar);
        _raiseState.Text = !double.IsFinite(raise.RequiredHighBar) ? "ابتدا آبشده معتبر ثبت کن." : raise.RequiredHighBar > 0 ? $"برای رسیدن به {Num(raiseT)}، {Num(raise.RequiredHighBar)} g شمش {Num(high)} نیاز است." : "افزایش عیار لازم نیست.";
        var lowerT = Parse(_lowerTarget.Text, 746); var silver = Parse(_silver.Text, 32); var lower = GoldCalculator.RequiredAlloy(s, lowerT, silver, s.Weight);
        _alloy.Text = Num(lower.TotalAlloyRequired); _totalAlloyTop.Text = Num(lower.TotalAlloyRequired); _silverNeed.Text = Num(lower.SilverRequired); _otherAlloy.Text = Num(lower.NonSilverRequired); _lowerAfter.Text = Num(lower.TotalAfterAlloy); _afterAlloy.Text = Num(lower.TotalAfterAlloy);
        _lowerState.Text = !double.IsFinite(lower.TotalAlloyRequired) ? "ابتدا آبشده معتبر ثبت کن." : lower.TotalAlloyRequired > 0 ? $"برای کاهش تا {Num(lowerT)}، {Num(lower.TotalAlloyRequired)} g بار نیاز است." : "کاهش عیار لازم نیست.";
        var baseV = Parse(_splitBase.Text, 800); var part = GoldCalculator.Split3679(baseV); _splitA.Text = Num(part); _splitB.Text = Num(baseV - part);
        var cw = Parse(_corrWeight.Text, 250); var ct = Parse(_corrTarget.Text, 750); var cd = Parse(_corrDrop.Text, 1); var add = GoldCalculator.CorrectionAddition(cw, ct, cd); _corrAdd.Text = Num(add); _corrTotal.Text = Num(cw + add);
    }

    private void RefreshAll()
    {
        Recalculate();
        if (_grid.Columns.Count > 0)
        {
            _grid.Rows.Clear();
            for (var i = 0; i < _entries.Count; i++) _grid.Rows.Add(i + 1, Num(_entries[i].Weight), Num(_entries[i].Assay), "ویرایش", "حذف");
        }
        RefreshRecent();
    }

    private void RefreshRecent()
    {
        if (_recentHost is null || _recentHost.IsDisposed) return;
        _recentHost.Controls.Clear();
        var recent = _entries.Select((e, i) => (e, i)).TakeLast(5).Reverse().ToList();
        if (recent.Count == 0)
        {
            var empty = L("هنوز آبشده‌ای ثبت نشده است.", 9.2f, Muted, false); empty.Width = Math.Max(250, _recentHost.ClientSize.Width - 20); empty.Height = 45; empty.TextAlign = ContentAlignment.MiddleCenter; _recentHost.Controls.Add(empty); return;
        }
        foreach (var x in recent)
        {
            var row = new RoundedPanel { Width = Math.Max(260, _recentHost.ClientSize.Width - 22), Height = 54, BackColor = Card2, BorderColor = Border, Radius = 12, Margin = new Padding(0, 0, 0, 7), Padding = new Padding(10) };
            var text = L($"وزن {Num(x.e.Weight)} g   •   عیار {Num(x.e.Assay)}", 9.2f, TextMain, true); text.Dock = DockStyle.Fill; row.Controls.Add(text); _recentHost.Controls.Add(row);
        }
    }

    private void SaveReport()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_settings.ReportFolder)) { OpenSettingsDrawer(); return; }
            if (!Directory.Exists(_settings.ReportFolder)) Directory.CreateDirectory(_settings.ReportFolder);
            var s = GoldCalculator.Summarize(_entries);
            var raise = GoldCalculator.RequiredHighAssayBar(s, Parse(_raiseTarget.Text, 747), Parse(_barAssay.Text, 995));
            var lower = GoldCalculator.RequiredAlloy(s, Parse(_lowerTarget.Text, 746), Parse(_silver.Text, 32), s.Weight);
            var b = new StringBuilder();
            b.AppendLine("GOLD BAR (by:Amirnourhan)"); b.AppendLine("تاریخ و ساعت: " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")); b.AppendLine();
            b.Append("آبشده‌ها: "); for (var i = 0; i < _entries.Count; i++) { if (i > 0) b.Append(" | "); b.Append($"{i + 1}) {Num(_entries[i].Weight)}g @ {Num(_entries[i].Assay)}"); } b.AppendLine();
            b.AppendLine($"وزن کل: {Num(s.Weight)} g | عیار میانگین: {Num(s.AverageAssay)} | تعداد: {s.Count} | وزن پس از بار: {Num(lower.TotalAfterAlloy)} g");
            b.AppendLine($"شمش عیار بالا: {Num(raise.RequiredHighBar)} g | کل بار مورد نیاز: {Num(lower.TotalAlloyRequired)} g | نقره: {Num(lower.SilverRequired)} g | بار بدون نقره: {Num(lower.NonSilverRequired)} g");
            b.AppendLine($"محاسبه سریع: 36.79%={_splitA.Text} | 63.21%={_splitB.Text} | اصلاح افت: {_corrAdd.Text} g | جمع وزن: {_corrTotal.Text} g");
            var path = Path.Combine(_settings.ReportFolder, "GoldBar_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt"); File.WriteAllText(path, b.ToString(), Encoding.UTF8);
            MessageBox.Show(this, "گزارش ذخیره شد:\n" + path, "گزارش", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, "ذخیره گزارش انجام نشد:\n" + ex.Message, "گزارش", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ConfigureGrid()
    {
        _grid.BackgroundColor = Card; _grid.BorderStyle = BorderStyle.None; _grid.GridColor = Border; _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Card2; _grid.ColumnHeadersDefaultCellStyle.ForeColor = TextMain; _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter; _grid.ColumnHeadersHeight = 44;
        _grid.DefaultCellStyle.BackColor = Card; _grid.DefaultCellStyle.ForeColor = TextMain; _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(61, 50, 27); _grid.DefaultCellStyle.SelectionForeColor = TextMain; _grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.RowTemplate.Height = 44; _grid.RowHeadersVisible = false; _grid.AllowUserToAddRows = false; _grid.AllowUserToDeleteRows = false; _grid.AllowUserToResizeRows = false; _grid.ReadOnly = true; _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _grid.RightToLeft = RightToLeft.Yes;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "No", HeaderText = "#", FillWeight = 10 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Weight", HeaderText = "وزن (g)", FillWeight = 28 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Assay", HeaderText = "عیار", FillWeight = 24 });
        _grid.Columns.Add(new DataGridViewButtonColumn { Name = "Edit", HeaderText = "", Text = "ویرایش", UseColumnTextForButtonValue = true, FillWeight = 19, FlatStyle = FlatStyle.Flat });
        _grid.Columns.Add(new DataGridViewButtonColumn { Name = "Delete", HeaderText = "", Text = "حذف", UseColumnTextForButtonValue = true, FillWeight = 19, FlatStyle = FlatStyle.Flat });
        _grid.CellContentClick += (_, e) => { if (e.RowIndex < 0) return; var name = _grid.Columns[e.ColumnIndex].Name; if (name == "Edit") EditEntry(e.RowIndex); else if (name == "Delete") DeleteEntry(e.RowIndex); };
    }

    private void LoadEntries()
    {
        try { if (!File.Exists(DataPath)) return; var loaded = JsonSerializer.Deserialize<List<GoldEntry>>(File.ReadAllText(DataPath)); if (loaded is not null) _entries.AddRange(loaded.Where(x => x.Weight > 0 && x.Assay > 0 && x.Assay <= 1000)); } catch { }
    }

    private void PersistEntries()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!); File.WriteAllText(DataPath, JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception ex) { MessageBox.Show(this, "ذخیره اطلاعات داخلی انجام نشد:\n" + ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private static Control CardWithHeader(string title, string subtitle, Control body)
    {
        var card = new RoundedPanel { Dock = DockStyle.Fill, Margin = new Padding(6), Padding = new Padding(15), Radius = 18, BackColor = Card, BorderColor = Border, MinimumSize = new Size(200, 180) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = Card };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var h = L(title, 13.5f, TextMain, true); h.Dock = DockStyle.Fill; var s = L(subtitle, 8.9f, Muted, false); s.Dock = DockStyle.Fill; body.Dock = DockStyle.Fill;
        layout.Controls.Add(h, 0, 0); layout.Controls.Add(s, 0, 1); layout.Controls.Add(body, 0, 2); card.Controls.Add(layout); return card;
    }

    private static Control MetricCard(string title, Label value, string unit)
    {
        var card = new RoundedPanel { Dock = DockStyle.Fill, Margin = new Padding(6), Padding = new Padding(14), Radius = 16, BackColor = Card, BorderColor = Border };
        var l = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = Card };
        l.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); l.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); l.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        var t = L(title, 9, Muted, false); t.Dock = DockStyle.Fill; value.Dock = DockStyle.Fill; value.TextAlign = ContentAlignment.MiddleRight; var u = L(unit, 8, Muted, false); u.Dock = DockStyle.Fill;
        l.Controls.Add(t, 0, 0); l.Controls.Add(value, 0, 1); l.Controls.Add(u, 0, 2); card.Controls.Add(l); return card;
    }

    private static Control MiniMetric(string title, Label value)
    {
        var c = new RoundedPanel { Dock = DockStyle.Fill, Margin = new Padding(5), Padding = new Padding(9), Radius = 13, BackColor = Card2, BorderColor = Border };
        var l = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Card2 }; l.RowStyles.Add(new RowStyle(SizeType.Absolute, 23)); l.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var t = L(title, 8.6f, Muted, false); t.Dock = DockStyle.Fill; t.TextAlign = ContentAlignment.MiddleCenter; value.Dock = DockStyle.Fill; value.TextAlign = ContentAlignment.MiddleCenter; l.Controls.Add(t, 0, 0); l.Controls.Add(value, 0, 1); c.Controls.Add(l); return c;
    }

    private static Control Field(string title, TextBox box)
    {
        var host = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Card, Margin = new Padding(5, 2, 5, 2) }; host.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var label = L(title, 8.7f, Muted, false); label.Dock = DockStyle.Fill;
        var inputHost = new RoundedPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 0), Padding = new Padding(12, 10, 12, 7), Radius = 12, BackColor = Card2, BorderColor = Border }; box.Dock = DockStyle.Fill; inputHost.Controls.Add(box);
        host.Controls.Add(label, 0, 0); host.Controls.Add(inputHost, 0, 1); return host;
    }

    private static TableLayoutPanel TwoColumns(float a, float b, Color? bg = null)
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = bg ?? Bg, Margin = Padding.Empty }; t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, a)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, b)); return t;
    }

    private static TextBox Input(string? text = null) => new() { Text = text ?? "", BorderStyle = BorderStyle.None, BackColor = Card2, ForeColor = TextMain, Font = new Font("Segoe UI", 11.5f), TextAlign = HorizontalAlignment.Right, RightToLeft = RightToLeft.No, Margin = Padding.Empty };
    private static RoundButton Primary(string text) => ButtonX(text, Gold, Color.FromArgb(20, 15, 3), GoldDark);
    private static RoundButton Secondary(string text) => ButtonX(text, Card2, Gold, Border);
    private static RoundButton ButtonX(string text, Color bg, Color fg, Color border)
    {
        var b = new RoundButton { Text = text, Height = 44, Radius = 12, FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = fg, Font = new Font("Segoe UI", 9.7f, FontStyle.Bold), Cursor = Cursors.Hand, Margin = new Padding(4), Padding = new Padding(7, 2, 7, 2), RightToLeft = RightToLeft.Yes }; b.FlatAppearance.BorderColor = border; b.FlatAppearance.BorderSize = 1; return b;
    }
    private static Label L(string text, float size, Color color, bool bold) => new() { Text = text, AutoSize = false, ForeColor = color, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), RightToLeft = RightToLeft.Yes, TextAlign = ContentAlignment.MiddleRight };
    private static Label MetricValue(float size = 19) => new() { Text = "—", AutoSize = false, ForeColor = Gold, Font = new Font("Segoe UI", size, FontStyle.Bold), RightToLeft = RightToLeft.No, TextAlign = ContentAlignment.MiddleRight };
    private static Label StatusLabel() => new() { Text = "—", AutoSize = false, ForeColor = Muted, BackColor = Card2, Font = new Font("Segoe UI", 9.1f, FontStyle.Bold), RightToLeft = RightToLeft.Yes, TextAlign = ContentAlignment.MiddleCenter, Padding = new Padding(8), AutoEllipsis = true };

    private static TextBox DrawerInput() => new() { BackColor = Bg, ForeColor = TextMain, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9.5f), RightToLeft = RightToLeft.No };
    private static ComboBox DrawerCombo() => new() { BackColor = Bg, ForeColor = TextMain, FlatStyle = FlatStyle.Flat, DropDownStyle = ComboBoxStyle.DropDown, Font = new Font("Segoe UI", 9.4f), RightToLeft = RightToLeft.No };
    private static CheckBox DrawerCheck(string text) => new() { Text = text, ForeColor = TextMain, AutoSize = true, Font = new Font("Segoe UI", 9.1f), Padding = new Padding(4, 7, 4, 7), RightToLeft = RightToLeft.Yes };
    private static NumericUpDown DrawerNumber(int min, int max, int value) => new() { Minimum = min, Maximum = max, Value = value, BackColor = Bg, ForeColor = TextMain, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9.4f), TextAlign = HorizontalAlignment.Center };
    private static NumericUpDown DrawerDecimal(decimal min, decimal max, decimal value, int decimals) => new() { Minimum = min, Maximum = max, Value = value, DecimalPlaces = decimals, Increment = 0.005m, BackColor = Bg, ForeColor = TextMain, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9.4f), TextAlign = HorizontalAlignment.Center };
    private static void SelectCombo(ComboBox c, string value) { var i = c.FindStringExact(value); if (i >= 0) c.SelectedIndex = i; else c.Text = value; }

    private static double Parse(string raw, double fallback)
    {
        try { var s = NormalizeDigits(raw).Trim().Replace('٫', '.').Replace(',', '.'); return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback; } catch { return fallback; }
    }
    private static string NormalizeDigits(string raw)
    {
        const string fa = "۰۱۲۳۴۵۶۷۸۹", ar = "٠١٢٣٤٥٦٧٨٩"; var chars = raw.ToCharArray(); for (var i = 0; i < chars.Length; i++) { var p = fa.IndexOf(chars[i]); if (p < 0) p = ar.IndexOf(chars[i]); if (p >= 0) chars[i] = (char)('0' + p); } return new string(chars);
    }
    private static string Num(double v) => !double.IsFinite(v) ? "—" : (Math.Abs(v) < 1e-7 ? 0 : v).ToString("0.###", CultureInfo.InvariantCulture);
    private static void OpenInstagram() { try { Process.Start(new ProcessStartInfo("https://www.instagram.com/4mirnourhan/") { UseShellExecute = true }); } catch { } }
}
