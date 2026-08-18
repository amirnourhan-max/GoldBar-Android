namespace GoldBar.Windows;

internal static class ModernLayoutPolish
{
    public static void Attach(ModernMainForm form)
    {
        var shell = form.Controls
            .OfType<TableLayoutPanel>()
            .FirstOrDefault(x => x.ColumnCount == 2 && x.RowCount == 1);
        if (shell is null) return;

        var side = shell.GetControlFromPosition(0, 0) as Panel;
        if (side is null) return;

        void ArrangeSidebar()
        {
            if (side.IsDisposed || side.ClientSize.Width < 100 || side.ClientSize.Height < 400) return;

            var rounded = side.Controls.OfType<RoundedPanel>().ToList();
            var brand = rounded.OrderByDescending(x => x.Height).FirstOrDefault();
            var scale = rounded.OrderBy(x => x.Height).FirstOrDefault();
            var nav = side.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
            var footer = side.Controls.OfType<Label>().FirstOrDefault(x => x.Text.Contains("GOLD BAR", StringComparison.OrdinalIgnoreCase));
            if (brand is null || scale is null || nav is null || footer is null) return;

            var left = side.Padding.Left;
            var top = side.Padding.Top;
            var width = Math.Max(150, side.ClientSize.Width - side.Padding.Horizontal);
            var footerHeight = 28;
            var scaleHeight = 145;
            var gap = 10;
            var brandHeight = side.ClientSize.Height < 760 ? 174 : 194;
            var footerY = side.ClientSize.Height - side.Padding.Bottom - footerHeight;
            var scaleY = footerY - gap - scaleHeight;
            var navY = top + brandHeight + gap;
            var navHeight = Math.Max(260, scaleY - gap - navY);

            foreach (var c in new Control[] { brand, nav, scale, footer }) c.Dock = DockStyle.None;

            brand.Bounds = new Rectangle(left, top, width, brandHeight);
            brand.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

            nav.Bounds = new Rectangle(left, navY, width, navHeight);
            nav.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
            foreach (Control item in nav.Controls)
                item.Width = Math.Max(120, nav.ClientSize.Width - 10);

            scale.Bounds = new Rectangle(left, scaleY, width, scaleHeight);
            scale.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            footer.Bounds = new Rectangle(left, footerY, width, footerHeight);
            footer.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            brand.BringToFront();
            nav.BringToFront();
            scale.BringToFront();
            footer.BringToFront();
        }

        form.Shown += (_, _) => ArrangeSidebar();
        form.Resize += (_, _) => ArrangeSidebar();
        side.SizeChanged += (_, _) => ArrangeSidebar();
    }
}
