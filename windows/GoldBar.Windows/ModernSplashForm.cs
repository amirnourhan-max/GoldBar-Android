namespace GoldBar.Windows;

public sealed class ModernSplashForm : Form
{
    private static readonly Color Bg = Color.FromArgb(6, 8, 12);
    private static readonly Color Card = Color.FromArgb(16, 20, 27);
    private static readonly Color Border = Color.FromArgb(47, 54, 68);
    private static readonly Color Gold = Color.FromArgb(247, 211, 112);
    private static readonly Color TextMain = Color.FromArgb(246, 244, 237);
    private static readonly Color Muted = Color.FromArgb(150, 159, 176);

    public ModernSplashForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(560, 320);
        BackColor = Bg;
        ShowInTaskbar = false;
        TopMost = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        var card = new RoundedPanel { Dock = DockStyle.Fill, Margin = new Padding(14), Padding = new Padding(28), BackColor = Card, BorderColor = Border, Radius = 26 };
        Controls.Add(card);

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, BackColor = Card };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var picture = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(8) };
        try
        {
            var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (icon is not null) picture.Image = icon.ToBitmap();
        }
        catch { }
        grid.Controls.Add(picture, 0, 0);
        grid.SetRowSpan(picture, 3);

        var title = new Label { Text = "GOLD BAR", Dock = DockStyle.Fill, ForeColor = Gold, Font = new Font("Segoe UI", 26, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft, RightToLeft = RightToLeft.No };
        var by = new Label { Text = "by: Amirnourhan", Dock = DockStyle.Fill, ForeColor = TextMain, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), TextAlign = ContentAlignment.TopLeft, RightToLeft = RightToLeft.No };
        var loading = new Label { Text = "در حال بارگذاری…", Dock = DockStyle.Fill, ForeColor = TextMain, Font = new Font("Segoe UI", 13, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes };
        grid.Controls.Add(title, 1, 0); grid.Controls.Add(by, 1, 1); grid.Controls.Add(loading, 1, 2);

        var progress = new ProgressBar { Dock = DockStyle.Bottom, Height = 9, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 20 };
        grid.Controls.Add(progress, 0, 3); grid.SetColumnSpan(progress, 2);
        card.Controls.Add(grid);
    }
}
