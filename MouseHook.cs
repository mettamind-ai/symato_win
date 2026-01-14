using System.Runtime.InteropServices;

namespace SymatoIME;

/// <summary>
/// Low-level mouse hook for capturing mouse wheel with modifiers
/// </summary>
public class MouseHook : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEWHEEL = 0x020A;

    public delegate void MouseWheelHandler(int delta, Keys modifiers);
    public event MouseWheelHandler? MouseWheel;

    private IntPtr _hookHandle = IntPtr.Zero;
    private NativeMethods.LowLevelMouseProc? _hookProc;

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero) return;

        _hookProc = HookCallback;
        // For WH_MOUSE_LL, the module handle is ignored by Windows
        // Using IntPtr.Zero is more reliable for .NET applications
        _hookHandle = NativeMethods.SetWindowsHookEx(
            WH_MOUSE_LL,
            _hookProc,
            IntPtr.Zero,
            0);

        if (_hookHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to install mouse hook");
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
        if (nCode >= 0 && (int)wParam == WM_MOUSEWHEEL)
        {
            var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            int delta = (short)(hookStruct.mouseData >> 16);

            // Get current modifier keys
            Keys modifiers = Keys.None;
            if ((NativeMethods.GetAsyncKeyState((int)Keys.ControlKey) & 0x8000) != 0)
                modifiers |= Keys.Control;
            if ((NativeMethods.GetAsyncKeyState((int)Keys.ShiftKey) & 0x8000) != 0)
                modifiers |= Keys.Shift;

            // If Ctrl+Shift is held, handle volume and block the wheel
            if (modifiers.HasFlag(Keys.Control) && modifiers.HasFlag(Keys.Shift))
            {
                MouseWheel?.Invoke(delta, modifiers);
                return (IntPtr)1; // Block the wheel event
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    ~MouseHook() => Stop();
}
