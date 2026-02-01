using Microsoft.Win32;

namespace SymatoIME;

/// <summary>
/// Main application context managing the system tray and all components
/// </summary>
public class SymatoContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly KeyboardHook _keyboardHook;
    private readonly MouseHook _mouseHook;
    private readonly VietnameseConverter _converter;
    private readonly Settings _settings;
    
    private bool _isActive = true;
    private bool _keyRemapEnabled = true;
    private bool _volumeControlEnabled = true;
    private bool _autoIeYeEnabled = true;
    private bool _doubleKeyRawEnabled = true;

    public SymatoContext()
    {
        _settings = Settings.Load();
        _isActive = _settings.ImeEnabled;
        _keyRemapEnabled = _settings.KeyRemapEnabled;
        _volumeControlEnabled = _settings.VolumeControlEnabled;
        _autoIeYeEnabled = _settings.AutoIeYeEnabled;
        _doubleKeyRawEnabled = _settings.DoubleKeyRawEnabled;
        
        _converter = new VietnameseConverter();
        _converter.AutoIeYeEnabled = _autoIeYeEnabled;
        _converter.DoubleKeyRawEnabled = _doubleKeyRawEnabled;
        
        // Create tray icon
        _trayIcon = new NotifyIcon
        {
            Icon = CreateIcon(_isActive),
            Visible = true,
            Text = "SymatoIME - " + (_isActive ? "Active" : "Inactive"),
            ContextMenuStrip = CreateContextMenu()
        };
        
        _trayIcon.MouseClick += TrayIcon_MouseClick;
        
        // Initialize hooks
        _keyboardHook = new KeyboardHook();
        _keyboardHook.KeyPressed += OnKeyPressed;
        _keyboardHook.Start();
        
        _mouseHook = new MouseHook();
        _mouseHook.MouseWheel += OnMouseWheel;
        _mouseHook.Start();
        
        // Apply startup setting
        SetStartup(_settings.StartWithWindows);
    }

    private Icon CreateIcon(bool active)
    {
        int size = 32;
        using var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        
        // Background color
        Color bgColor = active ? Color.FromArgb(0, 120, 215) : Color.FromArgb(128, 128, 128);
        using var bgBrush = new SolidBrush(bgColor);
        g.FillRectangle(bgBrush, 0, 0, size, size);
        
        // Draw "S" letter
        using var font = new Font("Segoe UI", 20, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.White);
        
        var textSize = g.MeasureString("S", font);
        float x = (size - textSize.Width) / 2;
        float y = (size - textSize.Height) / 2;
        
        g.DrawString("S", font, textBrush, x, y);
        
        return Icon.FromHandle(bitmap.GetHicon());
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();
        
        var imeItem = new ToolStripMenuItem("Vietnamese IME")
        {
            Checked = _isActive,
            CheckOnClick = true
        };
        imeItem.Click += (s, e) => ToggleIme();
        
        var keyRemapItem = new ToolStripMenuItem("Key Remap (~ ↔ CapsLock ↔ Tab)")
        {
            Checked = _keyRemapEnabled,
            CheckOnClick = true
        };
        keyRemapItem.Click += (s, e) => ToggleKeyRemap();
        
        var volumeItem = new ToolStripMenuItem("Volume Control (Ctrl+Shift+Wheel)")
        {
            Checked = _volumeControlEnabled,
            CheckOnClick = true
        };
        volumeItem.Click += (s, e) => ToggleVolumeControl();
        
        var autoIeYeItem = new ToolStripMenuItem("Auto ie/ye → iê/yê")
        {
            Checked = _autoIeYeEnabled,
            CheckOnClick = true
        };
        autoIeYeItem.Click += (s, e) => ToggleAutoIeYe();

        var doubleKeyRawItem = new ToolStripMenuItem("maxx => maxx (not mã)")
        {
            Checked = _doubleKeyRawEnabled,
            CheckOnClick = true
        };
        doubleKeyRawItem.Click += (s, e) => ToggleDoubleKeyRaw();
        
        var startupItem = new ToolStripMenuItem("Start with Windows")
        {
            Checked = _settings.StartWithWindows,
            CheckOnClick = true
        };
        startupItem.Click += (s, e) => ToggleStartup(startupItem);
        
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => Exit();
        
        menu.Items.Add(imeItem);
        menu.Items.Add(keyRemapItem);
        menu.Items.Add(volumeItem);
        menu.Items.Add(autoIeYeItem);
        menu.Items.Add(doubleKeyRawItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);
        
        return menu;
    }

    private void TrayIcon_MouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ToggleIme();
        }
    }

    private void ToggleIme()
    {
        _isActive = !_isActive;
        _settings.ImeEnabled = _isActive;
        _settings.Save();
        
        UpdateTrayIcon();
        
        if (_trayIcon.ContextMenuStrip?.Items[0] is ToolStripMenuItem item)
            item.Checked = _isActive;
    }

    private void ToggleKeyRemap()
    {
        _keyRemapEnabled = !_keyRemapEnabled;
        _settings.KeyRemapEnabled = _keyRemapEnabled;
        _settings.Save();
        
        if (_trayIcon.ContextMenuStrip?.Items[1] is ToolStripMenuItem item)
            item.Checked = _keyRemapEnabled;
    }

    private void ToggleVolumeControl()
    {
        _volumeControlEnabled = !_volumeControlEnabled;
        _settings.VolumeControlEnabled = _volumeControlEnabled;
        _settings.Save();
        
        if (_trayIcon.ContextMenuStrip?.Items[2] is ToolStripMenuItem item)
            item.Checked = _volumeControlEnabled;
    }

    private void ToggleAutoIeYe()
    {
        _autoIeYeEnabled = !_autoIeYeEnabled;
        _settings.AutoIeYeEnabled = _autoIeYeEnabled;
        _settings.Save();
        _converter.AutoIeYeEnabled = _autoIeYeEnabled;
        
        if (_trayIcon.ContextMenuStrip?.Items[3] is ToolStripMenuItem item)
            item.Checked = _autoIeYeEnabled;
    }

    private void ToggleDoubleKeyRaw()
    {
        _doubleKeyRawEnabled = !_doubleKeyRawEnabled;
        _settings.DoubleKeyRawEnabled = _doubleKeyRawEnabled;
        _settings.Save();
        _converter.DoubleKeyRawEnabled = _doubleKeyRawEnabled;

        if (_trayIcon.ContextMenuStrip?.Items[4] is ToolStripMenuItem item)
            item.Checked = _doubleKeyRawEnabled;
    }

    private void ToggleStartup(ToolStripMenuItem item)
    {
        _settings.StartWithWindows = item.Checked;
        _settings.Save();
        SetStartup(item.Checked);
    }

    private void SetStartup(bool enable)
    {
        const string keyName = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        const string valueName = "SymatoIME";
        
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyName, true);
            if (key == null) return;
            
            if (enable)
            {
                string exePath = Application.ExecutablePath;
                key.SetValue(valueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(valueName, false);
            }
        }
        catch
        {
            // Ignore registry errors
        }
    }

    private void UpdateTrayIcon()
    {
        var oldIcon = _trayIcon.Icon;
        _trayIcon.Icon = CreateIcon(_isActive);
        _trayIcon.Text = "SymatoIME - " + (_isActive ? "Active" : "Inactive");
        oldIcon?.Dispose();
    }

    private bool OnKeyPressed(Keys key, bool isKeyDown, ref bool handled)
    {
        // Handle Ctrl+Shift+S to toggle IME
        if (isKeyDown && key == Keys.S && 
            (Control.ModifierKeys & Keys.Control) != 0 && 
            (Control.ModifierKeys & Keys.Shift) != 0)
        {
            ToggleIme();
            handled = true;
            return true;
        }
        
        // Handle Ctrl+B to paste clipboard without backticks
        if (isKeyDown && key == Keys.B && 
            (Control.ModifierKeys & Keys.Control) != 0 &&
            (Control.ModifierKeys & Keys.Shift) == 0)
        {
            PasteWithoutBackticks();
            handled = true;
            return true;
        }

        // Handle key remapping
        if (_keyRemapEnabled && isKeyDown)
        {
            Keys? remappedKey = key switch
            {
                Keys.Oemtilde => Keys.CapsLock,  // ~ -> CapsLock
                Keys.CapsLock => Keys.Tab,        // CapsLock -> Tab
                Keys.Tab => Keys.Oemtilde,        // Tab -> ~
                _ => null
            };
            
            if (remappedKey.HasValue)
            {
                // Reset IME buffer when remapped keys are pressed (e.g., Tab for autocomplete)
                _converter.Reset();
                NativeMethods.SendKey(remappedKey.Value);
                handled = true;
                return true;
            }
        }
        
        // Handle Vietnamese input
        if (_isActive && isKeyDown)
        {
            return _converter.ProcessKey(key, ref handled);
        }
        
        return false;
    }

    private void PasteWithoutBackticks()
    {
        try
        {
            // Get clipboard text
            if (!Clipboard.ContainsText()) return;
            
            string text = Clipboard.GetText();
            if (string.IsNullOrEmpty(text)) return;
            
            // Remove all backticks
            string cleanText = text.Replace("`", "");
            
            // If nothing changed, just do normal paste
            if (cleanText == text)
            {
                NativeMethods.SendKeyCombo(Keys.Control, Keys.V);
                return;
            }
            
            // Save original clipboard content
            string originalText = text;
            
            // Set cleaned text to clipboard
            Clipboard.SetText(cleanText);
            
            // Send Ctrl+V to paste
            NativeMethods.SendKeyCombo(Keys.Control, Keys.V);
            
            // Restore original clipboard after a short delay
            Task.Delay(100).ContinueWith(_ =>
            {
                try
                {
                    // Run on UI thread
                    _trayIcon.ContextMenuStrip?.Invoke(() => Clipboard.SetText(originalText));
                }
                catch { }
            });
        }
        catch
        {
            // If anything fails, try normal paste
            NativeMethods.SendKeyCombo(Keys.Control, Keys.V);
        }
    }

    private void OnMouseWheel(int delta, Keys modifiers)
    {
        if (!_volumeControlEnabled) return;
        
        // Ctrl+Shift+Wheel for volume
        if (modifiers.HasFlag(Keys.Control) && modifiers.HasFlag(Keys.Shift))
        {
            VolumeControl.AdjustVolume(delta > 0 ? 2 : -2);
        }
    }

    private void Exit()
    {
        _keyboardHook.Stop();
        _mouseHook.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _keyboardHook?.Dispose();
            _mouseHook?.Dispose();
            _trayIcon?.Dispose();
        }
        base.Dispose(disposing);
    }
}
