using System.Drawing;
using System.Windows.Forms;

namespace Harmony.Helpers;

/// <summary>Dark theme for WinForms tray context menus.</summary>
public sealed class DarkTrayMenuRenderer : ToolStripProfessionalRenderer
{
    private static readonly Color Bg = Color.FromArgb(18, 20, 28);
    private static readonly Color BgHover = Color.FromArgb(36, 40, 54);
    private static readonly Color Border = Color.FromArgb(40, 255, 255, 255);
    private static readonly Color Text = Color.FromArgb(235, 235, 240);
    private static readonly Color Muted = Color.FromArgb(140, 145, 160);

    public DarkTrayMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(Border);
        var rect = new Rectangle(0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
        e.Graphics.DrawRectangle(pen, rect);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var g = e.Graphics;
        var rect = new Rectangle(Point.Empty, e.Item.Size);
        if (e.Item.Selected && e.Item.Enabled)
            using (var brush = new SolidBrush(BgHover))
                g.FillRectangle(brush, rect);
        else if (e.Item is ToolStripMenuItem { OwnerItem: null } && e.Item.Tag as string == "header")
            using (var brush = new SolidBrush(Color.FromArgb(28, 30, 40)))
                g.FillRectangle(brush, rect);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Tag as string == "header" ? Muted : Text;
        base.OnRenderItemText(e);
    }

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color ToolStripDropDownBackground => Bg;
        public override Color ImageMarginGradientBegin => Bg;
        public override Color ImageMarginGradientMiddle => Bg;
        public override Color ImageMarginGradientEnd => Bg;
        public override Color SeparatorDark => Color.FromArgb(30, 255, 255, 255);
        public override Color SeparatorLight => Color.FromArgb(15, 255, 255, 255);
    }
}
