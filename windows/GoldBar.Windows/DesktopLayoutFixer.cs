using System.Reflection;

namespace GoldBar.Windows;

internal static class DesktopLayoutFixer
{
    private const int HorizontalPagePadding = 48;

    public static void Attach(DesktopMainForm form)
    {
        var workspaceField = typeof(DesktopMainForm).GetField("_workspace", BindingFlags.Instance | BindingFlags.NonPublic);
        if (workspaceField?.GetValue(form) is not Panel workspace) return;

        void ScheduleFix()
        {
            if (form.IsDisposed || !form.IsHandleCreated) return;
            try
            {
                form.BeginInvoke((Action)(() => FixWorkspace(workspace)));
            }
            catch { }
        }

        form.Shown += (_, _) => ScheduleFix();
        form.Resize += (_, _) => ScheduleFix();
        workspace.ControlAdded += (_, _) => ScheduleFix();
        workspace.SizeChanged += (_, _) => ScheduleFix();
    }

    private static void FixWorkspace(Panel workspace)
    {
        if (workspace.IsDisposed || workspace.ClientSize.Width <= 100) return;

        foreach (Control page in workspace.Controls)
        {
            page.Dock = DockStyle.Fill;
            FixRecursive(page);
        }
    }

    private static void FixRecursive(Control control)
    {
        if (control is Panel scroll && scroll.AutoScroll)
        {
            foreach (Control child in scroll.Controls)
            {
                if (child is FlowLayoutPanel stack && stack.FlowDirection == FlowDirection.TopDown)
                {
                    var width = Math.Max(760, scroll.ClientSize.Width - HorizontalPagePadding);
                    stack.AutoSize = true;
                    stack.WrapContents = false;
                    stack.Width = width;
                    stack.MinimumSize = new Size(width, 0);

                    foreach (Control item in stack.Controls)
                    {
                        item.Width = width;
                        item.MinimumSize = new Size(width, item.MinimumSize.Height);
                        item.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                        FixDashboardRow(item, width);
                    }
                }
            }
        }

        foreach (Control child in control.Controls)
            FixRecursive(child);
    }

    private static void FixDashboardRow(Control control, int availableWidth)
    {
        if (control is TableLayoutPanel table)
        {
            table.Width = availableWidth;
            table.MinimumSize = new Size(availableWidth, table.MinimumSize.Height);
            table.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

            // Desktop dashboard rows must always stretch their cells. Without this,
            // WinForms RTL + AutoSize can collapse cards into narrow vertical strips.
            if (table.ColumnCount is 2 or 4)
            {
                foreach (Control cell in table.Controls)
                {
                    cell.Dock = DockStyle.Fill;
                    cell.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
                }
            }
        }

        if (control is RoundedPanel card)
        {
            card.AutoSize = false;
            card.Width = availableWidth;
            if (card.Height < 120) card.Height = 220;
            foreach (Control child in card.Controls)
            {
                if (child.Dock == DockStyle.None)
                    child.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            }
        }

        foreach (Control child in control.Controls)
        {
            if (child is TableLayoutPanel nested)
            {
                nested.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                if (nested.ColumnCount is 2 or 4)
                {
                    foreach (Control cell in nested.Controls)
                        cell.Dock = DockStyle.Fill;
                }
            }
        }
    }
}
