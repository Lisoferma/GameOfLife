﻿using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Drawing;
using ImGuiNET;
using DearImGui;
using FoxCanvas;

namespace GameOfLife;

internal class Window : GameWindow
{
    private const string TITLE = "GameOfLife";

    private double _frameTimeCounter = 0.0;   
    private int _fpsCounter = 0;
    private int _fps = 0;
    private int _limitFps = 0;
    private int _limitSteps = -1;

    private int _maxCores = Environment.ProcessorCount;

    private int _fieldWidth = 500;
    private int _fieldHeight = 500;

    private int _seed = 123;
    private float _density = 0.5f;
    private long _generation = 0;

    private ImGuiController _gui;
    private Canvas _canvas;
    private Color[,] _image;
    private GameOfLife _life;


    public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
        : base(gameWindowSettings, nativeWindowSettings)
    { }


    protected override void OnLoad()
    {
        base.OnLoad();

        GL.ClearColor(Color.HotPink);

        _gui = new ImGuiController(ClientSize.X, ClientSize.Y);

        CreateField();
    }


    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);

        _life.Step();
        _life.GetImage(_image, Color.DeepPink, Color.White);
        _canvas.SetImage(_image);

        ++_generation;

        UpdateFps(e.Time);
    }


    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);

        GL.Clear(ClearBufferMask.ColorBufferBit);

        _canvas.Render();

        _gui.Update(this, (float)e.Time);
        DrawGUI();
        _gui.Render();
        ImGuiController.CheckGLError("End of frame");

        SwapBuffers();    
    }


    protected override void OnFramebufferResize(FramebufferResizeEventArgs e)
    {
        base.OnFramebufferResize(e);
        GL.Viewport(0, 0, e.Width, e.Height);
        _gui.WindowResized(e.Width, e.Height);
        _canvas.SetViewport(e.Width, e.Height);         
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
        _life.Set(canvasX, canvasY, true);

        //Console.WriteLine($"\n    Width {ClientSize.X}");
        //Console.WriteLine($"   Height {ClientSize.Y}");
        //Console.WriteLine($"   MouseX {MouseState.X}");
        //Console.WriteLine($"   MouseY {MouseState.Y}");
        //Console.WriteLine($"  CanvasX {canvasX}");
        //Console.WriteLine($"  CanvasY {canvasY}");
        //Console.WriteLine($"PixelSize {_canvas.PixelSize}");
    }


    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Keys.Escape)
            Close();      
    }


    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        _gui.PressChar((char)e.Unicode);
    }


    private void UpdateFps(double time)
    {
        _frameTimeCounter += time;
        ++_fpsCounter;

        if (_frameTimeCounter >= 1.0f)
        {
            _fps = _fpsCounter;
            _frameTimeCounter = 0.0f;
            _fpsCounter = 0;

            UpdateTitle();
        }
    }


    private void UpdateTitle()
    {
        Title = $"{TITLE} | {_fps} fps";
    }


    private void DrawGUI()
    {
        ImGui.DockSpaceOverViewport(ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);
        ImGui.StyleColorsLight();

        ImGui.Begin("Game of Life");

        ImGui.TextWrapped($"Live cells: {_life.LiveCellCount}");
        ImGui.TextWrapped($"Generation: {_generation}");

        if (ImGui.CollapsingHeader("Field"))
        {
            ImGui.InputInt("width", ref _fieldWidth);
            ImGui.InputInt("height", ref _fieldHeight);
            ImGui.InputInt("seed", ref _seed);
            ImGui.SliderFloat("density", ref _density, 0.0f, 1.0f);

            if (ImGui.Button("Apply"))
            {
                if (IsFieldSizeChanged())
                    CreateField();
                else
                    _life.GenerateRandomField(_seed, _density);
            }           
        }

        if (ImGui.CollapsingHeader("Settings"))
        {
            ImGui.Text("Max FPS (0 - unlimited)");
            if (ImGui.InputInt("frames", ref _limitFps))
                UpdateFrequency = _limitFps;

            ImGui.NewLine();
            ImGui.Text("Max cores");
            if (ImGui.SliderInt("cores", ref _maxCores, 1, Environment.ProcessorCount))
                _life.MaxCores = _maxCores;
        }

        ImGui.End();
    }


    private void CreateField()
    {
        _canvas?.Dispose();

        _canvas = new Canvas(_fieldWidth, _fieldHeight, ClientSize.X, ClientSize.Y);
        _image = new Color[_fieldWidth, _fieldHeight];

        _life = new(_fieldWidth, _fieldHeight);
        _life.GenerateRandomField(_seed, _density);
    }


    private bool IsFieldSizeChanged()
    {
        return _life.Width != _fieldWidth
            || _life.Height != _fieldHeight;       
    }
}
