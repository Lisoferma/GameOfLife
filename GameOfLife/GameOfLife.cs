// @author Lisoferma
// Оптимизации игры взяты из https://habr.com/ru/articles/505606/

using System.Collections.Concurrent;
using System.Drawing;

namespace GameOfLife;

/// <summary>
/// Игра жизнь. Поле ограничено. Поддерживает многопоточность.
/// </summary>
public class GameOfLife
{
    /// <summary>
    /// Ширина поля.
    /// </summary>
    public int Width { get; private set; }

    /// <summary>
    /// Высота поля.
    /// </summary>
    public int Height { get; private set; }

    /// <summary>
    /// Максимальное количество потоков для вычислений.
    /// </summary>
    public int MaxCores
    {
        get => _parallelOptions.MaxDegreeOfParallelism;
        set => _parallelOptions.MaxDegreeOfParallelism = value;
        }

    /// <summary>
    /// Содержит состояния живых и мёртвых клеток для каждой комбинации расположения соседей.
    /// Заменяет проверки на количество соседей для оптимизации.
    /// </summary>
    private static readonly byte[] _alivePerNeighbours = new byte[16];

    /// <summary>
    /// Настройки для распаралеливающих циклов.
    /// </summary>
    private ParallelOptions _parallelOptions;

    /// <summary>
    /// Поле игры, содержащее результат вычислений.
    /// 1 - живая клетка, 0 - мёртвая клетка.
    /// </summary>
    private byte[] _field;

    /// <summary>
    /// Содержит количество соседей для каждой клетки.
    /// </summary>
    private byte[] _temp;


    static GameOfLife()
    {
        // Живая клетка с двумя соседями
        _alivePerNeighbours[3] = 1;

        // Живая клетка с тремя соседями
        _alivePerNeighbours[8 + 2] = 1;

        // Мёртвая клетка с тремя соседями
        _alivePerNeighbours[8 + 3] = 1;
    }


    /// <summary>
    /// Инициализировать игру с заданной шириной и высотой поля.
    /// </summary>
    /// <param name="width">Ширина поля.</param>
    /// <param name="height">Высота поля.</param>
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
    }


    /// <summary>
    /// Получить изображение поля.
    /// </summary>
    /// <param name="image">Массив цветов для отображения.</param>
    /// <param name="lifeColor">Цвет живой клетки.</param>
    /// <param name="deadColor">Цвет мёртвой клетки.</param>
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


    /// <summary>
    /// Получить состояние клетки по координатам.
    /// </summary>
    /// <param name="x">Координата по X.</param>
    /// <param name="y">Координата по Y.</param>
    /// <returns>Состояние клекти: true - живая, false - мёртвая.</returns>
    public bool Get(int x, int y)
    {
        return _field[x * Width + y] == 1;
    }


    /// <summary>
    /// Установить состояние клекти по координатам.
    /// </summary>
    /// <param name="x">Координата по X.</param>
    /// <param name="y">Координата по Y.</param>
    /// <param name="value">Состояние клекти: true - живая, false - мёртвая.</param>
    public void Set(int x, int y, bool value)
    {
        _field[x * Width + y] = (byte)(value ? 1 : 0);
    }


    /// <summary>
    /// Перейти на следующее поколение.
    /// </summary>
    public void Step()
    {
        int from = Width + 1;
        int to = Width * Height - Width - 1;

            Parallel.ForEach(Partitioner.Create(from, to), _parallelOptions, CountNeighbors);

        from = Width;
        to = Width * Height - Width;

        Parallel.ForEach(Partitioner.Create(from, to), _parallelOptions, DetermineCellsState);

        from = 1;
        to = Height - 1;

        Parallel.ForEach(Partitioner.Create(from, to), _parallelOptions, FillBorderWithZeros);
        }


    /// <summary>
    /// Подсчитать количество соседей для каждой клетки
    /// на заданном отрезке поля <see cref="_field"/>.
    /// Результат хранится в <see cref="_temp"/>.
    /// </summary>
    /// <param name="range">Отрезок массива на котором нужно провести подсчёт.</param>
    private unsafe void CountNeighbors(Tuple<int, int> range)
    {
        fixed (byte* fieldPtr = _field, tempPtr = _temp)
        {
            for (int i = range.Item1; i < range.Item2; i += 8)
            {
                ulong* ptr = (ulong*)(tempPtr + i);
                *ptr = 0;
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


    /// <summary>
    /// Определить состояние клеток на заданном отрезке поля <see cref="_field"/>
    /// в зависимости от числа соседей посчитаных в <see cref="_temp"/>.
    /// </summary>
    /// <param name="range">Отрезок массива на котором нужно определить состояние клеток.</param>
    private unsafe void DetermineCellsState(Tuple<int, int> range)
    {
        fixed (byte* fieldPtr = _field, tempPtr = _temp)
        {
            for (int i = range.Item1; i < range.Item2; i++)
            {
                // Максимальное число соседей - 8 (4 бита). Уменьшим до 3 битов, т.к.
                // 8 и 0 соседей дают одинаковый эффект, поэтому игнорируем четвертый бит
                // используя "& 7". Состояние текущей клетки положим в 4 бит используя "<< 3".
                // Объеденим количество соседей и состояние клетки в один байт используя "|".
                // По таблице узнаём новое состояние клетки.
                byte neighbours = (byte)((tempPtr[i] & 7) | (fieldPtr[i] << 3));
                fieldPtr[i] = _alivePerNeighbours[neighbours];
            }
        }
    }


    /// <summary>
    /// Заполнить левую и правую границы поля <see cref="_field"/>
    /// мёртвыми клетками на заданном отрезке.
    /// </summary>
    /// <param name="range">Отрезок границ на которых нужно заполнить клетки.</param>
    private void FillBorderWithZeros(Tuple<int, int> range)
    {
        for (int j = range.Item1; j < range.Item2; j++)
        {
            _field[j * Width] = 0;
            _field[j * Width + Width - 1] = 0;
        }
    }


    /// <summary>
    /// Создать случайное поле.
    /// </summary>
    /// <param name="seedForRandom">Сид для генерации.</param>
    /// <param name="density">Плотность живых клеток в диапазоне 0.0 - 1.0,
    /// чем больше плотность, тем больше живых клеток.</param>
    public void GenerateRandomField(int seedForRandom, double density)
    {
        Random rand = new(seedForRandom);

        for (int x = 1; x < Width - 1; x++)
        {
            for (int y = 1; y < Height - 1; y++)
            {
                bool isLiveCell = rand.NextDouble() < density;
                Set(x, y, isLiveCell);
            }
        }
    }


    /// <summary>
    /// Получить количество живых клеток.
    /// </summary>
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


    /// <summary>
    /// Очистить поле.
    /// </summary>
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
