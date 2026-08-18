using System.Reflection;

namespace GoldBar.Windows;

internal static class DesktopV2VisualFixer
{
    public static void Attach(DesktopMainFormV2 form)
    {
        FixSidebar(form);
        FixDrawer(form);
    }

    private static void FixSidebar(DesktopMainFormV2 form)
    {
        if (form.Controls.OfType<TableLayoutPanel>().FirstOrDefault() is not TableLayoutPanel shell) return;
        if (shell.GetControlFromPosition(0, 0) is not Panel side) return;

        var brand = side.Controls.OfType<RoundedPanel>().FirstOrDefault(p => ContainsText(p, "by: Amirnourhan"));
        var scale = side.Controls.OfType<RoundedPanel>().FirstOrDefault(p => ContainsText(p, "دریافت وزن"));
        var nav = side.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
        var version = side.Controls.OfType<Label>().FirstOrDefault(l => l.Text.StartsWith("GOLD BAR", StringComparison.OrdinalIgnoreCase));
        if (brand is null || scale is null || nav is null || version is null) return;

        foreach (var c in new Control[] { brand, scale, nav, version }) c.Dock = DockStyle.None;
        brand.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        nav.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        scale.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        version.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

        void Layout()
        {
            var w = Math.Max(190, side.ClientSize.Width - 32);
            brand.SetBounds(16, 18, w, 170);
            nav.SetBounds(16, 202, w, Math.Min(350, Math.Max(300, side.ClientSize.Height - 202 - 235)));
            version.SetBounds(16, Math.Max(0, side.ClientSize.Height - 42), w, 24);
            scale.SetBounds(16, Math.Max(0, side.ClientSize.Height - 226), w, 176);
            scale.BringToFront();
            version.BringToFront();

            // Keep the Au badge centered and the GOLD BAR / by line fully readable.
            var au = brand.Descendants().OfType<RoundedPanel>().FirstOrDefault(p => ContainsText(p, "Au"));
            if (au?.Parent is Control parent)
                au.Location = new Point(Math.Max(0, (parent.ClientSize.Width - au.Width) / 2), Math.Max(0, (parent.ClientSize.Height - au.Height) / 2));
        }

        side.Resize += (_, _) => Layout();
        form.Shown += (_, _) => Layout();
        Layout();
    }

    private static void FixDrawer(DesktopMainFormV2 form)
    {
        var field = typeof(DesktopMainFormV2).GetField("_drawerHost", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(form) is not Panel drawer || drawer.Parent is not Panel host) return;
        var center = host.Controls.Cast<Control>().FirstOrDefault(c => !ReferenceEquals(c, drawer));
        if (center is null) return;

        drawer.Dock = DockStyle.None;
        center.Dock = DockStyle.None;
        center.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        drawer.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;

        void Layout()
        {
            var dw = Math.Clamp(drawer.Width, 0, Math.Min(470, host.ClientSize.Width - 480));
            if (drawer.Controls.Count == 0) dw = 0;
            if (drawer.Controls.Count > 0 && dw < 390) dw = Math.Min(440, Math.Max(390, host.ClientSize.Width / 3));
            drawer.SetBounds(Math.Max(0, host.ClientSize.Width - dw), 0, dw, host.ClientSize.Height);
            center.SetBounds(0, 0, Math.Max(1, host.ClientSize.Width - dw), host.ClientSize.Height);
            drawer.BringToFront();
        }

        host.Resize += (_, _) => Layout();
        drawer.Resize += (_, _) => Layout();
        drawer.ControlAdded += (_, _) => form.BeginInvoke((Action)Layout);
        drawer.ControlRemoved += (_, _) => form.BeginInvoke((Action)Layout);
        form.Shown += (_, _) => Layout();
        Layout();
    }

    private static bool ContainsText(Control root, string text)
        => root.Descendants().Any(c => (c.Text ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<Control> Descendants(this Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in child.Descendants()) yield return nested;
        }
    }
}
