using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace StayAwake
{
    internal static class Native
    {
        [Flags]
        public enum EXECUTION_STATE : uint
        {
            ES_SYSTEM_REQUIRED = 0x00000001,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_CONTINUOUS = 0x80000000,
        }

        // Method 1: tell Windows this thread needs the system (and optionally the display) awake.
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool DestroyIcon(IntPtr handle);
    }

    // ---------------- Localization (Persian / English) ----------------

    internal enum Lang { Fa, En }

    internal static class L
    {
        public static Lang Current = Lang.En;
        public static bool IsFa => Current == Lang.Fa;
        private static string T(string fa, string en) => IsFa ? fa : en;

        public static string Title => T("StayAwake – جلوگیری از Sleep", "StayAwake – Prevent Sleep");
        public static string Duration => T("مدت:", "Duration:");
        public static string Hours => T("ساعت", "hours");
        public static string Minutes => T("دقیقه", "minutes");
        public static string Indefinite => T("نامحدود (تا وقتی دستی متوقف کنم)", "Indefinitely (until I stop it)");
        public static string KeepDisplay => T("صفحه‌نمایش هم روشن بماند", "Keep the display on too");
        public static string Start => T("شروع", "Start");
        public static string Stop => T("توقف", "Stop");
        public static string LanguageLabel => T("زبان / Language:", "Language / زبان:");
        public static string StatusOff => T("وضعیت: غیرفعال", "Status: inactive");
        public static string StatusLeft(string t) => T($"وضعیت: فعال — باقی‌مانده {t}", $"Status: active — {t} left");
        public static string StatusInf => T("وضعیت: فعال — نامحدود", "Status: active — indefinitely");
        public static string TipOff => T("StayAwake: غیرفعال", "StayAwake: inactive");
        public static string TipLeft(string t) => T($"StayAwake: فعال — {t}", $"StayAwake: active — {t} left");
        public static string TipInf => T("StayAwake: فعال (نامحدود)", "StayAwake: active (indefinite)");
        public static string MenuShow => T("نمایش پنجره", "Show window");
        public static string MenuQuick => T("شروع سریع", "Quick start");
        public static string QuickHours(int h) => T($"{h} ساعت", h == 1 ? "1 hour" : $"{h} hours");
        public static string QuickInf => T("نامحدود", "Indefinitely");
        public static string MenuLang => "زبان / Language";
        public static string MenuExit => T("خروج", "Exit");
        public static string ErrZero => T("مدت زمان باید بیشتر از صفر باشد.", "Duration must be greater than zero.");
        public static string ErrApi => T("فراخوانی SetThreadExecutionState ناموفق بود.", "SetThreadExecutionState call failed.");
        public static string Expired => T("زمان به پایان رسید؛ سیستم دوباره می‌تواند Sleep شود.",
                                          "Time is up; the computer can sleep normally again.");
        public static string TrayHint => T("برنامه کنار ساعت ادامه می‌دهد. برای بستن کامل، از منوی راست‌کلیک «خروج» را بزنید.",
                                           "StayAwake keeps running in the tray. Right-click the icon and choose Exit to quit.");

        // ---- persistence: %APPDATA%\StayAwake\lang.txt ----
        private static string SettingsPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StayAwake", "lang.txt");

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var s = File.ReadAllText(SettingsPath).Trim().ToLowerInvariant();
                    Current = s == "fa" ? Lang.Fa : Lang.En;
                    return;
                }
            }
            catch { }
            // First run: follow the Windows display language.
            Current = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fa" ? Lang.Fa : Lang.En;
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                File.WriteAllText(SettingsPath, IsFa ? "fa" : "en");
            }
            catch { }
        }
    }

    internal static class Program
    {
        private const string MutexName = "Local\\StayAwake_SingleInstance_7F3A";
        private const string ShowEventName = "Local\\StayAwake_ShowWindow_7F3A";

        [STAThread]
        private static void Main()
        {
            using (var mutex = new Mutex(true, MutexName, out bool isFirst))
            {
                if (!isFirst)
                {
                    // Already running: ask the existing instance to show its window, then quit.
                    try
                    {
                        using (var ev = EventWaitHandle.OpenExisting(ShowEventName)) ev.Set();
                    }
                    catch { }
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                L.Load();

                using (var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName))
                {
                    var form = new MainForm();
                    ThreadPool.RegisterWaitForSingleObject(showEvent, (s, t) =>
                    {
                        try { form.BeginInvoke(new Action(form.ShowMain)); } catch { }
                    }, null, Timeout.Infinite, false);

                    Application.Run(form);
                }
            }
        }
    }

    internal sealed class MainForm : Form
    {
        private static readonly int[] QuickHoursList = { 1, 2, 4, 8 };

        private readonly NumericUpDown nudHours = new NumericUpDown();
        private readonly NumericUpDown nudMinutes = new NumericUpDown();
        private readonly Label lblDuration = MakeLabel();
        private readonly Label lblHours = MakeLabel();
        private readonly Label lblMinutes = MakeLabel();
        private readonly Label lblLang = MakeLabel();
        private readonly ComboBox cmbLang = new ComboBox();
        private readonly CheckBox chkIndefinite = new CheckBox();
        private readonly CheckBox chkDisplay = new CheckBox();
        private readonly Button btnToggle = new Button();
        private readonly Label lblStatus = new Label();

        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly ContextMenuStrip trayMenu = new ContextMenuStrip();
        private readonly ToolStripMenuItem miShow = new ToolStripMenuItem();
        private readonly ToolStripMenuItem miToggle = new ToolStripMenuItem();
        private readonly ToolStripMenuItem miQuick = new ToolStripMenuItem();
        private readonly ToolStripMenuItem miLang = new ToolStripMenuItem();
        private readonly ToolStripMenuItem miLangFa = new ToolStripMenuItem("فارسی");
        private readonly ToolStripMenuItem miLangEn = new ToolStripMenuItem("English");
        private readonly ToolStripMenuItem miExit = new ToolStripMenuItem();

        private readonly System.Windows.Forms.Timer tick = new System.Windows.Forms.Timer { Interval = 1000 };

        private readonly Icon iconOn;
        private readonly Icon iconOff;

        private bool active;
        private DateTime? endUtc;      // null = indefinite
        private bool exiting;
        private bool trayHintShown;
        private bool applyingLang;

        public MainForm()
        {
            iconOn = MakeIcon(true);
            iconOff = MakeIcon(false);

            Icon = iconOff;
            Font = new Font("Tahoma", 9.5f);   // Tahoma renders both Persian and Latin well
            RightToLeftLayout = true;          // mirrors the form only when RightToLeft = Yes
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(14);

            BuildLayout();
            BuildTray();

            tick.Tick += (s, e) => OnTick();
            ApplyLanguage();
        }

        // ---------------- UI ----------------

        private void BuildLayout()
        {
            var table = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
            };

            var langRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 8) };
            cmbLang.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbLang.Items.AddRange(new object[] { "فارسی", "English" });
            cmbLang.Width = 110;
            cmbLang.SelectedIndexChanged += (s, e) =>
            {
                if (applyingLang) return;
                var lang = cmbLang.SelectedIndex == 0 ? Lang.Fa : Lang.En;
                // Defer: switching RightToLeft recreates window handles, including this combo's.
                BeginInvoke(new Action(() => SetLanguage(lang)));
            };
            langRow.Controls.Add(lblLang);
            langRow.Controls.Add(cmbLang);

            var timeRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 6) };
            nudHours.Minimum = 0; nudHours.Maximum = 999; nudHours.Value = 2; nudHours.Width = 60;
            nudMinutes.Minimum = 0; nudMinutes.Maximum = 59; nudMinutes.Value = 0; nudMinutes.Width = 50;
            timeRow.Controls.Add(lblDuration);
            timeRow.Controls.Add(nudHours);
            timeRow.Controls.Add(lblHours);
            timeRow.Controls.Add(nudMinutes);
            timeRow.Controls.Add(lblMinutes);

            chkIndefinite.AutoSize = true;
            chkIndefinite.CheckedChanged += (s, e) => UpdateUi();

            chkDisplay.AutoSize = true;
            chkDisplay.Checked = true;

            btnToggle.AutoSize = true;
            btnToggle.MinimumSize = new Size(280, 34);
            btnToggle.Margin = new Padding(0, 10, 0, 8);
            btnToggle.Click += (s, e) => Toggle();

            lblStatus.AutoSize = true;
            lblStatus.Font = new Font(Font, FontStyle.Bold);

            table.Controls.Add(langRow);
            table.Controls.Add(timeRow);
            table.Controls.Add(chkIndefinite);
            table.Controls.Add(chkDisplay);
            table.Controls.Add(btnToggle);
            table.Controls.Add(lblStatus);
            Controls.Add(table);

            AcceptButton = btnToggle;
        }

        private static Label MakeLabel() => new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(3, 6, 3, 3),
        };

        private void BuildTray()
        {
            foreach (var h in QuickHoursList)
            {
                int hours = h;
                miQuick.DropDownItems.Add(new ToolStripMenuItem("", null, (s, e) => Start(TimeSpan.FromHours(hours))));
            }
            miQuick.DropDownItems.Add(new ToolStripMenuItem("", null, (s, e) => Start(null)));

            miLangFa.Click += (s, e) => SetLanguage(Lang.Fa);
            miLangEn.Click += (s, e) => SetLanguage(Lang.En);
            miLang.DropDownItems.AddRange(new ToolStripItem[] { miLangFa, miLangEn });

            miShow.Font = new Font(miShow.Font, FontStyle.Bold);
            miShow.Click += (s, e) => ShowMain();
            miToggle.Click += (s, e) => Toggle();
            miExit.Click += (s, e) => ExitApp();

            trayMenu.Items.AddRange(new ToolStripItem[]
            {
                miShow, miToggle, miQuick, new ToolStripSeparator(), miLang, new ToolStripSeparator(), miExit
            });

            tray.ContextMenuStrip = trayMenu;
            tray.Icon = iconOff;
            tray.Visible = true;
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowMain(); };
        }

        private void SetLanguage(Lang lang)
        {
            if (L.Current == lang) return;
            L.Current = lang;
            L.Save();
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            applyingLang = true;
            try
            {
                var rtl = L.IsFa ? RightToLeft.Yes : RightToLeft.No;
                RightToLeft = rtl;          // children inherit
                trayMenu.RightToLeft = rtl;

                Text = L.Title;
                lblLang.Text = L.LanguageLabel;
                lblDuration.Text = L.Duration;
                lblHours.Text = L.Hours;
                lblMinutes.Text = L.Minutes;
                chkIndefinite.Text = L.Indefinite;
                chkDisplay.Text = L.KeepDisplay;
                cmbLang.SelectedIndex = L.IsFa ? 0 : 1;

                miShow.Text = L.MenuShow;
                miQuick.Text = L.MenuQuick;
                for (int i = 0; i < QuickHoursList.Length; i++)
                    miQuick.DropDownItems[i].Text = L.QuickHours(QuickHoursList[i]);
                miQuick.DropDownItems[QuickHoursList.Length].Text = L.QuickInf;
                miLang.Text = L.MenuLang;
                miLangFa.Checked = L.IsFa;
                miLangEn.Checked = !L.IsFa;
                miExit.Text = L.MenuExit;
            }
            finally { applyingLang = false; }

            UpdateUi();
        }

        public void ShowMain()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        private void ShowMessage(string text, MessageBoxIcon icon)
        {
            var opts = L.IsFa ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : 0;
            MessageBox.Show(this, text, "StayAwake", MessageBoxButtons.OK, icon, MessageBoxDefaultButton.Button1, opts);
        }

        // ---------------- Logic ----------------

        private void Toggle()
        {
            if (active) { Stop(false); return; }

            if (chkIndefinite.Checked)
            {
                Start(null);
                return;
            }

            var span = new TimeSpan((int)nudHours.Value, (int)nudMinutes.Value, 0);
            if (span <= TimeSpan.Zero)
            {
                ShowMessage(L.ErrZero, MessageBoxIcon.Warning);
                return;
            }
            Start(span);
        }

        private void Start(TimeSpan? duration)
        {
            var flags = Native.EXECUTION_STATE.ES_CONTINUOUS | Native.EXECUTION_STATE.ES_SYSTEM_REQUIRED;
            if (chkDisplay.Checked) flags |= Native.EXECUTION_STATE.ES_DISPLAY_REQUIRED;

            // Must be called on the UI thread: the request is tied to the calling thread
            // and stays in effect until cleared or the thread/process ends.
            if (Native.SetThreadExecutionState(flags) == 0)
            {
                ShowMessage(L.ErrApi, MessageBoxIcon.Error);
                return;
            }

            active = true;
            endUtc = duration.HasValue ? DateTime.UtcNow + duration.Value : (DateTime?)null;
            tick.Start();
            UpdateUi();
        }

        private void Stop(bool expired)
        {
            // ES_CONTINUOUS alone clears the previous request -> normal power settings apply again.
            Native.SetThreadExecutionState(Native.EXECUTION_STATE.ES_CONTINUOUS);
            active = false;
            endUtc = null;
            tick.Stop();
            UpdateUi();

            if (expired)
                tray.ShowBalloonTip(5000, "StayAwake", L.Expired, ToolTipIcon.Info);
        }

        private void OnTick()
        {
            if (active && endUtc.HasValue && DateTime.UtcNow >= endUtc.Value)
                Stop(true);
            else
                UpdateUi();
        }

        private void UpdateUi()
        {
            string status, tip;
            if (!active)
            {
                status = L.StatusOff;
                tip = L.TipOff;
            }
            else if (endUtc.HasValue)
            {
                var left = endUtc.Value - DateTime.UtcNow;
                if (left < TimeSpan.Zero) left = TimeSpan.Zero;
                string t = $"{(int)left.TotalHours:00}:{left.Minutes:00}:{left.Seconds:00}";
                status = L.StatusLeft(t);
                tip = L.TipLeft(t);
            }
            else
            {
                status = L.StatusInf;
                tip = L.TipInf;
            }

            lblStatus.Text = status;
            lblStatus.ForeColor = active ? Color.FromArgb(0, 128, 60) : Color.DimGray;
            btnToggle.Text = active ? L.Stop : L.Start;
            miToggle.Text = active ? L.Stop : L.Start;
            miQuick.Enabled = !active;

            bool editable = !active;
            chkIndefinite.Enabled = editable;
            chkDisplay.Enabled = editable;
            nudHours.Enabled = nudMinutes.Enabled = editable && !chkIndefinite.Checked;

            var icon = active ? iconOn : iconOff;
            if (tray.Icon != icon) tray.Icon = icon;
            if (Icon != icon) Icon = icon;
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;   // NotifyIcon limit
        }

        // Close button (X) -> hide to tray. Real exit only from tray menu (or Windows shutdown).
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                if (!trayHintShown)
                {
                    trayHintShown = true;
                    tray.ShowBalloonTip(3000, "StayAwake", L.TrayHint, ToolTipIcon.Info);
                }
                return;
            }

            if (active) Native.SetThreadExecutionState(Native.EXECUTION_STATE.ES_CONTINUOUS);
            tray.Visible = false;
            base.OnFormClosing(e);
        }

        private void ExitApp()
        {
            exiting = true;
            if (active) Stop(false);
            tray.Visible = false;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tick.Dispose();
                tray.Dispose();
                trayMenu.Dispose();
                iconOn.Dispose();
                iconOff.Dispose();
            }
            base.Dispose(disposing);
        }

        // ---------------- Icon (drawn at runtime: green cup = active, gray = inactive) ----------------

        private static Icon MakeIcon(bool on)
        {
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                var main = on ? Color.FromArgb(0, 160, 80) : Color.FromArgb(130, 130, 130);
                using (var fill = new SolidBrush(main))
                using (var pen = new Pen(main, 3f))
                {
                    g.FillRectangle(fill, 5, 13, 17, 15);                 // cup body
                    g.FillEllipse(fill, 5, 22, 17, 8);                    // rounded bottom
                    g.DrawArc(pen, 18, 15, 9, 9, -90, 180);               // handle
                    if (on)
                    {
                        using (var steam = new Pen(main, 2f))
                        {
                            g.DrawBezier(steam, 9, 11, 7, 8, 11, 5, 9, 1);
                            g.DrawBezier(steam, 15, 11, 13, 8, 17, 5, 15, 1);
                        }
                    }
                }

                IntPtr h = bmp.GetHicon();
                try { return (Icon)Icon.FromHandle(h).Clone(); }
                finally { Native.DestroyIcon(h); }
            }
        }
    }
}
