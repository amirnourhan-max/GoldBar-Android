using System.Reflection;

namespace GoldBar.Windows;

// Rebuilds only the contents of the integrated Settings drawer using explicit,
// DPI-safe heights. The original AutoSize sections could collapse to thin bars
// on high-DPI Windows displays. All original input controls and save logic are
// retained; only their layout is replaced.
internal static class SettingsDrawerLayoutFix
{
    private static readonly Color Card = Color.FromArgb(16, 20, 27);
    private static readonly Color Card2 = Color.FromArgb(22, 27, 36);
    private static readonly Color Border = Color.FromArgb(47, 54, 68);
    private static readonly Color Gold = Color.FromArgb(247, 211, 112);
    private static readonly Color TextMain = Color.FromArgb(246, 244, 237);
    private static readonly Color Muted = Color.FromArgb(150, 159, 176);

    public static void Attach(ModernMainForm form)
    {
        var t = typeof(ModernMainForm);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        T? Get<T>(string name) where T : class => t.GetField(name, flags)?.GetValue(form) as T;

        var drawer = Get<RoundedPanel>("_settingsDrawer");
        var scroll = Get<FlowLayoutPanel>("_settingsScroll");
        if (drawer is null || scroll is null) return;

        var report = Get<TextBox>("_setReport")!;
        var model = Get<ComboBox>("_setModel")!;
        var port = Get<ComboBox>("_setPort")!;
        var baud = Get<ComboBox>("_setBaud")!;
        var data = Get<ComboBox>("_setData")!;
        var parity = Get<ComboBox>("_setParity")!;
        var stop = Get<ComboBox>("_setStop")!;
        var flow = Get<ComboBox>("_setFlow")!;
        var autoRead = Get<CheckBox>("_setAuto")!;
        var up = Get<CheckBox>("_setUp")!;
        var print = Get<CheckBox>("_setPrint")!;
        var queryOnUp = Get<CheckBox>("_setQueryOnUp")!;
        var stableSamples = Get<NumericUpDown>("_setStableSamples")!;
        var tolerance = Get<NumericUpDown>("_setTolerance")!;
        var query = Get<TextBox>("_setQuery")!;
        var ending = Get<ComboBox>("_setEnding")!;
        var timeout = Get<NumericUpDown>("_setTimeout")!;
        var testStatus = Get<Label>("_settingsTestStatus")!;

        var testMethod = t.GetMethod("TestScaleAsync", flags);

        scroll.SuspendLayout();
        scroll.Controls.Clear();
        scroll.AutoScroll = true;
        scroll.WrapContents = false;
        scroll.FlowDirection = FlowDirection.TopDown;
        scroll.RightToLeft = RightToLeft.Yes;
        scroll.Padding = new Padding(8, 10, 8, 12);

        var reportBody = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Card2,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        reportBody.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        reportBody.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        report.Dock = DockStyle.Fill;
        var browse = Secondary("انتخاب پوشه گزارش");
        browse.Dock = DockStyle.Fill;
        browse.Click += (_, _) =>
        {
            using var d = new FolderBrowserDialog
            {
                Description = "پوشه گزارش‌های Gold Bar را انتخاب کن",
                UseDescriptionForTitle = true
            };
            if (Directory.Exists(report.Text)) d.SelectedPath = report.Text;
            if (d.ShowDialog(form) == DialogResult.OK) report.Text = d.SelectedPath;
        };
        reportBody.Controls.Add(report, 0, 0);
        reportBody.Controls.Add(browse, 0, 1);
        scroll.Controls.Add(Section("گزارش", reportBody, 142));

        scroll.Controls.Add(Section("اتصال RS-232", Grid(
            ("مدل ترازو", model), ("COM Port", port),
            ("Baud Rate", baud), ("Data Bits", data),
            ("Parity", parity), ("Stop Bits", stop),
            ("Flow Control", flow)), 342));

        var readBody = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Card2,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        for (var i = 0; i < 4; i++) readBody.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        readBody.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        autoRead.Text = "خواندن خودکار فقط پس از پایدار شدن وزن";
        foreach (var c in new[] { autoRead, up, print, queryOnUp })
        {
            c.Dock = DockStyle.Fill;
            c.AutoSize = false;
            c.TextAlign = ContentAlignment.MiddleRight;
        }
        readBody.Controls.Add(autoRead, 0, 0);
        readBody.Controls.Add(up, 0, 1);
        readBody.Controls.Add(print, 0, 2);
        readBody.Controls.Add(queryOnUp, 0, 3);
        readBody.Controls.Add(Grid(
            ("تعداد قرائت پایدار", stableSamples),
            ("حداکثر نوسان (g)", tolerance)), 0, 4);
        scroll.Controls.Add(Section("خواندن وزن", readBody, 270));

        scroll.Controls.Add(Section("فرمان درخواست وزن", Grid(
            ("فرمان", query), ("پایان فرمان", ending),
            ("مهلت دریافت (ms)", timeout)), 210));

        var testBox = new RoundedPanel
        {
            Height = 104,
            BackColor = Card2,
            BorderColor = Border,
            Radius = 14,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 10)
        };
        testStatus.Dock = DockStyle.Top;
        testStatus.Height = 34;
        var test = Secondary("تست اتصال و دریافت وزن");
        test.Dock = DockStyle.Bottom;
        test.Height = 44;
        test.Click += async (_, _) =>
        {
            if (testMethod?.Invoke(form, new object[] { test }) is Task task)
                await task;
        };
        testBox.Controls.Add(test);
        testBox.Controls.Add(testStatus);
        scroll.Controls.Add(testBox);
        scroll.ResumeLayout(true);

