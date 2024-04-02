using System.Drawing;

namespace GameOfLife;

public class GameOfLife
{
    public int Width {  get; private set; }

    public int Height { get; private set; }

    private static byte[] _alivePerNeighbours = new byte[256];

    private byte[] _field;

    private byte[] _temp;


    public GameOfLife(int width, int height)
    {
        Width = width;
        Height = height;

        _field = new byte[Width * Height];
        _temp = new byte[Width * Height];

        _alivePerNeighbours[3] = 1;
        _alivePerNeighbours[8 + 2] = 1;
        _alivePerNeighbours[8 + 3] = 1;
    }


    public void GetImage(Color[,] image, Color lifeColor, Color deadColor)
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (Get(x, y))
                    image[y, x] = lifeColor;
                else
                    image[y, x] = deadColor;
            }
        }
    }


    public bool Get(int i, int j)
    {
        return _field[j * Width + i] == 1;
    }


    public void Set(int i, int j, bool value)
    {
        _field[j * Width + i] = (byte)(value ? 1 : 0);
    }


    public unsafe void Step()
    {
        fixed (byte* fieldPtr = _field, tempPtr = _temp)
        {
            for (int i = 0; i < Width * Height; i += 8)
            {
                *(ulong*)(tempPtr + i) = 0;
            }

            for (int i = Width + 1; i < Width * Height - Width - 1; i += 8)
            {
                ulong* ptr = (ulong*)(tempPtr + i);
                *ptr += *(ulong*)(fieldPtr + i - Width - 1);
                *ptr += *(ulong*)(fieldPtr + i - Width);
                *ptr += *(ulong*)(fieldPtr + i - Width + 1);
                *ptr += *(ulong*)(fieldPtr + i - 1);
                *ptr += *(ulong*)(fieldPtr + i + 1);
                *ptr += *(ulong*)(fieldPtr + i + Width - 1);
                *ptr += *(ulong*)(fieldPtr + i + Width);
                *ptr += *(ulong*)(fieldPtr + i + Width + 1);
            }

            for (int i = Width; i < Width * Height - Width; i++)
            {
                byte neighbours = (byte)((tempPtr[i] & 7) | (fieldPtr[i] << 3));
                fieldPtr[i] = _alivePerNeighbours[neighbours];
            }

        }

        for (int j = 1; j < Height - 1; j++)
        {
            _field[j * Width] = 0;
            _field[j * Width + Width - 1] = 0;
        }
    }


    public void GenerateRandomField(int seedForRandom, double threshold)
    {
        Random rand = new(seedForRandom);

        for (int x = 1; x < Width - 1; x++)
        {
            for (int y = 1; y < Height - 1; y++)
            {
                bool isLiveCell = rand.NextDouble() < threshold;
                Set(x, y, isLiveCell);
            }
        }
    }


    public int GetLiveCellsCount()
    {
        int count = 0;

        for (int x = 1; x < Width - 1; x++)
        {
            for (int y = 1; y < Height - 1; y++)
            {
                if (Get(x, y)) count++;
            }
        }

        return count;
    }


    public void Clear()
    {
        for (int i = 1; i < Width - 1; i++)
        {
            for (int j = 1; j < Height - 1; j++)
            {
                Set(i, j, false);
            }
        }         
    }
}
