using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("WeekTray")]
[assembly: AssemblyProduct("WeekTray")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace WeekTray
{
    public static class IsoWeek
    {
        // An ISO week belongs to the year of its Thursday, and week 1 is the week holding the first Thursday.
        public static int GetWeek(DateTime date)
        {
            return (GetThursday(date).DayOfYear - 1) / 7 + 1;
        }

        public static int GetYear(DateTime date)
        {
            return GetThursday(date).Year;
        }

        public static DateTime GetMonday(DateTime date)
        {
            int daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
            return date.Date.AddDays(-daysSinceMonday);
        }

        private static DateTime GetThursday(DateTime date)
        {
            return GetMonday(date).AddDays(3);
        }

        // Returns the current week followed by the given number of upcoming weeks.
        public static WeekRow[] GetOverview(DateTime today, int weeksAhead)
        {
            WeekRow[] rows = new WeekRow[weeksAhead + 1];
            DateTime monday = GetMonday(today);
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = new WeekRow(GetWeek(monday), DescribePeriod(monday, today), i == 0);
                monday = monday.AddDays(7);
            }
            return rows;
        }

        // The year is only spelled out when it differs from the current one or changes within the week.
        private static string DescribePeriod(DateTime monday, DateTime today)
        {
            CultureInfo dutch = CultureInfo.GetCultureInfo("nl-NL");
            DateTime sunday = monday.AddDays(6);
            string startFormat = monday.Month != sunday.Month ? "d MMMM" : "%d";
            string endFormat = sunday.Year != today.Year || monday.Year != sunday.Year ? "d MMMM yyyy" : "d MMMM";
            return monday.ToString(startFormat, dutch) + " t/m " + sunday.ToString(endFormat, dutch);
        }
    }

    public sealed class WeekRow
    {
        public WeekRow(int week, string period, bool isCurrent)
        {
            Week = week;
            Period = period;
            IsCurrent = isCurrent;
        }

        public int Week { get; private set; }
        public string Period { get; private set; }
        public bool IsCurrent { get; private set; }
    }

    internal static class Theme
    {
        public static bool IsLight()
        {
            object value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "SystemUsesLightTheme", 0);
            return value is int && (int)value == 1;
        }
    }

    internal static class Settings
    {
        public const int DefaultWeeksAhead = 8;
        private const string KeyPath = @"Software\WeekTray";
        private const string ValueName = "WeeksAhead";

        public static int WeeksAhead
        {
            get
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath))
                {
                    object value = key == null ? null : key.GetValue(ValueName);
                    return value is int && (int)value >= 1 && (int)value <= 52 ? (int)value : DefaultWeeksAhead;
                }
            }
            set
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    key.SetValue(ValueName, value, RegistryValueKind.DWord);
                }
            }
        }
    }

    // Replaces the native tooltip, which is limited to a single short text.
    internal sealed class WeekOverviewForm : Form
    {
        private const int WsExTopmost = 0x8;
        private const int WsExTransparent = 0x20;
        private const int WsExToolWindow = 0x80;
        private const int WsExNoActivate = 0x08000000;
        private const int CsDropShadow = 0x20000;
        private const int DwmWindowCornerPreference = 33;
        private const int DwmCornerRound = 2;
        private const TextFormatFlags TextFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix
            | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;

        private readonly Font boldFont;
        private readonly float scale;
        private WeekRow[] rows = new WeekRow[0];
        private int weekColumnWidth;
        private int rowHeight;
        private Color textColor;
        private Color highlightColor;

        public WeekOverviewForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 9.75f);
            boldFont = new Font(Font, FontStyle.Bold);
            using (Graphics graphics = CreateGraphics())
            {
                scale = graphics.DpiX / 96f;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= WsExTopmost | WsExTransparent | WsExToolWindow | WsExNoActivate;
                parameters.ClassStyle |= CsDropShadow;
                return parameters;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int preference = DwmCornerRound;
            DwmSetWindowAttribute(Handle, DwmWindowCornerPreference, ref preference, sizeof(int));
        }

        public void ShowAt(Point anchor, WeekRow[] overview)
        {
            rows = overview;
            bool light = Theme.IsLight();
            BackColor = light ? Color.FromArgb(249, 249, 249) : Color.FromArgb(44, 44, 44);
            textColor = light ? Color.FromArgb(28, 28, 28) : Color.FromArgb(245, 245, 245);
            highlightColor = light ? Color.FromArgb(230, 230, 230) : Color.FromArgb(68, 68, 68);

            int periodWidth = 0;
            int textHeight = 0;
            weekColumnWidth = 0;
            foreach (WeekRow row in rows)
            {
                Size week = TextRenderer.MeasureText(WeekLabel(row), boldFont, Size.Empty, TextFlags);
                Size period = TextRenderer.MeasureText(row.Period, boldFont, Size.Empty, TextFlags);
                weekColumnWidth = Math.Max(weekColumnWidth, week.Width);
                periodWidth = Math.Max(periodWidth, period.Width);
                textHeight = Math.Max(textHeight, period.Height);
            }
            rowHeight = textHeight + Scaled(8);
            Size = new Size(
                Scaled(6) * 2 + Scaled(10) * 2 + weekColumnWidth + Scaled(20) + periodWidth,
                Scaled(6) * 2 + rowHeight * rows.Length);
            Location = GetLocation(anchor);

            Invalidate();
            if (!Visible)
            {
                Show();
            }
        }

        private Point GetLocation(Point anchor)
        {
            Rectangle area = Screen.FromPoint(anchor).WorkingArea;
            int margin = Scaled(12);
            int x = Math.Max(area.Left + margin, Math.Min(anchor.X - Width / 2, area.Right - Width - margin));
            int y = Math.Min(anchor.Y - margin - Height, area.Bottom - Height - margin);
            if (y < area.Top + margin)
            {
                y = Math.Max(area.Top, anchor.Y) + margin;
            }
            return new Point(x, y);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int padding = Scaled(6);
            int rowPadding = Scaled(10);
            for (int i = 0; i < rows.Length; i++)
            {
                WeekRow row = rows[i];
                Font font = row.IsCurrent ? boldFont : Font;
                Rectangle bounds = new Rectangle(padding, padding + i * rowHeight, Width - padding * 2, rowHeight);
                if (row.IsCurrent)
                {
                    using (Brush brush = new SolidBrush(highlightColor))
                    {
                        e.Graphics.FillRectangle(brush, bounds);
                    }
                }
                int weekLeft = bounds.Left + rowPadding;
                int periodLeft = weekLeft + weekColumnWidth + Scaled(20);
                TextRenderer.DrawText(e.Graphics, WeekLabel(row), font,
                    new Rectangle(weekLeft, bounds.Top, weekColumnWidth, rowHeight), textColor, TextFlags);
                TextRenderer.DrawText(e.Graphics, row.Period, font,
                    new Rectangle(periodLeft, bounds.Top, bounds.Right - periodLeft, rowHeight), textColor, TextFlags);
            }
        }

        private static string WeekLabel(WeekRow row)
        {
            return "Week " + row.Week.ToString(CultureInfo.InvariantCulture);
        }

        private int Scaled(int value)
        {
            return (int)Math.Round(value * scale);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                boldFont.Dispose();
            }
            base.Dispose(disposing);
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    }

    public static class IconRenderer
    {
        public static Bitmap Render(int week, int size, bool lightTaskbar)
        {
            Bitmap bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (GraphicsPath path = new GraphicsPath())
            using (FontFamily family = new FontFamily("Segoe UI"))
            using (StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone())
            using (Brush brush = new SolidBrush(lightTaskbar ? Color.FromArgb(28, 28, 28) : Color.White))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                path.AddString(week.ToString(CultureInfo.InvariantCulture), family, (int)FontStyle.Bold,
                    size * 4f, PointF.Empty, format);

                // Stretch the digits to fill the square: the height is what makes them readable at 16 px.
                RectangleF bounds = path.GetBounds();
                float scaleY = size * 0.875f / bounds.Height;
                float scaleX = Math.Min(size / bounds.Width, scaleY);
                using (Matrix matrix = new Matrix())
                {
                    matrix.Translate(size / 2f, size / 2f);
                    matrix.Scale(scaleX, scaleY);
                    matrix.Translate(-(bounds.Left + bounds.Width / 2f), -(bounds.Top + bounds.Height / 2f));
                    path.Transform(matrix);
                }
                graphics.FillPath(brush, path);
            }
            return bitmap;
        }

        public static Icon RenderIcon(int week, int size, bool lightTaskbar)
        {
            using (Bitmap bitmap = Render(week, size, lightTaskbar))
            {
                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon borrowed = Icon.FromHandle(handle))
                    {
                        return (Icon)borrowed.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);
    }

    internal static class Autostart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "WeekTray";

        public static bool IsEnabled
        {
            get
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    return key != null && key.GetValue(ValueName) != null;
                }
            }
            set
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value)
                    {
                        key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                    }
                    else
                    {
                        key.DeleteValue(ValueName, false);
                    }
                }
            }
        }
    }

    internal static class NotifyIconBounds
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        // NotifyIcon does not expose the window and id that identify it to the shell.
        public static bool TryGet(NotifyIcon icon, out Rectangle bounds)
        {
            bounds = Rectangle.Empty;
            FieldInfo windowField = typeof(NotifyIcon).GetField("window", PrivateInstance);
            FieldInfo idField = typeof(NotifyIcon).GetField("id", PrivateInstance);
            if (windowField == null || idField == null)
            {
                return false;
            }

            NotifyIconIdentifier identifier = new NotifyIconIdentifier();
            identifier.Size = Marshal.SizeOf(typeof(NotifyIconIdentifier));
            identifier.Window = ((NativeWindow)windowField.GetValue(icon)).Handle;
            identifier.Id = (int)idField.GetValue(icon);

            NativeRect rect;
            if (Shell_NotifyIconGetRect(ref identifier, out rect) != 0)
            {
                return false;
            }
            bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            return true;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NotifyIconIdentifier
        {
            public int Size;
            public IntPtr Window;
            public int Id;
            public Guid Item;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("shell32.dll")]
        private static extern int Shell_NotifyIconGetRect(ref NotifyIconIdentifier identifier, out NativeRect rect);
    }

    internal sealed class TrayContext : ApplicationContext
    {
        // Polling also covers sleep, resume and manual clock changes without separate handlers.
        private const int RefreshIntervalMs = 60 * 1000;
        private const int HoverPollIntervalMs = 100;
        private const int HoverDelayMs = 300;

        private readonly NotifyIcon notifyIcon = new NotifyIcon();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer hoverTimer = new System.Windows.Forms.Timer();
        private readonly ToolStripMenuItem autostartItem = new ToolStripMenuItem("Starten met Windows");
        private readonly ToolStripMenuItem weeksAheadItem = new ToolStripMenuItem("Aantal weken vooruit");
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly WeekOverviewForm overviewForm = new WeekOverviewForm();
        private DateTime shownDate;
        private Icon currentIcon;
        private int hoverStartTicks;

        public TrayContext()
        {
            autostartItem.Click += delegate
            {
                Autostart.IsEnabled = !Autostart.IsEnabled;
                autostartItem.Checked = Autostart.IsEnabled;
            };

            foreach (int weeks in new[] { 4, 8, 12 })
            {
                int selected = weeks;
                ToolStripMenuItem item = new ToolStripMenuItem(selected.ToString(CultureInfo.InvariantCulture));
                item.Tag = selected;
                item.Click += delegate { Settings.WeeksAhead = selected; };
                weeksAheadItem.DropDownItems.Add(item);
            }

            menu.Items.Add(weeksAheadItem);
            menu.Items.Add(autostartItem);
            menu.Items.Add("Afsluiten", null, delegate { ExitThread(); });
            menu.Opening += delegate
            {
                overviewForm.Hide();
                autostartItem.Checked = Autostart.IsEnabled;
                foreach (ToolStripMenuItem item in weeksAheadItem.DropDownItems)
                {
                    item.Checked = (int)item.Tag == Settings.WeeksAhead;
                }
            };
            notifyIcon.ContextMenuStrip = menu;

            // The tray only reports mouse moves over the icon, never leaving it, so the cursor is polled while hovering.
            notifyIcon.MouseMove += delegate
            {
                if (!hoverTimer.Enabled)
                {
                    hoverStartTicks = Environment.TickCount;
                    hoverTimer.Start();
                }
            };
            hoverTimer.Interval = HoverPollIntervalMs;
            hoverTimer.Tick += OnHoverTick;

            Redraw();
            notifyIcon.Visible = true;

            timer.Interval = RefreshIntervalMs;
            timer.Tick += delegate
            {
                if (DateTime.Today != shownDate)
                {
                    Redraw();
                }
            };
            timer.Start();

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        private void OnHoverTick(object sender, EventArgs e)
        {
            // Mouse-move notifications can arrive after the cursor has left, so only the icon's real bounds count.
            Rectangle iconBounds;
            if (!NotifyIconBounds.TryGet(notifyIcon, out iconBounds) || !iconBounds.Contains(Cursor.Position))
            {
                hoverTimer.Stop();
                overviewForm.Hide();
            }
            else if (!menu.Visible && !overviewForm.Visible && Environment.TickCount - hoverStartTicks >= HoverDelayMs)
            {
                Point anchor = new Point(iconBounds.Left + iconBounds.Width / 2, iconBounds.Top);
                overviewForm.ShowAt(anchor, IsoWeek.GetOverview(DateTime.Today, Settings.WeeksAhead));
            }
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            Redraw();
        }

        private void Redraw()
        {
            shownDate = DateTime.Today;
            Icon previous = currentIcon;
            currentIcon = IconRenderer.RenderIcon(IsoWeek.GetWeek(shownDate), SystemInformation.SmallIconSize.Width, Theme.IsLight());
            notifyIcon.Icon = currentIcon;
            if (previous != null)
            {
                previous.Dispose();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                timer.Dispose();
                hoverTimer.Dispose();
                overviewForm.Dispose();
                menu.Dispose();
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
                if (currentIcon != null)
                {
                    currentIcon.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool isFirstInstance;
            using (new Mutex(true, @"Local\WeekTray", out isFirstInstance))
            {
                if (!isFirstInstance)
                {
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayContext());
            }
        }
    }
}
