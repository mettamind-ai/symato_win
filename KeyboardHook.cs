using System.Runtime.InteropServices;

namespace SymatoIME;

/// <summary>
/// Low-level keyboard hook for capturing keystrokes
/// </summary>
public class KeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    public delegate bool KeyPressedHandler(Keys key, bool isKeyDown, ref bool handled);
    public event KeyPressedHandler? KeyPressed;

    private IntPtr _hookHandle = IntPtr.Zero;
    private NativeMethods.LowLevelKeyboardProc? _hookProc;

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero) return;

        _hookProc = HookCallback;
        using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookHandle = NativeMethods.SetWindowsHookEx(
            WH_KEYBOARD_LL,
            _hookProc,
            NativeMethods.GetModuleHandle(curModule.ModuleName),
            0);

        if (_hookHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to install keyboard hook");
        }
    }

    public void Stop()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            bool isKeyDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
            bool isKeyUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;

            if (isKeyDown || isKeyUp)
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                Keys key = (Keys)hookStruct.vkCode;

                // Skip injected keys to prevent recursion
                if ((hookStruct.flags & 0x10) != 0) // LLKHF_INJECTED
                {
                    return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
                }

                bool handled = false;
                KeyPressed?.Invoke(key, isKeyDown, ref handled);

                if (handled)
                {
                    return (IntPtr)1; // Block the key
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    ~KeyboardHook() => Stop();
}
