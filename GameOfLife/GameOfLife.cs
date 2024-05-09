using System.Collections.Concurrent;
using System.Drawing;

namespace GameOfLife;

public class GameOfLife
{
    public int Width {  get; private set; }

    public int Height { get; private set; }

    public int MaxCores
    {
        get => _parallelOptions.MaxDegreeOfParallelism;

        set
        {
            _parallelOptions.MaxDegreeOfParallelism = value;
        }
    }

    private ParallelOptions _parallelOptions;

    private static readonly byte[] _alivePerNeighbours = new byte[256];

    private byte[] _field;

    private byte[] _temp;


    public GameOfLife(int width, int height)
    {
        Width = width;
        Height = height;

        _parallelOptions = new ParallelOptions()
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        _field = new byte[Width * Height];
        _temp = new byte[Width * Height];

        _alivePerNeighbours[3] = 1;
        _alivePerNeighbours[8 + 2] = 1;
        _alivePerNeighbours[8 + 3] = 1;
    }


    public void GetImage(Color[,] image, Color lifeColor, Color deadColor)
    {
        int totalPixels = Height * Width;

        Parallel.ForEach(Partitioner.Create(0, totalPixels), _parallelOptions, range =>
        {
            for (int i = range.Item1; i < range.Item2; i++)
            {
                int y = i / Width;
                int x = i % Width;

                if (Get(x, y))
                    image[y, x] = lifeColor;
                else
                    image[y, x] = deadColor;
            }
        });
        }


    public bool Get(int i, int j)
    {
        return _field[j * Width + i] == 1;
    }


    public void Set(int i, int j, bool value)
    {
        _field[j * Width + i] = (byte)(value ? 1 : 0);
    }


    public void Step()
    {
        int from = 0;
        int to = Width * Height;

        Parallel.ForEach(Partitioner.Create(from, to), _parallelOptions, ClearTemp);

        from = Width + 1;
        to = Width * Height - Width - 1;

            Parallel.ForEach(Partitioner.Create(from, to), _parallelOptions, CountNeighbors);

        from = Width;
        to = Width * Height - Width;

        Parallel.ForEach(Partitioner.Create(from, to), _parallelOptions, FillLife);

        from = 1;
        to = Height - 1;

        Parallel.ForEach(Partitioner.Create(from, to), _parallelOptions, FillBorderWithZeros);
        }


    private unsafe void ClearTemp(Tuple<int, int> range)
    {
        fixed (byte* tempPtr = _temp)
        {
            for (int i = range.Item1; i < range.Item2; i += 8)
        {
                *(ulong*)(tempPtr + i) = 0;
            }
        }
    }


    private unsafe void CountNeighbors(Tuple<int, int> range)
    {
        fixed (byte* fieldPtr = _field, tempPtr = _temp)
        {
            for (int i = range.Item1; i < range.Item2; i += 8)
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
        }
    }


    private unsafe void FillLife(Tuple<int, int> range)
    {
        fixed (byte* fieldPtr = _field, tempPtr = _temp)
        {
            for (int i = range.Item1; i < range.Item2; i++)
            {
                byte neighbours = (byte)((tempPtr[i] & 7) | (fieldPtr[i] << 3));
                fieldPtr[i] = _alivePerNeighbours[neighbours];
            }
        }
    }


    private void FillBorderWithZeros(Tuple<int, int> range)
    {
        for (int j = range.Item1; j < range.Item2; j++)
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
