using System.Drawing;
using System.Runtime.InteropServices;

namespace Coffee;

sealed class CoffeeApp : IDisposable
{
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint esFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr RegisterPowerSettingNotification(
        IntPtr hRecipient, ref Guid powerSettingGuid, int flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterPowerSettingNotification(IntPtr handle);

    private const uint ES_CONTINUOUS = 0x80000000;
    private const uint ES_SYSTEM_REQUIRED = 0x00000001;
    private const uint ES_DISPLAY_REQUIRED = 0x00000002;
    private const uint ES_AWAYMODE_REQUIRED = 0x00000040;

    private const int WM_POWERBROADCAST = 0x0218;
    private const int PBT_POWERSETTINGCHANGE = 0x8013;
    private const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

    private static Guid GUID_LIDSWITCH_STATE_CHANGE =
        new("BA3E0F4D-B817-4094-A2D1-D56379E6A0F3");

    [StructLayout(LayoutKind.Sequential)]
    private struct POWERBROADCAST_SETTING
    {
        public Guid PowerSetting;
        public uint DataLength;
        public byte Data;
    }

    private readonly NotifyIcon _trayIcon;
    private readonly PowerWindow _powerWindow;
    private readonly System.Windows.Forms.Timer _batteryTimer;
    private IntPtr _lidNotification;
    private bool _caffeinated;

    private const float BatteryThreshold = 0.15f;

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

        // Hidden window for power notifications
        _powerWindow = new PowerWindow(this);
        _lidNotification = RegisterPowerSettingNotification(
            _powerWindow.Handle, ref GUID_LIDSWITCH_STATE_CHANGE, DEVICE_NOTIFY_WINDOW_HANDLE);

        // Poll battery every 30 seconds
        _batteryTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _batteryTimer.Tick += OnBatteryCheck;
        _batteryTimer.Start();
    }

    private void OnTrayClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        Toggle();
    }

    private void Toggle()
    {
        if (_caffeinated)
            SetState(false);
        else
            SetState(true);
    }

    private void SetState(bool caffeinated)
    {
        _caffeinated = caffeinated;

        if (_caffeinated)
            Caffeinate();
        else
            Decaffeinate();

        _trayIcon.Icon = CreateCupIcon(filled: _caffeinated);
        _trayIcon.Text = _caffeinated
            ? "Coffee - Staying awake (click to stop)"
            : "Coffee - Click to stay awake";
    }

    private void OnLidClosed()
    {
        if (_caffeinated)
            SetState(false);
    }

    private void OnBatteryCheck(object? sender, EventArgs e)
    {
        if (!_caffeinated) return;

        var power = SystemInformation.PowerStatus;
        // Only act if on battery and below threshold
        if (power.PowerLineStatus == PowerLineStatus.Offline
            && power.BatteryLifePercent < BatteryThreshold)
        {
            SetState(false);
        }
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
            g.DrawBezier(steamPen,
                new Point(18, 16), new Point(14, 8),
                new Point(22, 4), new Point(18, -2));
            g.DrawBezier(steamPen,
                new Point(28, 16), new Point(32, 6),
                new Point(24, 4), new Point(28, -4));
            g.DrawBezier(steamPen,
                new Point(38, 16), new Point(34, 8),
                new Point(42, 4), new Point(38, -2));
        }

        var handle = bmp.GetHicon();
        return Icon.FromHandle(handle);
    }

    public void Dispose()
    {
        _batteryTimer.Stop();
        _batteryTimer.Dispose();
        if (_lidNotification != IntPtr.Zero)
            UnregisterPowerSettingNotification(_lidNotification);
        _powerWindow.DestroyHandle();
        Decaffeinate();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
    }

    private class PowerWindow : NativeWindow
    {
        private readonly CoffeeApp _app;

        public PowerWindow(CoffeeApp app)
        {
            _app = app;
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_POWERBROADCAST && (int)m.WParam == PBT_POWERSETTINGCHANGE)
            {
                var setting = Marshal.PtrToStructure<POWERBROADCAST_SETTING>(m.LParam);
                if (setting.PowerSetting == GUID_LIDSWITCH_STATE_CHANGE)
                {
                    // Data == 0 means lid closed
                    if (setting.Data == 0)
                        _app.OnLidClosed();
                }
            }
            base.WndProc(ref m);
        }
    }
}
