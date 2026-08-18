namespace GoldBar.Windows;

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
