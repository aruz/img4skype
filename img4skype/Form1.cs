using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

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

        public bool ColorCmp(Color color1, Color color2, int e)
        {
            var d = Math.Abs(color1.R - color2.R) + Math.Abs(color1.G - color2.G) + Math.Abs(color1.B - color2.B);
            return d/3 <= e;
        }

        public string PrintColor(Color color)
        {
            return color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        }

        class ColorSum
        {
            public Color Color;
            public int Sum;
        }

        private void Button1Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() != DialogResult.OK) return;
            _src = new Bitmap(openFileDialog1.FileName);

            pictureBox1.Image = _src;

            button2.Enabled = true;
        }

        private void Button2Click(object sender, EventArgs e)
        {
            var rate = _src.Height/(double) _src.Width;
            var h = Convert.ToInt32(textBox1.Text);
            var img = ResizeImage(_src, Convert.ToInt32(h * 1.5), Convert.ToInt32(h * rate));

            h *= 4;
            var preview = new Bitmap(Convert.ToInt32(h*1.5), Convert.ToInt32(h * rate));

            var d = Convert.ToInt32(textBox2.Text);

            var list = new List<ColorSum>();
            for (var i = 0; i < img.Height; i++)
                for (var j = 0; j < img.Width; j++)
                {
                    var color = img.GetPixel(j, i);
                    color = color.A == 0 ? Color.White : Color.FromArgb(color.R, color.G, color.B);

                    if (ColorCmp(color, Color.White, d)) continue;
                    var colorSet = list.FirstOrDefault(c => ColorCmp(c.Color, color, d));
                    if (colorSet == null)
                        list.Add(new ColorSum {Color = color, Sum = 1});
                    else
                        colorSet.Sum++;
                }

            var maxColor = list.First(c => c.Sum == list.Max(c1 => c1.Sum)).Color;

            var data = "<font color=\"#" + PrintColor(maxColor) + "\" size=\"1\"><b>";

            var pcolor = new Color();
            var fcolor = new Color();
            var fonttag = false;
            var utag = false;

            for (var i = 0; i < img.Height; i++)
            {
                for (var j = 0; j < img.Width; j++)
                {
                    var color = img.GetPixel(j, i);
                    color = color.A == 0 ? Color.White : Color.FromArgb(color.R, color.G, color.B);

                    if (ColorCmp(color, maxColor, d))
                    {
                        if(fonttag)
                        {
                            data += "</font>";
                            fonttag = false;
                        }
                        if (!utag)
                        {
                            data += "<u>";
                            utag = true;
                        }
                        data += "█";
                        PreviewSetPixel(preview, j, i, maxColor);
                        continue;
                    }

                    if (ColorCmp(color, Color.White, d))
                    {
                        if (fonttag)
                        {
                            data += "</font>";
                            fonttag = false;
                        }
                        if (utag)
                        {
                            data += "</u>";
                            utag = false;
                        }
                        data += "  ";
                        PreviewSetPixel(preview, j, i, Color.White);
                        continue;
                    }

                    if (!ColorCmp(color, pcolor, d))
                    {
                        if (fonttag)
                        {
                            data += "</font>";
                            fonttag = false;
                        }
                    }

                    if (!fonttag)
                    {
                        if (!utag)
                        {
                            data += "<u>";
                            utag = true;
                        }
                        data += "<font color=\"#" + PrintColor(color) + "\">█";
                        fonttag = true;
                        fcolor = color;
                        PreviewSetPixel(preview, j, i, fcolor);
                    }
                    else
                    {
                        data += "█";
                        PreviewSetPixel(preview, j, i, fcolor);
                    }
                    pcolor = color;

                }
                data += Environment.NewLine;
            }

            if (fonttag) data += "</font>";
            if (utag) data += "</u>";
            data += "</b></font>";

            pictureBox2.Image = preview;
            label1.Text = data.Length.ToString();

            richTextBox1.Text = data;
        }

        private void PreviewSetPixel(Bitmap p, int x, int y, Color c)
        {
            for (var i = 0; i < 2; i++)
                for (var j = 0; j < 3; j++)
                    p.SetPixel(x * 2 + i, y * 3 + j, c);

        }
    }
}
