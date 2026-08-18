namespace GoldBar.Windows;

public sealed class SplashForm : Form
{
    private static readonly Color Bg = Color.FromArgb(8, 9, 11);
    private static readonly Color Card = Color.FromArgb(18, 22, 29);
    private static readonly Color Border = Color.FromArgb(48, 55, 68);
    private static readonly Color Gold = Color.FromArgb(247, 211, 112);
    private static readonly Color TextMain = Color.FromArgb(246, 244, 237);
    private static readonly Color Muted = Color.FromArgb(151, 160, 176);

    private readonly ProgressBar _progress = new();

    public SplashForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(520, 300);
        BackColor = Bg;
        ShowInTaskbar = false;
        TopMost = true;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(14),
            Padding = new Padding(36),
            BackColor = Card,
            BorderColor = Border,
            Radius = 26
        };
        Controls.Add(card);

        var au = new Label
        {
            Text = "Au",
            Size = new Size(66, 66),
            BackColor = Gold,
            ForeColor = Color.FromArgb(22, 16, 3),
            Font = new Font("Segoe UI", 19, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(388, 40)
        };
        card.Controls.Add(au);

        var title = new Label
        {
            Text = "GOLD BAR",
            AutoSize = true,
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 26, FontStyle.Bold),
            Location = new Point(52, 46),
            RightToLeft = RightToLeft.No
        };
        card.Controls.Add(title);

        var by = new Label
        {
            Text = "by: Amirnourhan",
            AutoSize = true,
            ForeColor = Gold,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(55, 91),
            RightToLeft = RightToLeft.No
        };
        card.Controls.Add(by);

        var loading = new Label
        {
            Text = "در حال بارگذاری…",
            AutoSize = true,
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            Location = new Point(296, 162)
        };
        card.Controls.Add(loading);

        var hint = new Label
        {
            Text = "آماده‌سازی داشبورد و سرویس ترازو",
            AutoSize = true,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9.5f),
            Location = new Point(238, 193)
        };
        card.Controls.Add(hint);

        _progress.Style = ProgressBarStyle.Marquee;
        _progress.MarqueeAnimationSpeed = 22;
        _progress.Size = new Size(395, 8);
        _progress.Location = new Point(54, 232);
        card.Controls.Add(_progress);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int CS_DROPSHADOW = 0x00020000;
            var cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }
}
