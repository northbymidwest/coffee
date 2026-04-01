using System.Drawing;
using System.Runtime.InteropServices;

namespace Coffee;

sealed class CoffeeApp : IDisposable
{
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint esFlags);

    private const uint ES_CONTINUOUS = 0x80000000;
    private const uint ES_SYSTEM_REQUIRED = 0x00000001;
    private const uint ES_DISPLAY_REQUIRED = 0x00000002;
    private const uint ES_AWAYMODE_REQUIRED = 0x00000040;

    private readonly NotifyIcon _trayIcon;
    private bool _caffeinated;

    public CoffeeApp()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = CreateCupIcon(filled: false),
            Text = "Coffee - Click to stay awake",
            Visible = true,
        };

        _trayIcon.MouseClick += OnTrayClick;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Exit", null, (_, _) =>
        {
            Decaffeinate();
            _trayIcon.Visible = false;
            Application.Exit();
        });
        _trayIcon.ContextMenuStrip = menu;
    }

    private void OnTrayClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;

        _caffeinated = !_caffeinated;

        if (_caffeinated)
            Caffeinate();
        else
            Decaffeinate();

        _trayIcon.Icon = CreateCupIcon(filled: _caffeinated);
        _trayIcon.Text = _caffeinated
            ? "Coffee - Staying awake (click to stop)"
            : "Coffee - Click to stay awake";
    }

    private static void Caffeinate()
    {
        SetThreadExecutionState(
            ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED | ES_AWAYMODE_REQUIRED);
    }

    private static void Decaffeinate()
    {
        SetThreadExecutionState(ES_CONTINUOUS);
    }

    private static Icon CreateCupIcon(bool filled)
    {
        const int size = 64;
        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using var outlinePen = new Pen(Color.White, 3f);
        using var fillBrush = new SolidBrush(Color.SaddleBrown);

        // Cup body
        var cupRect = new Rectangle(8, 16, 36, 38);
        if (filled)
        {
            g.FillRectangle(fillBrush, cupRect);
        }
        g.DrawRectangle(outlinePen, cupRect);

        // Handle
        g.DrawArc(outlinePen, 44, 22, 14, 24, -90, 180);

        // Steam wisps (only when full)
        if (filled)
        {
            using var steamPen = new Pen(Color.LightGray, 2f);
            // Left wisp
            g.DrawBezier(steamPen,
                new Point(18, 16), new Point(14, 8),
                new Point(22, 4), new Point(18, -2));
            // Middle wisp
            g.DrawBezier(steamPen,
                new Point(28, 16), new Point(32, 6),
                new Point(24, 4), new Point(28, -4));
            // Right wisp
            g.DrawBezier(steamPen,
                new Point(38, 16), new Point(34, 8),
                new Point(42, 4), new Point(38, -2));
        }

        var handle = bmp.GetHicon();
        return Icon.FromHandle(handle);
    }

    public void Dispose()
    {
        Decaffeinate();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
    }
}
