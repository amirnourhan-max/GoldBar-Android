using System.Reflection;

namespace GoldBar.Windows;

// Locks the dashboard to the same fixed visual hierarchy as the approved mockup.
// No user-resizable cards/splitters remain. The main scale status stays in the
// sidebar, so the registration card can occupy the full top row like the reference.
internal static class MockupFixedLayout
{
    private static readonly Color Bg = Color.FromArgb(6, 8, 12);

    public static void Attach(ModernMainForm form)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(ModernMainForm);

        T? Get<T>(string field) where T : class
            => type.GetField(field, flags)?.GetValue(form) as T;

        void LockSplit(SplitContainer? split)
        {
            if (split is null || split.IsDisposed) return;
            split.IsSplitterFixed = true;
            split.SplitterWidth = 1;
            split.BackColor = Bg;
            split.Cursor = Cursors.Default;
            split.Panel1.BackColor = Bg;
            split.Panel2.BackColor = Bg;
        }

        void ApplyReferenceGeometry()
        {
            // Re-read these fields every time. ShowPage() rebuilds the dashboard and
            // replaces all SplitContainer instances, so retaining the old references
            // would accidentally restore the resizable layout after a page refresh.
            var mainSplit = Get<SplitContainer>("_dashboardMainSplit");
            var topSplit = Get<SplitContainer>("_dashboardTopSplit");
            var bottomLeft = Get<SplitContainer>("_dashboardBottomLeft");
            var bottomRight = Get<SplitContainer>("_dashboardBottomRight");
            var drawer = Get<RoundedPanel>("_settingsDrawer");

            var shell = form.Controls
                .OfType<TableLayoutPanel>()
                .FirstOrDefault(x => x.ColumnCount == 2 && x.RowCount == 1);
            if (shell is not null && shell.ColumnStyles.Count >= 2)
            {
                shell.ColumnStyles[0].SizeType = SizeType.Absolute;
                shell.ColumnStyles[0].Width = 278;
                shell.ColumnStyles[1].SizeType = SizeType.Percent;
                shell.ColumnStyles[1].Width = 100;
            }

            // Approved mockup: quick registration is one full-width card. Scale
            // status/receive remains available in the left sidebar and settings drawer.
            if (topSplit is not null && !topSplit.IsDisposed)
            {
                try { topSplit.Panel2Collapsed = true; } catch { }
            }

            LockSplit(mainSplit);
            LockSplit(topSplit);
            LockSplit(bottomLeft);
            LockSplit(bottomRight);

            // Fixed proportions: registration row then three equal lower cards.
            SetPercent(mainSplit, 46);
            SetPercent(bottomLeft, 33);
            SetPercent(bottomRight, 50);

            if (drawer is not null && !drawer.IsDisposed)
            {
                drawer.Width = Math.Clamp((int)(form.ClientSize.Width * 0.22), 330, 372);
                drawer.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
            }

            ImproveBrandReadability(form);
        }

        form.Shown += (_, _) =>
        {
            try { form.BeginInvoke((Action)ApplyReferenceGeometry); } catch { }
        };
        form.Resize += (_, _) => ApplyReferenceGeometry();

        var workspace = Get<Panel>("_workspace");
        if (workspace is not null)
        {
            workspace.ControlAdded += (_, _) =>
            {
                try
                {
                    form.BeginInvoke((Action)(() =>
                    {
                        ApplyReferenceGeometry();
                        // One second layout pass catches nested controls created by
                        // WinForms after the page root was inserted.
                        try { form.BeginInvoke((Action)ApplyReferenceGeometry); } catch { }
                    }));
                }
                catch { }
            };
        }
    }

    private static void SetPercent(SplitContainer? split, int percent)
    {
        if (split is null || split.IsDisposed || split.Panel2Collapsed) return;
        try
        {
            var total = split.Orientation == Orientation.Vertical
                ? split.ClientSize.Width
                : split.ClientSize.Height;
            var available = total - split.SplitterWidth;
            if (available < 160) return;
            var distance = (int)Math.Round(available * Math.Clamp(percent, 10, 90) / 100.0);
            distance = Math.Clamp(distance, 80, Math.Max(80, available - 80));
            split.SplitterDistance = distance;
        }
        catch { }
    }

    private static void ImproveBrandReadability(Control root)
    {
        foreach (var label in Descendants(root).OfType<Label>())
        {
            if (string.Equals(label.Text, "GOLD BAR", StringComparison.OrdinalIgnoreCase))
            {
                label.Font = new Font("Segoe UI", 19f, FontStyle.Bold);
                label.AutoEllipsis = false;
                label.TextAlign = ContentAlignment.MiddleCenter;
                label.RightToLeft = RightToLeft.No;
            }
            else if (label.Text.Contains("Windows Desktop", StringComparison.OrdinalIgnoreCase))
            {
                label.ForeColor = Color.FromArgb(150, 159, 176);
            }
            else if (label.Text.StartsWith("GOLD BAR", StringComparison.OrdinalIgnoreCase)
                && label.Text.Contains('v'))
            {
                label.Text = "GOLD BAR • v1.5.1";
                label.RightToLeft = RightToLeft.No;
            }
        }

        foreach (var link in Descendants(root).OfType<LinkLabel>())
        {
            if (link.Text.Contains("Amirnourhan", StringComparison.OrdinalIgnoreCase))
            {
                link.Font = new Font("Segoe UI", 9.4f, FontStyle.Bold);
                link.TextAlign = ContentAlignment.MiddleCenter;
                link.RightToLeft = RightToLeft.No;
            }
        }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
