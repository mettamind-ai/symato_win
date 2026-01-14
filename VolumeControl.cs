using System.Runtime.InteropServices;

namespace SymatoIME;

/// <summary>
/// Windows 11 volume control using Windows Core Audio API
/// </summary>
public static class VolumeControl
{
    private const int CLSCTX_ALL = 23;

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int NotImpl1();
        [PreserveSig]
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, 
            [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int NotImpl1();
        int NotImpl2();
        [PreserveSig]
        int GetChannelCount(out int pnChannelCount);
        [PreserveSig]
        int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig]
        int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        [PreserveSig]
        int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig]
        int GetMasterVolumeLevelScalar(out float pfLevel);
    }

    private enum EDataFlow
    {
        eRender = 0,
        eCapture = 1,
        eAll = 2
    }

    private enum ERole
    {
        eConsole = 0,
        eMultimedia = 1,
        eCommunications = 2
    }

    private static readonly Guid IID_IAudioEndpointVolume = 
        new("5CDF2C82-841E-4546-9722-0CF74078229A");

    // Fallback: Use keybd_event for volume control
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const byte VK_VOLUME_UP = 0xAF;
    private const byte VK_VOLUME_DOWN = 0xAE;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    /// <summary>
    /// Adjust system volume by simulating volume keys (shows Windows OSD)
    /// </summary>
    public static void AdjustVolume(int delta)
    {
        // Use volume key simulation to show Windows volume OSD
        byte vk = delta > 0 ? VK_VOLUME_UP : VK_VOLUME_DOWN;
        int steps = Math.Max(1, Math.Abs(delta) / 2);
        for (int i = 0; i < steps; i++)
        {
            keybd_event(vk, 0, 0, UIntPtr.Zero);
            keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }

    private static bool TryAdjustVolumeCore(int delta)
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            int hr = enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out IMMDevice device);
            if (hr != 0 || device == null) return false;
            
            var iid = IID_IAudioEndpointVolume;
            hr = device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out object volumeObj);
            if (hr != 0 || volumeObj == null) return false;
            
            var volume = (IAudioEndpointVolume)volumeObj;

            volume.GetMasterVolumeLevelScalar(out float currentVolume);
            float newVolume = Math.Clamp(currentVolume + (delta / 100f), 0f, 1f);
            
            var guid = Guid.Empty;
            volume.SetMasterVolumeLevelScalar(newVolume, ref guid);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get current volume level (0-100)
    /// </summary>
    public static int GetVolume()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out IMMDevice device);
            
            var iid = IID_IAudioEndpointVolume;
            device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out object volumeObj);
            var volume = (IAudioEndpointVolume)volumeObj;

            volume.GetMasterVolumeLevelScalar(out float currentVolume);
            return (int)(currentVolume * 100);
        }
        catch
        {
            return 50;
        }
    }
}
