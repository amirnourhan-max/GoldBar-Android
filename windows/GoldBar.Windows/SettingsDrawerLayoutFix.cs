using System.Reflection;

namespace GoldBar.Windows;

// Fixed, DPI-safe settings drawer matching the approved reference: one readable
// field per row, no collapsible/resizable sections, and a narrow right-side panel.
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
        scroll.Padding = new Padding(6, 8, 6, 12);
        scroll.BackColor = Card;

        var reportBody = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Card2,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        reportBody.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        reportBody.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
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
        scroll.Controls.Add(Section("گزارش", reportBody, 136));

        // Reference mockup uses one serial setting per row for legibility.
        scroll.Controls.Add(Section("اتصال RS-232", SingleColumn(
            ("COM Port", port),
            ("مدل ترازو", model),
            ("Baud Rate", baud),
            ("Data Bits", data),
            ("Parity", parity),
            ("Stop Bits", stop),
            ("Flow Control", flow)), 474));

        var readBody = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = Card2,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        for (var i = 0; i < 4; i++) readBody.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        readBody.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        readBody.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));

        autoRead.Text = "خواندن خودکار فقط بعد از پایدار شدن وزن";
        up.Text = "دریافت وزن با کلید ↑";
        print.Text = "دریافت با PRINT ترازو";
        queryOnUp.Text = "ارسال فرمان درخواست وزن هنگام ↑";
        foreach (var c in new[] { autoRead, up, print, queryOnUp })
        {
            c.Dock = DockStyle.Fill;
            c.AutoSize = false;
            c.TextAlign = ContentAlignment.MiddleRight;
            c.Padding = new Padding(4, 0, 4, 0);
        }
        readBody.Controls.Add(autoRead, 0, 0);
        readBody.Controls.Add(up, 0, 1);
        readBody.Controls.Add(print, 0, 2);
        readBody.Controls.Add(queryOnUp, 0, 3);
        readBody.Controls.Add(Field("تعداد قرائت پایدار", stableSamples), 0, 4);
        readBody.Controls.Add(Field("حداکثر نوسان (g)", tolerance), 0, 5);
        scroll.Controls.Add(Section("خواندن وزن", readBody, 322));

        scroll.Controls.Add(Section("فرمان درخواست وزن", SingleColumn(
            ("فرمان", query),
            ("پایان فرمان", ending),
            ("مهلت دریافت (ms)", timeout)), 232));

        var testBox = new RoundedPanel
        {
            Height = 100,
            BackColor = Card2,
            BorderColor = Border,
            Radius = 14,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 10)
        };
        testStatus.Dock = DockStyle.Top;
        testStatus.Height = 32;
        var test = Secondary("تست دریافت وزن");
        test.Dock = DockStyle.Bottom;
        test.Height = 42;
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
            var width = Math.Max(284, scroll.ClientSize.Width - 22);
            foreach (Control c in scroll.Controls) c.Width = width;
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
            Padding = new Padding(10),
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
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var h = Label(title, 10.8f, Gold, true);
        h.Dock = DockStyle.Fill;
        h.TextAlign = ContentAlignment.MiddleRight;
        body.Dock = DockStyle.Fill;
        layout.Controls.Add(h, 0, 0);
        layout.Controls.Add(body, 0, 1);
        section.Controls.Add(layout);
        return section;
    }

    private static TableLayoutPanel SingleColumn(params (string Title, Control Control)[] fields)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = fields.Length,
            BackColor = Card2,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            RightToLeft = RightToLeft.Yes
        };
        for (var i = 0; i < fields.Length; i++)
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / fields.Length));
        for (var i = 0; i < fields.Length; i++)
            grid.Controls.Add(Field(fields[i].Title, fields[i].Control), 0, i);
        return grid;
    }

    private static Control Field(string title, Control control)
    {
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Card2,
            Margin = new Padding(2, 2, 2, 3),
            Padding = Padding.Empty,
            RightToLeft = RightToLeft.Yes
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        var l = Label(title, 8.7f, Muted, false);
        l.Dock = DockStyle.Fill;
        l.TextAlign = ContentAlignment.MiddleRight;
        control.Dock = DockStyle.Fill;
        host.Controls.Add(l, 0, 0);
        host.Controls.Add(control, 1, 0);
        return host;
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
            Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
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
