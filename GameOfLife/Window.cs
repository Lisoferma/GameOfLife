using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Drawing;
using FoxCanvas;

namespace GameOfLife;

internal class Window : GameWindow
{
    private const string TITLE = "GameOfLife";
    private const int CANVAS_WIDTH = 500;
    private const int CANVAS_HEIGHT = 500;

    private double _frameTime = 0.0;
    private int _fps = 0;

    private Canvas _canvas;
    private Color[,] _image;
    private GameOfLife _life;


    public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
        : base(gameWindowSettings, nativeWindowSettings)
    { }


    protected override void OnLoad()
    {
        GL.ClearColor(Color.HotPink);

        _canvas = new Canvas(CANVAS_WIDTH, CANVAS_HEIGHT, ClientSize.X, ClientSize.Y);
        _image = new Color[CANVAS_WIDTH, CANVAS_HEIGHT];
        
        _life = new(CANVAS_WIDTH, CANVAS_HEIGHT);
        _life.GenerateRandomField(123, 0.5);

        base.OnLoad();
    }


    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        _life.Step();
        _life.GetImage(_image, Color.DeepPink, Color.White);
        _canvas.SetImage(_image);

        UpdateTitle(e.Time);

        base.OnUpdateFrame(e);
    }


    protected override void OnRenderFrame(FrameEventArgs e)
    {
        GL.Clear(ClearBufferMask.ColorBufferBit);

        _canvas.Render();
        SwapBuffers();

        base.OnRenderFrame(e);
    }


    protected override void OnFramebufferResize(FramebufferResizeEventArgs e)
    {
        GL.Viewport(0, 0, e.Width, e.Height);
        _canvas.SetViewport(e.Width, e.Height);
        base.OnFramebufferResize(e);       
    }


    protected override void OnUnload()
    {
        _canvas.Dispose();
        base.OnUnload();
    }


    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButton.Left)
            return;

        (int canvasX, int canvasY) = _canvas.GetCoord(MouseState.X, MouseState.Y);
        _life.Set(canvasY, canvasX, true);

        Console.WriteLine($"\n    Width {ClientSize.X}");
        Console.WriteLine($"   Height {ClientSize.Y}");
        Console.WriteLine($"   MouseX {MouseState.X}");
        Console.WriteLine($"   MouseY {MouseState.Y}");
        Console.WriteLine($"  CanvasX {canvasX}");
        Console.WriteLine($"  CanvasY {canvasY}");
        Console.WriteLine($"PixelSize {_canvas.PixelSize}");
    }


    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        if (e.Key == Keys.Escape)
            Close();
            
        base.OnKeyDown(e);
    }


    private void UpdateTitle(double time)
    {
        _frameTime += time;
        _fps++;

        if (_frameTime >= 1.0f)
        {
            Title = $"{TITLE} | {_fps} fps";
            _frameTime = 0.0f;
            _fps = 0;
        }
    }
}
