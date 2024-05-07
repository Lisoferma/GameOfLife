using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;

namespace GameOfLife;

internal class Program
{
    static void Main()
    {
        NativeWindowSettings nativeWindowSettings = new()
        {
            Title = "GameOfLife",
            ClientSize = new Vector2i(600, 600),      
        };

        GameWindowSettings gameWindowSettings = new()
        {
            UpdateFrequency = 0.0
        };

        using (Window window = new(gameWindowSettings, nativeWindowSettings))
        {
            window.Run();
        }
    }
}
