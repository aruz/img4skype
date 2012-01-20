using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Text;

namespace img4skype
{
    public partial class Form1 : Form
    {
        private Bitmap _src;

        public Form1()
        {
            InitializeComponent();
        }

        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            var result = new Bitmap(width, height);

            using (var graphics = Graphics.FromImage(result))
            {
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                graphics.DrawImage(image, 0, 0, result.Width, result.Height);
            }

            return result;
        }


        private void Button1Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() != DialogResult.OK) return;
            _src = new Bitmap(openFileDialog1.FileName);

            pictureBox1.Image = _src;

            button2.Enabled = true;
            textBox2.Text = "0";
        }


        private IEnumerable<Color?> Scan(Bitmap bitmap)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    yield return pixel.A == 0
                                     ? Color.White
                                     : Color.FromArgb(pixel.R, pixel.G, pixel.B);
                }
                yield return null;
            }
        }


        private Dictionary<Color, Color> CorrectColors(IEnumerable<Color?> scaned, Bitmap img, int error)
        {
            Func<int, int> D = v => v * v;

            Func<Color, Color, bool> colorCmp = (color1, color2) =>
                error < 100 && (color1 == Color.White || color2 == Color.White) ? false :
                Math.Sqrt(D(color1.R - color2.R) + D(color1.G - color2.G) + D(color1.B - color2.B)) <= error;

            var mapColors = scaned
                .Where(c => c != null)
                .Select(row => row.Value)
                .GroupBy(row => row)
                .ToDictionary(row => row.Key,
                              row => new
                                         {
                                             newColor = row.Key.A == 0
                                                            ? Color.White
                                                            : Color.FromArgb(row.Key.R, row.Key.G, row.Key.B),
                                             count = row.Count()
                                         });

            var processed = new HashSet<Color>();

            foreach (var color in mapColors.OrderByDescending(row => row.Value.count).Select(row => row.Key).ToArray())
            {
                if (processed.Add(color))
                {
                    foreach (var oldcolor in mapColors.Keys.ToArray().Where(c => !processed.Contains(c)).Where(c => colorCmp(color, c)))
                    {
                        processed.Add(oldcolor);
                        mapColors[oldcolor] = new { newColor = color, count = 0 };
                    }
                }
            }

            return mapColors.ToDictionary(row => row.Key, row => row.Value.newColor);
        }


        private Tuple<Action<Color?, int>, Action> Preview(int width, int height, Action<Bitmap> onCustomFinish)
        {
            var preview = new Bitmap(width * 4, height * 4);

            Action<int, int, Color> previewSetPixel = (x, y, c) =>
            {
                for (var i = 0; i < 2; i++)
                    for (var j = 0; j < 3; j++)
                        preview.SetPixel(x * 2 + i, y * 3 + j, c);
            };

            int lastx = 0, lasty = 0;
            Action<Color?, int> write = (c, len) =>
            {
                if (c != null)
                {
                    for (var x = 0; x < len; x++)
                    {
                        previewSetPixel(lastx + x, lasty, c.Value);
                    }
                    lastx += len;
                }
                else
                {
                    lastx = 0;
                    lasty++;
                }
            };

            Action onFinish = () => onCustomFinish(preview);

            return new Tuple<Action<Color?, int>, Action>(write, onFinish);
        }

        private Tuple<Action<Color?, int>, Action> ToSkype(Color backgroundColor, Action<string> onCustomFinish)
        {
            Func<Color, string> printColor = (color) => "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");

            var result = new StringBuilder();
            result.AppendFormat("<font color='{0}' size='1'><b><u>", printColor(backgroundColor));

            Action<Color?, int> write = (color, len) =>
            {
                if (color != null)
                {
                    if (color != backgroundColor)
                    {
                        result.AppendFormat("<font color='{0}'>{1}</font>", printColor(color.Value),
                                                new string('█', len));
                    }
                    else
                    {
                        result.Append(new string('█', len));
                    }
                }
                else
                {
                    result.AppendLine();
                }
            };

            Action onFinish = () =>
            {
                result.Append("</u></b></font>");
                onCustomFinish(result.ToString());
            };

            return new Tuple<Action<Color?, int>, Action>(write, onFinish);
        }


        private void Button2Click(object sender, EventArgs e)
        {
            var rate = _src.Height / (double)_src.Width;
            var h = Convert.ToInt32(textBox1.Text);
            var img = ResizeImage(_src, Convert.ToInt32(h * 1.5), Convert.ToInt32(h * rate));
            var scaned = Scan(img).ToArray();
            if (scaned.Length > 7000)
            {
                label1.Text = "Very big size";
                return;
            }
            var error = Convert.ToInt32(textBox2.Text);

            // попробуем подобрать минимальное возможное колличество ошибок
            if (checkBox1.Checked)
            {
                error = Enumerable.Range(0, int.MaxValue).BinarySearch(err =>
                {
                    var mc = CorrectColors(scaned, img, err);

                    var size = 0;
                    var checkSize = ToSkype(
                        mc.GroupBy(row => row.Value).OrderByDescending(row => row.Count()).First().Key,
                        (result) =>
                        {
                            size = result.Length + result.Count(c => c == '█') * 2;
                        });

                    MergeColumns(
                        scaned,
                        mc,
                        write: checkSize.Item1,
                        finish: checkSize.Item2
                     );

                    return size < 30000;
                });

                textBox2.Text = error.ToString();
            }

            // вывод
            Dictionary<Color, Color> mapColors = CorrectColors(scaned, img, error);
            var preview = Preview(img.Width, img.Height, result => pictureBox2.Image = result);
            var toSkype = ToSkype(
                mapColors.GroupBy(row => row.Value).OrderByDescending(row => row.Count()).First().Key,
                (result) =>
                {
                    Clipboard.SetText(result);
                    label1.Text = "Symbols: " + (result.Length + result.Count(c => c == '█') * 2).ToString() + " (copied to the clipboard)";
                });

            MergeColumns(scaned, mapColors,
                write: (Action<Color?, int>)Delegate.Combine(preview.Item1, toSkype.Item1),
                finish: (Action)Delegate.Combine(preview.Item2, toSkype.Item2)
             );
        }

        private void MergeColumns(IEnumerable<Color?> scaned, Dictionary<Color, Color> mapColors, Action<Color?, int> write, Action finish)
        {
            var bufferLen = 0;
            Color? bufferColor = null;
            foreach (var c in scaned)
            {
                if (c != null)
                {
                    var nc = mapColors[c.Value];
                    if (bufferColor != null)
                    {
                        if (bufferColor == nc)
                        {
                            bufferLen += 1;
                        }
                        else
                        {
                            write(bufferColor, bufferLen);
                            bufferColor = nc;
                            bufferLen = 1;
                        }
                    }
                    else
                    {
                        bufferColor = nc;
                        bufferLen = 1;
                    }
                }
                else
                {
                    if (bufferColor != null)
                    {
                        write(bufferColor, bufferLen);
                        bufferColor = null;
                        bufferLen = 0;
                    }

                    write(null, 0);
                }
            }

            finish();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            textBox2.Enabled = !checkBox1.Checked;
        }


    }

    public static class Ext
    {
        public static T BinarySearch<T>(this IEnumerable<T> ar, Func<T, bool> check)
        {
            var en = ar.GetEnumerator();
            var buffer = new List<T>();

            var found = false;
            do
            {
                buffer.Clear();
                for (var i = 0; i < 5; i++)
                {
                    en.MoveNext();
                    buffer.Add(en.Current);
                }

                found = check(buffer.Last());
            }
            while (!found);

            buffer.Reverse();
            return buffer.TakeWhile(check).Last();
        }
    }
}