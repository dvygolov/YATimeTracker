using System;
using System.IO;
using System.Windows.Forms;
using SharpHook;
using SharpHook.Reactive;
using SharpHook.Native;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace TimeTracker;

public static class Program
{
    private static NotifyIcon _notifyIcon;
    private static IReactiveGlobalHook _globalHook;
    private static DateTime? _startTime;
    private static string _configPath;
    private static string _csvPath;
    private static System.Drawing.Icon _inactiveIcon;
    private static System.Drawing.Icon _activeIcon;

    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        string baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string inactiveIconPath = Path.Combine(baseDirectory, "icon.ico");
        string activeIconPath = Path.Combine(baseDirectory, "icon_active.ico");
        _configPath = Path.Combine(baseDirectory, "config.json");
        _csvPath = Path.Combine(baseDirectory, "work.csv");

        _inactiveIcon = new System.Drawing.Icon(inactiveIconPath);
        _activeIcon = new System.Drawing.Icon(activeIconPath);

        // Subscribe to system events for hibernation detection
        SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;

        _notifyIcon = new NotifyIcon
        {
            Icon = _inactiveIcon,
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
        var (keyCombination, mainKey) = ParseHotkey(hotkeyString);
        SubscribeHotKeys(keyCombination, mainKey);
    }

    private static void SubscribeHotKeys(List<ModifierMask> keyCombination, KeyCode mainKey)
    {
        _globalHook = new SimpleReactiveGlobalHook();
        _globalHook.KeyPressed.Subscribe(e =>
        {
            if (e.Data.KeyCode == mainKey)
            {
                if (keyCombination.All(k => e.RawEvent.Mask.HasFlag(k))) ToggleTimer();
            }
        });
        _globalHook.Run();
    }

    private static (List<ModifierMask>, KeyCode) ParseHotkey(string hotkeyString)
    {
        var keys = hotkeyString.Split('+');
        var modifiers = new List<ModifierMask>();
        KeyCode mainKey = KeyCode.VcF12;
        for (int i = 0; i < keys.Length; i++)
        {
            var key = keys[i].Trim();
            switch (key.ToUpper())
            {
                case "CTRL":
                case "LEFTCTRL":
                    modifiers.Add(ModifierMask.LeftCtrl);
                    break;
                case "RIGHTCTRL":
                    modifiers.Add(ModifierMask.RightCtrl);
                    break;
                case "SHIFT":
                case "LEFTSHIFT":
                    modifiers.Add(ModifierMask.LeftShift);
                    break;
                case "RIGHTSHIFT":
                    modifiers.Add(ModifierMask.RightShift);
                    break;
                case "ALT":
                case "LEFTALT":
                    modifiers.Add(ModifierMask.LeftAlt);
                    break;
                case "RIGHTALT":
                    modifiers.Add(ModifierMask.RightAlt);
                    break;
                default:
                    if (i == keys.Length - 1) // Last key is the main key
                    {
                        mainKey = (KeyCode)Enum.Parse(typeof(KeyCode), "Vc" + key, true);
                    }
                    break;
            }
        }
        return (modifiers, mainKey);
    }

    private static void ToggleTimer()
    {
        if (_startTime == null)
            StartTimer();
        else
            StopTimer();
        TimerStatusChanged();
    }

    private static void StartTimer(){
        _startTime = DateTime.Now;
        _notifyIcon.Icon = _activeIcon;
        ((ToolStripMenuItem)_notifyIcon.ContextMenuStrip.Items[0]).Text = "Stop Timer";
        _notifyIcon.BalloonTipTitle = "Yellow Time Tracker";
        _notifyIcon.BalloonTipText = "Timer started.";
        _notifyIcon.ShowBalloonTip(1000);
    }
    private static void StopTimer(){
        var endTime = DateTime.Now;
        var duration = endTime - _startTime.Value;
        _notifyIcon.BalloonTipTitle = "Yellow Time Tracker";
        _notifyIcon.BalloonTipText = "Timer stopped.";
        _notifyIcon.ShowBalloonTip(1000);
        LogTime(duration);
        _startTime = null;
        _notifyIcon.Icon = _inactiveIcon;
        ((ToolStripMenuItem)_notifyIcon.ContextMenuStrip.Items[0]).Text = "Start Timer";
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

    private static void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (_startTime != null && (e.Reason == SessionSwitchReason.SessionLock || 
            e.Reason == SessionSwitchReason.SessionLogoff))
        {
            StopTimer();
            TimerStatusChanged();
        }
    }
}
