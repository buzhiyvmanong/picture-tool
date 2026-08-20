using System.Runtime.InteropServices;
using DrawingPoint = System.Drawing.Point;

namespace PictureTool.Services;

public sealed class GlobalWheelHook : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmMouseWheel = 0x020A;
    private const int WmMouseHWheel = 0x020E;

    private readonly LowLevelMouseProc _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _disposed;

    public GlobalWheelHook()
    {
        _proc = HookCallback;
    }

    public event Action<int, DrawingPoint, bool>? WheelScrolled;

    public void Install()
    {
        if (_hookId != IntPtr.Zero)
        {
            return;
        }

        _hookId = SetHook(_proc);
        if (_hookId == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to install global wheel hook.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            if (wParam == (IntPtr)WmMouseWheel)
            {
                var info = Marshal.PtrToStructure<MouseLowLevelHookStruct>(lParam);
                var delta = (short)((info.MouseData >> 16) & 0xFFFF);
                WheelScrolled?.Invoke(delta, new DrawingPoint(info.Point.X, info.Point.Y), false);
            }
            else if (wParam == (IntPtr)WmMouseHWheel)
            {
                var info = Marshal.PtrToStructure<MouseLowLevelHookStruct>(lParam);
                var delta = (short)((info.MouseData >> 16) & 0xFFFF);
                WheelScrolled?.Invoke(delta, new DrawingPoint(info.Point.X, info.Point.Y), true);
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static IntPtr SetHook(LowLevelMouseProc proc)
    {
        using var module = System.Diagnostics.Process.GetCurrentProcess().MainModule;
        var moduleHandle = module is null ? IntPtr.Zero : GetModuleHandle(module.ModuleName);
        return SetWindowsHookEx(WhMouseLl, proc, moduleHandle, 0);
    }

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseLowLevelHookStruct
    {
        public Point Point;

        public uint MouseData;

        public uint Flags;

        public uint Time;

        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;

        public int Y;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hHook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hHook, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}
