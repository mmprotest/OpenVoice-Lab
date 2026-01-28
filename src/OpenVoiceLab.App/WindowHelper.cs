using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace OpenVoiceLab;

public static class WindowHelper
{
    private static Window? _window;

    public static void SetWindow(Window window)
    {
        _window = window;
    }

    public static IntPtr GetWindowHandle()
    {
        if (_window == null)
        {
            throw new InvalidOperationException("Main window not set.");
        }
        return WindowNative.GetWindowHandle(_window);
    }
}
