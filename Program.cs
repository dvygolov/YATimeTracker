using System;
using System.IO;
using System.Windows.Forms;
using SharpHook;
using SharpHook.Reactive;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace TimeTracker
{
    class Program
    {
        private static NotifyIcon _notifyIcon;
        private static IReactiveGlobalHook _globalHook;
        private static DateTime? _startTime;
        private static string _configPath;
        private static string _csvPath;

        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            string baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string iconPath = Path.Combine(baseDirectory, "icon.ico");
            _configPath = Path.Combine(baseDirectory, "config.json");
            _csvPath = Path.Combine(baseDirectory, "work.csv");

            _notifyIcon = new NotifyIcon
            {
                Icon = new System.Drawing.Icon(iconPath),
                Text = "Yellow Time Tracker ver 0.1",
                Visible = true
            };

            var contextMenu = new ContextMenuStrip();
            var startStopMenuItem = new ToolStripMenuItem("Start Timer");
            var exitMenuItem = new ToolStripMenuItem("Exit");

            startStopMenuItem.Click += (s, e) => ToggleTimer();
            exitMenuItem.Click += (s, e) =>
            {
                if (_startTime != null)
                {
                    ToggleTimer(); // Stop the timer and log the time
                }

                _notifyIcon.Visible = false; // Hide the tray icon
                _notifyIcon.Dispose(); // Dispose of the tray icon
                _globalHook.Dispose(); // Dispose of the global hook
                Environment.Exit(0); // Forcefully exit the application
            };

            contextMenu.Items.Add(startStopMenuItem);
            contextMenu.Items.Add(exitMenuItem);

            _notifyIcon.ContextMenuStrip = contextMenu;

            LoadConfig();
            SubscribeHotKeys();

            Application.Run();
        }

        private static void LoadConfig()
        {
            if (!File.Exists(_configPath))
            {
                Console.WriteLine("Config file not found.");
                return;
            }

            var json = File.ReadAllText(_configPath);
            dynamic config = JObject.Parse(json);
            string hotkeyString = config.hotkey;
            var keyCombination = ParseHotkey(hotkeyString);
            SubscribeHotKeys(keyCombination);
        }

        private static void SubscribeHotKeys(IEnumerable<ModifierMask> keyCombination)
        {
            _globalHook = new SimpleReactiveGlobalHook();
            _globalHook.KeyPressed.Subscribe(e =>
            {
                if (keyCombination.Contains(e.Data.KeyCode) &&
                    keyCombination.All(k => e.Data.Modifiers.HasFlag(k)))
                {
                    ToggleTimer();
                }
            });
            _globalHook.Run();
        }

        private static IEnumerable<ModifierMask> ParseHotkey(string hotkeyString)
        {
            var keys = hotkeyString.Split('+');
            var modifiers = new List<ModifierMask>();

            foreach (var key in keys)
            {
                switch (key.Trim().ToUpper())
                {
                    case "CTRL":
                        modifiers.Add(ModifierMask.Control);
                        break;
                    case "SHIFT":
                        modifiers.Add(ModifierMask.Shift);
                        break;
                    case "ALT":
                        modifiers.Add(ModifierMask.Alt);
                        break;
                    default:
                        // Assuming the last part is the main key
                        yield return (ModifierMask)Enum.Parse(typeof(KeyCode), "Vc" + key.Trim(), true);
                        break;
                }
            }
        }

        private static void ToggleTimer()
        {
            if (_startTime == null)
            {
                _startTime = DateTime.Now;
                _notifyIcon.BalloonTipTitle = "Yellow Time Tracker";
                _notifyIcon.BalloonTipText = "Timer started.";
                _notifyIcon.ShowBalloonTip(1000);
            }
            else
            {
                var endTime = DateTime.Now;
                var duration = endTime - _startTime.Value;
                _notifyIcon.BalloonTipTitle = "Yellow Time Tracker";
                _notifyIcon.BalloonTipText = "Timer stopped.";
                _notifyIcon.ShowBalloonTip(1000);
                LogTime(duration);
                _startTime = null;
            }
            TimerStatusChanged();
        }

        private static void TimerStatusChanged()
        {
            if (_notifyIcon.ContextMenuStrip != null)
            {
                var startStopMenuItem = _notifyIcon.ContextMenuStrip.Items[0] as ToolStripMenuItem;
                if (startStopMenuItem != null)
                {
                    startStopMenuItem.Text = _startTime == null ? "Start Timer" : "Stop Timer";
                }
            }
        }

        private static void LogTime(TimeSpan duration)
        {
            var date = DateTime.Now.ToString("yyyy-MM-dd");
            var seconds = (int)duration.TotalSeconds;
            var line = $"{date};{seconds}\n";
            File.AppendAllText(_csvPath, line);
        }
    }
}
