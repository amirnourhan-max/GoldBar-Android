using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.Json;
using GoldBar.Core;

namespace GoldBar.Windows;

public sealed class DesktopMainFormV2 : Form
{
    private static readonly Color Bg = Color.FromArgb(7, 9, 12);
    private static readonly Color Sidebar = Color.FromArgb(11, 14, 19);
    private static readonly Color Card = Color.FromArgb(17, 21, 27);
    private static readonly Color Card2 = Color.FromArgb(23, 28, 36);
    private static readonly Color Border = Color.FromArgb(50, 57, 69);
    private static readonly Color Gold = Color.FromArgb(247, 194, 55);
    private static readonly Color GoldSoft = Color.FromArgb(247, 211, 112);
    private static readonly Color TextMain = Color.FromArgb(246, 244, 237);
    private static readonly Color Muted = Color.FromArgb(151, 160, 176);
    private static readonly Color Success = Color.FromArgb(92, 218, 135);
    private static readonly Color Danger = Color.FromArgb(255, 104, 104);

    private readonly ScaleReader _scale = new();
    private AppSettings _settings = AppSettings.Load();
    private readonly List<GoldEntry> _entries = new();
    private int _editingIndex = -1;

    private readonly Panel _workspace = new() { Dock = DockStyle.Fill, BackColor = Bg };
    private readonly Panel _drawerHost = new() { Dock = DockStyle.Right, Width = 0, BackColor = Sidebar };
    private readonly Label _pageTitle = L("داشبورد", 22, TextMain, true);
    private readonly Label _pageSubtitle = L("نمای کلی وزن، عیار و عملیات پرکاربرد", 9.5f, Muted, false);
    private readonly Label _scaleHeader = L("ترازو آماده", 9.2f, Muted, true);
    private readonly Label _sideScaleWeight = L("— g", 22, GoldSoft, true);
    private readonly Label _sideScaleState = L("● آماده", 8.8f, Muted, true);
    private readonly Dictionary<string, RoundButton> _nav = new();

    private readonly Label _totalWeight = MetricValue();
    private readonly Label _avgAssay = MetricValue();
    private readonly Label _count = MetricValue();
    private readonly Label _totalAlloyMetric = MetricValue();

    private readonly TextBox _weight = Input();
    private readonly TextBox _assay = Input();
    private readonly RoundButton _saveEntry = Primary("ثبت آبشده");
    private readonly Label _entryScaleHint = L("کلید ↑ : دریافت وزن از ترازو", 9, Muted, true);
    private readonly DataGridView _grid = new();
    private readonly FlowLayoutPanel _recentPanel = new();

    private readonly TextBox _raiseTarget = Input("747");
    private readonly TextBox _barAssay = Input("995");
    private readonly Label _raiseDiff = MetricValue(16);
    private readonly Label _raiseNeed = MetricValue(16);
    private readonly Label _raiseState = Status();

    private readonly TextBox _lowerTarget = Input("746");
    private readonly TextBox _silver = Input("32");
    private readonly Label _alloy = MetricValue(16);
    private readonly Label _silverNeed = MetricValue(16);
    private readonly Label _otherAlloy = MetricValue(16);
    private readonly Label _lowerAfter = MetricValue(16);
    private readonly Label _lowerState = Status();

    private readonly TextBox _splitBase = Input("800");
    private readonly Label _splitA = MetricValue(20);
    private readonly Label _splitB = MetricValue(20);
    private readonly TextBox _corrWeight = Input("250");
    private readonly TextBox _corrTarget = Input("750");
    private readonly TextBox _corrDrop = Input("1");
    private readonly Label _corrAdd = MetricValue(18);
    private readonly Label _corrTotal = MetricValue(18);

    private static string DataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GoldBar", "entries.json");

    public DesktopMainFormV2()
    {
        Text = "GOLD BAR (by:Amirnourhan)";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1540, 930);
        MinimumSize = new Size(1180, 740);
        BackColor = Bg;
        ForeColor = TextMain;
        Font = new Font("Segoe UI", 10f);
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;
        RightToLeft = RightToLeft.No;
        RightToLeftLayout = false;

