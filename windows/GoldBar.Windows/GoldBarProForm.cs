using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using GoldBar.Core;

namespace GoldBar.Windows;

public sealed class GoldBarProForm : Form
{
    private static readonly Color Bg = Color.FromArgb(7, 8, 10);
    private static readonly Color Sidebar = Color.FromArgb(12, 13, 16);
    private static readonly Color Card = Color.FromArgb(18, 19, 22);
    private static readonly Color Card2 = Color.FromArgb(24, 26, 30);
    private static readonly Color Border = Color.FromArgb(54, 57, 64);
    private static readonly Color Gold = Color.FromArgb(245, 190, 49);
    private static readonly Color GoldSoft = Color.FromArgb(255, 210, 92);
    private static readonly Color Text = Color.FromArgb(246, 245, 240);
    private static readonly Color Muted = Color.FromArgb(154, 158, 167);
    private static readonly Color Success = Color.FromArgb(57, 205, 104);
    private static readonly Color Danger = Color.FromArgb(245, 101, 101);

    private readonly ScaleReader _scale = new();
    private AppSettings _settings = AppSettings.Load();
    private readonly List<GoldEntry> _entries = new();
    private int _editing = -1;

    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = Bg };
    private readonly Panel _settingsDrawer = new() { Dock = DockStyle.Right, Width = 390, BackColor = Card, Visible = false };
    private readonly Label _pageTitle = L("داشبورد", 21, Text, true);
    private readonly Label _pageSub = L("نمای کلی وزن، عیار و عملیات", 9, Muted, false);
    private readonly Dictionary<string, Button> _nav = new();

    private readonly Label _totalWeight = Metric();
    private readonly Label _avgAssay = Metric();
    private readonly Label _count = Metric();
    private readonly Label _alloyTop = Metric();
    private readonly Label _liveScale = Metric(24);
    private readonly Label _scaleStatus = L("ترازو آماده", 9, Muted, true);

    private readonly TextBox _weight = Input();
    private readonly TextBox _assay = Input();
    private readonly Button _saveEntry = Primary("ثبت آبشده");
    private readonly Label _entryHint = L("↑  دریافت وزن از ترازو", 9, Muted, true);
    private readonly DataGridView _grid = new();

    private readonly TextBox _raiseTarget = Input("747");
    private readonly TextBox _barAssay = Input("995");
    private readonly Label _raiseDiff = Metric(16);
    private readonly Label _raiseNeed = Metric(16);
    private readonly Label _raiseState = Status();

    private readonly TextBox _lowerTarget = Input("746");
    private readonly TextBox _silver = Input("32");
    private readonly Label _alloy = Metric(16);
    private readonly Label _silverNeed = Metric(16);
    private readonly Label _otherAlloy = Metric(16);
    private readonly Label _afterAlloy = Metric(16);
    private readonly Label _lowerState = Status();

    private readonly TextBox _splitBase = Input("800");
    private readonly Label _splitA = Metric(19);
    private readonly Label _splitB = Metric(19);
    private readonly TextBox _corrWeight = Input("250");
    private readonly TextBox _corrTarget = Input("750");
    private readonly TextBox _corrDrop = Input("1");
    private readonly Label _corrAdd = Metric(18);
    private readonly Label _corrTotal = Metric(18);

    // Drawer controls
    private readonly ComboBox _setModel = Combo();
    private readonly ComboBox _setPort = Combo();
    private readonly ComboBox _setBaud = Combo();
    private readonly ComboBox _setDataBits = Combo();
    private readonly ComboBox _setParity = Combo();
    private readonly ComboBox _setStopBits = Combo();
    private readonly ComboBox _setFlow = Combo();
    private readonly CheckBox _setAuto = Check("خواندن خودکار (فقط وزن پایدار)");
    private readonly CheckBox _setUp = Check("پاسخ‌دهی ترازو با کلید ↑");
    private readonly CheckBox _setPrint = Check("دریافت با کلید PRINT ترازو");
    private readonly CheckBox _setQuery = Check("هنگام ↑ فرمان درخواست وزن ارسال شود");
    private readonly TextBox _setQueryText = Input();
    private readonly NumericUpDown _setTimeout = Number(500, 10000);
    private readonly NumericUpDown _stableCount = Number(2, 10);
    private readonly NumericUpDown _stableTolerance = DecimalNumber(0.001m, 2m, 0.001m, 3);
    private readonly TextBox _reportFolder = Input();
    private readonly Label _testResult = L("آماده تست", 9, Muted, true);

    private static string DataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GoldBar", "entries.json");

    public GoldBarProForm()
    {
        Text = "GOLD BAR (by:Amirnourhan)";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 760);
        BackColor = Bg;
        ForeColor = Text;
        Font = new Font("Segoe UI", 10f);
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;
        TrySetAppIcon();

        BuildShell();
        ConfigureGrid();
        PopulateSettingsOptions();
        LoadSettingsControls();
        BindEvents();
        LoadEntries();
        ApplyScaleSettings();
        ShowPage("dashboard");
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _scale.Dispose();
        base.OnFormClosed(e);
    }

    private void TrySetAppIcon()
    {
        try
        {
            var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (icon != null) Icon = icon;
        }
        catch { }
    }

    private void BuildShell()
    {
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Bg, Padding = Padding.Empty, Margin = Padding.Empty };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 265));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.Controls.Add(BuildSidebar(), 0, 0);
        shell.Controls.Add(BuildMain(), 1, 0);
        Controls.Add(shell);

        BuildSettingsDrawer();
        Controls.Add(_settingsDrawer);
        _settingsDrawer.BringToFront();
    }

    private Control BuildSidebar()
    {
        var side = new Panel { Dock = DockStyle.Fill, BackColor = Sidebar, Padding = new Padding(18, 22, 18, 16) };

        var brand = new ProCard { Dock = DockStyle.Top, Height = 225, Radius = 18, BackColor = Sidebar, BorderColor = Color.FromArgb(35,35,38), Padding = new Padding(12) };
        var brandLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, BackColor = Sidebar };
        brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
        brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        brandLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var pic = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Sidebar, Margin = new Padding(12, 0, 12, 4) };
        try { if (Icon != null) pic.Image = Icon.ToBitmap(); } catch { }
        var title = L("GOLD BAR", 20, GoldSoft, true); title.TextAlign = ContentAlignment.MiddleCenter; title.RightToLeft = RightToLeft.No;
        var by = new LinkLabel { Text = "by: Amirnourhan", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, LinkColor = Gold, ActiveLinkColor = GoldSoft, VisitedLinkColor = Gold, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Cursor = Cursors.Hand, RightToLeft = RightToLeft.No };
        by.LinkClicked += (_, _) => OpenInstagram();
        brandLayout.Controls.Add(pic, 0, 0); brandLayout.Controls.Add(title, 0, 1); brandLayout.Controls.Add(by, 0, 2);
        brand.Controls.Add(brandLayout);
        side.Controls.Add(brand);

        var nav = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 405, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Sidebar, Padding = new Padding(0, 18, 0, 0) };
        AddNav(nav, "dashboard", "▦", "داشبورد");
        AddNav(nav, "entries", "◆", "آبشده‌ها");
        AddNav(nav, "calculations", "∑", "محاسبات عیار");
        AddNav(nav, "quick", "⚡", "محاسبه سریع");
        AddNav(nav, "reports", "▤", "گزارش‌ها");
        AddNav(nav, "settings", "⚙", "تنظیمات");
        side.Controls.Add(nav);

        var scaleCard = new ProCard { Dock = DockStyle.Bottom, Height = 164, Radius = 16, BackColor = Card, BorderColor = Border, Padding = new Padding(12) };
        var sl = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, BackColor = Card };
        sl.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); sl.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); sl.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); sl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var st = L("ترازو", 9, Gold, true); st.TextAlign = ContentAlignment.MiddleRight;
        _liveScale.Dock = DockStyle.Fill; _liveScale.TextAlign = ContentAlignment.MiddleCenter;
        _scaleStatus.Dock = DockStyle.Fill; _scaleStatus.TextAlign = ContentAlignment.MiddleCenter;
        var openSet = Secondary("تنظیمات اتصال"); openSet.Dock = DockStyle.Fill; openSet.Click += (_, _) => ToggleSettings(true);
        sl.Controls.Add(st, 0, 0); sl.Controls.Add(_liveScale, 0, 1); sl.Controls.Add(_scaleStatus, 0, 2); sl.Controls.Add(openSet, 0, 3);
        scaleCard.Controls.Add(sl);
        side.Controls.Add(scaleCard);

        return side;
    }

    private void AddNav(Control host, string key, string icon, string text)
    {
        var b = Secondary($"{icon}     {text}");
        b.Width = 222; b.Height = 48; b.TextAlign = ContentAlignment.MiddleRight; b.Margin = new Padding(0,0,0,6);
        b.Click += (_, _) => { if (key == "settings") ToggleSettings(true); else ShowPage(key); };
        _nav[key] = b;
        host.Controls.Add(b);
    }

    private Control BuildMain()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Bg };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Bg, Padding = new Padding(26, 15, 26, 8) };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Bg };
        var report = Secondary("ذخیره گزارش"); report.Width = 135; report.Click += (_, _) => SaveReport();
        var settings = Secondary("تنظیمات ترازو"); settings.Width = 150; settings.Click += (_, _) => ToggleSettings(true);
        actions.Controls.Add(report); actions.Controls.Add(settings);
        var titles = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Bg };
        _pageTitle.Dock = DockStyle.Fill; _pageTitle.TextAlign = ContentAlignment.BottomRight;
        _pageSub.Dock = DockStyle.Fill; _pageSub.TextAlign = ContentAlignment.TopRight;
        titles.Controls.Add(_pageTitle,0,0); titles.Controls.Add(_pageSub,0,1);
        top.Controls.Add(actions,0,0); top.Controls.Add(titles,1,0);
        root.Controls.Add(top,0,0); root.Controls.Add(_content,0,1);
        return root;
    }

    private void ShowPage(string key)
    {
        ToggleSettings(false);
        foreach (var kv in _nav) { var active = kv.Key == key; kv.Value.BackColor = active ? Color.FromArgb(43,35,16) : Card2; kv.Value.ForeColor = active ? GoldSoft : Text; }
        _content.Controls.Clear();
        Control page;
        switch (key)
        {
            case "entries": _pageTitle.Text = "آبشده‌ها"; _pageSub.Text = "ثبت، ویرایش و مدیریت آبشده‌ها"; page = BuildEntriesPage(); break;
            case "calculations": _pageTitle.Text = "محاسبات عیار"; _pageSub.Text = "افزایش و کاهش عیار با فرمول مستقل"; page = BuildCalculationsPage(); break;
            case "quick": _pageTitle.Text = "محاسبه سریع"; _pageSub.Text = "محاسبات پرکاربرد کارگاه"; page = BuildQuickPage(); break;
            case "reports": _pageTitle.Text = "گزارش‌ها"; _pageSub.Text = "ذخیره خروجی مرتب و تاریخ‌دار"; page = BuildReportsPage(); break;
            default: _pageTitle.Text = "داشبورد"; _pageSub.Text = "نمای کلی وزن، عیار و عملیات"; page = BuildDashboardPage(); key = "dashboard"; break;
        }
        page.Dock = DockStyle.Fill; _content.Controls.Add(page); RefreshAll();
    }

    private Control BuildDashboardPage()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Bg, Padding = new Padding(24, 6, 24, 24) };
        var stack = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Bg, Margin = Padding.Empty };
        scroll.Controls.Add(stack);
        void ResizeStack() { stack.Width = Math.Max(860, scroll.ClientSize.Width - 52); foreach (Control c in stack.Controls) c.Width = stack.Width; }
        scroll.SizeChanged += (_, _) => ResizeStack();

        var metrics = FourColumns(); metrics.Height = 122; metrics.Margin = new Padding(0,0,0,10);
        metrics.Controls.Add(MetricCard("وزن کل (g)", _totalWeight),0,0);
        metrics.Controls.Add(MetricCard("عیار میانگین (‰)", _avgAssay),1,0);
        metrics.Controls.Add(MetricCard("تعداد آبشده‌ها", _count),2,0);
        metrics.Controls.Add(MetricCard("کل بار مورد نیاز (g)", _alloyTop),3,0);
        stack.Controls.Add(metrics);

        var entryRow = TwoColumns(70,30); entryRow.Height = CardHeight("dashboard-entry", 300); entryRow.Margin = new Padding(0,0,0,10);
        var entry = BuildEntryCard(); var scale = BuildScaleCard();
        entryRow.Controls.Add(entry,0,0); entryRow.Controls.Add(scale,1,0);
        AddResizeGrip(entry, entryRow, "dashboard-entry", 280, 520);
        AddResizeGrip(scale, entryRow, "dashboard-entry", 280, 520);
        stack.Controls.Add(entryRow);

        var bottom = ThreeColumns(); bottom.Height = CardHeight("dashboard-bottom", 330); bottom.Margin = new Padding(0,0,0,10);
        var raise = BuildRaiseCard(); var lower = BuildLowerCard(); var recent = BuildRecentCard();
        bottom.Controls.Add(raise,0,0); bottom.Controls.Add(lower,1,0); bottom.Controls.Add(recent,2,0);
        AddResizeGrip(raise,bottom,"dashboard-bottom",300,600); AddResizeGrip(lower,bottom,"dashboard-bottom",300,600); AddResizeGrip(recent,bottom,"dashboard-bottom",300,600);
        stack.Controls.Add(bottom);
        ResizeStack();
        return scroll;
    }

    private Control BuildEntriesPage()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Bg, Padding = new Padding(24,6,24,24) };
        var h = CardHeight("entries-entry", 300);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, h)); root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var entry = BuildEntryCard(); root.Controls.Add(entry,0,0); root.Controls.Add(BuildGridCard(),0,1);
        AddResizeGrip(entry, root, "entries-entry", 280, 520, 0);
        return root;
    }

    private Control BuildCalculationsPage()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Bg, Padding = new Padding(24,6,24,24) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 122)); root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var m = FourColumns(); m.Controls.Add(MetricCard("وزن کل (g)",_totalWeight),0,0); m.Controls.Add(MetricCard("عیار میانگین",_avgAssay),1,0); m.Controls.Add(MetricCard("کل بار (g)",_alloyTop),2,0); m.Controls.Add(MetricCard("وزن پس از بار (g)",_afterAlloy),3,0);
        var row = TwoColumns(50,50); row.Controls.Add(BuildRaiseCard(),0,0); row.Controls.Add(BuildLowerCard(),1,0);
        root.Controls.Add(m,0,0); root.Controls.Add(row,0,1); return root;
    }

    private Control BuildQuickPage()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Bg, Padding = new Padding(24) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        root.Controls.Add(BuildSplitCard(),0,0); root.Controls.Add(BuildCorrectionCard(),1,0); return root;
    }

    private Control BuildReportsPage()
    {
        var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3, BackColor = Bg };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,15)); host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,70)); host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,15));
        host.RowStyles.Add(new RowStyle(SizeType.Percent,15)); host.RowStyles.Add(new RowStyle(SizeType.Percent,70)); host.RowStyles.Add(new RowStyle(SizeType.Percent,15));
        var body = new TableLayoutPanel { Dock=DockStyle.Fill, RowCount=4, BackColor=Card, Padding=new Padding(6) };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute,40)); body.RowStyles.Add(new RowStyle(SizeType.Absolute,80)); body.RowStyles.Add(new RowStyle(SizeType.Absolute,55)); body.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var p1=L("مسیر ذخیره گزارش",9,Muted,true); p1.Dock=DockStyle.Fill;
        var p2=L(_settings.ReportFolder,11,Text,true); p2.Dock=DockStyle.Fill; p2.AutoEllipsis=true;
        var acts=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,BackColor=Card}; var save=Primary("ذخیره گزارش"); save.Width=180; save.Click+=(_,_)=>SaveReport(); var set=Secondary("تنظیم مسیر"); set.Width=150; set.Click+=(_,_)=>ToggleSettings(true); acts.Controls.Add(save); acts.Controls.Add(set);
        var note=L("گزارش شامل آبشده‌ها و نتیجه‌های نهایی است؛ فرمول‌ها داخل فایل نوشته نمی‌شوند.",9,Muted,false); note.Dock=DockStyle.Fill;
        body.Controls.Add(p1,0,0); body.Controls.Add(p2,0,1); body.Controls.Add(acts,0,2); body.Controls.Add(note,0,3);
        host.Controls.Add(CardWithHeader("گزارش کامل","فایل متنی تاریخ‌دار",body),1,1); return host;
    }

    private Control BuildEntryCard()
    {
        var body = new TableLayoutPanel { Dock=DockStyle.Fill, RowCount=4, ColumnCount=1, BackColor=Card, Padding=Padding.Empty };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute,84)); body.RowStyles.Add(new RowStyle(SizeType.Absolute,34)); body.RowStyles.Add(new RowStyle(SizeType.Absolute,58)); body.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var fields=TwoColumns(50,50,Card); fields.Controls.Add(Field("وزن (g)",_weight),0,0); fields.Controls.Add(Field("عیار (‰)",_assay),1,0); body.Controls.Add(fields,0,0);
        _entryHint.Dock=DockStyle.Fill; _entryHint.TextAlign=ContentAlignment.MiddleRight; body.Controls.Add(_entryHint,0,1);
        var actions=ThreeColumns(Card); _saveEntry.Dock=DockStyle.Fill; var read=Secondary("خواندن از ترازو"); read.Dock=DockStyle.Fill; read.Click+=async(_,_)=>await ReadScaleIntoWeightAsync(); var clear=Secondary("پاک کردن"); clear.ForeColor=Danger; clear.Dock=DockStyle.Fill; clear.Click+=(_,_)=>ClearAll(); actions.Controls.Add(_saveEntry,0,0); actions.Controls.Add(read,1,0); actions.Controls.Add(clear,2,0); body.Controls.Add(actions,0,2);
        var hint=L("وزن را دستی وارد کن یا روی فیلد وزن کلید ↑ را بزن.",8.8f,Muted,false); hint.Dock=DockStyle.Fill; body.Controls.Add(hint,0,3);
        return CardWithHeader("ثبت سریع آبشده","ورود سریع و دریافت مستقیم از ترازو",body);
    }

    private Control BuildScaleCard()
    {
        var body=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=4,BackColor=Card}; body.RowStyles.Add(new RowStyle(SizeType.Absolute,78)); body.RowStyles.Add(new RowStyle(SizeType.Absolute,36)); body.RowStyles.Add(new RowStyle(SizeType.Absolute,52)); body.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        _liveScale.Dock=DockStyle.Fill; _liveScale.TextAlign=ContentAlignment.MiddleCenter; body.Controls.Add(_liveScale,0,0);
        var cfg=L($"{_settings.PortName}  •  {_settings.BaudRate}  •  {_settings.DataBits}bit  •  {_settings.Parity}",8.5f,Muted,false); cfg.Dock=DockStyle.Fill; cfg.TextAlign=ContentAlignment.MiddleCenter; cfg.RightToLeft=RightToLeft.No; body.Controls.Add(cfg,0,1);
        var read=Primary("دریافت وزن ↑"); read.Dock=DockStyle.Fill; read.Click+=async(_,_)=>await ReadScaleIntoWeightAsync(); body.Controls.Add(read,0,2);
        var set=Secondary("تنظیمات ترازو"); set.Dock=DockStyle.Top; set.Height=44; set.Click+=(_,_)=>ToggleSettings(true); body.Controls.Add(set,0,3);
        return CardWithHeader("ترازو","RS-232 / COM",body);
    }

    private Control BuildRaiseCard()
    {
        var body=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,BackColor=Card}; body.RowStyles.Add(new RowStyle(SizeType.Absolute,80)); body.RowStyles.Add(new RowStyle(SizeType.Absolute,82)); body.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var f=TwoColumns(50,50,Card); f.Controls.Add(Field("عیار هدف",_raiseTarget),0,0); f.Controls.Add(Field("عیار شمش",_barAssay),1,0); body.Controls.Add(f,0,0);
        var m=TwoColumns(50,50,Card); m.Controls.Add(MiniMetric("اختلاف تا هدف",_raiseDiff),0,0); m.Controls.Add(MiniMetric("شمش مورد نیاز (g)",_raiseNeed),1,0); body.Controls.Add(m,0,1); _raiseState.Dock=DockStyle.Fill; body.Controls.Add(_raiseState,0,2);
        return CardWithHeader("بالا بردن عیار","افزودن شمش عیار بالا",body);
    }

    private Control BuildLowerCard()
    {
        var body=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=4,BackColor=Card}; body.RowStyles.Add(new RowStyle(SizeType.Absolute,76)); body.RowStyles.Add(new RowStyle(SizeType.Absolute,70)); body.RowStyles.Add(new RowStyle(SizeType.Absolute,70)); body.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var f=TwoColumns(50,50,Card); f.Controls.Add(Field("عیار هدف",_lowerTarget),0,0); f.Controls.Add(Field("درصد نقره",_silver),1,0); body.Controls.Add(f,0,0);
        var m1=TwoColumns(50,50,Card); m1.Controls.Add(MiniMetric("کل بار (g)",_alloy),0,0); m1.Controls.Add(MiniMetric("نقره (g)",_silverNeed),1,0); body.Controls.Add(m1,0,1);
        var m2=TwoColumns(50,50,Card); m2.Controls.Add(MiniMetric("بار بدون نقره (g)",_otherAlloy),0,0); m2.Controls.Add(MiniMetric("وزن پس از بار (g)",_afterAlloy),1,0); body.Controls.Add(m2,0,2); _lowerState.Dock=DockStyle.Fill; body.Controls.Add(_lowerState,0,3);
        return CardWithHeader("پایین آوردن عیار","افزودن بار ریخته‌گری",body);
    }

    private Control BuildRecentCard()
    {
        var body=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Card,AutoScroll=true};
        var recent=_entries.TakeLast(5).Reverse().ToList();
        if(recent.Count==0){var e=L("هنوز آبشده‌ای ثبت نشده است.",9,Muted,false);e.Width=260;e.Height=40;body.Controls.Add(e);} else foreach(var x in recent){var l=L($"{Num(x.Weight)} g     عیار {Num(x.Assay)}",9.3f,Text,true);l.Width=290;l.Height=42;l.Padding=new Padding(8);l.BackColor=Card2;body.Controls.Add(l);}
        var card=CardWithHeader("آخرین آبشده‌ها","آخرین موارد ثبت‌شده",body); return card;
    }

    private Control BuildGridCard(){_grid.Dock=DockStyle.Fill;return CardWithHeader("لیست آبشده‌ها","ویرایش و حذف",_grid);}

    private Control BuildSplitCard()
    {
        var body=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,BackColor=Card};body.RowStyles.Add(new RowStyle(SizeType.Absolute,100));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));body.Controls.Add(Field("عدد پایه",_splitBase),0,0);var m=TwoColumns(50,50,Card);m.Controls.Add(MiniMetric("۳۶.۷۹٪",_splitA),0,0);m.Controls.Add(MiniMetric("۶۳.۲۱٪",_splitB),1,0);body.Controls.Add(m,0,1);return CardWithHeader("تقسیم ۳۶.۷۹٪ / ۶۳.۲۱٪","محاسبه لحظه‌ای",body);
    }

    private Control BuildCorrectionCard()
    {
        var body=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,BackColor=Card};body.RowStyles.Add(new RowStyle(SizeType.Absolute,92));body.RowStyles.Add(new RowStyle(SizeType.Absolute,92));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));var top=TwoColumns(50,50,Card);top.Controls.Add(Field("وزن پایه",_corrWeight),0,0);top.Controls.Add(Field("عیار هدف",_corrTarget),1,0);body.Controls.Add(top,0,0);body.Controls.Add(Field("مقدار افت عیار",_corrDrop),0,1);var m=TwoColumns(50,50,Card);m.Controls.Add(MiniMetric("بار افزوده (g)",_corrAdd),0,0);m.Controls.Add(MiniMetric("جمع وزن (g)",_corrTotal),1,0);body.Controls.Add(m,0,2);return CardWithHeader("اصلاح وزن برای افت عیار","وزن پایه، هدف و افت",body);
    }

    private void AddResizeGrip(Control card, Control row, string key, int min, int max, int tableRow = -1)
    {
        var grip=new Label{Text="⋮⋮",AutoSize=false,Size=new Size(36,18),ForeColor=Gold,BackColor=Color.Transparent,Cursor=Cursors.SizeNS,TextAlign=ContentAlignment.MiddleCenter,Anchor=AnchorStyles.Bottom|AnchorStyles.Right};
        grip.Location=new Point(Math.Max(0,card.Width-44),Math.Max(0,card.Height-24)); card.Controls.Add(grip); grip.BringToFront();
        card.Resize+=(_,_)=>grip.Location=new Point(Math.Max(0,card.Width-44),Math.Max(0,card.Height-24));
        int startY=0,startH=0; bool dragging=false;
        grip.MouseDown+=(_,e)=>{if(e.Button!=MouseButtons.Left)return;dragging=true;startY=Cursor.Position.Y;startH=row.Height;};
        grip.MouseMove+=(_,_)=>{if(!dragging)return;var h=Math.Clamp(startH+(Cursor.Position.Y-startY),min,max);if(row is TableLayoutPanel tl && tableRow>=0){tl.RowStyles[tableRow].SizeType=SizeType.Absolute;tl.RowStyles[tableRow].Height=h;}else row.Height=h;};
        grip.MouseUp+=(_,_)=>{if(!dragging)return;dragging=false;var h=row is TableLayoutPanel tl&&tableRow>=0?(int)tl.RowStyles[tableRow].Height:row.Height;SetCardHeight(key,h);};
    }

    private int CardHeight(string key,int fallback)=>_settings.CardHeights.TryGetValue(key,out var h)?Math.Max(220,h):fallback;
    private void SetCardHeight(string key,int h){_settings.CardHeights[key]=h;try{_settings.Save();}catch{}}

    private void BuildSettingsDrawer()
    {
        _settingsDrawer.Padding=new Padding(18); _settingsDrawer.BorderStyle=BorderStyle.FixedSingle;
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,BackColor=Card};root.RowStyles.Add(new RowStyle(SizeType.Absolute,62));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,62));
        var head=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,BackColor=Card};head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,44));var title=L("⚙  تنظیمات ترازو",17,Text,true);title.Dock=DockStyle.Fill;var close=Secondary("×");close.Dock=DockStyle.Fill;close.Font=new Font("Segoe UI",16,FontStyle.Bold);close.Click+=(_,_)=>ToggleSettings(false);head.Controls.Add(title,0,0);head.Controls.Add(close,1,0);root.Controls.Add(head,0,0);
        var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=Card};var stack=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Card};scroll.Controls.Add(stack);
        stack.Controls.Add(DrawerSection("ارتباط RS-232", SettingsGrid(("مدل",_setModel),("COM Port",_setPort),("Baud Rate",_setBaud),("Data Bits",_setDataBits),("Parity",_setParity),("Stop Bits",_setStopBits),("Flow Control",_setFlow))));
        var behavior=new FlowLayoutPanel{Width=330,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Card2,Padding=new Padding(10)};foreach(var c in new Control[]{_setAuto,_setUp,_setPrint,_setQuery}){c.Width=300;behavior.Controls.Add(c);}behavior.Controls.Add(SettingsGrid(("تعداد قرائت پایدار",_stableCount),("تلورانس پایداری (g)",_stableTolerance),("فرمان درخواست",_setQueryText),("مهلت دریافت ms",_setTimeout)));var test=Primary("تست دریافت وزن");test.Width=300;test.Height=44;test.Click+=async(_,_)=>await TestScale(test);behavior.Controls.Add(test);_testResult.Width=300;_testResult.Height=36;behavior.Controls.Add(_testResult);stack.Controls.Add(DrawerSection("رفتار دریافت وزن",behavior));
        var reportPanel=new FlowLayoutPanel{Width=330,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Card2,Padding=new Padding(10)};_reportFolder.Width=300;reportPanel.Controls.Add(_reportFolder);var browse=Secondary("انتخاب پوشه گزارش…");browse.Width=300;browse.Click+=(_,_)=>ChooseReportFolder();reportPanel.Controls.Add(browse);stack.Controls.Add(DrawerSection("گزارش",reportPanel));
        root.Controls.Add(scroll,0,1);
        var bottom=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,BackColor=Card};bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));var reset=Secondary("بازنشانی");reset.Dock=DockStyle.Fill;reset.Click+=(_,_)=>{_settings=new AppSettings();LoadSettingsControls();};var save=Primary("ذخیره تنظیمات");save.Dock=DockStyle.Fill;save.Click+=(_,_)=>SaveDrawerSettings();bottom.Controls.Add(reset,0,0);bottom.Controls.Add(save,1,0);root.Controls.Add(bottom,0,2);
        _settingsDrawer.Controls.Add(root);
    }

    private Control DrawerSection(string title,Control body){var c=new ProCard{Width=340,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Radius=14,BackColor=Card2,BorderColor=Border,Padding=new Padding(12),Margin=new Padding(0,0,0,12)};var host=new FlowLayoutPanel{Width=314,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Card2};var t=L(title,11,Gold,true);t.Width=310;t.Height=34;host.Controls.Add(t);body.Width=310;host.Controls.Add(body);c.Controls.Add(host);return c;}
    private Control SettingsGrid(params (string,Control)[] items){var g=new TableLayoutPanel{Width=310,AutoSize=true,ColumnCount=2,BackColor=Card2};g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));foreach(var it in items){var h=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,RowCount=2,BackColor=Card2,Margin=new Padding(4)};var l=L(it.Item1,8,Muted,false);l.Dock=DockStyle.Top;l.Height=22;it.Item2.Dock=DockStyle.Top;it.Item2.Height=35;h.Controls.Add(l,0,0);h.Controls.Add(it.Item2,0,1);g.Controls.Add(h);}return g;}
    private void ToggleSettings(bool show){_settingsDrawer.Visible=show;if(show){LoadSettingsControls();_settingsDrawer.BringToFront();}}
    private void ChooseReportFolder(){using var dlg=new FolderBrowserDialog{Description="پوشه گزارش‌های Gold Bar"};if(Directory.Exists(_reportFolder.Text))dlg.SelectedPath=_reportFolder.Text;if(dlg.ShowDialog(this)==DialogResult.OK)_reportFolder.Text=dlg.SelectedPath;}

    private void PopulateSettingsOptions()
    {
        _setModel.Items.AddRange(new object[]{"A&D","Custom / Generic"});
        foreach(var p in System.IO.Ports.SerialPort.GetPortNames().OrderBy(x=>x))_setPort.Items.Add(p);if(!_setPort.Items.Contains("COM1"))_setPort.Items.Add("COM1");
        _setBaud.Items.AddRange(new object[]{"1200","2400","4800","9600","19200","38400","57600","115200"});_setDataBits.Items.AddRange(new object[]{"7","8"});_setParity.Items.AddRange(Enum.GetNames<System.IO.Ports.Parity>());_setStopBits.Items.AddRange(new object[]{"One","OnePointFive","Two"});_setFlow.Items.AddRange(Enum.GetNames<System.IO.Ports.Handshake>());
    }

    private void LoadSettingsControls()
    {
        Select(_setModel,_settings.ScaleModel);Select(_setPort,_settings.PortName);Select(_setBaud,_settings.BaudRate.ToString());Select(_setDataBits,_settings.DataBits.ToString());Select(_setParity,_settings.Parity);Select(_setStopBits,_settings.StopBits);Select(_setFlow,_settings.Handshake);_setAuto.Checked=_settings.AutoRead;_setUp.Checked=_settings.ReadOnUpArrow;_setPrint.Checked=_settings.ReceivePrintKey;_setQuery.Checked=_settings.SendQueryOnUpArrow;_setQueryText.Text=_settings.QueryCommand;_setTimeout.Value=Math.Clamp(_settings.ReadTimeoutMs,(int)_setTimeout.Minimum,(int)_setTimeout.Maximum);_stableCount.Value=Math.Clamp(_settings.StableReadingsRequired,(int)_stableCount.Minimum,(int)_stableCount.Maximum);_stableTolerance.Value=(decimal)Math.Clamp(_settings.StableToleranceGrams,(double)_stableTolerance.Minimum,(double)_stableTolerance.Maximum);_reportFolder.Text=_settings.ReportFolder;
    }

    private void SaveDrawerSettings()
    {
        _settings.ScaleModel=string.IsNullOrWhiteSpace(_setModel.Text)?"A&D":_setModel.Text;_settings.PortName=string.IsNullOrWhiteSpace(_setPort.Text)?"COM1":_setPort.Text;_settings.BaudRate=int.TryParse(_setBaud.Text,out var br)?br:2400;_settings.DataBits=int.TryParse(_setDataBits.Text,out var db)?db:7;_settings.Parity=_setParity.Text;_settings.StopBits=_setStopBits.Text;_settings.Handshake=_setFlow.Text;_settings.AutoRead=_setAuto.Checked;_settings.ReadOnUpArrow=_setUp.Checked;_settings.ReceivePrintKey=_setPrint.Checked;_settings.SendQueryOnUpArrow=_setQuery.Checked;_settings.QueryCommand=_setQueryText.Text;_settings.ReadTimeoutMs=(int)_setTimeout.Value;_settings.StableReadingsRequired=(int)_stableCount.Value;_settings.StableToleranceGrams=(double)_stableTolerance.Value;_settings.ReportFolder=_reportFolder.Text.Trim();
        try{_settings.Save();ApplyScaleSettings();ToggleSettings(false);ShowPage("dashboard");}catch(Exception ex){MessageBox.Show(this,"ذخیره تنظیمات انجام نشد:\n"+ex.Message,"خطا",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }

    private async Task TestScale(Button b){b.Enabled=false;_testResult.Text="در حال دریافت…";_testResult.ForeColor=Gold;try{SaveDrawerSettingsValuesOnly();using var r=new ScaleReader();r.ApplySettings(_settings,false);var w=await r.ReadNowAsync();_testResult.Text="وزن دریافتی: "+Num(w)+" g";_testResult.ForeColor=Success;}catch(Exception ex){_testResult.Text="خطا: "+ex.Message;_testResult.ForeColor=Danger;}finally{b.Enabled=true;}}
    private void SaveDrawerSettingsValuesOnly(){_settings.PortName=string.IsNullOrWhiteSpace(_setPort.Text)?"COM1":_setPort.Text;_settings.BaudRate=int.TryParse(_setBaud.Text,out var br)?br:2400;_settings.DataBits=int.TryParse(_setDataBits.Text,out var db)?db:7;_settings.Parity=_setParity.Text;_settings.StopBits=_setStopBits.Text;_settings.Handshake=_setFlow.Text;_settings.SendQueryOnUpArrow=_setQuery.Checked;_settings.QueryCommand=_setQueryText.Text;_settings.ReadTimeoutMs=(int)_setTimeout.Value;_settings.AutoRead=false;}

    private void BindEvents()
    {
        _saveEntry.Click+=(_,_)=>SaveEntry();
        _weight.KeyDown+=async(_,e)=>{if(e.KeyCode==Keys.Up&&_settings.ReadOnUpArrow){e.SuppressKeyPress=true;await ReadScaleIntoWeightAsync();}else if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;_assay.Focus();_assay.SelectAll();}};
        _assay.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;SaveEntry();}};
        foreach(var b in new[]{_raiseTarget,_barAssay,_lowerTarget,_silver,_splitBase,_corrWeight,_corrTarget,_corrDrop}){b.TextChanged+=(_,_)=>Recalculate();b.Enter+=(_,_)=>b.SelectAll();}
        _scale.WeightReceived+=v=>Ui(()=>{_liveScale.Text=Num(v)+" g";_scaleStatus.Text="● متصل";_scaleStatus.ForeColor=Success;_entryHint.Text="● وزن پایدار: "+Num(v)+" g";_entryHint.ForeColor=Success;if(_settings.AutoRead&&_weight.Focused){_weight.Text=Num(v);_weight.SelectAll();}});
        _scale.StatusChanged+=(s,ok)=>Ui(()=>{_scaleStatus.Text=ok?"● متصل":"● "+s;_scaleStatus.ForeColor=ok?Success:Muted;});
    }

    private void ApplyScaleSettings(){_scale.ApplySettings(_settings,_settings.AutoRead);_scaleStatus.Text=_settings.AutoRead?"خواندن پایدار فعال":"↑ خواندن دستی";_scaleStatus.ForeColor=Muted;}
    private async Task ReadScaleIntoWeightAsync(){try{_entryHint.Text="در حال دریافت وزن…";_entryHint.ForeColor=Gold;var w=await _scale.ReadNowAsync();_weight.Text=Num(w);_weight.Focus();_weight.SelectAll();_entryHint.Text="● وزن دریافتی: "+Num(w)+" g";_entryHint.ForeColor=Success;}catch(Exception ex){_entryHint.Text="دریافت وزن ناموفق";_entryHint.ForeColor=Danger;MessageBox.Show(this,ex.Message,"ترازو",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
    private void Ui(Action a){if(IsDisposed||!IsHandleCreated)return;try{BeginInvoke(a);}catch{}}

    private void SaveEntry(){var w=Parse(_weight.Text,-1);var a=Parse(_assay.Text,-1);if(w<=0||a<=0||a>1000){MessageBox.Show(this,"وزن و عیار را صحیح وارد کن.","ورودی نامعتبر",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}var item=new GoldEntry(w,a);if(_editing>=0&&_editing<_entries.Count)_entries[_editing]=item;else _entries.Add(item);_editing=-1;_saveEntry.Text="ثبت آبشده";_weight.Clear();_assay.Clear();PersistEntries();RefreshAll();_weight.Focus();}
    private void ClearAll(){if(_entries.Count==0)return;if(MessageBox.Show(this,"همه آبشده‌ها حذف شوند؟","پاک کردن همه",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;_entries.Clear();_editing=-1;_weight.Clear();_assay.Clear();PersistEntries();RefreshAll();}
    private void EditEntry(int i){if(i<0||i>=_entries.Count)return;ShowPage("entries");_editing=i;_weight.Text=Num(_entries[i].Weight);_assay.Text=Num(_entries[i].Assay);_saveEntry.Text="ذخیره تغییرات";_weight.Focus();}
    private void DeleteEntry(int i){if(i<0||i>=_entries.Count)return;_entries.RemoveAt(i);PersistEntries();RefreshAll();}

    private void Recalculate()
    {
        var s=GoldCalculator.Summarize(_entries);_totalWeight.Text=Num(s.Weight);_avgAssay.Text=Num(s.AverageAssay);_count.Text=s.Count.ToString(CultureInfo.InvariantCulture);
        var rt=Parse(_raiseTarget.Text,747);var hi=Parse(_barAssay.Text,995);var raise=GoldCalculator.RequiredHighAssayBar(s,rt,hi);_raiseDiff.Text=Num(raise.DifferenceNeeded);_raiseNeed.Text=Num(raise.RequiredHighBar);_raiseState.Text=!double.IsFinite(raise.RequiredHighBar)?"ابتدا آبشده ثبت کن.":raise.RequiredHighBar>0?$"{Num(raise.RequiredHighBar)} g شمش {Num(hi)} نیاز است.":"افزایش عیار لازم نیست.";
        var lt=Parse(_lowerTarget.Text,746);var sp=Parse(_silver.Text,32);var lower=GoldCalculator.RequiredAlloy(s,lt,sp,s.Weight);_alloy.Text=Num(lower.TotalAlloyRequired);_alloyTop.Text=Num(lower.TotalAlloyRequired);_silverNeed.Text=Num(lower.SilverRequired);_otherAlloy.Text=Num(lower.NonSilverRequired);_afterAlloy.Text=Num(lower.TotalAfterAlloy);_lowerState.Text=!double.IsFinite(lower.TotalAlloyRequired)?"ابتدا آبشده ثبت کن.":lower.TotalAlloyRequired>0?$"{Num(lower.TotalAlloyRequired)} g بار مورد نیاز است.":"کاهش عیار لازم نیست.";
        var baseV=Parse(_splitBase.Text,800);var p=GoldCalculator.Split3679(baseV);_splitA.Text=Num(p);_splitB.Text=Num(baseV-p);var cw=Parse(_corrWeight.Text,250);var ct=Parse(_corrTarget.Text,750);var cd=Parse(_corrDrop.Text,1);var add=GoldCalculator.CorrectionAddition(cw,ct,cd);_corrAdd.Text=Num(add);_corrTotal.Text=Num(cw+add);
    }

    private void RefreshAll(){Recalculate();if(_grid.Columns.Count==0)return;_grid.Rows.Clear();for(int i=0;i<_entries.Count;i++)_grid.Rows.Add(i+1,Num(_entries[i].Weight),Num(_entries[i].Assay),"ویرایش","حذف");}

    private void ConfigureGrid(){_grid.BackgroundColor=Card;_grid.BorderStyle=BorderStyle.None;_grid.GridColor=Border;_grid.EnableHeadersVisualStyles=false;_grid.ColumnHeadersDefaultCellStyle.BackColor=Card2;_grid.ColumnHeadersDefaultCellStyle.ForeColor=Text;_grid.ColumnHeadersHeight=42;_grid.DefaultCellStyle.BackColor=Card;_grid.DefaultCellStyle.ForeColor=Text;_grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(55,43,18);_grid.RowTemplate.Height=42;_grid.RowHeadersVisible=false;_grid.AllowUserToAddRows=false;_grid.ReadOnly=true;_grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;_grid.RightToLeft=RightToLeft.Yes;_grid.Columns.Add(new DataGridViewTextBoxColumn{Name="No",HeaderText="#",FillWeight=10});_grid.Columns.Add(new DataGridViewTextBoxColumn{Name="Weight",HeaderText="وزن (g)",FillWeight=28});_grid.Columns.Add(new DataGridViewTextBoxColumn{Name="Assay",HeaderText="عیار",FillWeight=24});_grid.Columns.Add(new DataGridViewButtonColumn{Name="Edit",HeaderText="",Text="ویرایش",UseColumnTextForButtonValue=true,FillWeight=19,FlatStyle=FlatStyle.Flat});_grid.Columns.Add(new DataGridViewButtonColumn{Name="Delete",HeaderText="",Text="حذف",UseColumnTextForButtonValue=true,FillWeight=19,FlatStyle=FlatStyle.Flat});_grid.CellContentClick+=(_,e)=>{if(e.RowIndex<0)return;var n=_grid.Columns[e.ColumnIndex].Name;if(n=="Edit")EditEntry(e.RowIndex);else if(n=="Delete")DeleteEntry(e.RowIndex);};}
    private void LoadEntries(){try{if(!File.Exists(DataPath))return;var x=JsonSerializer.Deserialize<List<GoldEntry>>(File.ReadAllText(DataPath));if(x!=null)_entries.AddRange(x.Where(e=>e.Weight>0&&e.Assay>0&&e.Assay<=1000));}catch{}}
    private void PersistEntries(){try{Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!);File.WriteAllText(DataPath,JsonSerializer.Serialize(_entries,new JsonSerializerOptions{WriteIndented=true}));}catch{}}

    private void SaveReport(){try{if(string.IsNullOrWhiteSpace(_settings.ReportFolder)){ToggleSettings(true);return;}if(!Directory.Exists(_settings.ReportFolder))Directory.CreateDirectory(_settings.ReportFolder);var s=GoldCalculator.Summarize(_entries);var r=GoldCalculator.RequiredHighAssayBar(s,Parse(_raiseTarget.Text,747),Parse(_barAssay.Text,995));var l=GoldCalculator.RequiredAlloy(s,Parse(_lowerTarget.Text,746),Parse(_silver.Text,32),s.Weight);var b=new StringBuilder();b.AppendLine("GOLD BAR (by:Amirnourhan)");b.AppendLine("تاریخ و ساعت: "+DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));b.AppendLine();b.AppendLine("آبشده‌ها:");for(int i=0;i<_entries.Count;i++)b.AppendLine($"{i+1}) وزن {Num(_entries[i].Weight)} g | عیار {Num(_entries[i].Assay)}");b.AppendLine();b.AppendLine($"وزن کل: {Num(s.Weight)} g | عیار میانگین: {Num(s.AverageAssay)} | تعداد: {s.Count}");b.AppendLine($"کل بار مورد نیاز: {Num(l.TotalAlloyRequired)} g | وزن پس از بار: {Num(l.TotalAfterAlloy)} g");b.AppendLine($"شمش عیار بالا: {Num(r.RequiredHighBar)} g | نقره: {Num(l.SilverRequired)} g | بار بدون نقره: {Num(l.NonSilverRequired)} g");var path=Path.Combine(_settings.ReportFolder,"GoldBar_"+DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")+".txt");File.WriteAllText(path,b.ToString(),Encoding.UTF8);MessageBox.Show(this,"گزارش ذخیره شد:\n"+path,"گزارش",MessageBoxButtons.OK,MessageBoxIcon.Information);}catch(Exception ex){MessageBox.Show(this,"ذخیره گزارش انجام نشد:\n"+ex.Message,"خطا",MessageBoxButtons.OK,MessageBoxIcon.Error);}}

    private static ProCard CardWithHeader(string title,string subtitle,Control body){var c=new ProCard{Dock=DockStyle.Fill,Radius=18,BackColor=Card,BorderColor=Border,Padding=new Padding(15),Margin=new Padding(6)};var l=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,BackColor=Card};l.RowStyles.Add(new RowStyle(SizeType.Absolute,32));l.RowStyles.Add(new RowStyle(SizeType.Absolute,34));l.RowStyles.Add(new RowStyle(SizeType.Percent,100));var t=L(title,13,Text,true);t.Dock=DockStyle.Fill;var s=L(subtitle,8.6f,Muted,false);s.Dock=DockStyle.Fill;body.Dock=DockStyle.Fill;l.Controls.Add(t,0,0);l.Controls.Add(s,0,1);l.Controls.Add(body,0,2);c.Controls.Add(l);return c;}
    private static Control MetricCard(string title,Label value){var c=new ProCard{Dock=DockStyle.Fill,Radius=15,BackColor=Card,BorderColor=Border,Padding=new Padding(14),Margin=new Padding(5)};var l=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,BackColor=Card};l.RowStyles.Add(new RowStyle(SizeType.Absolute,28));l.RowStyles.Add(new RowStyle(SizeType.Percent,100));var t=L(title,8.8f,Muted,false);t.Dock=DockStyle.Fill;value.Dock=DockStyle.Fill;value.TextAlign=ContentAlignment.MiddleRight;l.Controls.Add(t,0,0);l.Controls.Add(value,0,1);c.Controls.Add(l);return c;}
    private static Control MiniMetric(string title,Label value){var c=new ProCard{Dock=DockStyle.Fill,Radius=12,BackColor=Card2,BorderColor=Border,Padding=new Padding(8),Margin=new Padding(4)};var l=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,BackColor=Card2};l.RowStyles.Add(new RowStyle(SizeType.Absolute,22));l.RowStyles.Add(new RowStyle(SizeType.Percent,100));var t=L(title,8,Muted,false);t.Dock=DockStyle.Fill;t.TextAlign=ContentAlignment.MiddleCenter;value.Dock=DockStyle.Fill;value.TextAlign=ContentAlignment.MiddleCenter;l.Controls.Add(t,0,0);l.Controls.Add(value,0,1);c.Controls.Add(l);return c;}
    private static Control Field(string title,TextBox box){var h=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,BackColor=Card,Margin=new Padding(5)};h.RowStyles.Add(new RowStyle(SizeType.Absolute,24));h.RowStyles.Add(new RowStyle(SizeType.Percent,100));var l=L(title,8.6f,Muted,false);l.Dock=DockStyle.Fill;var ih=new ProCard{Dock=DockStyle.Fill,Radius=11,BackColor=Card2,BorderColor=Border,Padding=new Padding(12,10,12,6)};box.Dock=DockStyle.Fill;ih.Controls.Add(box);h.Controls.Add(l,0,0);h.Controls.Add(ih,0,1);return h;}
    private static TableLayoutPanel TwoColumns(float a,float b,Color? bg=null){var t=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,BackColor=bg??Bg};t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,a));t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,b));return t;}
    private static TableLayoutPanel ThreeColumns(Color? bg=null){var t=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,BackColor=bg??Bg};for(int i=0;i<3;i++)t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.333f));return t;}
    private static TableLayoutPanel FourColumns(){var t=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,BackColor=Bg};for(int i=0;i<4;i++)t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));return t;}
    private static TextBox Input(string? text=null)=>new(){Text=text??"",BorderStyle=BorderStyle.None,BackColor=Card2,ForeColor=Text,Font=new Font("Segoe UI",11.2f),TextAlign=HorizontalAlignment.Right,RightToLeft=RightToLeft.No};
    private static ComboBox Combo()=>new(){BackColor=Card2,ForeColor=Text,FlatStyle=FlatStyle.Flat,DropDownStyle=ComboBoxStyle.DropDown,Font=new Font("Segoe UI",9.5f),RightToLeft=RightToLeft.No};
    private static CheckBox Check(string s)=>new(){Text=s,ForeColor=Text,AutoSize=false,Height=34,Font=new Font("Segoe UI",9f),RightToLeft=RightToLeft.Yes};
    private static NumericUpDown Number(int min,int max)=>new(){Minimum=min,Maximum=max,BackColor=Card2,ForeColor=Text,BorderStyle=BorderStyle.FixedSingle,Font=new Font("Segoe UI",9.5f)};
    private static NumericUpDown DecimalNumber(decimal min,decimal max,decimal inc,int dec)=>new(){Minimum=min,Maximum=max,Increment=inc,DecimalPlaces=dec,BackColor=Card2,ForeColor=Text,BorderStyle=BorderStyle.FixedSingle,Font=new Font("Segoe UI",9.5f)};
    private static Button Primary(string s)=>ButtonX(s,Gold,Color.FromArgb(20,15,3));private static Button Secondary(string s)=>ButtonX(s,Card2,Text);
    private static Button ButtonX(string s,Color bg,Color fg){var b=new ProButton{Text=s,Radius=11,FlatStyle=FlatStyle.Flat,BackColor=bg,ForeColor=fg,Font=new Font("Segoe UI",9.4f,FontStyle.Bold),Cursor=Cursors.Hand,Margin=new Padding(4)};b.FlatAppearance.BorderColor=bg==Gold?GoldSoft:Border;b.FlatAppearance.BorderSize=1;return b;}
    private static Label L(string s,float z,Color c,bool bold)=>new(){Text=s,AutoSize=false,ForeColor=c,Font=new Font("Segoe UI",z,bold?FontStyle.Bold:FontStyle.Regular),RightToLeft=RightToLeft.Yes,TextAlign=ContentAlignment.MiddleRight};
    private static Label Metric(float z=18)=>new(){Text="—",AutoSize=false,ForeColor=GoldSoft,Font=new Font("Segoe UI",z,FontStyle.Bold),RightToLeft=RightToLeft.No,TextAlign=ContentAlignment.MiddleRight};
    private static Label Status()=>new(){Text="—",AutoSize=false,ForeColor=Gold,BackColor=Color.FromArgb(20,20,19),Font=new Font("Segoe UI",8.7f,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter,RightToLeft=RightToLeft.Yes,Padding=new Padding(8)};
    private static void Select(ComboBox c,string v){var i=c.FindStringExact(v);if(i>=0)c.SelectedIndex=i;else c.Text=v;}
    private static double Parse(string raw,double fallback){var s=Normalize(raw).Trim().Replace('٫','.').Replace(',','.');return double.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out var v)?v:fallback;}
    private static string Normalize(string s){const string fa="۰۱۲۳۴۵۶۷۸۹",ar="٠١٢٣٤٥٦٧٨٩";var a=s.ToCharArray();for(int i=0;i<a.Length;i++){var p=fa.IndexOf(a[i]);if(p<0)p=ar.IndexOf(a[i]);if(p>=0)a[i]=(char)('0'+p);}return new string(a);}
    private static string Num(double v)=>!double.IsFinite(v)?"—":(Math.Abs(v)<1e-7?0:v).ToString("0.###",CultureInfo.InvariantCulture);
    private static void OpenInstagram(){try{Process.Start(new ProcessStartInfo("https://www.instagram.com/4mirnourhan/"){UseShellExecute=true});}catch{}}
}

public class ProCard : Panel
{
    public int Radius { get; set; }=16; public Color BorderColor { get; set; }=Color.Transparent;
    public ProCard(){DoubleBuffered=true;Resize+=(_,_)=>UpdateRegion();}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var r=ClientRectangle;r.Width-=1;r.Height-=1;if(r.Width<=2||r.Height<=2)return;e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using var p=Path(r,Radius);using var pen=new Pen(BorderColor,1);e.Graphics.DrawPath(pen,p);}
    private void UpdateRegion(){var r=ClientRectangle;if(r.Width<=2||r.Height<=2)return;using var p=Path(r,Radius);Region?.Dispose();Region=new Region(p);}
    internal static System.Drawing.Drawing2D.GraphicsPath Path(Rectangle r,int radius){var p=new System.Drawing.Drawing2D.GraphicsPath();var d=Math.Max(2,radius*2);p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
}

public sealed class ProButton : Button
{
    public int Radius { get; set; }=11;
    public ProButton(){Resize+=(_,_)=>{var r=ClientRectangle;if(r.Width<=2||r.Height<=2)return;using var p=ProCard.Path(r,Radius);Region?.Dispose();Region=new Region(p);};}
}
