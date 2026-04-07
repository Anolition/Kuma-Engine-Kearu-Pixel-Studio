using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Silk.NET.OpenGL;

namespace ProjectSPlus.App.Rendering;

public sealed unsafe class ImageRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly uint _program;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly Dictionary<string, ImageTexture> _textureCache = [];

    private int _screenWidth;
    private int _screenHeight;

    public ImageRenderer(GL gl)
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

    public void DrawImage(string filePath, float x, float y, float width, float height)
    {
        if (string.IsNullOrWhiteSpace(filePath) || width <= 0 || height <= 0 || !File.Exists(filePath))
        {
            return;
        }

        ImageTexture texture = GetOrCreateTexture(filePath);
        float textureAspect = texture.Width / (float)Math.Max(texture.Height, 1);
        float targetAspect = width / Math.Max(height, 1f);

        float drawWidth = width;
        float drawHeight = height;
        if (textureAspect > targetAspect)
        {
            drawHeight = width / textureAspect;
        }
        else
        {
            drawWidth = height * textureAspect;
        }

        float drawX = x + ((width - drawWidth) * 0.5f);
        float drawY = y + ((height - drawHeight) * 0.5f);

        float left = drawX;
        float top = drawY;
        float right = drawX + drawWidth;
        float bottom = drawY + drawHeight;

        float[] vertices =
        [
            left, top, 0.0f, 0.0f,
            right, top, 1.0f, 0.0f,
            right, bottom, 1.0f, 1.0f,
            left, top, 0.0f, 0.0f,
            right, bottom, 1.0f, 1.0f,
            left, bottom, 0.0f, 1.0f
        ];

        _gl.Disable(EnableCap.ScissorTest);
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
        int textureLocation = _gl.GetUniformLocation(_program, "uImageTexture");
        _gl.Uniform1(textureLocation, 0);

        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        _gl.Disable(EnableCap.Blend);
    }

    public void Dispose()
    {
        foreach (ImageTexture texture in _textureCache.Values)
        {
            _gl.DeleteTexture(texture.Handle);
        }

        _textureCache.Clear();
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteProgram(_program);
        GC.SuppressFinalize(this);
    }

    private ImageTexture GetOrCreateTexture(string filePath)
    {
        if (_textureCache.TryGetValue(filePath, out ImageTexture? cached))
        {
            return cached;
        }

        using Image<Rgba32> image = Image.Load<Rgba32>(filePath);
        int width = image.Width;
        int height = image.Height;
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

        ImageTexture created = new()
        {
            Handle = handle,
            Width = width,
            Height = height
        };

        _textureCache[filePath] = created;
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

            uniform sampler2D uImageTexture;

            void main()
            {
                FragColor = texture(uImageTexture, vTexCoord);
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
            throw new InvalidOperationException($"Image shader link failed: {_gl.GetProgramInfoLog(program)}");
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
            throw new InvalidOperationException($"Image shader compile failed: {_gl.GetShaderInfoLog(shader)}");
        }

        return shader;
    }

    private sealed class ImageTexture
    {
        public required uint Handle { get; init; }

        public required int Width { get; init; }

        public required int Height { get; init; }
    }
}