        var area = Screen.PrimaryScreen?.WorkingArea ?? Rectangle.Empty;
        if (area.Width >= 1500 && area.Height >= 850)
            Size = new Size(Math.Min(1660, area.Width - 70), Math.Min(990, area.Height - 70));

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
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Bg, Margin = Padding.Empty };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.Controls.Add(BuildSidebar(), 0, 0);
        shell.Controls.Add(BuildMainArea(), 1, 0);
        Controls.Add(shell);
    }

    private Control BuildSidebar()
    {
        var side = new Panel { Dock = DockStyle.Fill, BackColor = Sidebar, Padding = new Padding(16, 18, 16, 16) };

        var brand = new RoundedPanel { Dock = DockStyle.Top, Height = 170, Radius = 18, BackColor = Card, BorderColor = Border, Padding = new Padding(14) };
        var brandLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Card };
        brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        brandLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var au = new RoundedPanel { Width = 70, Height = 70, Radius = 16, BackColor = Gold, BorderColor = Gold, Anchor = AnchorStyles.None };
        var auText = L("Au", 22, Color.FromArgb(24, 17, 2), true); auText.Dock = DockStyle.Fill; auText.TextAlign = ContentAlignment.MiddleCenter; auText.RightToLeft = RightToLeft.No;
        au.Controls.Add(auText);
        var auHost = new Panel { Dock = DockStyle.Fill, BackColor = Card };
        auHost.Controls.Add(au);
        au.Location = new Point(14, 3);

        var title = L("GOLD BAR", 19, GoldSoft, true); title.Dock = DockStyle.Fill; title.TextAlign = ContentAlignment.MiddleCenter; title.RightToLeft = RightToLeft.No;
        var by = new LinkLabel { Text = "by: Amirnourhan", Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopCenter, RightToLeft = RightToLeft.No, LinkColor = GoldSoft, ActiveLinkColor = Gold, VisitedLinkColor = GoldSoft, Font = new Font("Segoe UI", 9.4f, FontStyle.Bold), Cursor = Cursors.Hand };
        by.LinkClicked += (_, _) => OpenInstagram();
        brandLayout.Controls.Add(auHost, 0, 0); brandLayout.Controls.Add(title, 0, 1); brandLayout.Controls.Add(by, 0, 2);
        brand.Controls.Add(brandLayout);
        side.Controls.Add(brand);

        var navHost = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 390, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Sidebar, Padding = new Padding(0, 18, 0, 0) };
        AddNav(navHost, "dashboard", "داشبورد", "▦");
        AddNav(navHost, "entries", "آبشده‌ها", "◆");
        AddNav(navHost, "calculations", "محاسبات عیار", "∑");
        AddNav(navHost, "quick", "محاسبه سریع", "⚡");
        AddNav(navHost, "reports", "گزارش", "▤");
        AddNav(navHost, "settings", "تنظیمات", "⚙");
        side.Controls.Add(navHost);

        var scaleCard = new RoundedPanel { Dock = DockStyle.Bottom, Height = 176, Radius = 17, BackColor = Card, BorderColor = Border, Padding = new Padding(14) };
        var scaleLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Card };
        scaleLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        scaleLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        scaleLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        scaleLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var st = L("ترازو", 10, TextMain, true); st.Dock = DockStyle.Fill; st.TextAlign = ContentAlignment.MiddleRight;
        _sideScaleWeight.Dock = DockStyle.Fill; _sideScaleWeight.TextAlign = ContentAlignment.MiddleCenter; _sideScaleWeight.RightToLeft = RightToLeft.No;
        _sideScaleState.Dock = DockStyle.Fill; _sideScaleState.TextAlign = ContentAlignment.MiddleCenter;
        var read = Secondary("دریافت وزن  ↑"); read.Dock = DockStyle.Fill; read.Click += async (_, _) => await ReadScaleIntoWeightAsync();
        scaleLayout.Controls.Add(st, 0, 0); scaleLayout.Controls.Add(_sideScaleWeight, 0, 1); scaleLayout.Controls.Add(_sideScaleState, 0, 2); scaleLayout.Controls.Add(read, 0, 3);
        scaleCard.Controls.Add(scaleLayout);
        side.Controls.Add(scaleCard);

        var version = L("GOLD BAR  •  v1.5.0", 8.2f, Muted, false); version.Dock = DockStyle.Bottom; version.Height = 28; version.TextAlign = ContentAlignment.MiddleCenter; version.RightToLeft = RightToLeft.No;
        side.Controls.Add(version);
        return side;
    }

    private void AddNav(Control host, string key, string title, string icon)
    {
        var b = new RoundButton { Text = $"{icon}     {title}", Width = 236, Height = 48, Radius = 12, FlatStyle = FlatStyle.Flat, BackColor = Sidebar, ForeColor = Muted, Font = new Font("Segoe UI", 10.3f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(8, 0, 14, 0), Cursor = Cursors.Hand, Margin = new Padding(0, 0, 0, 7), RightToLeft = RightToLeft.Yes };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (_, _) => ShowPage(key);
        _nav[key] = b;
        host.Controls.Add(b);
    }

    private Control BuildMainArea()
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Bg };
        var center = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Bg };
        center.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        center.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        center.Controls.Add(BuildTopbar(), 0, 0);
        center.Controls.Add(_workspace, 0, 1);
        host.Controls.Add(center);
        host.Controls.Add(_drawerHost);
        _drawerHost.BringToFront();
        return host;
    }

    private Control BuildTopbar()
    {
        var bar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Bg, Padding = new Padding(22, 12, 22, 8) };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var chip = new RoundedPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 6, 12, 6), Radius = 14, BackColor = Card, BorderColor = Border, Padding = new Padding(10) };
        _scaleHeader.Dock = DockStyle.Fill; _scaleHeader.TextAlign = ContentAlignment.MiddleCenter; chip.Controls.Add(_scaleHeader);
        var titles = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Bg };
        titles.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); titles.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _pageTitle.Dock = DockStyle.Fill; _pageTitle.TextAlign = ContentAlignment.MiddleRight;
        _pageSubtitle.Dock = DockStyle.Fill; _pageSubtitle.TextAlign = ContentAlignment.TopRight;
        titles.Controls.Add(_pageTitle, 0, 0); titles.Controls.Add(_pageSubtitle, 0, 1);
        bar.Controls.Add(chip, 0, 0); bar.Controls.Add(titles, 1, 0);
        return bar;
    }

    private void ShowPage(string key)
    {
        var openSettings = key == "settings";
        if (!openSettings) CloseSettingsDrawer();
        foreach (var p in _nav)
        {
            p.Value.BackColor = p.Key == key ? Color.FromArgb(39, 34, 20) : Sidebar;
            p.Value.ForeColor = p.Key == key ? GoldSoft : Muted;
            p.Value.FlatAppearance.BorderSize = p.Key == key ? 1 : 0;
            p.Value.FlatAppearance.BorderColor = Gold;
        }

        _workspace.Controls.Clear();
        Control page;
        switch (key)
        {
            case "entries":
                _pageTitle.Text = "آبشده‌ها"; _pageSubtitle.Text = "ثبت سریع، دریافت وزن از ترازو و مدیریت لیست"; page = BuildEntriesPage(); break;
            case "calculations":
                _pageTitle.Text = "محاسبات عیار"; _pageSubtitle.Text = "بالا بردن و پایین آوردن عیار با دو فرمول مستقل"; page = BuildCalculationsPage(); break;
            case "quick":
                _pageTitle.Text = "محاسبه سریع"; _pageSubtitle.Text = "تقسیم ۳۶.۷۹٪ و اصلاح وزن برای افت عیار"; page = BuildQuickPage(); break;
            case "reports":
                _pageTitle.Text = "گزارش"; _pageSubtitle.Text = "ذخیره خروجی کامل در مسیر ثابت"; page = BuildReportsPage(); break;
            case "settings":
                _pageTitle.Text = "داشبورد"; _pageSubtitle.Text = "تنظیمات در پنل کناری باز شده است"; page = BuildDashboardPage(); break;
            default:
                key = "dashboard"; _pageTitle.Text = "داشبورد"; _pageSubtitle.Text = "نمای کلی وزن، عیار و عملیات پرکاربرد"; page = BuildDashboardPage(); break;
        }
        page.Dock = DockStyle.Fill;
        _workspace.Controls.Add(page);
        RefreshAll();
        if (openSettings) OpenSettingsDrawer();
    }

    private Control BuildDashboardPage()
    {
        var metricsH = UiLayoutStore.Get("dashboard.metrics", 122);
        var entryH = UiLayoutStore.Get("dashboard.entry", 300);
        var bottomH = UiLayoutStore.Get("dashboard.bottom", 350);
        var panel = ScrollPage(out var root, metricsH + entryH + bottomH + 52, 3);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, metricsH));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, entryH));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, bottomH));
        root.Controls.Add(BuildMetricsStrip(), 0, 0);
        root.Controls.Add(BuildEntryCard("dashboard.entry"), 0, 1);
        var bottom = Columns(new[] { 34f, 34f, 32f });
        bottom.Controls.Add(BuildRaiseCard("dashboard.bottom"), 0, 0);
        bottom.Controls.Add(BuildLowerCard("dashboard.bottom"), 1, 0);
        bottom.Controls.Add(BuildRecentCard("dashboard.bottom"), 2, 0);
        root.Controls.Add(bottom, 0, 2);
        return panel;
    }

    private Control BuildEntriesPage()
    {
        var entryH = UiLayoutStore.Get("entries.entry", 300);
        var gridH = UiLayoutStore.Get("entries.grid", 520);
        var panel = ScrollPage(out var root, entryH + gridH + 44, 2);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, entryH));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, gridH));
        root.Controls.Add(BuildEntryCard("entries.entry"), 0, 0);
        root.Controls.Add(BuildGridCard("entries.grid"), 0, 1);
        return panel;
    }

    private Control BuildCalculationsPage()
    {
        var metricsH = UiLayoutStore.Get("calc.metrics", 122);
        var cardsH = UiLayoutStore.Get("calc.cards", 500);
        var panel = ScrollPage(out var root, metricsH + cardsH + 44, 2);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, metricsH));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, cardsH));
        root.Controls.Add(BuildMetricsStrip(), 0, 0);
        var row = Columns(new[] { 50f, 50f });
        row.Controls.Add(BuildRaiseCard("calc.cards"), 0, 0);
        row.Controls.Add(BuildLowerCard("calc.cards"), 1, 0);
        root.Controls.Add(row, 0, 1);
        return panel;
    }

    private Control BuildQuickPage()
    {
        var h = UiLayoutStore.Get("quick.cards", 520);
        var panel = ScrollPage(out var root, h + 30, 1);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        var row = Columns(new[] { 50f, 50f });
        row.Controls.Add(BuildSplitCard("quick.cards"), 0, 0);
        row.Controls.Add(BuildCorrectionCard("quick.cards"), 1, 0);
        root.Controls.Add(row, 0, 0);
        return panel;
    }

    private Control BuildReportsPage()
    {
        var panel = ScrollPage(out var root, 470, 1);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 430));
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Card, Padding = new Padding(8) };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 80)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var p1 = L("مسیر ذخیره فعلی", 9.3f, Muted, true); p1.Dock = DockStyle.Fill; p1.TextAlign = ContentAlignment.BottomRight;
        var p2 = L(_settings.ReportFolder, 11, TextMain, true); p2.Dock = DockStyle.Fill; p2.TextAlign = ContentAlignment.TopRight; p2.AutoEllipsis = true;
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Card };
        var save = Primary("ذخیره گزارش"); save.Width = 190; save.Click += (_, _) => SaveReport();
        var cfg = Secondary("تنظیمات"); cfg.Width = 150; cfg.Click += (_, _) => ShowPage("settings");
        actions.Controls.Add(save); actions.Controls.Add(cfg);
        var note = L("گزارش متنی شامل آبشده‌ها و نتیجه‌های نهایی است و روش محاسبه داخل فایل نوشته نمی‌شود.", 9.4f, Muted, false); note.Dock = DockStyle.Fill; note.TextAlign = ContentAlignment.TopRight;
        body.Controls.Add(p1,0,0); body.Controls.Add(p2,0,1); body.Controls.Add(actions,0,2); body.Controls.Add(note,0,3);
        root.Controls.Add(CardWithHeader("گزارش کامل", "خروجی یک‌تکه، تاریخ‌دار و خوانا", body, "reports.card"), 0, 0);
        return panel;
    }

    private Control BuildMetricsStrip()
    {
        var row = Columns(new[] { 25f, 25f, 25f, 25f });
        row.Controls.Add(MetricCard("وزن کل", _totalWeight, "g"), 0, 0);
        row.Controls.Add(MetricCard("عیار میانگین", _avgAssay, "‰"), 1, 0);
        row.Controls.Add(MetricCard("تعداد آبشده", _count, "ردیف"), 2, 0);
        row.Controls.Add(MetricCard("کل بار مورد نیاز", _totalAlloyMetric, "g"), 3, 0);
        return row;
    }

    private Control BuildEntryCard(string resizeKey)
    {
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Card };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 92)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        var fields = Columns(new[] { 50f, 50f }, Card);
        fields.Controls.Add(Field("وزن آبشده (g)", _weight),0,0); fields.Controls.Add(Field("عیار آبشده", _assay),1,0);
        _entryScaleHint.Dock = DockStyle.Fill; _entryScaleHint.TextAlign = ContentAlignment.MiddleRight;
        var actions = Columns(new[] { 78f, 22f }, Card);
        _saveEntry.Dock = DockStyle.Fill;
        var clear = Secondary("پاک‌کردن همه"); clear.ForeColor = Danger; clear.Dock = DockStyle.Fill; clear.Click += (_, _) => ClearAll();
        actions.Controls.Add(_saveEntry,0,0); actions.Controls.Add(clear,1,0);
        body.Controls.Add(fields,0,0); body.Controls.Add(_entryScaleHint,0,1); body.Controls.Add(actions,0,2);
        return CardWithHeader("ثبت سریع آبشده", "وزن را دستی وارد کن یا داخل فیلد وزن کلید ↑ را بزن.", body, resizeKey);
    }

    private Control BuildRaiseCard(string key)
    {
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Card };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 82)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 90)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var fields = Columns(new[] { 50f, 50f }, Card); fields.Controls.Add(Field("عیار هدف", _raiseTarget),0,0); fields.Controls.Add(Field("عیار شمش", _barAssay),1,0);
        var m = Columns(new[] { 50f, 50f }, Card); m.Controls.Add(MiniMetric("اختلاف تا هدف", _raiseDiff),0,0); m.Controls.Add(MiniMetric("شمش مورد نیاز (g)", _raiseNeed),1,0);
        _raiseState.Dock = DockStyle.Fill;
        body.Controls.Add(fields,0,0); body.Controls.Add(m,0,1); body.Controls.Add(_raiseState,0,2);
        return CardWithHeader("بالا بردن عیار", "اگر عیار میانگین پایین‌تر از هدف باشد", body, key);
    }

    private Control BuildLowerCard(string key)
    {
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Card };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 82)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 82)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 82)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var fields = Columns(new[] { 50f, 50f }, Card); fields.Controls.Add(Field("عیار هدف", _lowerTarget),0,0); fields.Controls.Add(Field("درصد نقره", _silver),1,0);
        var m1 = Columns(new[] { 50f,50f }, Card); m1.Controls.Add(MiniMetric("کل بار (g)", _alloy),0,0); m1.Controls.Add(MiniMetric("نقره (g)", _silverNeed),1,0);
        var m2 = Columns(new[] { 50f,50f }, Card); m2.Controls.Add(MiniMetric("بار بدون نقره (g)", _otherAlloy),0,0); m2.Controls.Add(MiniMetric("وزن پس از بار (g)", _lowerAfter),1,0);
        _lowerState.Dock = DockStyle.Fill;
        body.Controls.Add(fields,0,0); body.Controls.Add(m1,0,1); body.Controls.Add(m2,0,2); body.Controls.Add(_lowerState,0,3);
        return CardWithHeader("پایین آوردن عیار", "اگر عیار میانگین بالاتر از هدف باشد", body, key);
    }

    private Control BuildRecentCard(string key)
    {
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Card };
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        _recentPanel.Dock = DockStyle.Fill; _recentPanel.FlowDirection = FlowDirection.TopDown; _recentPanel.WrapContents = false; _recentPanel.AutoScroll = true; _recentPanel.BackColor = Card;
        var all = Secondary("مشاهده همه آبشده‌ها"); all.Dock = DockStyle.Fill; all.Click += (_, _) => ShowPage("entries");
        body.Controls.Add(_recentPanel,0,0); body.Controls.Add(all,0,1);
        return CardWithHeader("آخرین آبشده‌ها", "آخرین ردیف‌های ثبت‌شده", body, key);
    }

    private Control BuildGridCard(string key)
    {
        _grid.Dock = DockStyle.Fill;
        return CardWithHeader("لیست آبشده‌ها", "برای ویرایش یا حذف از ستون عملیات استفاده کن.", _grid, key);
    }

    private Control BuildSplitCard(string key)
    {
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Card };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.Controls.Add(Field("عدد پایه", _splitBase),0,0);
        var m = Columns(new[] { 50f,50f }, Card); m.Controls.Add(MiniMetric("۳۶.۷۹٪", _splitA),0,0); m.Controls.Add(MiniMetric("۶۳.۲۱٪", _splitB),1,0); body.Controls.Add(m,0,1);
        return CardWithHeader("تقسیم ۳۶.۷۹٪ / ۶۳.۲۱٪", "خروجی‌ها لحظه‌ای محاسبه می‌شوند.", body, key);
    }

    private Control BuildCorrectionCard(string key)
    {
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Card };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 94)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 94)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var top = Columns(new[] { 50f,50f }, Card); top.Controls.Add(Field("وزن پایه", _corrWeight),0,0); top.Controls.Add(Field("عیار هدف", _corrTarget),1,0);
        body.Controls.Add(top,0,0); body.Controls.Add(Field("مقدار افت عیار", _corrDrop),0,1);
        var m = Columns(new[] { 50f,50f }, Card); m.Controls.Add(MiniMetric("بار افزوده (g)", _corrAdd),0,0); m.Controls.Add(MiniMetric("جمع وزن (g)", _corrTotal),1,0); body.Controls.Add(m,0,2);
        return CardWithHeader("اصلاح وزن برای افت عیار", "وزن پایه، عیار هدف و مقدار افت", body, key);
    }

    private Panel ScrollPage(out TableLayoutPanel root, int height, int rows)
    {
        var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Bg };
        root = new TableLayoutPanel { Dock = DockStyle.Top, Height = Math.Max(height, 420), ColumnCount = 1, RowCount = rows, BackColor = Bg, Padding = new Padding(20, 8, 20, 20) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        panel.Controls.Add(root);
        return panel;
    }

    private static TableLayoutPanel Columns(float[] widths, Color? bg = null)
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = widths.Length, RowCount = 1, BackColor = bg ?? Bg, Margin = Padding.Empty };
        foreach (var w in widths) t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, w));
        t.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        return t;
    }

    private Control CardWithHeader(string title, string subtitle, Control body, string resizeKey)
    {
        var card = new ResizableCardPanel { Dock = DockStyle.Fill, Margin = new Padding(6), Padding = new Padding(15), Radius = 18, BackColor = Card, BorderColor = Border, ResizeKey = resizeKey, MinimumResizeHeight = 220 };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Card };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,34)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var h = L(title, 13.5f, TextMain, true); h.Dock = DockStyle.Fill; h.TextAlign = ContentAlignment.MiddleRight;
        var s = L(subtitle + "     ↕ لبه پایین را برای تغییر اندازه بکش", 8.8f, Muted, false); s.Dock = DockStyle.Fill; s.TextAlign = ContentAlignment.TopRight;
        body.Dock = DockStyle.Fill;
        layout.Controls.Add(h,0,0); layout.Controls.Add(s,0,1); layout.Controls.Add(body,0,2); card.Controls.Add(layout);
        return card;
    }

    private static Control MetricCard(string title, Label value, string unit)
    {
        var c = new RoundedPanel { Dock = DockStyle.Fill, Margin = new Padding(6), Padding = new Padding(14), Radius = 16, BackColor = Card, BorderColor = Border };
        var l = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Card };
        l.RowStyles.Add(new RowStyle(SizeType.Absolute,24)); l.RowStyles.Add(new RowStyle(SizeType.Percent,100)); l.RowStyles.Add(new RowStyle(SizeType.Absolute,18));
        var t = L(title,9.2f,Muted,false); t.Dock=DockStyle.Fill; t.TextAlign=ContentAlignment.MiddleRight;
        value.Dock=DockStyle.Fill; value.TextAlign=ContentAlignment.MiddleRight;
        var u=L(unit,8.2f,Muted,false); u.Dock=DockStyle.Fill; u.TextAlign=ContentAlignment.MiddleRight;
        l.Controls.Add(t,0,0); l.Controls.Add(value,0,1); l.Controls.Add(u,0,2); c.Controls.Add(l); return c;
    }

    private static Control MiniMetric(string title, Label value)
    {
        var c=new RoundedPanel{Dock=DockStyle.Fill,Margin=new Padding(5),Padding=new Padding(9),Radius=13,BackColor=Card2,BorderColor=Border};
        var l=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,BackColor=Card2}; l.RowStyles.Add(new RowStyle(SizeType.Absolute,24)); l.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var t=L(title,8.6f,Muted,false); t.Dock=DockStyle.Fill; t.TextAlign=ContentAlignment.MiddleCenter; value.Dock=DockStyle.Fill; value.TextAlign=ContentAlignment.MiddleCenter;
        l.Controls.Add(t,0,0); l.Controls.Add(value,0,1); c.Controls.Add(l); return c;
    }

    private static Control Field(string title, TextBox box)
    {
        var h=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,BackColor=Card,Margin=new Padding(5,2,5,2)}; h.RowStyles.Add(new RowStyle(SizeType.Absolute,25)); h.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var t=L(title,8.9f,Muted,false); t.Dock=DockStyle.Fill; t.TextAlign=ContentAlignment.MiddleRight;
        var input=new RoundedPanel{Dock=DockStyle.Fill,Margin=new Padding(0,2,0,0),Padding=new Padding(12,10,12,6),Radius=12,BackColor=Card2,BorderColor=Border}; box.Dock=DockStyle.Fill; input.Controls.Add(box);
        h.Controls.Add(t,0,0); h.Controls.Add(input,0,1); return h;
    }

    private static TextBox Input(string? text=null)=>new(){Text=text??"",BorderStyle=BorderStyle.None,BackColor=Card2,ForeColor=TextMain,Font=new Font("Segoe UI",11.5f),TextAlign=HorizontalAlignment.Right,RightToLeft=RightToLeft.No,Margin=Padding.Empty};
    private static Label L(string text,float size,Color color,bool bold)=>new(){Text=text,AutoSize=false,ForeColor=color,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),RightToLeft=RightToLeft.Yes,TextAlign=ContentAlignment.MiddleRight};
    private static Label MetricValue(float size=18)=>new(){Text="—",AutoSize=false,ForeColor=GoldSoft,Font=new Font("Segoe UI",size,FontStyle.Bold),RightToLeft=RightToLeft.No,TextAlign=ContentAlignment.MiddleRight};
    private static Label Status()=>new(){Text="—",AutoSize=false,ForeColor=Muted,BackColor=Card2,Font=new Font("Segoe UI",9.2f,FontStyle.Bold),RightToLeft=RightToLeft.Yes,TextAlign=ContentAlignment.MiddleCenter,Padding=new Padding(8),AutoEllipsis=true};
    private static RoundButton Primary(string text)=>Button(text,Gold,Color.FromArgb(25,18,2),Gold);
    private static RoundButton Secondary(string text)=>Button(text,Card2,GoldSoft,Border);
    private static RoundButton Button(string text,Color bg,Color fg,Color border){var b=new RoundButton{Text=text,Height=46,Radius=12,FlatStyle=FlatStyle.Flat,BackColor=bg,ForeColor=fg,Font=new Font("Segoe UI",9.8f,FontStyle.Bold),Cursor=Cursors.Hand,Margin=new Padding(4),RightToLeft=RightToLeft.Yes};b.FlatAppearance.BorderColor=border;b.FlatAppearance.BorderSize=1;return b;}

    private void BindEvents()
    {
        _saveEntry.Click += (_,_)=>SaveEntry();
        _weight.KeyDown += async (_,e)=>{if(e.KeyCode==Keys.Up&&_settings.ReadOnUpArrow){e.SuppressKeyPress=true;await ReadScaleIntoWeightAsync();}else if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;_assay.Focus();_assay.SelectAll();}};
        _assay.KeyDown += (_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;SaveEntry();}};
        foreach(var b in new[]{_raiseTarget,_barAssay,_lowerTarget,_silver,_splitBase,_corrWeight,_corrTarget,_corrDrop}){b.TextChanged+=(_,_)=>Recalculate();b.Enter+=(_,_)=>b.SelectAll();}
        _scale.WeightReceived += value => Ui(()=>{
            var txt=Num(value)+" g"; _sideScaleWeight.Text=txt; _sideScaleState.Text="● وزن پایدار"; _sideScaleState.ForeColor=Success; _scaleHeader.Text="● ترازو متصل  •  "+txt; _scaleHeader.ForeColor=Success; _entryScaleHint.Text="● وزن دریافتی: "+txt; _entryScaleHint.ForeColor=Success;
            if(_settings.AutoRead&&_weight.Focused){_weight.Text=Num(value);_weight.SelectAll();}
        });
        _scale.StatusChanged += (text,ok)=>Ui(()=>{_scaleHeader.Text=ok?"● "+text:text;_scaleHeader.ForeColor=ok?Success:Muted;_sideScaleState.Text=ok?"● متصل":"● آماده";_sideScaleState.ForeColor=ok?Success:Muted;});
    }

    private void ApplyScaleSettings(){_scale.ApplySettings(_settings,_settings.AutoRead);_scaleHeader.Text=_settings.AutoRead?"ترازو • Auto Read پایدار":"ترازو • دریافت با ↑";_scaleHeader.ForeColor=Muted;_sideScaleState.Text=_settings.AutoRead?"● Auto Read پایدار":"● دریافت با ↑";_sideScaleState.ForeColor=Muted;}
    private void Ui(Action a){if(IsDisposed||!IsHandleCreated)return;try{BeginInvoke(a);}catch{}}

    private async Task ReadScaleIntoWeightAsync()
    {
        try{_entryScaleHint.Text="● در حال دریافت وزن…";_entryScaleHint.ForeColor=GoldSoft;var w=await _scale.ReadNowAsync();_weight.Text=Num(w);_weight.Focus();_weight.SelectAll();_entryScaleHint.Text="● وزن دریافتی: "+Num(w)+" g";_entryScaleHint.ForeColor=Success;}
        catch(Exception ex){_entryScaleHint.Text="● دریافت وزن ناموفق";_entryScaleHint.ForeColor=Danger;MessageBox.Show(this,ex.Message+"\n\nPort و Baud Rate را بررسی کن.","ترازو",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }

    private void OpenSettingsDrawer()
    {
        _drawerHost.Controls.Clear(); _drawerHost.Width=Math.Min(455,Math.Max(390,ClientSize.Width/3));
        var drawer=new SettingsDrawer(_settings){Dock=DockStyle.Fill};
        drawer.CloseRequested += ()=>{CloseSettingsDrawer();ShowPage("dashboard");};
        drawer.SettingsSaved += s=>{_settings=s;ApplyScaleSettings();CloseSettingsDrawer();ShowPage("dashboard");};
        _drawerHost.Controls.Add(drawer); _drawerHost.BringToFront();
    }
    private void CloseSettingsDrawer(){_drawerHost.Controls.Clear();_drawerHost.Width=0;}

    private void SaveEntry()
    {
        var w=Parse(_weight.Text,-1);var a=Parse(_assay.Text,-1);if(w<=0||a<=0||a>1000){MessageBox.Show(this,"وزن و عیار را صحیح وارد کن.","ورودی نامعتبر",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
        var item=new GoldEntry(w,a);if(_editingIndex>=0&&_editingIndex<_entries.Count)_entries[_editingIndex]=item;else _entries.Add(item);_editingIndex=-1;_saveEntry.Text="ثبت آبشده";_weight.Clear();_assay.Clear();PersistEntries();RefreshAll();_weight.Focus();
    }
    private void EditEntry(int i){if(i<0||i>=_entries.Count)return;ShowPage("entries");_editingIndex=i;_weight.Text=Num(_entries[i].Weight);_assay.Text=Num(_entries[i].Assay);_saveEntry.Text="ذخیره تغییرات";_weight.Focus();_weight.SelectAll();}
    private void DeleteEntry(int i){if(i<0||i>=_entries.Count)return;_entries.RemoveAt(i);if(_editingIndex==i)_editingIndex=-1;else if(_editingIndex>i)_editingIndex--;PersistEntries();RefreshAll();}
    private void ClearAll(){if(_entries.Count==0)return;if(MessageBox.Show(this,"همه آبشده‌ها حذف شوند؟","پاک‌کردن همه",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;_entries.Clear();_editingIndex=-1;_weight.Clear();_assay.Clear();_saveEntry.Text="ثبت آبشده";PersistEntries();RefreshAll();}

    private void Recalculate()
    {
        var s=GoldCalculator.Summarize(_entries);_totalWeight.Text=Num(s.Weight);_avgAssay.Text=Num(s.AverageAssay);_count.Text=s.Count.ToString(CultureInfo.InvariantCulture);
        var rt=Parse(_raiseTarget.Text,747);var high=Parse(_barAssay.Text,995);var raise=GoldCalculator.RequiredHighAssayBar(s,rt,high);_raiseDiff.Text=Num(raise.DifferenceNeeded);_raiseNeed.Text=Num(raise.RequiredHighBar);_raiseState.Text=!double.IsFinite(raise.RequiredHighBar)?"ابتدا آبشده معتبر ثبت کن.":raise.RequiredHighBar>0?$"برای رسیدن به {Num(rt)}، {Num(raise.RequiredHighBar)} g شمش {Num(high)} نیاز است.":"افزایش عیار لازم نیست.";_raiseState.ForeColor=double.IsFinite(raise.RequiredHighBar)?GoldSoft:Muted;
        var lt=Parse(_lowerTarget.Text,746);var sp=Parse(_silver.Text,32);var lower=GoldCalculator.RequiredAlloy(s,lt,sp,s.Weight);_alloy.Text=Num(lower.TotalAlloyRequired);_totalAlloyMetric.Text=Num(lower.TotalAlloyRequired);_silverNeed.Text=Num(lower.SilverRequired);_otherAlloy.Text=Num(lower.NonSilverRequired);_lowerAfter.Text=Num(lower.TotalAfterAlloy);_lowerState.Text=!double.IsFinite(lower.TotalAlloyRequired)?"ابتدا آبشده معتبر ثبت کن.":lower.TotalAlloyRequired>0?$"برای کاهش تا {Num(lt)}، {Num(lower.TotalAlloyRequired)} g بار نیاز است.":"کاهش عیار لازم نیست.";_lowerState.ForeColor=double.IsFinite(lower.TotalAlloyRequired)?GoldSoft:Muted;
        var bv=Parse(_splitBase.Text,800);var p=GoldCalculator.Split3679(bv);_splitA.Text=Num(p);_splitB.Text=Num(bv-p);var cw=Parse(_corrWeight.Text,250);var ct=Parse(_corrTarget.Text,750);var cd=Parse(_corrDrop.Text,1);var add=GoldCalculator.CorrectionAddition(cw,ct,cd);_corrAdd.Text=Num(add);_corrTotal.Text=Num(cw+add);
    }

    private void RefreshAll(){Recalculate();RefreshRecent();if(_grid.Columns.Count==0)return;_grid.Rows.Clear();for(var i=0;i<_entries.Count;i++)_grid.Rows.Add(i+1,Num(_entries[i].Weight),Num(_entries[i].Assay),"ویرایش","حذف");}
    private void RefreshRecent(){_recentPanel.Controls.Clear();if(_entries.Count==0){var e=L("هنوز آبشده‌ای ثبت نشده است.",9.2f,Muted,false);e.Width=300;e.Height=38;e.TextAlign=ContentAlignment.MiddleCenter;_recentPanel.Controls.Add(e);return;}for(var i=_entries.Count-1;i>=Math.Max(0,_entries.Count-4);i--){var x=_entries[i];var row=new RoundedPanel{Width=330,Height=48,Radius=11,BackColor=Card2,BorderColor=Border,Margin=new Padding(2,2,2,5)};var label=L($"{Num(x.Weight)} g     •     عیار {Num(x.Assay)}",9.3f,TextMain,true);label.Dock=DockStyle.Fill;label.TextAlign=ContentAlignment.MiddleCenter;row.Controls.Add(label);_recentPanel.Controls.Add(row);}}

    private void SaveReport()
    {
        try{if(string.IsNullOrWhiteSpace(_settings.ReportFolder)){ShowPage("settings");return;}if(!Directory.Exists(_settings.ReportFolder))Directory.CreateDirectory(_settings.ReportFolder);var s=GoldCalculator.Summarize(_entries);var raise=GoldCalculator.RequiredHighAssayBar(s,Parse(_raiseTarget.Text,747),Parse(_barAssay.Text,995));var lower=GoldCalculator.RequiredAlloy(s,Parse(_lowerTarget.Text,746),Parse(_silver.Text,32),s.Weight);var b=new StringBuilder();b.AppendLine("GOLD BAR (by:Amirnourhan)");b.AppendLine("تاریخ و ساعت: "+DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));b.AppendLine();b.AppendLine("آبشده‌ها:");for(var i=0;i<_entries.Count;i++)b.AppendLine($"{i+1}) وزن {Num(_entries[i].Weight)} g | عیار {Num(_entries[i].Assay)}");b.AppendLine();b.AppendLine($"وزن کل: {Num(s.Weight)} g | عیار میانگین: {Num(s.AverageAssay)} | تعداد: {s.Count}");b.AppendLine($"وزن پس از بار: {Num(lower.TotalAfterAlloy)} g");b.AppendLine($"شمش عیار بالا مورد نیاز: {Num(raise.RequiredHighBar)} g");b.AppendLine($"کل بار مورد نیاز: {Num(lower.TotalAlloyRequired)} g | نقره: {Num(lower.SilverRequired)} g | بار بدون نقره: {Num(lower.NonSilverRequired)} g");var split=GoldCalculator.Split3679(Parse(_splitBase.Text,800));b.AppendLine($"محاسبه سریع: 36.79% = {Num(split)} | 63.21% = {Num(Parse(_splitBase.Text,800)-split)}");b.AppendLine($"اصلاح افت عیار: بار افزوده {_corrAdd.Text} g | جمع وزن {_corrTotal.Text} g");var path=Path.Combine(_settings.ReportFolder,"GoldBar_"+DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")+".txt");File.WriteAllText(path,b.ToString(),Encoding.UTF8);MessageBox.Show(this,"گزارش ذخیره شد:\n"+path,"گزارش",MessageBoxButtons.OK,MessageBoxIcon.Information);}catch(Exception ex){MessageBox.Show(this,"ذخیره گزارش انجام نشد:\n"+ex.Message,"خطای گزارش",MessageBoxButtons.OK,MessageBoxIcon.Error);}}

    private void ConfigureGrid(){_grid.BackgroundColor=Card;_grid.BorderStyle=BorderStyle.None;_grid.GridColor=Border;_grid.EnableHeadersVisualStyles=false;_grid.ColumnHeadersDefaultCellStyle.BackColor=Card2;_grid.ColumnHeadersDefaultCellStyle.ForeColor=TextMain;_grid.ColumnHeadersDefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;_grid.ColumnHeadersHeight=42;_grid.DefaultCellStyle.BackColor=Card;_grid.DefaultCellStyle.ForeColor=TextMain;_grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(59,50,28);_grid.DefaultCellStyle.SelectionForeColor=TextMain;_grid.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;_grid.RowTemplate.Height=43;_grid.RowHeadersVisible=false;_grid.AllowUserToAddRows=false;_grid.ReadOnly=true;_grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;_grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;_grid.RightToLeft=RightToLeft.Yes;_grid.Columns.Add(new DataGridViewTextBoxColumn{Name="No",HeaderText="#",FillWeight=10});_grid.Columns.Add(new DataGridViewTextBoxColumn{Name="Weight",HeaderText="وزن (g)",FillWeight=28});_grid.Columns.Add(new DataGridViewTextBoxColumn{Name="Assay",HeaderText="عیار",FillWeight=24});_grid.Columns.Add(new DataGridViewButtonColumn{Name="Edit",HeaderText="",Text="ویرایش",UseColumnTextForButtonValue=true,FillWeight=19,FlatStyle=FlatStyle.Flat});_grid.Columns.Add(new DataGridViewButtonColumn{Name="Delete",HeaderText="",Text="حذف",UseColumnTextForButtonValue=true,FillWeight=19,FlatStyle=FlatStyle.Flat});_grid.CellContentClick+=(_,e)=>{if(e.RowIndex<0)return;var n=_grid.Columns[e.ColumnIndex].Name;if(n=="Edit")EditEntry(e.RowIndex);else if(n=="Delete")DeleteEntry(e.RowIndex);};}
    private void LoadEntries(){try{if(!File.Exists(DataPath))return;var list=JsonSerializer.Deserialize<List<GoldEntry>>(File.ReadAllText(DataPath));if(list!=null)_entries.AddRange(list.Where(x=>x.Weight>0&&x.Assay>0&&x.Assay<=1000));}catch{}}
    private void PersistEntries(){try{Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!);File.WriteAllText(DataPath,JsonSerializer.Serialize(_entries,new JsonSerializerOptions{WriteIndented=true}));}catch(Exception ex){MessageBox.Show(this,"ذخیره اطلاعات داخلی انجام نشد:\n"+ex.Message,"خطا",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
    private static double Parse(string raw,double fallback){try{var s=NormalizeDigits(raw).Trim().Replace('٫','.').Replace(',','.');return double.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out var v)?v:fallback;}catch{return fallback;}}
    private static string NormalizeDigits(string raw){const string fa="۰۱۲۳۴۵۶۷۸۹",ar="٠١٢٣٤٥٦٧٨٩";var c=raw.ToCharArray();for(var i=0;i<c.Length;i++){var p=fa.IndexOf(c[i]);if(p<0)p=ar.IndexOf(c[i]);if(p>=0)c[i]=(char)('0'+p);}return new string(c);}
    private static string Num(double v)=>!double.IsFinite(v)?"—":(Math.Abs(v)<1e-7?0:v).ToString("0.###",CultureInfo.InvariantCulture);
    private static void OpenInstagram(){try{Process.Start(new ProcessStartInfo("https://www.instagram.com/4mirnourhan/"){UseShellExecute=true});}catch{}}
}

public sealed class ResizableCardPanel : RoundedPanel
{
    public string ResizeKey { get; set; } = "card";
    public int MinimumResizeHeight { get; set; } = 220;
    private bool _dragging;
    private int _lastScreenY;

    public ResizableCardPanel()
    {
        MouseMove += OnMove; MouseDown += OnDown; MouseUp += (_,_)=>{_dragging=false;Capture=false;Cursor=Cursors.Default;};
    }
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!_dragging)Cursor=e.Y>=Height-9?Cursors.SizeNS:Cursors.Default;}
    private void OnMove(object? s,MouseEventArgs e){if(!_dragging)return;var y=PointToScreen(e.Location).Y;var delta=y-_lastScreenY;if(Math.Abs(delta)<2)return;_lastScreenY=y;ResizeAncestor(delta);}
    private void OnDown(object? s,MouseEventArgs e){if(e.Button!=MouseButtons.Left||e.Y<Height-12)return;_dragging=true;Capture=true;_lastScreenY=PointToScreen(e.Location).Y;Cursor=Cursors.SizeNS;}
    private void ResizeAncestor(int delta){Control child=this;Control? p=Parent;while(p!=null){if(p is TableLayoutPanel table&&table.RowCount>1){var pos=table.GetPositionFromControl(child);if(pos.Row>=0&&pos.Row<table.RowStyles.Count){var style=table.RowStyles[pos.Row];var current=table.GetRowHeights().ElementAtOrDefault(pos.Row);var next=Math.Clamp(current+delta,MinimumResizeHeight,850);style.SizeType=SizeType.Absolute;style.Height=next;UiLayoutStore.Set(ResizeKey,next);var sum=table.RowStyles.Cast<RowStyle>().Sum(x=>x.SizeType==SizeType.Absolute?x.Height:0);if(table.Dock==DockStyle.Top&&sum>0)table.Height=(int)sum+32;table.PerformLayout();return;}}child=p;p=p.Parent;}}
}

internal static class UiLayoutStore
{
    private static readonly object Gate=new();
    private static Dictionary<string,int>? _values;
    private static string PathName=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GoldBar","layout.json");
    private static Dictionary<string,int> Values{get{lock(Gate){if(_values!=null)return _values;try{_values=File.Exists(PathName)?JsonSerializer.Deserialize<Dictionary<string,int>>(File.ReadAllText(PathName))??new():new();}catch{_values=new();}return _values;}}}
    public static int Get(string key,int fallback)=>Values.TryGetValue(key,out var v)?Math.Clamp(v,180,850):fallback;
    public static void Set(string key,int value){lock(Gate){Values[key]=Math.Clamp(value,180,850);try{Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathName)!);File.WriteAllText(PathName,JsonSerializer.Serialize(Values,new JsonSerializerOptions{WriteIndented=true}));}catch{}}}
}
