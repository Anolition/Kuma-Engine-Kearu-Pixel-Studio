using ProjectSPlus.Core.Configuration;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace ProjectSPlus.Runtime.Application;

public interface IWindowScene : IDisposable
{
    void Initialize(IWindow window, GL gl, Vector2D<int> framebufferSize);

    void Resize(int width, int height);

    void Render();

    void OnKeyDown(IKeyboard keyboard, Key key, int scancode);

    void OnKeyUp(IKeyboard keyboard, Key key, int scancode);

    void OnKeyChar(IKeyboard keyboard, char character);

    void OnMouseDown(IMouse mouse, MouseButton button);

    void OnMouseUp(IMouse mouse, MouseButton button);

    void OnMouseMove(IMouse mouse, System.Numerics.Vector2 position);

    void OnMouseScroll(IMouse mouse, ScrollWheel scrollWheel);

    bool TryPrepareClose(IWindow window);

    AppSettings CaptureSettings(AppSettings currentSettings, IWindow window);
}
