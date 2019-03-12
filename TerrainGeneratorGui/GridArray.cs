using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerrainGenerator
{
    public class GridArray<T>
    {

        private readonly T[,] data;
        private readonly int size;

        private int _currentSize;
        private int _currentLevel;
        private int _currentStep;

        public GridArray(int levels)
        {
            size = (int)Math.Pow(2, levels)  +1;
            Levels = levels;

         
            data = new T[size, size];
        }

        public int Levels { get; }

        public T this[int x, int y]
        {
            get
            {
                TranslateIndex(ref x, ref y);

                return data[x, y];
            }

            set
            {
                TranslateIndex(ref x, ref y);

                data[x, y] = value;
            }
        }

        private void TranslateIndex(ref int x, ref int y)
        {
            x = (x + CurrentSize) % CurrentSize;
            y = (y + CurrentSize) % CurrentSize;
            x = x * _currentStep;
            y = y * _currentStep;
        }

        public int CurrentLevel
        {
            get { return _currentLevel; }
            set
            {
                if (value > Levels)
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        $"The maximum allowed value is {Levels}.");

                _currentLevel = value;
                _currentSize = (int)Math.Pow(2, _currentLevel) + 1;
                _currentStep = (int)Math.Pow(2, Levels - CurrentLevel);
            }
        }

        public int CurrentSize => _currentSize;

        public static implicit operator T[,](GridArray<T> grid)
        {
            return (T[,]) grid.data.Clone();
        }
    }

    public static class GridArrayExtensions
    {
        public static double CalculateGradient(this GridArray<double> instance, int x, int y, Directions direction)
        {
            var vector = direction.GetVector();
            //var farMultiplicator = direction.IsDiagonal() ? 2 : 3;
            var near = instance[x + vector.Item1, y + vector.Item2];
            //var far = instance[x + vector.Item1 * farMultiplicator, y + vector.Item2 * farMultiplicator];
            var far = instance[x - vector.Item1, y - vector.Item2];
            return (far - near) / (2* (direction.IsDiagonal() ? Math.Sqrt(2) * instance.CurrentSize : instance.CurrentSize));
        }

        public static T GetNeighborValue<T>(this GridArray<T> instance, int x, int y, Directions direction)
        {
            return instance[x + direction.GetVector().Item1, y + direction.GetVector().Item2];
        }

        public static (int, int) GetNeighbor<T>(this GridArray<T> instance, int x, int y, Directions direction)
        {
            return (x + direction.GetVector().Item1, y + direction.GetVector().Item2);
        }

        private static (int, int) GetVector(this Directions direction)
        {
            var ret = (0, 0);

            if (direction == Directions.North || direction == Directions.NorthWest || direction == Directions.NorthEast)
                ret = (ret.Item1, ret.Item2-1);
            if (direction == Directions.South || direction == Directions.SouthWest || direction == Directions.SouthEast)
                ret = (ret.Item1 , ret.Item2+1);

            if (direction == Directions.West || direction == Directions.NorthWest || direction == Directions.SouthWest)
                ret = (ret.Item1-1, ret.Item2);

            if (direction == Directions.East || direction == Directions.NorthEast || direction == Directions.SouthEast)
                ret = (ret.Item1+1, ret.Item2 );

            return ret;
        }

        private static bool IsDiagonal(this Directions direction)
        {
            return direction != Directions.North && direction != Directions.South && direction != Directions.West && direction != Directions.East;
        }

        public static (double min, double max) GetAngle(this GridArray<double> instance, int x, int y, Directions direction, double maximulDeltaAngle)
        {
            var preAngle = instance.CalculateGradient(x, y, direction);
            var upperAngle = preAngle + Math.Tan(maximulDeltaAngle) * (direction.IsDiagonal() ? 1 / Math.Sqrt(2) : 0.5);
            var lowerAngle = preAngle - Math.Tan(maximulDeltaAngle) * (direction.IsDiagonal() ? 1 / Math.Sqrt(2) : 0.5);
            var distance = direction.IsDiagonal() ? instance.CurrentSize * Math.Sqrt(2) : instance.CurrentSize;
            var height = instance.GetNeighborValue(x, y, direction);

            return (lowerAngle * distance + height, upperAngle * distance + height);
        }

        public enum Directions
        {
            North,
            NorthWest,
            West,
            SouthWest,
            South,
            SouthEast,
            East,
            NorthEast
        }
    }
}
