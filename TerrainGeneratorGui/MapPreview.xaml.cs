using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TerrainGeneratorGui
{
    /// <summary>
    /// Interaktionslogik für MapPreview.xaml
    /// </summary>
    public partial class MapPreview : UserControl
    {
        public static readonly DependencyProperty HeightGradientProperty =
            DependencyProperty.Register("HeightGradient", typeof(LinearGradientBrush), typeof(MapPreview));
        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register("Data", typeof(double[,]), typeof(MapPreview), new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnDataChanged
            ));

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var mapPreview = (MapPreview)d;
            mapPreview._cancel.Cancel();
            mapPreview._cancel = new CancellationTokenSource();
            mapPreview.img.Source = Redraw(mapPreview._cancel.Token, (double[,])mapPreview.Data.Clone(), mapPreview.HeightGradient.GradientStops);

        }




        private CancellationTokenSource _cancel = new CancellationTokenSource();

        public MapPreview()
        {
            InitializeComponent();
        }

        public LinearGradientBrush HeightGradient
        {
            get => (LinearGradientBrush)GetValue(HeightGradientProperty);
            set => SetValue(HeightGradientProperty, value);
        }

        public double[,] Data
        {
            get => (double[,])GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }



        private static ImageSource Redraw(CancellationToken token, double[,] datas, GradientStopCollection stops)
        {
            var heigth = datas.GetLength(0);
            var width = datas.GetLength(1);


            var ms = new MemoryStream();
            var t = Task.Delay(0, token);
            for (var x = 0; x < heigth; x++)
            {
                if (token.IsCancellationRequested)
                    return null;
                for (var y = 0; y < width; y++)
                {

                    var color = GetRelativeColor(stops, datas[x, y]);
                    t = t.ContinueWith((task, cp) =>
                        {
                            var c = (Color)cp;
                            ms.WriteAsync(new[] { c.R, c.G, c.B }, 0, 3, token);
                        },
                        color, token
                    );
                }

            }

            t.Wait(token);

            var renderedData = ms.ToArray();


            if (token.IsCancellationRequested)
                return null;

            return BitmapSource.Create(datas.GetLength(0), datas.GetLength(1), 90, 90, PixelFormats.Rgb24,
                BitmapPalettes.WebPalette, renderedData, 3 * datas.GetLength(1));


        }

        private static Color GetRelativeColor(GradientStopCollection gsc, double offset)
        {
            offset += 50;
            offset /= 350;
            var before = gsc.First(w => Math.Abs(w.Offset - gsc.Min(m => m.Offset)) < double.Epsilon);
            var after = gsc.First(w => Math.Abs(w.Offset - gsc.Max(m => m.Offset)) < double.Epsilon);

            foreach (var gs in gsc)
            {
                if (gs.Offset < offset && gs.Offset > before.Offset)
                {
                    before = gs;
                }
                if (gs.Offset > offset && gs.Offset < after.Offset)
                {
                    after = gs;
                }
            }

            var color = new Color
            {
                ScA = (float)((offset - before.Offset) * (after.Color.ScA - before.Color.ScA) /
                               (after.Offset - before.Offset) + before.Color.ScA),
                ScR = (float)((offset - before.Offset) * (after.Color.ScR - before.Color.ScR) /
                               (after.Offset - before.Offset) + before.Color.ScR),
                ScG = (float)((offset - before.Offset) * (after.Color.ScG - before.Color.ScG) /
                               (after.Offset - before.Offset) + before.Color.ScG),
                ScB = (float)((offset - before.Offset) * (after.Color.ScB - before.Color.ScB) /
                               (after.Offset - before.Offset) + before.Color.ScB)
            };


            return color;
        }
    }


}
