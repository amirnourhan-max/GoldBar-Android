using System.Drawing.Drawing2D;
using System.Reflection;

namespace GoldBar.Windows;

// Final visual pass against the approved mockup: simplified header, gold-accented
// active navigation/registration card, and an in-app gold-ingot brand mark.
internal static class ReferenceVisualPolish
{
    private static readonly Color Bg = Color.FromArgb(6, 8, 12);
    private static readonly Color Gold = Color.FromArgb(247, 211, 112);
    private static readonly Color GoldDark = Color.FromArgb(184, 132, 29);
    private static readonly Color Border = Color.FromArgb(47, 54, 68);

    public static void Attach(ModernMainForm form)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var t = typeof(ModernMainForm);
        T? Get<T>(string name) where T : class => t.GetField(name, flags)?.GetValue(form) as T;

        var title = Get<Label>("_pageTitle");
        var subtitle = Get<Label>("_pageSubtitle");
        var scaleHeader = Get<Label>("_scaleHeader");
        var nav = Get<Dictionary<string, RoundButton>>("_nav");
        var workspace = Get<Panel>("_workspace");

        void Apply()
        {
            // The approved mockup keeps scale status in the sidebar and uses the top
            // bar for the page identity. Remove the duplicate scale chip.
            if (scaleHeader?.Parent is Control chip && chip.Parent is TableLayoutPanel bar)
            {
                chip.Visible = false;
                if (bar.ColumnStyles.Count >= 2)
                {
                    bar.ColumnStyles[0].SizeType = SizeType.Absolute;
                    bar.ColumnStyles[0].Width = 0;
                    bar.ColumnStyles[1].SizeType = SizeType.Percent;
                    bar.ColumnStyles[1].Width = 100;
                }
            }
            if (title is not null)
            {
                title.TextAlign = ContentAlignment.MiddleLeft;
                title.Padding = new Padding(18, 0, 0, 0);
            }
            if (subtitle is not null)
            {
                subtitle.TextAlign = ContentAlignment.MiddleLeft;
                subtitle.Padding = new Padding(18, 0, 0, 0);
            }

            if (nav is not null)
            {
                foreach (var button in nav.Values)
                {
                    var active = button.ForeColor.ToArgb() == Gold.ToArgb();
                    button.FlatAppearance.BorderSize = active ? 1 : 0;
                    button.FlatAppearance.BorderColor = active ? GoldDark : Border;
                }
            }

            // Gold border around the primary registration card, matching the mockup.
            foreach (var panel in Descendants(form).OfType<RoundedPanel>())
            {
                if (ContainsLabel(panel, "ثبت سریع آبشده"))
                    panel.BorderColor = GoldDark;
            }

            ApplyBrandLogo(form);
        }

        form.Shown += (_, _) =>
        {
            try { form.BeginInvoke((Action)Apply); } catch { }
        };
        form.Resize += (_, _) => Apply();
        if (workspace is not null)
            workspace.ControlAdded += (_, _) => { try { form.BeginInvoke((Action)Apply); } catch { } };
    }

    private static void ApplyBrandLogo(Control root)
    {
        var sidebar = root.Controls.OfType<TableLayoutPanel>()
            .FirstOrDefault(x => x.ColumnCount == 2 && x.RowCount == 1)?
            .GetControlFromPosition(0, 0);
        if (sidebar is null) return;

        var picture = Descendants(sidebar).OfType<PictureBox>().FirstOrDefault();
        if (picture is null || picture.Tag as string == "gold-ingot") return;
        picture.Image?.Dispose();
        picture.Image = CreateGoldIngot(220, 130);
        picture.SizeMode = PictureBoxSizeMode.Zoom;
        picture.Tag = "gold-ingot";
    }

    private static Bitmap CreateGoldIngot(int width, int height)
    {
        var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // soft glow
        using (var glow = new SolidBrush(Color.FromArgb(35, 255, 192, 35)))
            g.FillEllipse(glow, 38, 18, width - 76, height - 28);

        var top = new[]
        {
            new PointF(width * .34f, height * .17f),
            new PointF(width * .65f, height * .17f),
            new PointF(width * .75f, height * .35f),
            new PointF(width * .25f, height * .35f)
        };
        var body = new[]
        {
            new PointF(width * .25f, height * .35f),
            new PointF(width * .75f, height * .35f),
            new PointF(width * .66f, height * .84f),
            new PointF(width * .34f, height * .84f)
        };

        using (var topBrush = new LinearGradientBrush(
            new RectangleF(width * .2f, height * .1f, width * .6f, height * .3f),
            Color.FromArgb(255, 240, 150), Color.FromArgb(214, 145, 22), 35f))
            g.FillPolygon(topBrush, top);
        using (var bodyBrush = new LinearGradientBrush(
            new RectangleF(width * .2f, height * .3f, width * .6f, height * .55f),
            Color.FromArgb(255, 220, 92), Color.FromArgb(184, 111, 9), 20f))
            g.FillPolygon(bodyBrush, body);

        using var outline = new Pen(Color.FromArgb(255, 230, 120), 3f);
        g.DrawPolygon(outline, top);
        g.DrawPolygon(outline, body);

        using var font = new Font("Segoe UI", Math.Max(16, height * .22f), FontStyle.Bold);
        using var shadow = new SolidBrush(Color.FromArgb(120, 85, 45, 0));
        using var text = new SolidBrush(Color.FromArgb(255, 245, 190));
        var label = "Au";
        var size = g.MeasureString(label, font);
        var x = (width - size.Width) / 2f;
        var y = height * .43f;
        g.DrawString(label, font, shadow, x + 2, y + 2);
        g.DrawString(label, font, text, x, y);
        return bmp;
    }

    private static bool ContainsLabel(Control root, string text)
        => Descendants(root).OfType<Label>().Any(x => x.Text == text);

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
