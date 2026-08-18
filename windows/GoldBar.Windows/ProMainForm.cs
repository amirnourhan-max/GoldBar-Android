using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.Json;
using GoldBar.Core;

namespace GoldBar.Windows;

/// <summary>
/// Desktop-first shell modeled on the approved dark/gold reference UI.
/// The dashboard uses split containers so the operator can resize the main cards manually.
/// </summary>
public sealed class ProMainForm : Form
{
    private static readonly Color Bg = Color.FromArgb(6, 8, 11);
    private static readonly Color Sidebar = Color.FromArgb(10, 13, 18);
    private static readonly Color Card = Color.FromArgb(16, 20, 27);
    private static readonly Color Card2 = Color.FromArgb(22, 27, 35);
    private static readonly Color Border = Color.FromArgb(48, 55, 68);
    private static readonly Color Gold = Color.FromArgb(247, 193, 55);
    private static readonly Color GoldSoft = Color.FromArgb(247, 211, 112);
    private static readonly Color TextMain = Color.FromArgb(246, 244, 237);
    private static readonly Color Muted = Color.FromArgb(151, 160, 176);
    private static readonly Color Success = Color.FromArgb(72, 211, 121);
    private static readonly Color Danger = Color.FromArgb(255, 105, 105);

    private readonly ScaleReader _scale = new();
    private AppSettings _settings = AppSettings.Load();
    private readonly List<GoldEntry> _entries = new();
    private int _editingIndex = -1;

    private readonly Panel _workspace = new() { Dock = DockStyle.Fill, BackColor = Bg };
    private readonly Label _pageTitle = L("داشبورد", 20, TextMain, true);
    private readonly Label _pageSubtitle = L("نمای کلی وزن، عیار و عملیات پرکاربرد", 9.3f, Muted, false);
    private readonly Label _topScaleStatus = L("ترازو • آماده", 9.2f, Muted, true);
    private readonly Dictionary<string, RoundButton> _nav = new();

    private readonly Label _totalWeight = MetricLabel(18);
    private readonly Label _avgAssay = MetricLabel(18);
    private readonly Label _count = MetricLabel(18);
    private readonly Label _totalAlloy = MetricLabel(18);

