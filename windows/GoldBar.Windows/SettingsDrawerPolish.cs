using System.Reflection;

namespace GoldBar.Windows;

internal static class SettingsDrawerPolish
{
    public static void Attach(ModernMainForm form)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var drawer = typeof(ModernMainForm).GetField("_settingsDrawer", flags)?.GetValue(form) as RoundedPanel;
        var workspace = typeof(ModernMainForm).GetField("_workspace", flags)?.GetValue(form) as Panel;
        if (drawer is null || workspace is null) return;

        void Arrange()
        {
            if (!drawer.Visible || workspace.IsDisposed || workspace.ClientSize.Width < 300) return;
            if (!ReferenceEquals(drawer.Parent, workspace))
            {
                drawer.Parent?.Controls.Remove(drawer);
                workspace.Controls.Add(drawer);
            }

            var width = Math.Clamp((int)(workspace.ClientSize.Width * 0.34), 405, 500);
            var height = Math.Max(420, workspace.ClientSize.Height - 18);
            drawer.Dock = DockStyle.None;
            drawer.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
            drawer.Bounds = new Rectangle(
                Math.Max(8, workspace.ClientSize.Width - width - 10),
                9,
                width,
                height);
            drawer.BringToFront();
        }

        drawer.VisibleChanged += (_, _) =>
        {
            if (drawer.Visible)
                drawer.BeginInvoke((Action)Arrange);
        };
        workspace.SizeChanged += (_, _) => Arrange();
        form.Resize += (_, _) => Arrange();
    }
}