        void ResizeChildren()
        {
            var width = Math.Max(340, scroll.ClientSize.Width - 26);
            foreach (Control c in scroll.Controls)
                c.Width = width;
        }

        scroll.SizeChanged += (_, _) => ResizeChildren();
        drawer.VisibleChanged += (_, _) =>
        {
            if (!drawer.Visible) return;
            try { drawer.BeginInvoke((Action)ResizeChildren); } catch { }
        };
        form.Shown += (_, _) => ResizeChildren();
        ResizeChildren();
    }

    private static RoundedPanel Section(string title, Control body, int height)
    {
        var section = new RoundedPanel
        {
            Height = height,
            BackColor = Card2,
            BorderColor = Border,
            Radius = 15,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 10),
            AutoSize = false
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Card2,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var h = Label(title, 11.2f, Gold, true);
        h.Dock = DockStyle.Fill;
        h.TextAlign = ContentAlignment.MiddleRight;
        body.Dock = DockStyle.Fill;
        layout.Controls.Add(h, 0, 0);
        layout.Controls.Add(body, 0, 1);
        section.Controls.Add(layout);
        return section;
    }

    private static TableLayoutPanel Grid(params (string Title, Control Control)[] fields)
    {
        var rows = (int)Math.Ceiling(fields.Length / 2.0);
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = rows,
            BackColor = Card2,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            RightToLeft = RightToLeft.Yes
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var r = 0; r < rows; r++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));

        for (var i = 0; i < fields.Length; i++)
        {
            var host = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Card2,
                Margin = new Padding(4)
            };
            host.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var l = Label(fields[i].Title, 8.7f, Muted, false);
            l.Dock = DockStyle.Fill;
            l.TextAlign = ContentAlignment.MiddleRight;
            fields[i].Control.Dock = DockStyle.Fill;
            host.Controls.Add(l, 0, 0);
            host.Controls.Add(fields[i].Control, 0, 1);
            grid.Controls.Add(host, i % 2, i / 2);
        }
        return grid;
    }

    private static RoundButton Secondary(string text)
    {
        var b = new RoundButton
        {
            Text = text,
            Radius = 11,
            BackColor = Card,
            ForeColor = Gold,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.3f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            RightToLeft = RightToLeft.Yes
        };
        b.FlatAppearance.BorderColor = Border;
        b.FlatAppearance.BorderSize = 1;
        return b;
    }

    private static Label Label(string text, float size, Color color, bool bold) => new()
    {
        Text = text,
        ForeColor = color,
        BackColor = Color.Transparent,
        Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
        RightToLeft = RightToLeft.Yes,
        AutoSize = false
    };
}
