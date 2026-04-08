using ProjectSPlus.Core.Configuration;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace ProjectSPlus.Runtime.Application;

public sealed class WindowHost : IApplicationHost
{
    public void Run(AppSettings settings, IWindowScene scene, Action<AppSettings> persistSettings)
    {
        WindowOptions options = CreateWindowOptions(settings);
        IWindow window = Window.Create(options);

        GL? gl = null;
        IInputContext? inputContext = null;
        bool titleBarThemeApplied = false;

        window.Load += () =>
        {
            gl = GL.GetApi(window);
            scene.Initialize(window, gl, window.FramebufferSize);

            window.IsVisible = true;
            window.Center();
            window.Focus();

            inputContext = window.CreateInput();
            foreach (IKeyboard keyboard in inputContext.Keyboards)
            {
                keyboard.KeyDown += scene.OnKeyDown;
                keyboard.KeyChar += scene.OnKeyChar;
            }

            foreach (IMouse mouse in inputContext.Mice)
            {
                mouse.MouseDown += scene.OnMouseDown;
                mouse.MouseUp += scene.OnMouseUp;
                mouse.MouseMove += scene.OnMouseMove;
                mouse.Scroll += scene.OnMouseScroll;
            }

            if (settings.Window.StartMaximized)
            {
                window.WindowState = WindowState.Maximized;
            }
        };

        window.FramebufferResize += size =>
        {
            scene.Resize(size.X, size.Y);
        };

        window.Render += _ =>
        {
            if (!titleBarThemeApplied)
            {
                titleBarThemeApplied = TryApplyWindowsTitleBarTheme(settings.Editor.ThemeName);
            }

            scene.Render();
        };

        window.Closing += () =>
        {
            persistSettings(scene.CaptureSettings(settings, window));
            inputContext?.Dispose();
            scene.Dispose();
            gl?.Dispose();
        };

        window.Run();
    }

    private static WindowOptions CreateWindowOptions(AppSettings settings)
    {
        WindowSettings windowSettings = settings.Window.Normalize();

        WindowOptions options = WindowOptions.Default;
        options.Title = windowSettings.Title;
        options.Size = new Vector2D<int>(windowSettings.Width, windowSettings.Height);
        options.Position = new Vector2D<int>(120, 120);
        options.IsVisible = true;
        options.WindowState = WindowState.Normal;
        options.API = new GraphicsAPI(
            ContextAPI.OpenGL,
            ContextProfile.Core,
            ContextFlags.ForwardCompatible,
            new APIVersion(3, 3));
        options.VSync = true;

        return options;
    }

    private static bool TryApplyWindowsTitleBarTheme(string themeName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        IntPtr hwnd = Process.GetCurrentProcess().MainWindowHandle;
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        int darkMode = string.Equals(themeName, "ProjectSPlus.Light", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        DwmSetWindowAttribute(hwnd, 20, ref darkMode, sizeof(int));
        DwmSetWindowAttribute(hwnd, 19, ref darkMode, sizeof(int));
        return true;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
}
