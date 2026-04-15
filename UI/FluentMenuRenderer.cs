using System.Drawing.Drawing2D;
using Microsoft.Win32;

namespace WindowsAssistant.UI;

/// <summary>
/// <see cref="ToolStripProfessionalRenderer"/> that approximates the
/// Windows 11 Fluent Design menu look: dark/light palette synced to the
/// system theme, rounded hover highlight, padded items, subtle separators,
/// and accent colors for checked items. Paired with DWM rounded corners
/// on the menu popup it gives a look close to native Win11 apps.
/// </summary>
internal sealed class FluentMenuRenderer : ToolStripProfessionalRenderer
{
    public FluentMenuRenderer() : base(new FluentColors(IsSystemDark()))
    {
        RoundedEdges = false; // we draw our own rounded highlights
    }

    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Rounded highlight on menu item hover / selection.</summary>
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item is not ToolStripMenuItem item) { base.OnRenderMenuItemBackground(e); return; }
        if (!item.Selected && !item.Pressed)   return;

        var colors = (FluentColors)ColorTable;
        var rect   = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);

        using var path  = RoundedRectangle(rect, radius: 4);
        using var brush = new SolidBrush(item.Pressed ? colors.ItemPressed : colors.ItemHover);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, path);
    }

    /// <summary>Dim separators so they're visible but not noisy.</summary>
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var colors = (FluentColors)ColorTable;
        int y = e.Item.Height / 2;
        using var pen = new Pen(colors.Separator, 1f);
        e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
    }

    /// <summary>Accent color on the check glyph.</summary>
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var colors = (FluentColors)ColorTable;
        var rect   = new Rectangle(
            e.ImageRectangle.X, e.ImageRectangle.Y + 1,
            e.ImageRectangle.Width, e.ImageRectangle.Height);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var pen = new Pen(colors.Accent, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        // Draw a clean checkmark: two segments forming a tick.
        e.Graphics.DrawLines(pen, new[]
        {
            new PointF(rect.Left  + rect.Width * 0.20f, rect.Top    + rect.Height * 0.55f),
            new PointF(rect.Left  + rect.Width * 0.42f, rect.Top    + rect.Height * 0.78f),
            new PointF(rect.Left  + rect.Width * 0.80f, rect.Top    + rect.Height * 0.28f),
        });
    }

    /// <summary>Don't draw the image margin bar — Win11 menus don't use it.</summary>
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        // deliberately empty
    }

    private static GraphicsPath RoundedRectangle(Rectangle rect, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X,             rect.Y,              d, d, 180, 90);
        path.AddArc(rect.Right - d,     rect.Y,              d, d, 270, 90);
        path.AddArc(rect.Right - d,     rect.Bottom - d,     d, d, 0,   90);
        path.AddArc(rect.X,             rect.Bottom - d,     d, d, 90,  90);
        path.CloseFigure();
        return path;
    }

    /// <summary>
    /// Fluent-esque color palette. Two variants: dark (close to Mica dark)
    /// and light (close to Mica light). The palette is locked at menu
    /// creation — if the user flips the system theme the app should be
    /// restarted for the new colors to take effect (cheap to accept here).
    /// </summary>
    internal sealed class FluentColors : ProfessionalColorTable
    {
        private readonly bool _dark;

        public FluentColors(bool dark)
        {
            _dark = dark;
            UseSystemColors = false;
        }

        public Color Background    => _dark ? Color.FromArgb(255, 43, 43, 43)  : Color.FromArgb(255, 249, 249, 249);
        public Color Foreground    => _dark ? Color.FromArgb(255, 240, 240, 240) : Color.FromArgb(255, 24, 24, 24);
        public Color ItemHover     => _dark ? Color.FromArgb(255, 60, 60, 60)   : Color.FromArgb(255, 237, 237, 237);
        public Color ItemPressed   => _dark ? Color.FromArgb(255, 72, 72, 72)   : Color.FromArgb(255, 224, 224, 224);
        public Color Separator     => _dark ? Color.FromArgb(255, 70, 70, 70)   : Color.FromArgb(255, 220, 220, 220);
        public Color Accent        => Color.FromArgb(255, 118, 185, 237); // Windows 11 default accent (blue)

        public override Color ToolStripDropDownBackground => Background;
        public override Color MenuBorder                  => Background;
        public override Color MenuItemBorder              => Color.Transparent;
        public override Color MenuItemSelected            => Color.Transparent;
        public override Color MenuItemSelectedGradientBegin => Color.Transparent;
        public override Color MenuItemSelectedGradientEnd   => Color.Transparent;
        public override Color MenuItemPressedGradientBegin  => Color.Transparent;
        public override Color MenuItemPressedGradientEnd    => Color.Transparent;
        public override Color MenuItemPressedGradientMiddle => Color.Transparent;
        public override Color ImageMarginGradientBegin  => Background;
        public override Color ImageMarginGradientMiddle => Background;
        public override Color ImageMarginGradientEnd    => Background;
        public override Color SeparatorDark             => Separator;
        public override Color SeparatorLight            => Separator;
    }
}
