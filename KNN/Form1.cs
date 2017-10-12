using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using LibSVMsharp;
using LibSVMsharp.Core;
using LibSVMsharp.Extensions;
using LibSVMsharp.Helpers;
using System.IO;
using System.Windows.Forms.DataVisualization.Charting;

namespace KNN
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        List<int[]> TrainingSet = new List<int[]>();
        int[,] CoinTrainingSet = new int[2000,2];

        int[] RV_XY;

        //原始圖片的資訊
        Bitmap source;
        Bitmap copy;
        int height;
        int wide;
        int[,,] Image;

        //擲硬幣變數
        double CoinA_Probability;
        double CoinB_Probability;

        //畫框框的變數
        Rectangle rect;
        Graphics g;
        Point[] choose = new Point[2];

        //影像處理指標法的變數
        BitmapData BmData;
        
        //LOAD IMAGE 按鈕事件
        private void button3_Click(object sender, EventArgs e)
        {
            OpenFileDialog open = new OpenFileDialog();
            open.Filter = "JPG|*.jpg|BMP|*.bmp|PNG|*.png";
            if (open.ShowDialog() == DialogResult.OK)
            {
                pictureBox1.ImageLocation = open.FileName;
                source = new Bitmap(open.FileName);
                copy = new Bitmap(open.FileName);
                g = pictureBox1.CreateGraphics();
                wide = source.Width;
                height = source.Height;
                Image = new int[3, wide, height];
                TrainingSet.Clear();

                //指標法將影像RGB抽取出來存放在陣列中
                Rectangle recta = new Rectangle(0, 0, wide, height);
                BmData = source.LockBits(recta, ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
                IntPtr Scan = BmData.Scan0;
                int Offset = BmData.Stride - wide * 3;
                unsafe
                {
                    byte* P = (byte*)(void*)Scan;
                    for (int y = 0; y < height; y++, P += Offset)
                    {
                        for (int x = 0; x < wide; x++, P += 3)
                        {
                            Image[0, x, y] = P[2];
                            Image[1, x, y] = P[1];
                            Image[2, x, y] = P[0];
                        }
                    }
                }
                source.UnlockBits(BmData);
            }
        }

        //前景按鈕事件
        private void button1_Click(object sender, EventArgs e)
        {
            int[] Bound = new int[4];
            Bound[0] = choose[0].X;
            Bound[1] = choose[0].Y;
            Bound[2] = choose[1].X;
            Bound[3] = choose[1].Y;
            label1.Text = "(" + choose[0].X + "," + choose[0].Y + ") " + "(" + choose[1].X + "," + choose[1].Y + ") ";
            AddTrainingData(Bound, 1);
        }

        //背景按鈕事件
        private void button2_Click(object sender, EventArgs e)
        {
            int[] Bound = new int[4];
            Bound[0] = choose[0].X;
            Bound[1] = choose[0].Y;
            Bound[2] = choose[1].X;
            Bound[3] = choose[1].Y;
            label2.Text = "(" + choose[0].X + "," + choose[0].Y + ") " + "(" + choose[1].X + "," + choose[1].Y + ") ";
            AddTrainingData(Bound, 0);
        }

        //KNN按鈕事件
        private void button4_Click(object sender, EventArgs e)
        {
            int K = (int)numericUpDown1.Value;
            int[] mindis = new int[K];
            int[] get;
            int[] vote;
            int[,] classify = new int[wide, height];
            List<distance> dis = new List<distance>();

            for (int x = 0; x < wide; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    dis.Clear();
                    vote = new int[2];
                    for (int i = 0; i < TrainingSet.Count; i++)
                    {
                        get = TrainingSet[i];
                        dis.Add(new distance(i, ED(Image[0, x, y], Image[1, x, y], Image[2, x, y], get[1], get[2], get[3])));
                    }

                    dis.Sort((disA, disB) => { return disA.dis.CompareTo(disB.dis); });
                    for (int i = 0; i < K; i++)
                    {
                        vote[TrainingSet[dis[i].index][0]]++;
                    }
                    if (vote[0] > vote[1])
                        classify[x, y] = 0;
                    else
                        classify[x, y] = 1;
                }
            }

            //改圖
            Rectangle recta = new Rectangle(0, 0, wide, height);
            BmData = copy.LockBits(recta, ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            IntPtr Scan = BmData.Scan0;
            int Offset = BmData.Stride - wide * 3;
            unsafe
            {
                byte* P = (byte*)(void*)Scan;
                for (int y = 0; y < height; y++, P += Offset)
                {
                    for (int x = 0; x < wide; x++, P += 3)
                    {
                        if (classify[x, y] == 1)
                        {
                            P[2] = 255;
                            P[1] = 255;
                            P[0] = 255;
                        }
                        else
                        {
                            P[2] = 0;
                            P[1] = 0;
                            P[0] = 0;
                        }
                    }
                }
            }
            copy.UnlockBits(BmData);
            pictureBox2.Image = copy;
        }

        //SVM按鈕事件
        private void button5_Click(object sender, EventArgs e)
        {
            StreamWriter Train_txt = new StreamWriter(@"train.txt");
            StreamWriter Test_txt = new StreamWriter(@"test.txt");

            int[] get;
            for (int i = 0; i < TrainingSet.Count; i++)
            {
                get = TrainingSet[i];
                if (get[0] == 1)
                    Train_txt.WriteLine("1" + " 1:" + get[1] + " 2:" + get[2] + " 3:" + get[3]);
                else
                    Train_txt.WriteLine("-1" + " 1:" + get[1] + " 2:" + get[2] + " 3:" + get[3]);
            }

            for (int i = 0; i < height; i++)
                for (int j = 0; j < wide; j++)
                    Test_txt.WriteLine("1" + " 1:" + Image[0, j, i] + " 2:" + Image[1, j, i] + " 3:" + Image[2, j, i]);

            Train_txt.Close();
            Test_txt.Close();

            SVMProblem problem = SVMProblemHelper.Load(@"train.txt");
            SVMProblem testProblem = SVMProblemHelper.Load(@"test.txt");

            SVMParameter parameter = new SVMParameter();
            parameter.Type = SVMType.C_SVC;
            parameter.Kernel = SVMKernelType.RBF;
            parameter.C = 1;
            parameter.Gamma = 0.0001;

            SVMModel model = SVM.Train(problem, parameter);

            double[] target = new double[testProblem.Length];
            for (int i = 0; i < testProblem.Length; i++)
                target[i] = SVM.Predict(model, testProblem.X[i]);

            //改圖
            Rectangle recta = new Rectangle(0, 0, wide, height);
            BmData = copy.LockBits(recta, ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            IntPtr Scan = BmData.Scan0;
            int Offset = BmData.Stride - wide * 3;
            unsafe
            {
                byte* P = (byte*)(void*)Scan;
                for (int y = 0; y < height; y++, P += Offset)
                {
                    for (int x = 0; x < wide; x++, P += 3)
                    {
                        if (target[y * wide + x] > 0)
                        {
                            P[2] = 255;
                            P[1] = 255;
                            P[0] = 255;
                        }
                        else
                        {
                            P[2] = 0;
                            P[1] = 0;
                            P[0] = 0;
                        }
                    }
                }
            }
            copy.UnlockBits(BmData);
            pictureBox3.Image = copy;
        }

        //Coin Random按鈕事件
        private void button6_Click(object sender, EventArgs e)
        {
            int[] RV_X = new int[10000];
            int[] RV_Y = new int[10000];
            RV_XY = new int[20000];
            bool[] choosed = new bool[20000];

            CoinA_Probability = (double)numericUpDown2.Value / 100.0;

            CoinB_Probability = (double)numericUpDown3.Value / 100.0;

            Random flip = new Random(Guid.NewGuid().GetHashCode());
            double result;
            int result2;

            //Flip Coin A 1,000,000 times and save head appear times
            for (int i = 1, j = 0; i <= 1000000; i++)
            {
                result = flip.NextDouble();
                if (result <= CoinA_Probability)
                    RV_X[j]++;
                if (i % 100 == 0)
                    j++;
            }

            //Flip Coin B 1,000,000 times and save head appear times
            for (int i = 1, j = 0; i <= 1000000; i++)
            {
                result = flip.NextDouble();
                if (result <= CoinB_Probability)
                    RV_Y[j]++;
                if (i % 100 == 0)
                    j++;
            }

            //mix CoinA and CoinB
            for(int i = 0; i < RV_XY.Length; i++)
            {
                if (i < 10000)
                    RV_XY[i] = RV_X[i];
                else
                    RV_XY[i] = RV_Y[i - 10000];
            }

            //Random Catch
            for(int i = 0; i < 2000; i++)
            {
                result2 = flip.Next(0,19999);
                while (choosed[result2] == true)
                    result2 = flip.Next(0, 19999);
                if(result2 < 10000)
                    CoinTrainingSet[i, 0] = 1;
                else
                    CoinTrainingSet[i, 0] = -1;
                CoinTrainingSet[i, 1] = RV_XY[result2];
                choosed[result2] = true;
            }

            Series Series1 = new Series("coinA", 10000);
            Series Series2 = new Series("coinB", 10000);
            Series1.Color = Color.Blue;
            Series2.Color = Color.Red;
            Series1.ChartType = SeriesChartType.Line;
            Series2.ChartType = SeriesChartType.Line;
            int[] times_X = new int[101];
            int[] times_Y = new int[101];


            for (int i = 0; i < 10000; i++)
            {
                //  if (RV[i] >= 0 && RV[i] < 100)
                times_X[RV_X[i]]++;
                times_Y[RV_Y[i]]++;
            }

            int maxbound = 0;

            for (int i = 0; i <= 100; i++)
            {
                if (times_X[i] > maxbound)
                    maxbound = times_X[i];
                if (times_Y[i] > maxbound)
                    maxbound = times_Y[i];
            }

            for (int i = 0; i < 101; i++)
            {
                Series1.Points.AddXY(i + 1, times_X[i]);
                Series2.Points.AddXY(i + 1, times_Y[i]);
            }

            this.chart1.Series.Clear();
            this.chart1.Series.Add(Series1);
            this.chart1.Series.Add(Series2);

        }

        //Coin KNN按鈕事件
        private void button7_Click(object sender, EventArgs e)
        {
            int K = (int)numericUpDown4.Value;
            int neibor = 0;
            int dis = 0;
            int[] vote = new int[2];
            int[] classify = new int[20000];
            int hit = 0;

            for(int i = 0; i < 20000; i++)
            {
                neibor = 0;
                dis = 0;
                vote = new int[2];
                while (neibor < K || vote[0] == vote[1])
                {
                    for (int j = 0; j < 2000; j++)
                    {
                        if(CoinTrainingSet[j,1] == RV_XY[i] + dis || CoinTrainingSet[j, 1] == RV_XY[i] - dis)
                        {
                            neibor++;
                            if (CoinTrainingSet[j, 0] == -1)
                                vote[1]++;
                            else
                                vote[0]++;
                        }
                    }
                    dis++;
                }
                if (vote[0] > vote[1])
                    classify[i] = 1;
                else
                    classify[i] = 0;
            }

            for(int i = 0; i < 20000; i++)
            {
                if (i < 10000 && classify[i] == 1)
                    hit++;
                if (i >= 10000 && classify[i] == 0)
                    hit++;
            }
            label5.Text = ((double)hit / 20000 * 100).ToString();
        }

        //Coin SVM按鈕事件
        private void button8_Click(object sender, EventArgs e)
        {
            StreamWriter Train_txt = new StreamWriter(@"train.txt");
            StreamWriter Test_txt = new StreamWriter(@"test.txt");

            for (int i = 0; i < 2000; i++)
            {
                if (CoinTrainingSet[i,0] == 1)
                    Train_txt.WriteLine("1" + " 1:" + CoinTrainingSet[i, 1]);
                else
                    Train_txt.WriteLine("-1" + " 1:" + CoinTrainingSet[i, 1]);
            }

            for (int i = 0; i < 20000; i++)
                if (i < 10000)
                    Test_txt.WriteLine("1" + " 1:" + RV_XY[i]);
                else
                    Test_txt.WriteLine("-1" + " 1:" + RV_XY[i]);

            Train_txt.Close();
            Test_txt.Close();

            SVMProblem problem = SVMProblemHelper.Load(@"train.txt");
            SVMProblem testProblem = SVMProblemHelper.Load(@"test.txt");

            SVMParameter parameter = new SVMParameter();
            parameter.Type = SVMType.C_SVC;
            parameter.Kernel = SVMKernelType.RBF;
            parameter.C = 1;
            parameter.Gamma = 0.0001;

            SVMModel model = SVM.Train(problem, parameter);

            double[] target = new double[testProblem.Length];
            for (int i = 0; i < testProblem.Length; i++)
                target[i] = SVM.Predict(model, testProblem.X[i]);

            double accuracy = SVMHelper.EvaluateClassificationProblem(testProblem, target);
            label6.Text = accuracy.ToString();
        }

        class distance
        {
            public int index;
            public int dis;
            public distance(int i, int d)
            {
                index = i;
                dis = d;
            }
        }

        private void AddTrainingData(int[] bound, int kind)
        {
            for (int i = bound[0]; i < bound[2]; i++)
                for (int j = bound[1]; j < bound[3]; j++)
                {
                    TrainingSet.Add(new int[] { kind, Image[0, i, j], Image[1, i, j], Image[2, i, j] });
                }
            Console.Write("test");
        }

        private int ED(int a1, int a2, int a3, int b1, int b2, int b3)
        {
            int dis = (a1 - b1) * (a1 - b1) + (a2 - b2) * (a2 - b2) + (a3 - b3) * (a3 - b3);
            return dis;
        }

        #region 畫框框
        Point ToImageXY(Point picboxXY)
        {
            Point imageXY = new Point();
            double w1 = source.Width;
            double w2 = pictureBox1.Width;
            double h1 = source.Height;
            double h2 = pictureBox1.Height;
            if (w1 > h1)
            {
                imageXY.X = (int)(w1 / w2 * picboxXY.X);
                imageXY.Y = (int)(w1 / w2 * (picboxXY.Y - (1 - h1 / w1) * h2 / 2));
            }
            else
            {
                imageXY.Y = (int)(h1 / h2 * picboxXY.Y);
                imageXY.X = (int)(h1 / h2 * (picboxXY.X - (1 - w1 / h1) * w2 / 2));
            }
            return imageXY;
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                //滑鼠邊移動邊畫方框

                pictureBox1.Image = source;
                pictureBox1.Update();
                rect = new Rectangle(rect.Left, rect.Top, e.X - rect.Left, e.Y - rect.Top);
                choose[0] = ToImageXY(new Point(rect.Left, rect.Top));
                choose[1] = ToImageXY(new Point(e.X, e.Y));
                pictureBox1_Paint();
            }
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            pictureBox1.Image = source;
            pictureBox1.Update();
            rect = new Rectangle(e.X, e.Y, 0, 0);
            pictureBox1_Paint();
        }

        private void pictureBox1_Paint()
        {
            g.DrawRectangle(new Pen(Color.Red, 2), rect);
        }


        #endregion
    }
}
