// @author Lisoferma
// Некоторые оптимизации игры взяты из https://habr.com/ru/articles/505606/

using System.Collections.Concurrent;
using System.Drawing;

namespace GameOfLife;

/// <summary>
/// Игра жизнь. Поле ограничено. Поддерживает многопоточность.
/// </summary>
public class GameOfLife
{
    /// <summary>
    /// Максимальное количество потоков для вычислений.
    /// </summary>
    public int MaxCores
    {
        get => _parallelOptions.MaxDegreeOfParallelism;
        set => _parallelOptions.MaxDegreeOfParallelism = value;
    }

    /// <summary>
    /// Количество живых клеток.
    /// </summary>
    public int LiveCellCount;

    /// <summary>
    /// Ширина поля.
    /// </summary>
    private readonly int _width;

    /// <summary>
    /// Высота поля.
    /// </summary>
    private readonly int _height;

    /// <summary>
    /// Количество клеток поля.
    /// </summary>
    private readonly int _totalCells;

    /// <summary>
    /// Содержит состояния живых и мёртвых клеток для каждой комбинации расположения соседей.
    /// Чтобы получить состояние клетки - обратиться к ячейке по индексу числом у которого:
    /// младшие 3 бита хранят кол-во соседей, 4 бит хранит состояние клетки.
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
        _width = width;
        _height = height;
        _totalCells = _width * _width;

        _field = new byte[_totalCells];
        _temp = new byte[_totalCells];

        _parallelOptions = new ParallelOptions()
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };
    }


    /// <summary>
    /// Получить ширину поля.
    /// </summary>
    public int GetWidth()
    {
        return _width;
    }


    /// <summary>
    /// Получить высоту поля.
    /// </summary>
    public int GetHeight()
    {
        return _height;
    }


    /// <summary>
    /// Получить изображение поля.
    /// </summary>
    /// <param name="image">Массив цветов для отображения.</param>
    /// <param name="lifeColor">Цвет живой клетки.</param>
    /// <param name="deadColor">Цвет мёртвой клетки.</param>
    public void GetImage(Color[,] image, Color lifeColor, Color deadColor)
    {
        Parallel.ForEach(Partitioner.Create(0, _totalCells), _parallelOptions, range =>
        {
            // (uint)i < (uint)length - подсказка компилятору,
            // что обращение по индексу к массиву не нуждается в проверке диапазона
            for (int i = range.Item1; (uint)i < (uint)range.Item2; i++)
            {
                int y = i / _width;
                int x = i % _width;

                if (_field[x * _width + y] == 1)
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
        return _field[x * _width + y] == 1;
    }


    /// <summary>
    /// Установить состояние клекти по координатам.
    /// </summary>
    /// <param name="x">Координата по X.</param>
    /// <param name="y">Координата по Y.</param>
    /// <param name="value">Состояние клекти: true - живая, false - мёртвая.</param>
    public void Set(int x, int y, bool value)
    {
        _field[x * _width + y] = (byte)(value ? 1 : 0);
    }


    /// <summary>
    /// Перейти на следующее поколение.
    /// </summary>
    public void Step()
    {
        int from = _width + 1;
        int to = _totalCells - _width - 1;

        Parallel.ForEach(Partitioner.Create(from, to), _parallelOptions, CountNeighbors);

        from = _width;
        to = _totalCells - _width;

        Parallel.ForEach(Partitioner.Create(from, to), _parallelOptions, DetermineCellsState);

        from = 1;
        to = _height - 1;

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
            byte* currFieldPtr;
            ulong* currTempPtr;

            for (int i = range.Item1; (uint)i < (uint)range.Item2; i += 8)
            {
                currFieldPtr = fieldPtr + i;
                currTempPtr = (ulong*)(tempPtr + i);

                *currTempPtr = 0;
                *currTempPtr += *(ulong*)(currFieldPtr - _width - 1);
                *currTempPtr += *(ulong*)(currFieldPtr - _width);
                *currTempPtr += *(ulong*)(currFieldPtr - _width + 1);
                *currTempPtr += *(ulong*)(currFieldPtr - 1);
                *currTempPtr += *(ulong*)(currFieldPtr + 1);
                *currTempPtr += *(ulong*)(currFieldPtr + _width - 1);
                *currTempPtr += *(ulong*)(currFieldPtr + _width);
                *currTempPtr += *(ulong*)(currFieldPtr + _width + 1);
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
            byte neighbours;
            byte cellStatus;
            int liveCellCount = 0;

            for (int i = range.Item1; (uint)i < (uint)range.Item2; i++)
            {
                // Максимальное число соседей - 8 (4 бита). Уменьшим до 3 битов, т.к.
                // 8 и 0 соседей дают одинаковый эффект, поэтому игнорируем четвертый бит
                // используя "& 7". Состояние текущей клетки положим в 4 бит используя "<< 3".
                // Объеденим количество соседей и состояние клетки в один байт используя "|".
                // По таблице узнаём новое состояние клетки.
                neighbours = (byte)((tempPtr[i] & 7) | (fieldPtr[i] << 3));
                cellStatus = _alivePerNeighbours[neighbours];
                fieldPtr[i] = cellStatus;
                liveCellCount += cellStatus;
            }

            LiveCellCount = liveCellCount;
        }
    }


    /// <summary>
    /// Заполнить левую и правую границы поля <see cref="_field"/>
    /// мёртвыми клетками на заданном отрезке.
    /// </summary>
    /// <param name="range">Отрезок границ на которых нужно заполнить клетки.</param>
    private void FillBorderWithZeros(Tuple<int, int> range)
    {
        for (int j = range.Item1; (uint)j < (uint)range.Item2; j++)
        {
            _field[j * _width] = 0;
            _field[j * _width + _width - 1] = 0;
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

        for (int x = 1; (uint)x < (uint)_width - 1; x++)
        {
            for (int y = 1; (uint)y < (uint)_height - 1; y++)
            {
                bool isLiveCell = rand.NextDouble() < density;
                Set(x, y, isLiveCell);
            }
        }
    }


    /// <summary>
    /// Очистить поле.
    /// </summary>
    public void Clear()
    {
        for (int i = 1; (uint)i < (uint)_width - 1; i++)
        {
            for (int j = 1; (uint)j < (uint)_height - 1; j++)
            {
                Set(i, j, false);
            }
        }
    }
}
