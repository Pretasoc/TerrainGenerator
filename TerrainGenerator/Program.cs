using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerrainGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            var gen = new Generator2()
            {
                MinimumHeight = 0,
                MaximumHeight = 400,
                //EstimateElevation = 50,
                MaximulDeltaAngle = 0.17,
            };

            var prof = gen.CreateTerrain(13, null);

            var max = (int) Math.Pow(2, 13) +1;

            var fs = new StreamWriter(@"C:\Users\phile\Desktop\data.csv");
            var img = new Bitmap(max, max , PixelFormat.Format24bppRgb);
            var data = img.LockBits(new Rectangle(0, 0, max , max ), ImageLockMode.ReadWrite, img.PixelFormat);

            IntPtr ptr = data.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int bytes = Math.Abs(data.Stride) * img.Height;
            

            var ms = new MemoryStream(bytes);

            var filler = 3 * max % 4;
            if (filler > 0)
                filler = 4 - filler;
            var f = new byte[filler];

            var sw = new StreamWriter(ms);
            for (int i = 0; i < max; i++)
            {

                for (int j = 0; j < max; j++)
                {
                    var value = (byte) (prof[i, j] / 400 * 255);
                    var c = GetColor(prof[i, j], 0, 255);
                    ms.Write(new[] {c.R, c.G, c.B},0,3);

                    fs.Write($"{value,5:D5} ");
                }
                ms.Write(f, 0, filler);
              
                fs.WriteLine();
            }

            System.Runtime.InteropServices.Marshal.Copy(ms.ToArray(), 0, ptr, (int) ms.Position);

            // Unlock the bits.
            img.UnlockBits(data);

            img.Save(@"C:\Users\phile\Desktop\data.png", ImageFormat.Png);

            fs.Close();
        }

        private static Color GetColor(double value, int min, int max)
        {
            var targetRange = (uint) 0xFFFFFF;
            var position = (int) (targetRange * (value / (max - min)));
            return Color.FromArgb(position);

        }
    }

    public class Generator
    {
        private readonly Random rand = new Random(); //reuse this if you are generating many

        public double MinHeight { get; set; }
        public double MaxHeight { get; set; }
        public double EstimateElevation { get; set; }

        public double MaxGradient { get; set; }

        public double StdDev { get; set; }

        public double[,] GenerateElevationProfile(int x, int y)
        {
            var cache = new double[x, y];

            cache[0, 0] = NextRandom(MinHeight, MaxHeight, EstimateElevation);

            for (int i = 1; i < x * y; i++)
            {

                var cx = i % x;
                var cy = i / x;

                Console.WriteLine($"({cx}, {cy})");

                // short operations
                var targety = cx == 0 ? null : GetTargetInterval(cache[cx - 1, cy]);
                var targetx = cy == 0 ? null : GetTargetInterval(cache[cx, cy - 1]);

                var intersection = Intersect(targety, targetx);

                var backtraces = 1;
                while (intersection == null)
                {
                    backtraces++;
                    targetx = GetTargetInterval(cache[cx - backtraces, cy], backtraces);
                    intersection = Intersect(targety, targetx);
                }

                if (backtraces > 1)
                {
                    i -= backtraces + 1;
                    continue;
                }


                cache[cx, cy] = NextRandom(intersection.Value.min, intersection.Value.max, (intersection.Value.min + intersection.Value.max) / 2);

                // do backtrace


            }

            return cache;
        }

        private (double min, double max)? GetTargetInterval(double height, int times = 1)
        {
            return Intersect<double>((height - MaxGradient * times, height + MaxGradient * times), (MinHeight, MaxHeight));
        }

        private (T min, T max)? Intersect<T>(IEnumerable<(T min, T max)?> intervals) where T : IComparable<T>
        {
            var cintervals = intervals.Where(t => t != null).Cast<(T, T)>();
            var valueTuples = cintervals as IList<(T min, T max)> ?? cintervals.ToList();
            var first = valueTuples.First();
            var min = first.min;
            var max = first.max;

            foreach (var interval in valueTuples)
            {
                if (min.CompareTo(interval.min) < 0)
                    min = interval.min;

                if (max.CompareTo(interval.max) > 0)
                    max = interval.max;

            }

            if (min.CompareTo(max) > 0)
                return null;

            return (min, max);
        }

        private (T min, T max)? Intersect<T>(params (T min, T max)?[] intervals) where T : IComparable<T>
        {
            return Intersect((IEnumerable<(T, T)?>)intervals);
        }

        private double NextRandom(double min, double max, double mean)
        {
            double randNormal;
            do
            {
                var u1 = 1.0 - rand.NextDouble(); //uniform(0,1] random doubles
                var u2 = 1.0 - rand.NextDouble();
                var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                                    Math.Sin(2.0 * Math.PI * u2); //random normal(0,1)
                randNormal = mean + StdDev * randStdNormal; //random normal(mean,stdDev^2)
            } while (randNormal < min || randNormal > max);


            return randNormal;
        }
    }

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

        private struct DiamondRect
        {
            private readonly double[,] data;

            private readonly int x;

            private readonly int y;

            private readonly int width;

            private readonly int size;

            public DiamondRect(double[,] data, int x, int y, int width, int size)
            {
                this.data = data;
                this.x = (x + size) % size;
                this.y = (y + size) % size;
                this.width = width;
                this.size = size;
            }

            public double Left
            {
                get => data[width * x, width * y + width / 2];
                set => data[width * x, width * y + width / 2] = value;
            }

            public double Right
            {
                get => data[width * (x + 1), width * y + width / 2];
                set => data[width * (x + 1), width * y + width / 2] = value;
            }

            public double Upper
            {
                get => data[width * x + width / 2, width * y];
                set => data[width * x + width / 2, width * y] = value;
            }

            public double Lower
            {
                get => data[width * x + width / 2, width * (y + 1)];
                set => data[width * x + width / 2, width * (y + 1)] = value;
            }


            public double UpperLeft => data[width * x, width * y];

            public double UpperRicht => data[width * (x + 1), width * y];

            public double LowerLeft => data[width * x, width * (y + 1)];

            public double LowerRight => data[width * (x + 1), width * (y + 1)];

            public double DownRightAngle => (data[width * x, width * y] -
                                             data[(width * (x - 1) + size) % size, (width * (y - 1) + size) % size]) /
                                            (width * sqrt2);

            public double DownLeftAngle => (data[width * (x + 1), width * y] -
                                             data[(width * (x + 2) + size) % size, (width * (y - 1) + size) % size]) /
                                            (width * sqrt2);

            public double UpRightAngle => (data[width * x, width * (y + 1)] -
                                             data[(width * (x - 1) + size) % size, (width * (y + 2) + size) % size]) /
                                            (width * sqrt2);

            public double UpLefttAngle => (data[width * (x + 2), width * (y + 1)] -
                                             data[(width * (x + 2) + size) % size, (width * (y + 2) + size) % size]) /
                                            (width * sqrt2);

            public double Center
            {
                get => data[width * x + width / 2, width * y + width / 2];
                set => data[width * x + width / 2, width * y + width / 2] = value;
            }
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
