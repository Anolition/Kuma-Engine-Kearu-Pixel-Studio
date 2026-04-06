using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Silk.NET.OpenGL;

namespace ProjectSPlus.App.Rendering;

public sealed unsafe class TextRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly uint _program;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly Dictionary<string, TextTexture> _textureCache = [];

    private int _screenWidth;
    private int _screenHeight;

    public TextRenderer(GL gl)
    {
        _gl = gl;
        _program = CreateProgram();
        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(6 * 4 * sizeof(float)), ReadOnlySpan<float>.Empty, BufferUsageARB.DynamicDraw);

        const uint positionIndex = 0;
        const uint uvIndex = 1;
        const int stride = 4 * sizeof(float);

        _gl.EnableVertexAttribArray(positionIndex);
        _gl.VertexAttribPointer(positionIndex, 2, VertexAttribPointerType.Float, false, (uint)stride, (void*)0);
        _gl.EnableVertexAttribArray(uvIndex);
        _gl.VertexAttribPointer(uvIndex, 2, VertexAttribPointerType.Float, false, (uint)stride, (void*)(2 * sizeof(float)));
    }

    public void Resize(int screenWidth, int screenHeight)
    {
        _screenWidth = Math.Max(screenWidth, 1);
        _screenHeight = Math.Max(screenHeight, 1);
    }

    public void DrawText(string text, Font font, SixLabors.ImageSharp.Color color, float x, float y)
    {
        DrawTextCore(text, font, color, x, y, null);
    }

    public void DrawTextClipped(string text, Font font, SixLabors.ImageSharp.Color color, float x, float y, float clipX, float clipY, float clipWidth, float clipHeight)
    {
        DrawTextCore(text, font, color, x, y, new ClipRect(clipX, clipY, clipWidth, clipHeight));
    }

    private void DrawTextCore(string text, Font font, SixLabors.ImageSharp.Color color, float x, float y, ClipRect? clipRect)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string key = $"{font.Family.Name}|{font.Size}|{color}|{text}";
        TextTexture texture = GetOrCreateTexture(key, text, font, color);

        float left = x;
        float top = y;
        float right = x + texture.Width;
        float bottom = y + texture.Height;

        float[] vertices =
        [
            left, top, 0.0f, 0.0f,
            right, top, 1.0f, 0.0f,
            right, bottom, 1.0f, 1.0f,
            left, top, 0.0f, 0.0f,
            right, bottom, 1.0f, 1.0f,
            left, bottom, 0.0f, 1.0f
        ];

        if (clipRect is ClipRect clip)
        {
            int scissorX = Math.Max((int)MathF.Floor(clip.X), 0);
            int scissorY = Math.Max(_screenHeight - (int)MathF.Ceiling(clip.Y + clip.Height), 0);
            int scissorWidth = Math.Max((int)MathF.Ceiling(clip.Width), 0);
            int scissorHeight = Math.Max((int)MathF.Ceiling(clip.Height), 0);
            _gl.Enable(EnableCap.ScissorTest);
            _gl.Scissor(scissorX, scissorY, (uint)scissorWidth, (uint)scissorHeight);
        }
        else
        {
            _gl.Disable(EnableCap.ScissorTest);
        }

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.UseProgram(_program);
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferSubData<float>(BufferTargetARB.ArrayBuffer, 0, vertices);

        int screenSizeLocation = _gl.GetUniformLocation(_program, "uScreenSize");
        if (screenSizeLocation >= 0)
        {
            _gl.Uniform2(screenSizeLocation, (float)_screenWidth, (float)_screenHeight);
        }

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, texture.Handle);
        int textureLocation = _gl.GetUniformLocation(_program, "uTextTexture");
        _gl.Uniform1(textureLocation, 0);

        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        _gl.Disable(EnableCap.Blend);
        _gl.Disable(EnableCap.ScissorTest);
    }

    public void ClearCache()
    {
        foreach (TextTexture texture in _textureCache.Values)
        {
            _gl.DeleteTexture(texture.Handle);
        }

        _textureCache.Clear();
    }

    public void Dispose()
    {
        ClearCache();
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteProgram(_program);
        GC.SuppressFinalize(this);
    }

    private TextTexture GetOrCreateTexture(string key, string text, Font font, SixLabors.ImageSharp.Color color)
    {
        if (_textureCache.TryGetValue(key, out TextTexture? cached))
        {
            return cached;
        }

        FontRectangle bounds = TextMeasurer.MeasureBounds(text, new RichTextOptions(font));
        const float padding = 6.0f;
        int width = Math.Max((int)Math.Ceiling(bounds.Width + (padding * 2.0f)), 8);
        int height = Math.Max((int)Math.Ceiling(bounds.Height + (padding * 2.0f)), 8);
        float drawX = padding - bounds.X;
        float drawY = padding - bounds.Y;

        using Image<Rgba32> image = new(width, height);
        image.Mutate(context =>
        {
            context.Clear(SixLabors.ImageSharp.Color.Transparent);
            context.DrawText(text, font, color, new PointF(drawX, drawY));
        });

        Rgba32[] pixels = new Rgba32[width * height];
        image.CopyPixelDataTo(pixels);

        uint handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ref pixels[0]);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        TextTexture created = new()
        {
            Handle = handle,
            Width = width,
            Height = height
        };

        _textureCache[key] = created;
        return created;
    }

    private uint CreateProgram()
    {
        const string vertexShaderSource = """
            #version 330 core
            layout (location = 0) in vec2 aPosition;
            layout (location = 1) in vec2 aTexCoord;

            out vec2 vTexCoord;
            uniform vec2 uScreenSize;

            void main()
            {
                vec2 normalized = vec2(
                    (aPosition.x / uScreenSize.x) * 2.0 - 1.0,
                    1.0 - (aPosition.y / uScreenSize.y) * 2.0
                );

                gl_Position = vec4(normalized, 0.0, 1.0);
                vTexCoord = aTexCoord;
            }
            """;

        const string fragmentShaderSource = """
            #version 330 core
            in vec2 vTexCoord;
            out vec4 FragColor;

            uniform sampler2D uTextTexture;

            void main()
            {
                FragColor = texture(uTextTexture, vTexCoord);
            }
            """;

        uint vertexShader = CompileShader(ShaderType.VertexShader, vertexShaderSource);
        uint fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentShaderSource);

        uint program = _gl.CreateProgram();
        _gl.AttachShader(program, vertexShader);
        _gl.AttachShader(program, fragmentShader);
        _gl.LinkProgram(program);
        _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int isLinked);
        if (isLinked == 0)
        {
            throw new InvalidOperationException($"Text shader link failed: {_gl.GetProgramInfoLog(program)}");
        }

        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);
        return program;
    }

    private uint CompileShader(ShaderType shaderType, string source)
    {
        uint shader = _gl.CreateShader(shaderType);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int compiled);
        if (compiled == 0)
        {
            throw new InvalidOperationException($"Text shader compile failed: {_gl.GetShaderInfoLog(shader)}");
        }

        return shader;
    }

    private readonly record struct ClipRect(float X, float Y, float Width, float Height);
}