    private readonly TextBox _weight = Input();
    private readonly TextBox _assay = Input();
    private readonly RoundButton _saveEntry = Primary("ثبت آبشده");
    private readonly Label _entryScaleHint = L("↑  دریافت وزن از ترازو", 9.1f, Muted, true);
    private readonly DataGridView _grid = new();
    private readonly FlowLayoutPanel _recentHost = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Card };

    private readonly TextBox _raiseTarget = Input("747");
    private readonly TextBox _barAssay = Input("995");
    private readonly Label _raiseDiff = MetricLabel(15);
    private readonly Label _raiseNeed = MetricLabel(15);
    private readonly Label _raiseState = StatusLabel();

    private readonly TextBox _lowerTarget = Input("746");
    private readonly TextBox _silver = Input("32");
    private readonly Label _alloy = MetricLabel(15);
    private readonly Label _silverNeed = MetricLabel(15);
    private readonly Label _otherAlloy = MetricLabel(15);
    private readonly Label _afterAlloy = MetricLabel(15);
    private readonly Label _lowerState = StatusLabel();

    private readonly TextBox _splitBase = Input("800");
    private readonly Label _splitA = MetricLabel(20);
    private readonly Label _splitB = MetricLabel(20);
    private readonly TextBox _corrWeight = Input("250");
    private readonly TextBox _corrTarget = Input("750");
    private readonly TextBox _corrDrop = Input("1");
    private readonly Label _corrAdd = MetricLabel(18);
    private readonly Label _corrTotal = MetricLabel(18);

    // Persistent settings controls used both in dashboard drawer and settings page.
    private ComboBox? _sModel, _sPort, _sBaud, _sDataBits, _sParity, _sStopBits, _sFlow, _sEnding;
    private TextBox? _sQuery, _sReportFolder;
    private NumericUpDown? _sTimeout, _sStableSamples, _sTolerance;
    private CheckBox? _sAuto, _sUp, _sPrint, _sSendQuery, _sStableOnly;
    private Label? _sTestStatus;

    private readonly Label _sideWeight = L("— g", 24, GoldSoft, true);
    private readonly Label _sideScaleState = L("●  آماده", 8.8f, Muted, true);

    private SplitContainer? _dashboardOuter;
    private SplitContainer? _dashboardMainVertical;

    private static string DataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GoldBar", "entries.json");

    public ProMainForm()
    {
        Text = "GOLD BAR (by:Amirnourhan)";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1600, 940);
        MinimumSize = new Size(1180, 760);
        WindowState = FormWindowState.Maximized;
        BackColor = Bg;
        ForeColor = TextMain;
        Font = new Font("Segoe UI", 10f);
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        RightToLeft = RightToLeft.No;
        RightToLeftLayout = false;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

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
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 248));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.Controls.Add(BuildSidebar(), 0, 0);
        shell.Controls.Add(BuildMainArea(), 1, 0);
        Controls.Add(shell);
    }

    private Control BuildSidebar()
    {
        var side = new Panel { Dock = DockStyle.Fill, BackColor = Sidebar, Padding = new Padding(16, 18, 16, 16) };

        var brand = new RoundedPanel { Dock = DockStyle.Top, Height = 176, Radius = 20, BackColor = Card, BorderColor = Border, Padding = new Padding(12) };
        var brandLayout = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
        brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        brandLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var iconBox = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Card, Margin = new Padding(46, 2, 46, 4) };
        try { iconBox.Image = Icon.ExtractAssociatedIcon(Application.ExecutablePath)?.ToBitmap(); } catch { }
        var title = L("GOLD BAR", 20, TextMain, true); title.Dock = DockStyle.Fill; title.TextAlign = ContentAlignment.MiddleCenter; title.RightToLeft = RightToLeft.No;
        var by = new LinkLabel { Text = "by: Amirnourhan", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, LinkColor = GoldSoft, ActiveLinkColor = GoldSoft, VisitedLinkColor = GoldSoft, Font = new Font("Segoe UI", 9.2f, FontStyle.Bold), Cursor = Cursors.Hand, RightToLeft = RightToLeft.No };
        by.LinkClicked += (_, _) => OpenInstagram();
        var edition = L("Windows Desktop Edition", 8.1f, Muted, false); edition.Dock = DockStyle.Fill; edition.TextAlign = ContentAlignment.MiddleCenter; edition.RightToLeft = RightToLeft.No;
        brandLayout.Controls.Add(iconBox, 0, 0); brandLayout.Controls.Add(title, 0, 1); brandLayout.Controls.Add(by, 0, 2); brandLayout.Controls.Add(edition, 0, 3);
        brand.Controls.Add(brandLayout);
        side.Controls.Add(brand);

        var nav = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 385, Top = 194, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Sidebar, Padding = new Padding(0, 18, 0, 0) };
        AddNav(nav, "dashboard", "داشبورد", "▦");
        AddNav(nav, "entries", "آبشده‌ها", "◆");
        AddNav(nav, "calculations", "محاسبات عیار", "∑");
        AddNav(nav, "quick", "محاسبه سریع", "⚡");
        AddNav(nav, "reports", "گزارش‌ها", "▤");
        AddNav(nav, "settings", "تنظیمات", "⚙");
        side.Controls.Add(nav);

        var scaleCard = new RoundedPanel { Dock = DockStyle.Bottom, Height = 162, Radius = 18, BackColor = Card, BorderColor = Border, Padding = new Padding(14) };
        var scaleLayout = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, RowCount = 4, ColumnCount = 1 };
        scaleLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        scaleLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        scaleLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        scaleLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var st = L("ترازو", 10, TextMain, true); st.Dock = DockStyle.Fill; st.TextAlign = ContentAlignment.MiddleRight;
        _sideWeight.Dock = DockStyle.Fill; _sideWeight.TextAlign = ContentAlignment.MiddleCenter; _sideWeight.RightToLeft = RightToLeft.No;
        _sideScaleState.Dock = DockStyle.Fill; _sideScaleState.TextAlign = ContentAlignment.MiddleCenter;
        var read = Secondary("دریافت وزن با ↑"); read.Dock = DockStyle.Fill; read.Click += async (_, _) => await ReadScaleIntoWeightAsync();
        scaleLayout.Controls.Add(st, 0, 0); scaleLayout.Controls.Add(_sideWeight, 0, 1); scaleLayout.Controls.Add(_sideScaleState, 0, 2); scaleLayout.Controls.Add(read, 0, 3);
        scaleCard.Controls.Add(scaleLayout);
        side.Controls.Add(scaleCard);
        return side;
    }

    private void AddNav(Control host, string key, string title, string icon)
    {
        var b = new RoundButton { Text = $"{icon}     {title}", Width = 214, Height = 48, Radius = 12, FlatStyle = FlatStyle.Flat, BackColor = Sidebar, ForeColor = Muted, Font = new Font("Segoe UI", 10.2f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(10, 0, 14, 0), Cursor = Cursors.Hand, Margin = new Padding(0, 0, 0, 6), RightToLeft = RightToLeft.Yes };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (_, _) => ShowPage(key);
        _nav[key] = b;
        host.Controls.Add(b);
    }

    private Control BuildMainArea()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildTopbar(), 0, 0);
        root.Controls.Add(_workspace, 0, 1);
        return root;
    }

    private Control BuildTopbar()
    {
        var bar = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, ColumnCount = 2, RowCount = 1, Padding = new Padding(22, 12, 22, 8) };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var chip = new RoundedPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 10, 8), Radius = 14, BackColor = Card, BorderColor = Border, Padding = new Padding(10) };
        _topScaleStatus.Dock = DockStyle.Fill; _topScaleStatus.TextAlign = ContentAlignment.MiddleCenter;
        chip.Controls.Add(_topScaleStatus);

        var titles = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, ColumnCount = 1, RowCount = 2 };
        titles.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); titles.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _pageTitle.Dock = DockStyle.Fill; _pageTitle.TextAlign = ContentAlignment.MiddleRight;
        _pageSubtitle.Dock = DockStyle.Fill; _pageSubtitle.TextAlign = ContentAlignment.TopRight;
        titles.Controls.Add(_pageTitle, 0, 0); titles.Controls.Add(_pageSubtitle, 0, 1);
        bar.Controls.Add(chip, 0, 0); bar.Controls.Add(titles, 1, 0);
        return bar;
    }

    private void ShowPage(string key)
    {
        foreach (var p in _nav)
        {
            var active = p.Key == key;
            p.Value.BackColor = active ? Color.FromArgb(48, 38, 18) : Sidebar;
            p.Value.ForeColor = active ? GoldSoft : Muted;
            p.Value.FlatAppearance.BorderSize = active ? 1 : 0;
            p.Value.FlatAppearance.BorderColor = Gold;
        }

        _workspace.SuspendLayout();
        _workspace.Controls.Clear();
        Control page;
        switch (key)
        {
            case "entries": _pageTitle.Text = "آبشده‌ها"; _pageSubtitle.Text = "ثبت دستی یا دریافت وزن از ترازو و مدیریت لیست"; page = BuildEntriesPage(); break;
            case "calculations": _pageTitle.Text = "محاسبات عیار"; _pageSubtitle.Text = "افزایش و کاهش عیار با دو فرمول مستقل"; page = BuildCalculationsPage(); break;
            case "quick": _pageTitle.Text = "محاسبه سریع"; _pageSubtitle.Text = "تقسیم ۳۶.۷۹٪ و اصلاح وزن برای افت عیار"; page = BuildQuickPage(); break;
            case "reports": _pageTitle.Text = "گزارش‌ها"; _pageSubtitle.Text = "ذخیره خروجی کامل در مسیر ثابت"; page = BuildReportsPage(); break;
            case "settings": _pageTitle.Text = "تنظیمات"; _pageSubtitle.Text = "گزارش، ترازو و رفتار دریافت وزن"; page = BuildSettingsPage(); break;
            default: key = "dashboard"; _pageTitle.Text = "داشبورد"; _pageSubtitle.Text = "نمای کلی وزن، عیار و عملیات پرکاربرد"; page = BuildDashboardPage(); break;
        }
        page.Dock = DockStyle.Fill;
        _workspace.Controls.Add(page);
        _workspace.ResumeLayout(true);
        RefreshAll();
    }

    private Control BuildDashboardPage()
    {
        var outer = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterWidth = 6, BackColor = Border, FixedPanel = FixedPanel.None, Panel1MinSize = 700, Panel2MinSize = 300 };
        _dashboardOuter = outer;
        outer.Resize += (_, _) =>
        {
            if (outer.Width > 1100 && outer.SplitterDistance <= 0)
                outer.SplitterDistance = Math.Max(700, outer.Width - 360);
        };
        outer.SplitterMoved += (_, _) => { _settings.DashboardSettingsWidth = outer.Panel2.Width; TrySaveSettingsSilently(); };

        var left = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, ColumnCount = 1, RowCount = 2, Padding = new Padding(18, 4, 10, 18) };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.Controls.Add(BuildMetricsStrip(), 0, 0);

        var vertical = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6, BackColor = Border, Panel1MinSize = 210, Panel2MinSize = 230 };
        _dashboardMainVertical = vertical;
        vertical.SplitterMoved += (_, _) => { _settings.DashboardEntryHeight = vertical.Panel1.Height; TrySaveSettingsSilently(); };
        vertical.Panel1.Controls.Add(BuildQuickEntryDashboardCard());
        vertical.Panel2.Controls.Add(BuildBottomDashboard());
        left.Controls.Add(vertical, 0, 1);
        outer.Panel1.Controls.Add(left);

        var settingsHost = new Panel { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(8, 4, 18, 18) };
        settingsHost.Controls.Add(BuildScaleSettingsCard(true));
        outer.Panel2.Controls.Add(settingsHost);

        outer.HandleCreated += (_, _) =>
        {
            BeginInvoke((Action)(() =>
            {
                if (_dashboardOuter is not null && _dashboardOuter.Width > 1050)
                {
                    var right = Math.Clamp(_settings.DashboardSettingsWidth, 310, Math.Min(470, _dashboardOuter.Width / 2));
                    _dashboardOuter.SplitterDistance = Math.Max(700, _dashboardOuter.Width - right);
                }
                if (_dashboardMainVertical is not null && _dashboardMainVertical.Height > 520)
                    _dashboardMainVertical.SplitterDistance = Math.Clamp(_settings.DashboardEntryHeight, 235, Math.Max(235, _dashboardMainVertical.Height - 250));
            }));
        };
        return outer;
    }

    private Control BuildMetricsStrip()
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, ColumnCount = 4, RowCount = 1, Margin = Padding.Empty };
        for (int i = 0; i < 4; i++) row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        row.Controls.Add(MetricCard("وزن کل (g)", _totalWeight, "⚖"), 0, 0);
        row.Controls.Add(MetricCard("عیار میانگین (‰)", _avgAssay, "Au"), 1, 0);
        row.Controls.Add(MetricCard("تعداد آبشده‌ها", _count, "◆"), 2, 0);
        row.Controls.Add(MetricCard("کل بار مورد نیاز (g)", _totalAlloy, "∑"), 3, 0);
        return row;
    }

    private Control BuildQuickEntryDashboardCard()
    {
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, ColumnCount = 1, RowCount = 3, Padding = Padding.Empty };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 84)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var fields = TwoColumns(56, 44, Card); fields.Controls.Add(Field("وزن آبشده (g)", _weight), 0, 0); fields.Controls.Add(Field("عیار آبشده", _assay), 1, 0); body.Controls.Add(fields, 0, 0);
        _entryScaleHint.Dock = DockStyle.Fill; _entryScaleHint.TextAlign = ContentAlignment.MiddleRight; body.Controls.Add(_entryScaleHint, 0, 1);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, ColumnCount = 3, RowCount = 1, Padding = new Padding(0, 4, 0, 0) };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 17));
        _saveEntry.Dock = DockStyle.Fill;
        var read = Secondary("خواندن از ترازو ↑"); read.Dock = DockStyle.Fill; read.Click += async (_, _) => await ReadScaleIntoWeightAsync();
        var clear = Secondary("پاک کردن"); clear.Dock = DockStyle.Fill; clear.ForeColor = Danger; clear.Click += (_, _) => { _weight.Clear(); _assay.Clear(); _weight.Focus(); };
        actions.Controls.Add(_saveEntry, 0, 0); actions.Controls.Add(read, 1, 0); actions.Controls.Add(clear, 2, 0); body.Controls.Add(actions, 0, 2);
        return CardWithHeader("ثبت سریع آبشده", "وزن را دستی وارد کن یا کلید ↑ را برای خواندن ترازو بزن.", body);
    }

    private Control BuildBottomDashboard()
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        row.Controls.Add(BuildRaiseCard(), 0, 0); row.Controls.Add(BuildLowerCard(), 1, 0); row.Controls.Add(BuildRecentCard(), 2, 0);
        return row;
    }

    private Control BuildRecentCard()
    {
        _recentHost.Controls.Clear();
        return CardWithHeader("آخرین آبشده‌ها", "آخرین ثبت‌های انجام‌شده", _recentHost);
    }

    private Control BuildScaleSettingsCard(bool compact)
    {
        CreateSettingsControls();
        LoadSettingsControls();
        var body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Card, Padding = new Padding(2, 2, 2, 8) };
        var stack = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Card, RightToLeft = RightToLeft.Yes };
        body.Controls.Add(stack);

        stack.Controls.Add(SectionTitle("اتصال ترازو"));
        stack.Controls.Add(SettingsGrid(("COM Port", _sPort!), ("Baud Rate", _sBaud!), ("Data Bits", _sDataBits!), ("Parity", _sParity!), ("Stop Bits", _sStopBits!), ("Flow Control", _sFlow!)));
        stack.Controls.Add(SectionTitle("خواندن وزن"));
        stack.Controls.Add(_sAuto!); stack.Controls.Add(_sStableOnly!); stack.Controls.Add(_sUp!); stack.Controls.Add(_sPrint!);
        stack.Controls.Add(SettingsGrid(("تعداد قرائت پایدار", _sStableSamples!), ("تلرانس (g)", _sTolerance!), ("مهلت (ms)", _sTimeout!)));
        stack.Controls.Add(SectionTitle("فرمان درخواست وزن"));
        stack.Controls.Add(SettingsGrid(("فرمان", _sQuery!), ("پایان فرمان", _sEnding!)));
        stack.Controls.Add(_sSendQuery!);
        _sTestStatus!.Width = 290; _sTestStatus.Height = 34; _sTestStatus.TextAlign = ContentAlignment.MiddleRight; stack.Controls.Add(_sTestStatus);

        var test = Secondary("تست دریافت وزن"); test.Width = 290; test.Height = 44; test.Click += async (_, _) => await TestScaleAsync(test); stack.Controls.Add(test);
        var save = Primary("ذخیره تنظیمات"); save.Width = 290; save.Height = 46; save.Click += (_, _) => SaveScaleSettings(); stack.Controls.Add(save);
        var reset = Secondary("بازنشانی A&D"); reset.Width = 290; reset.Height = 42; reset.Click += (_, _) => ResetScaleDefaults(); stack.Controls.Add(reset);

        stack.SizeChanged += (_, _) => { foreach (Control c in stack.Controls) if (c is not CheckBox && c is not Label) c.Width = Math.Max(290, body.ClientSize.Width - 28); };
        var card = CardWithHeader("تنظیمات ترازو", "RS-232 / COM • خواندن پایدار و سریع", body);
        if (compact) card.MinimumSize = new Size(300, 500);
        return card;
    }

    private Control BuildEntriesPage()
    {
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6, BackColor = Border, Panel1MinSize = 220, Panel2MinSize = 220, SplitterDistance = 280 };
        var p1 = new Panel { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(20, 6, 20, 8) }; p1.Controls.Add(BuildQuickEntryDashboardCard());
        var p2 = new Panel { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(20, 8, 20, 20) }; _grid.Dock = DockStyle.Fill; p2.Controls.Add(CardWithHeader("لیست آبشده‌ها", "ویرایش و حذف آبشده‌های ثبت‌شده", _grid));
        split.Panel1.Controls.Add(p1); split.Panel2.Controls.Add(p2); return split;
    }

    private Control BuildCalculationsPage()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, ColumnCount = 1, RowCount = 2, Padding = new Padding(20, 6, 20, 20) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 126)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildMetricsStrip(), 0, 0);
        var row = TwoColumns(50, 50); row.Controls.Add(BuildRaiseCard(), 0, 0); row.Controls.Add(BuildLowerCard(), 1, 0); root.Controls.Add(row, 0, 1); return root;
    }

    private Control BuildQuickPage()
    {
        var root = new Panel { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(20, 8, 20, 20) };
        var row = TwoColumns(50, 50); row.Controls.Add(BuildSplitCard(), 0, 0); row.Controls.Add(BuildCorrectionCard(), 1, 0); root.Controls.Add(row); return root;
    }

    private Control BuildReportsPage()
    {
        var root = new Panel { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(80, 70, 80, 70) };
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, RowCount = 4, ColumnCount = 1, Padding = new Padding(6) };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 74)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var t = L("مسیر ذخیره فعلی", 9.5f, Muted, true); t.Dock = DockStyle.Fill; t.TextAlign = ContentAlignment.BottomRight;
        var path = L(_settings.ReportFolder, 11, TextMain, true); path.Dock = DockStyle.Fill; path.TextAlign = ContentAlignment.TopRight; path.AutoEllipsis = true;
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = Card };
        var save = Primary("ذخیره گزارش کامل"); save.Width = 210; save.Click += (_, _) => SaveReport();
        var settings = Secondary("تنظیم مسیر"); settings.Width = 150; settings.Click += (_, _) => ShowPage("settings"); actions.Controls.Add(save); actions.Controls.Add(settings);
        var note = L("گزارش شامل آبشده‌ها و نتیجه‌های نهایی است و فرمول‌ها داخل فایل نوشته نمی‌شوند.", 9.3f, Muted, false); note.Dock = DockStyle.Fill; note.TextAlign = ContentAlignment.TopRight;
        body.Controls.Add(t, 0, 0); body.Controls.Add(path, 0, 1); body.Controls.Add(actions, 0, 2); body.Controls.Add(note, 0, 3);
        root.Controls.Add(CardWithHeader("گزارش‌ها", "خروجی تاریخ‌دار و مرتب", body)); return root;
    }

    private Control BuildSettingsPage()
    {
        CreateSettingsControls(); LoadSettingsControls();
        var root = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterWidth = 6, BackColor = Border, Panel1MinSize = 500, Panel2MinSize = 350, SplitterDistance = 720 };
        var reportPanel = new Panel { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(20) };
        var reportBody = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, RowCount = 3, ColumnCount = 1, Padding = new Padding(8) };
        reportBody.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); reportBody.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); reportBody.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var label = L("مسیر ثابت ذخیره گزارش", 9.5f, Muted, true); label.Dock = DockStyle.Fill;
        var folderRow = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, ColumnCount = 2 }; folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75)); folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        _sReportFolder!.Dock = DockStyle.Fill; var browse = Secondary("انتخاب پوشه…"); browse.Dock = DockStyle.Fill; browse.Click += (_, _) => BrowseReportFolder(); folderRow.Controls.Add(_sReportFolder, 0, 0); folderRow.Controls.Add(browse, 1, 0);
        var reportSave = Primary("ذخیره مسیر گزارش"); reportSave.Dock = DockStyle.Top; reportSave.Height = 46; reportSave.Click += (_, _) => SaveReportFolder();
        reportBody.Controls.Add(label, 0, 0); reportBody.Controls.Add(folderRow, 0, 1); reportBody.Controls.Add(reportSave, 0, 2);
        reportPanel.Controls.Add(CardWithHeader("گزارش", "مسیر را یک‌بار تعیین کن", reportBody));
        root.Panel1.Controls.Add(reportPanel);
        var scalePanel = new Panel { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(8, 20, 20, 20) }; scalePanel.Controls.Add(BuildScaleSettingsCard(false)); root.Panel2.Controls.Add(scalePanel); return root;
    }

    private Control BuildRaiseCard()
    {
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, RowCount = 3, ColumnCount = 1 };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 74)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 78)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var fields = TwoColumns(50, 50, Card); fields.Controls.Add(Field("عیار هدف", _raiseTarget), 0, 0); fields.Controls.Add(Field("عیار شمش", _barAssay), 1, 0); body.Controls.Add(fields, 0, 0);
        var metrics = TwoColumns(50, 50, Card); metrics.Controls.Add(MiniMetric("اختلاف تا هدف", _raiseDiff), 0, 0); metrics.Controls.Add(MiniMetric("شمش مورد نیاز (g)", _raiseNeed), 1, 0); body.Controls.Add(metrics, 0, 1);
        _raiseState.Dock = DockStyle.Fill; body.Controls.Add(_raiseState, 0, 2); return CardWithHeader("بالا بردن عیار", "با شمش عیار بالا", body);
    }

    private Control BuildLowerCard()
    {
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, RowCount = 4, ColumnCount = 1 };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 70)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 66)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 66)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var fields = TwoColumns(50, 50, Card); fields.Controls.Add(Field("عیار هدف", _lowerTarget), 0, 0); fields.Controls.Add(Field("درصد نقره", _silver), 1, 0); body.Controls.Add(fields, 0, 0);
        var m1 = TwoColumns(50, 50, Card); m1.Controls.Add(MiniMetric("کل بار (g)", _alloy), 0, 0); m1.Controls.Add(MiniMetric("نقره (g)", _silverNeed), 1, 0); body.Controls.Add(m1, 0, 1);
        var m2 = TwoColumns(50, 50, Card); m2.Controls.Add(MiniMetric("بار بدون نقره (g)", _otherAlloy), 0, 0); m2.Controls.Add(MiniMetric("وزن پس از بار (g)", _afterAlloy), 1, 0); body.Controls.Add(m2, 0, 2);
        _lowerState.Dock = DockStyle.Fill; body.Controls.Add(_lowerState, 0, 3); return CardWithHeader("پایین آوردن عیار", "با بار ریخته‌گری", body);
    }

    private Control BuildSplitCard()
    {
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, RowCount = 2, ColumnCount = 1 };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 92)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); body.Controls.Add(Field("عدد پایه", _splitBase), 0, 0);
        var metrics = TwoColumns(50, 50, Card); metrics.Controls.Add(MiniMetric("۳۶.۷۹٪", _splitA), 0, 0); metrics.Controls.Add(MiniMetric("۶۳.۲۱٪", _splitB), 1, 0); body.Controls.Add(metrics, 0, 1); return CardWithHeader("تقسیم ۳۶.۷۹٪ / ۶۳.۲۱٪", "محاسبه لحظه‌ای", body);
    }

    private Control BuildCorrectionCard()
    {
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, RowCount = 3, ColumnCount = 1 };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 88)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 88)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var top = TwoColumns(50, 50, Card); top.Controls.Add(Field("وزن پایه", _corrWeight), 0, 0); top.Controls.Add(Field("عیار هدف", _corrTarget), 1, 0); body.Controls.Add(top, 0, 0); body.Controls.Add(Field("مقدار افت عیار", _corrDrop), 0, 1);
        var m = TwoColumns(50, 50, Card); m.Controls.Add(MiniMetric("بار افزوده (g)", _corrAdd), 0, 0); m.Controls.Add(MiniMetric("جمع وزن (g)", _corrTotal), 1, 0); body.Controls.Add(m, 0, 2); return CardWithHeader("اصلاح وزن برای افت عیار", "مطابق ابزار سریع", body);
    }

    // ---------- Settings ----------
    private void CreateSettingsControls()
    {
        if (_sPort is not null) return;
        _sModel = Combo(); _sPort = Combo(); _sBaud = Combo(); _sDataBits = Combo(); _sParity = Combo(); _sStopBits = Combo(); _sFlow = Combo(); _sEnding = Combo();
        _sQuery = Input(); _sReportFolder = Input(); _sTimeout = NumBox(500, 10000, 0); _sStableSamples = NumBox(2, 10, 0); _sTolerance = NumBox(0.001m, 5m, 3);
        _sAuto = Check("خواندن خودکار وزن (پیش‌فرض خاموش)"); _sStableOnly = Check("در حالت خودکار فقط وزن پایدار پذیرفته شود"); _sUp = Check("پاسخ به کلید ↑ در فیلد وزن"); _sPrint = Check("دریافت PRINT ترازو"); _sSendQuery = Check("با ↑ فرمان درخواست وزن ارسال شود");
        _sTestStatus = L("آماده تست", 8.8f, Muted, true);
        _sModel.Items.AddRange(new object[] { "A&D", "Custom / Generic" });
        foreach (var p in SerialPort.GetPortNames().OrderBy(x => x)) _sPort.Items.Add(p); if (!_sPort.Items.Contains("COM1")) _sPort.Items.Add("COM1");
        _sBaud.Items.AddRange(new object[] { "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200" }); _sDataBits.Items.AddRange(new object[] { "7", "8" });
        _sParity.Items.AddRange(Enum.GetNames<Parity>()); _sStopBits.Items.AddRange(new object[] { nameof(StopBits.One), nameof(StopBits.OnePointFive), nameof(StopBits.Two) }); _sFlow.Items.AddRange(Enum.GetNames<Handshake>()); _sEnding.Items.AddRange(new object[] { "CRLF", "CR", "LF", "None" });
    }

    private void LoadSettingsControls()
    {
        if (_sPort is null) return;
        Select(_sModel!, _settings.ScaleModel); Select(_sPort!, _settings.PortName); Select(_sBaud!, _settings.BaudRate.ToString()); Select(_sDataBits!, _settings.DataBits.ToString()); Select(_sParity!, _settings.Parity); Select(_sStopBits!, _settings.StopBits); Select(_sFlow!, _settings.Handshake); Select(_sEnding!, _settings.QueryLineEnding);
        _sQuery!.Text = _settings.QueryCommand; _sReportFolder!.Text = _settings.ReportFolder; _sTimeout!.Value = Math.Clamp(_settings.ReadTimeoutMs, (int)_sTimeout.Minimum, (int)_sTimeout.Maximum); _sStableSamples!.Value = Math.Clamp(_settings.StableSampleCount, (int)_sStableSamples.Minimum, (int)_sStableSamples.Maximum); _sTolerance!.Value = (decimal)Math.Clamp(_settings.StableToleranceGrams, (double)_sTolerance.Minimum, (double)_sTolerance.Maximum);
        _sAuto!.Checked = _settings.AutoRead; _sStableOnly!.Checked = _settings.StableAutoReadOnly; _sUp!.Checked = _settings.ReadOnUpArrow; _sPrint!.Checked = _settings.ReceivePrintKey; _sSendQuery!.Checked = _settings.SendQueryOnUpArrow;
    }

    private void SaveScaleSettings()
    {
        if (_sPort is null) return;
        _settings.ScaleModel = _sModel!.Text.Length == 0 ? "A&D" : _sModel.Text; _settings.PortName = _sPort.Text.Length == 0 ? "COM1" : _sPort.Text; _settings.BaudRate = int.TryParse(_sBaud!.Text, out var br) ? br : 2400; _settings.DataBits = int.TryParse(_sDataBits!.Text, out var db) ? db : 7; _settings.Parity = _sParity!.Text; _settings.StopBits = _sStopBits!.Text; _settings.Handshake = _sFlow!.Text;
        _settings.AutoRead = _sAuto!.Checked; _settings.StableAutoReadOnly = _sStableOnly!.Checked; _settings.ReadOnUpArrow = _sUp!.Checked; _settings.ReceivePrintKey = _sPrint!.Checked; _settings.SendQueryOnUpArrow = _sSendQuery!.Checked; _settings.QueryCommand = _sQuery!.Text; _settings.QueryLineEnding = _sEnding!.Text; _settings.ReadTimeoutMs = (int)_sTimeout!.Value; _settings.StableSampleCount = (int)_sStableSamples!.Value; _settings.StableToleranceGrams = (double)_sTolerance!.Value;
        try { _settings.Save(); ApplyScaleSettings(); _sTestStatus!.Text = "✓ تنظیمات ذخیره شد"; _sTestStatus.ForeColor = Success; } catch (Exception ex) { MessageBox.Show(this, ex.Message, "خطای ذخیره تنظیمات", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ResetScaleDefaults()
    {
        if (_sPort is null) return;
        Select(_sModel!, "A&D"); Select(_sPort!, "COM1"); Select(_sBaud!, "2400"); Select(_sDataBits!, "7"); Select(_sParity!, nameof(Parity.Even)); Select(_sStopBits!, nameof(StopBits.Two)); Select(_sFlow!, nameof(Handshake.None));
        _sAuto!.Checked = false; _sStableOnly!.Checked = true; _sUp!.Checked = true; _sPrint!.Checked = true; _sSendQuery!.Checked = true; _sStableSamples!.Value = 3; _sTolerance!.Value = 0.02m; _sQuery!.Text = "Q"; Select(_sEnding!, "CRLF"); _sTimeout!.Value = 1800;
    }

    private async Task TestScaleAsync(Button button)
    {
        SaveScaleSettings(); button.Enabled = false; _sTestStatus!.Text = "در حال دریافت…"; _sTestStatus.ForeColor = GoldSoft;
        try { var w = await _scale.ReadNowAsync(); _sTestStatus.Text = "✓ وزن دریافتی: " + Num(w) + " g"; _sTestStatus.ForeColor = Success; UpdateScaleDisplay(w); }
        catch (Exception ex) { _sTestStatus.Text = "خطا: " + ex.Message; _sTestStatus.ForeColor = Danger; }
        finally { button.Enabled = true; }
    }

    private void BrowseReportFolder()
    {
        using var dlg = new FolderBrowserDialog { Description = "پوشه گزارش‌های Gold Bar را انتخاب کن", UseDescriptionForTitle = true }; if (Directory.Exists(_sReportFolder!.Text)) dlg.SelectedPath = _sReportFolder.Text; if (dlg.ShowDialog(this) == DialogResult.OK) _sReportFolder.Text = dlg.SelectedPath;
    }
    private void SaveReportFolder() { _settings.ReportFolder = _sReportFolder!.Text.Trim(); try { _settings.Save(); MessageBox.Show(this, "مسیر گزارش ذخیره شد.", "Gold Bar", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error); } }

    // ---------- Events & data ----------
    private void BindEvents()
    {
        _saveEntry.Click += (_, _) => SaveEntry();
        _weight.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Up && _settings.ReadOnUpArrow) { e.SuppressKeyPress = true; await ReadScaleIntoWeightAsync(); } else if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _assay.Focus(); _assay.SelectAll(); } };
        _assay.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SaveEntry(); } };
        foreach (var b in new[] { _raiseTarget, _barAssay, _lowerTarget, _silver, _splitBase, _corrWeight, _corrTarget, _corrDrop }) { b.TextChanged += (_, _) => Recalculate(); b.Enter += (_, _) => b.SelectAll(); }
        _scale.WeightReceived += v => Ui(() => { UpdateScaleDisplay(v); if (_settings.AutoRead && _weight.Focused) { _weight.Text = Num(v); _weight.SelectAll(); } });
        _scale.StatusChanged += (t, ok) => Ui(() => { _topScaleStatus.Text = ok ? "●  " + t : t; _topScaleStatus.ForeColor = ok ? Success : Muted; _sideScaleState.Text = ok ? "●  متصل" : "●  آماده"; _sideScaleState.ForeColor = ok ? Success : Muted; });
    }

    private void UpdateScaleDisplay(double v) { _sideWeight.Text = Num(v) + " g"; _topScaleStatus.Text = "●  ترازو متصل • " + Num(v) + " g"; _topScaleStatus.ForeColor = Success; _sideScaleState.Text = "●  متصل"; _sideScaleState.ForeColor = Success; _entryScaleHint.Text = "●  وزن دریافتی: " + Num(v) + " g"; _entryScaleHint.ForeColor = Success; }
    private void ApplyScaleSettings() { _scale.ApplySettings(_settings, _settings.AutoRead); _topScaleStatus.Text = "ترازو • " + _settings.PortName + (_settings.AutoRead ? " • Auto" : " • دستی"); _topScaleStatus.ForeColor = Muted; }

    private async Task ReadScaleIntoWeightAsync()
    {
        try { _entryScaleHint.Text = "●  در حال دریافت وزن…"; _entryScaleHint.ForeColor = GoldSoft; var w = await _scale.ReadNowAsync(); _weight.Text = Num(w); _weight.Focus(); _weight.SelectAll(); UpdateScaleDisplay(w); }
        catch (Exception ex) { _entryScaleHint.Text = "●  دریافت وزن ناموفق"; _entryScaleHint.ForeColor = Danger; MessageBox.Show(this, ex.Message + "\n\nتنظیمات Port و Baud Rate را بررسی کن.", "ترازو", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void SaveEntry()
    {
        var w = Parse(_weight.Text, -1); var a = Parse(_assay.Text, -1); if (w <= 0 || a <= 0 || a > 1000) { MessageBox.Show(this, "وزن و عیار را صحیح وارد کن.", "ورودی نامعتبر", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        var item = new GoldEntry(w, a); if (_editingIndex >= 0 && _editingIndex < _entries.Count) _entries[_editingIndex] = item; else _entries.Add(item); _editingIndex = -1; _saveEntry.Text = "ثبت آبشده"; _weight.Clear(); _assay.Clear(); PersistEntries(); RefreshAll(); _weight.Focus();
    }
    private void EditEntry(int i) { if (i < 0 || i >= _entries.Count) return; ShowPage("entries"); _editingIndex = i; _weight.Text = Num(_entries[i].Weight); _assay.Text = Num(_entries[i].Assay); _saveEntry.Text = "ذخیره تغییرات"; _weight.Focus(); }
    private void DeleteEntry(int i) { if (i < 0 || i >= _entries.Count) return; _entries.RemoveAt(i); PersistEntries(); RefreshAll(); }
    private void ClearAll() { if (_entries.Count == 0) return; if (MessageBox.Show(this, "همه آبشده‌ها حذف شوند؟", "پاک‌کردن همه", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return; _entries.Clear(); PersistEntries(); RefreshAll(); }

    private void Recalculate()
    {
        var s = GoldCalculator.Summarize(_entries); _totalWeight.Text = Num(s.Weight); _avgAssay.Text = Num(s.AverageAssay); _count.Text = s.Count.ToString(CultureInfo.InvariantCulture);
        var rt = Parse(_raiseTarget.Text, 747); var high = Parse(_barAssay.Text, 995); var raise = GoldCalculator.RequiredHighAssayBar(s, rt, high); _raiseDiff.Text = Num(raise.DifferenceNeeded); _raiseNeed.Text = Num(raise.RequiredHighBar); _raiseState.Text = !double.IsFinite(raise.RequiredHighBar) ? "ابتدا آبشده معتبر ثبت کن." : raise.RequiredHighBar > 0 ? $"نیاز: {Num(raise.RequiredHighBar)} g شمش {Num(high)}" : "افزایش عیار لازم نیست.";
        var lt = Parse(_lowerTarget.Text, 746); var sp = Parse(_silver.Text, 32); var lower = GoldCalculator.RequiredAlloy(s, lt, sp, s.Weight); _alloy.Text = Num(lower.TotalAlloyRequired); _totalAlloy.Text = Num(lower.TotalAlloyRequired); _silverNeed.Text = Num(lower.SilverRequired); _otherAlloy.Text = Num(lower.NonSilverRequired); _afterAlloy.Text = Num(lower.TotalAfterAlloy); _lowerState.Text = !double.IsFinite(lower.TotalAlloyRequired) ? "ابتدا آبشده معتبر ثبت کن." : lower.TotalAlloyRequired > 0 ? $"کل بار مورد نیاز: {Num(lower.TotalAlloyRequired)} g" : "کاهش عیار لازم نیست.";
        var bv = Parse(_splitBase.Text, 800); var p = GoldCalculator.Split3679(bv); _splitA.Text = Num(p); _splitB.Text = Num(bv - p); var cw = Parse(_corrWeight.Text, 250); var ct = Parse(_corrTarget.Text, 750); var cd = Parse(_corrDrop.Text, 1); var add = GoldCalculator.CorrectionAddition(cw, ct, cd); _corrAdd.Text = Num(add); _corrTotal.Text = Num(cw + add);
    }

    private void RefreshAll()
    {
        Recalculate();
        if (_grid.Columns.Count > 0) { _grid.Rows.Clear(); for (int i = 0; i < _entries.Count; i++) _grid.Rows.Add(i + 1, Num(_entries[i].Weight), Num(_entries[i].Assay), "ویرایش", "حذف"); }
        RefreshRecent();
    }

    private void RefreshRecent()
    {
        _recentHost.Controls.Clear(); var recent = _entries.Select((e, i) => (e, i)).Reverse().Take(5).ToList(); if (recent.Count == 0) { var empty = L("هنوز آبشده‌ای ثبت نشده است.", 9, Muted, false); empty.Width = 320; empty.Height = 40; _recentHost.Controls.Add(empty); return; }
        foreach (var x in recent) { var row = new TableLayoutPanel { Width = 340, Height = 46, ColumnCount = 3, BackColor = Card2, Margin = new Padding(0, 0, 0, 5), Padding = new Padding(8, 4, 8, 4) }; row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22)); var w = L(Num(x.e.Weight) + " g", 9, TextMain, true); var a = L("عیار " + Num(x.e.Assay), 9, GoldSoft, true); var ed = Secondary("ویرایش"); ed.Dock = DockStyle.Fill; int idx = x.i; ed.Click += (_, _) => EditEntry(idx); w.Dock = a.Dock = DockStyle.Fill; row.Controls.Add(w, 0, 0); row.Controls.Add(a, 1, 0); row.Controls.Add(ed, 2, 0); _recentHost.Controls.Add(row); }
    }

    private void SaveReport()
    {
        try { if (string.IsNullOrWhiteSpace(_settings.ReportFolder)) { ShowPage("settings"); return; } Directory.CreateDirectory(_settings.ReportFolder); var s = GoldCalculator.Summarize(_entries); var raise = GoldCalculator.RequiredHighAssayBar(s, Parse(_raiseTarget.Text, 747), Parse(_barAssay.Text, 995)); var lower = GoldCalculator.RequiredAlloy(s, Parse(_lowerTarget.Text, 746), Parse(_silver.Text, 32), s.Weight); var b = new StringBuilder(); b.AppendLine("GOLD BAR (by:Amirnourhan)"); b.AppendLine("تاریخ و ساعت: " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")); b.AppendLine(); b.AppendLine("آبشده‌ها:"); for (int i = 0; i < _entries.Count; i++) b.AppendLine($"{i + 1}) وزن {Num(_entries[i].Weight)} g | عیار {Num(_entries[i].Assay)}"); b.AppendLine(); b.AppendLine($"وزن کل: {Num(s.Weight)} g | عیار میانگین: {Num(s.AverageAssay)} | تعداد: {s.Count}"); b.AppendLine($"وزن پس از بار: {Num(lower.TotalAfterAlloy)} g"); b.AppendLine($"شمش عیار بالا مورد نیاز: {Num(raise.RequiredHighBar)} g"); b.AppendLine($"کل بار مورد نیاز: {Num(lower.TotalAlloyRequired)} g | نقره: {Num(lower.SilverRequired)} g | بار بدون نقره: {Num(lower.NonSilverRequired)} g"); var split = GoldCalculator.Split3679(Parse(_splitBase.Text, 800)); b.AppendLine($"محاسبه سریع: 36.79% = {Num(split)} | 63.21% = {Num(Parse(_splitBase.Text, 800) - split)}"); b.AppendLine($"اصلاح افت عیار: بار افزوده {_corrAdd.Text} g | جمع وزن {_corrTotal.Text} g"); var path = Path.Combine(_settings.ReportFolder, "GoldBar_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt"); File.WriteAllText(path, b.ToString(), Encoding.UTF8); MessageBox.Show(this, "گزارش ذخیره شد:\n" + path, "گزارش", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        catch (Exception ex) { MessageBox.Show(this, "ذخیره گزارش انجام نشد:\n" + ex.Message, "خطای گزارش", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void LoadEntries() { try { if (!File.Exists(DataPath)) return; var loaded = JsonSerializer.Deserialize<List<GoldEntry>>(File.ReadAllText(DataPath)); if (loaded is not null) _entries.AddRange(loaded.Where(x => x.Weight > 0 && x.Assay > 0 && x.Assay <= 1000)); } catch { } }
    private void PersistEntries() { try { Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!); File.WriteAllText(DataPath, JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true })); } catch { } }
    private void TrySaveSettingsSilently() { try { _settings.Save(); } catch { } }
    private void Ui(Action a) { if (IsDisposed || !IsHandleCreated) return; try { BeginInvoke(a); } catch { } }

    private void ConfigureGrid()
    {
        _grid.BackgroundColor = Card; _grid.BorderStyle = BorderStyle.None; _grid.GridColor = Border; _grid.EnableHeadersVisualStyles = false; _grid.ColumnHeadersDefaultCellStyle.BackColor = Card2; _grid.ColumnHeadersDefaultCellStyle.ForeColor = TextMain; _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter; _grid.ColumnHeadersHeight = 44; _grid.DefaultCellStyle.BackColor = Card; _grid.DefaultCellStyle.ForeColor = TextMain; _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(58, 46, 22); _grid.DefaultCellStyle.SelectionForeColor = TextMain; _grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter; _grid.RowTemplate.Height = 44; _grid.RowHeadersVisible = false; _grid.AllowUserToAddRows = false; _grid.AllowUserToDeleteRows = false; _grid.AllowUserToResizeRows = false; _grid.ReadOnly = true; _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _grid.RightToLeft = RightToLeft.Yes;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "No", HeaderText = "#", FillWeight = 10 }); _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Weight", HeaderText = "وزن (g)", FillWeight = 28 }); _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Assay", HeaderText = "عیار", FillWeight = 24 }); _grid.Columns.Add(new DataGridViewButtonColumn { Name = "Edit", HeaderText = "", Text = "ویرایش", UseColumnTextForButtonValue = true, FillWeight = 19, FlatStyle = FlatStyle.Flat }); _grid.Columns.Add(new DataGridViewButtonColumn { Name = "Delete", HeaderText = "", Text = "حذف", UseColumnTextForButtonValue = true, FillWeight = 19, FlatStyle = FlatStyle.Flat });
        _grid.CellContentClick += (_, e) => { if (e.RowIndex < 0) return; var n = _grid.Columns[e.ColumnIndex].Name; if (n == "Edit") EditEntry(e.RowIndex); else if (n == "Delete") DeleteEntry(e.RowIndex); };
    }

    // ---------- UI helpers ----------
    private static Control CardWithHeader(string title, string subtitle, Control body)
    {
        var card = new RoundedPanel { Dock = DockStyle.Fill, Margin = new Padding(6), Padding = new Padding(14), Radius = 18, BackColor = Card, BorderColor = Border };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty }; layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var h = L(title, 13, TextMain, true); h.Dock = DockStyle.Fill; h.TextAlign = ContentAlignment.MiddleRight; var s = L(subtitle, 8.8f, Muted, false); s.Dock = DockStyle.Fill; s.TextAlign = ContentAlignment.TopRight; body.Dock = DockStyle.Fill; layout.Controls.Add(h, 0, 0); layout.Controls.Add(s, 0, 1); layout.Controls.Add(body, 0, 2); card.Controls.Add(layout); return card;
    }
    private static Control MetricCard(string title, Label value, string icon)
    {
        var c = new RoundedPanel { Dock = DockStyle.Fill, Margin = new Padding(6), Padding = new Padding(12), Radius = 16, BackColor = Card, BorderColor = Border }; var row = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, ColumnCount = 2, RowCount = 2 }; row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48)); row.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); row.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); var t = L(title, 8.8f, Muted, false); t.Dock = DockStyle.Fill; var ic = L(icon, 13, GoldSoft, true); ic.Dock = DockStyle.Fill; ic.TextAlign = ContentAlignment.MiddleCenter; value.Dock = DockStyle.Fill; value.TextAlign = ContentAlignment.MiddleRight; row.Controls.Add(t, 0, 0); row.Controls.Add(ic, 1, 0); row.Controls.Add(value, 0, 1); row.SetColumnSpan(value, 2); c.Controls.Add(row); return c;
    }
    private static Control MiniMetric(string title, Label value) { var c = new RoundedPanel { Dock = DockStyle.Fill, Margin = new Padding(4), Padding = new Padding(8), Radius = 12, BackColor = Card2, BorderColor = Border }; var l = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Card2, RowCount = 2, ColumnCount = 1 }; l.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); l.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); var t = L(title, 8.3f, Muted, false); t.Dock = DockStyle.Fill; t.TextAlign = ContentAlignment.MiddleCenter; value.Dock = DockStyle.Fill; value.TextAlign = ContentAlignment.MiddleCenter; l.Controls.Add(t, 0, 0); l.Controls.Add(value, 0, 1); c.Controls.Add(l); return c; }
    private static Control Field(string title, TextBox box) { var host = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, RowCount = 2, ColumnCount = 1, Margin = new Padding(4) }; host.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); host.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); var t = L(title, 8.6f, Muted, false); t.Dock = DockStyle.Fill; var inputHost = new RoundedPanel { Dock = DockStyle.Fill, Radius = 11, BackColor = Card2, BorderColor = Border, Padding = new Padding(11, 8, 11, 7) }; box.Dock = DockStyle.Fill; inputHost.Controls.Add(box); host.Controls.Add(t, 0, 0); host.Controls.Add(inputHost, 0, 1); return host; }
    private static TableLayoutPanel TwoColumns(float a, float b, Color? bg = null) { var t = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = bg ?? Bg, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty }; t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, a)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, b)); return t; }
    private static TextBox Input(string? text = null) => new() { Text = text ?? "", BorderStyle = BorderStyle.None, BackColor = Card2, ForeColor = TextMain, Font = new Font("Segoe UI", 11.2f), TextAlign = HorizontalAlignment.Right, RightToLeft = RightToLeft.No, Margin = Padding.Empty };
    private static ComboBox Combo() => new() { BackColor = Card2, ForeColor = TextMain, FlatStyle = FlatStyle.Flat, DropDownStyle = ComboBoxStyle.DropDown, Font = new Font("Segoe UI", 9.5f), RightToLeft = RightToLeft.No, Height = 34 };
    private static NumericUpDown NumBox(decimal min, decimal max, int decimals) => new() { Minimum = min, Maximum = max, DecimalPlaces = decimals, Increment = decimals == 0 ? 1 : 0.005m, BackColor = Card2, ForeColor = TextMain, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9.5f), TextAlign = HorizontalAlignment.Center };
    private static CheckBox Check(string text) => new() { Text = text, ForeColor = TextMain, AutoSize = true, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.9f), Padding = new Padding(4, 5, 4, 5), RightToLeft = RightToLeft.Yes };
    private static Label SectionTitle(string text) { var l = L(text, 10.2f, GoldSoft, true); l.Width = 290; l.Height = 34; l.TextAlign = ContentAlignment.BottomRight; return l; }
    private static TableLayoutPanel SettingsGrid(params (string Label, Control C)[] fields) { var g = new TableLayoutPanel { Width = 290, AutoSize = true, ColumnCount = 2, BackColor = Card, RightToLeft = RightToLeft.Yes, Margin = new Padding(0, 2, 0, 8) }; g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); foreach (var f in fields) { var h = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, RowCount = 2, ColumnCount = 1, BackColor = Card, Margin = new Padding(4) }; var l = L(f.Label, 8, Muted, false); l.Height = 22; l.Dock = DockStyle.Top; f.C.Dock = DockStyle.Top; h.Controls.Add(l, 0, 0); h.Controls.Add(f.C, 0, 1); g.Controls.Add(h); } return g; }
    private static RoundButton Primary(string text) => ButtonX(text, Gold, Color.FromArgb(22, 16, 3), Gold);
    private static RoundButton Secondary(string text) => ButtonX(text, Card2, GoldSoft, Border);
    private static RoundButton ButtonX(string text, Color bg, Color fg, Color border) { var b = new RoundButton { Text = text, Height = 44, Radius = 11, FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = fg, Font = new Font("Segoe UI", 9.4f, FontStyle.Bold), Cursor = Cursors.Hand, Margin = new Padding(4), Padding = new Padding(6, 2, 6, 2), RightToLeft = RightToLeft.Yes }; b.FlatAppearance.BorderColor = border; b.FlatAppearance.BorderSize = 1; return b; }
    private static Label L(string text, float size, Color color, bool bold) => new() { Text = text, AutoSize = false, ForeColor = color, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), RightToLeft = RightToLeft.Yes, TextAlign = ContentAlignment.MiddleRight };
    private static Label MetricLabel(float size) => new() { Text = "—", AutoSize = false, ForeColor = GoldSoft, Font = new Font("Segoe UI", size, FontStyle.Bold), RightToLeft = RightToLeft.No, TextAlign = ContentAlignment.MiddleRight };
    private static Label StatusLabel() => new() { Text = "—", AutoSize = false, ForeColor = GoldSoft, BackColor = Card2, Font = new Font("Segoe UI", 8.7f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, Padding = new Padding(8), AutoEllipsis = true };
    private static void Select(ComboBox c, string value) { var i = c.FindStringExact(value); if (i >= 0) c.SelectedIndex = i; else c.Text = value; }

    private static double Parse(string raw, double fallback) { try { var s = NormalizeDigits(raw).Trim().Replace('٫', '.').Replace(',', '.'); return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback; } catch { return fallback; } }
    private static string NormalizeDigits(string raw) { const string fa = "۰۱۲۳۴۵۶۷۸۹", ar = "٠١٢٣٤٥٦٧٨٩"; var a = raw.ToCharArray(); for (int i = 0; i < a.Length; i++) { var p = fa.IndexOf(a[i]); if (p < 0) p = ar.IndexOf(a[i]); if (p >= 0) a[i] = (char)('0' + p); } return new string(a); }
    private static string Num(double v) => !double.IsFinite(v) ? "—" : (Math.Abs(v) < 1e-7 ? 0 : v).ToString("0.###", CultureInfo.InvariantCulture);
    private static void OpenInstagram() { try { Process.Start(new ProcessStartInfo("https://www.instagram.com/4mirnourhan/") { UseShellExecute = true }); } catch { } }
}
