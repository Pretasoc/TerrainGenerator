using System;
using System.Collections.Generic;
using System.Linq;
using TerrainGenerator;

namespace TerrainGeneratorGui
{

    public class Generator2
    {
        public double MinimumHeight { get; set; }

        public double MaximumHeight { get; set; }

        public double MaximulDeltaAngle { get; set; }

        private readonly Random rand = new Random();

        private const double sqrt2 = 1.4142135623731D;

        public double[,] CreateTerrain(int detail, IProgress<RenderProgress> status)
        {
            var grid = new GridArray<double>(detail) { CurrentLevel = 0 };

            // Seed the edged

            //edge at (0,0)

            var target = 2 * (Math.Pow(2, detail + 1) - 1);


       
                var h00 = NextRandom(MinimumHeight, MaximumHeight);
              
                grid[0, 0] = h00;
                grid[1, 0] = h00;
                grid[0, 1] = h00;
                grid[1, 1] = h00;



            for (var currentLevel = 1; currentLevel <= detail; currentLevel++)
            {
                grid.CurrentLevel = currentLevel;


                var baseValue = 2 * (Math.Pow(2, currentLevel) - 1);
                // perform diamond step

                for (var x = 1; x < grid.CurrentSize; x += 2)
                {

                    status?.Report(new RenderProgress() {Progress = x / grid.CurrentSize * 50, Level=currentLevel});

                    for (var y = 1; y < grid.CurrentSize; y += 2)
                    {

                        var intersection = GetTargetInterval(grid, x, y, true);
                        if (intersection == null)
                        {
                            currentLevel -= 2;
                            goto backtrace;
                        }

                        grid[x, y] = NextRandom(intersection.Value);
                    }
                }
                    


                // perform square step

                for (var x = 1; x < grid.CurrentSize; x += 2)
                {
                    status?.Report(new RenderProgress() { Progress = x / grid.CurrentSize * 50 +50 ,Level = currentLevel });
                    for (var y = 1; y < grid.CurrentSize; y += 2)
                    {
                        foreach (var direction in orthogonalDirections)
                        {
                            (var cx, var cy) = grid.GetNeighbor(x, y, direction);

                            var intersection = GetTargetInterval(grid, cx, cy, false);
                            if (intersection == null)
                            {
                                currentLevel -= 2;
                                goto backtrace;
                            }

                            grid[cx, cy] = NextRandom(intersection.Value);
                        }

                    }
                }

                    
                continue;
                backtrace:

                ;

            }

            return grid;
        }

    

        private readonly IEnumerable<GridArrayExtensions.Directions> diagonalDirections = new[]
        {
            GridArrayExtensions.Directions.NorthEast, GridArrayExtensions.Directions.NorthWest,
            GridArrayExtensions.Directions.SouthEast, GridArrayExtensions.Directions.SouthWest
        };

        private readonly IEnumerable<GridArrayExtensions.Directions> orthogonalDirections = new[]
        {
            GridArrayExtensions.Directions.North, GridArrayExtensions.Directions.South,
            GridArrayExtensions.Directions.East, GridArrayExtensions.Directions.West
        };
        private (double, double)? GetTargetInterval(GridArray<double> instance, int x, int y, bool diamond)
        {
            var directions = diamond ? diagonalDirections : orthogonalDirections;
            return Intersect(directions.Select(d => instance.GetAngle(x, y, d, MaximulDeltaAngle)).ToArray());
        }



        private (double min, double max) GetAngle(double preAngle, double distance, double height)
        {
            var upperAngle = preAngle + Math.Tan(MaximulDeltaAngle)*0.5;
            var lowerAngle = preAngle - Math.Tan(MaximulDeltaAngle)*0.5;

            return (lowerAngle * distance + height, upperAngle * distance + height);
        }





        private (T min, T max)? Intersect<T>(IEnumerable<(T min, T max)?> intervals) where T : IComparable<T>
        {
            var cintervals = intervals.Where(t => t.HasValue).Select(t => t.Value);
            var valueTuples = cintervals as IList<(T min, T max)> ?? cintervals.ToList();
            var first = valueTuples.First();
            var min = valueTuples.Min(t => t.min);
            var max = valueTuples.Max(t => t.max);



            return min.CompareTo(max) > 0 ? ((T,T)?) null : (min, max);
        }



        private (T min, T max)? Intersect<T>(params (T min, T max)[] intervals) where T : IComparable<T>
        {
            return Intersect(intervals.Select(i => new(T, T)?(i)));
        }
        private double NextRandom(double min, double max)
        {

            return rand.NextDouble() * (max - min) + min;

        }

        private double NextRandom((double min, double max) d)
        {

            return NextRandom(d.min, d.max);

        }
    }

    public struct RenderProgress
    {
        public double Progress { get; set; }
        public int Level { get; set; }
 
    }
}
