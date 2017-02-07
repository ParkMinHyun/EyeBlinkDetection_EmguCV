using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.VideoSurveillance;
using System.Threading;

using AForge;
using AForge.Imaging;
using AForge.Imaging.Filters;

namespace Eyeblink_EmguCV
{
    public partial class Form1 : Form
    {
        private Capture _capture;
        private HaarCascade _faces;
        private Image<Bgr, Byte> frame;

        private Rectangle possibleROI_rightEye, possibleROI_leftEye;
        private LineSegment2D a, b, c;
        private Bgr d;

        private Bitmap Thimage;
        private BackgroundWorker worker;

        private int blurAmount = 1;
        private int thresholdValue = 30;
        private int prevThresholdValue = 0;
        private int blinkNum = 0;

        private List<int> averageThresholdValue;
        public static Boolean catchBlackPixel = false;
        public static Boolean catchBlink = false;

        public Form1()
        {
            InitializeComponent();

            // capture 생성
            if (_capture == null)
            {
                try
                {
                    _capture = new Capture();
                    _faces = new HaarCascade("C:\\haarcascade_frontalface_alt_tree.xml");


                    averageThresholdValue = new List<int>();
                    worker = new BackgroundWorker();
                    worker.WorkerReportsProgress = true;
                    worker.WorkerSupportsCancellation = true;
                    worker.DoWork += new DoWorkEventHandler(worker_DoWork);
                    worker.ProgressChanged += new ProgressChangedEventHandler(worker_ProgressChanged);
                    worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
                }
                catch (NullReferenceException excpt) { }
            }

            if (_capture != null)
            {
                Application.Idle += FrameGrabber;
            }
        }

        void FrameGrabber(object sender, EventArgs e)
        {
            //새로운 Frame 얻기
            frame = _capture.QueryFrame();
            imageBoxCapturedFrame.Image = frame;
            //grayscale로 변환( haarcascade를 적용할 땐 회색 화면을 더 잘 잡는다고 함 )
            Image<Gray, Byte> grayFrame = frame.Convert<Gray, Byte>();
            grayFrame._EqualizeHist();

            if (!worker.IsBusy)
                worker.RunWorkerAsync(grayFrame);

            #region 눈 영역이 Null이 아닐 경우
            if (possibleROI_rightEye.IsEmpty.Equals(false) && possibleROI_leftEye.IsEmpty.Equals(false))
            {
                try
                {
                    imageBox1.Image = frame.Copy(possibleROI_rightEye).Convert<Bgr, byte>();
                    imageBox3.Image = frame.Copy(possibleROI_leftEye).Convert<Bgr, byte>();

                    Form1.catchBlackPixel = false;
                    thresholdEffect(thresholdValue);

                    pictureBox1.Image = Thimage;
                }
                catch (ArgumentException expt) { }
            }
            #endregion
        }//FrameGrapper

        public void thresholdEffect(int catchThreshold)
        {
            if (Form1.catchBlackPixel.Equals(true))
            {
                return;
            }
            Thimage = (Bitmap)imageBox1.Image.Bitmap;
            IFilter threshold = new Threshold(catchThreshold);
            Thimage = Grayscale.CommonAlgorithms.RMY.Apply(Thimage);
            Thimage = threshold.Apply(Thimage);

            if (Thimage.Width > 50)
                Thimage = ResizeImage(Thimage, new Size(40, 30));

            Median filter = new Median();
            filter.ApplyInPlace(Thimage);
            //ConservativeSmoothing filter2 = new ConservativeSmoothing();
            //filter.ApplyInPlace(Thimage);

            for (int x = blurAmount + 5; x <= Thimage.Width - blurAmount; x++)
            {
                for (int y = blurAmount + 5; y <= Thimage.Height - blurAmount; y++)
                {
                    try
                    {
                        Color prevX = Thimage.GetPixel(x - blurAmount, y);
                        Color nextX = Thimage.GetPixel(x + blurAmount, y);
                        Color prevY = Thimage.GetPixel(x, y - blurAmount);
                        Color nextY = Thimage.GetPixel(x, y + blurAmount);

                        int avgR = (int)((prevX.R + nextX.R + prevY.R + nextY.R) / 4);
                        if (avgR < 150)
                        {
                            Form1.catchBlackPixel = true;
                            break;
                        }
                    }
                    catch (Exception) { }
                }
            }

            if (Form1.catchBlackPixel.Equals(true))
            {
                if (averageThresholdValue.Count > 3)
                {
                    averageThresholdValue.Add((averageThresholdValue[1] + averageThresholdValue[2]) / 2);
                    Double doubleValue = averageThresholdValue.Average() - averageThresholdValue.Average() % 10;
                    int a = (int)doubleValue;
                    thresholdValue = a;
                }
                else
                {
                    averageThresholdValue.Add(catchThreshold);
                }

                if (catchThreshold > averageThresholdValue.Average() + 8 &&
                    catchThreshold < averageThresholdValue.Average() + 25)
                {
                    if (!catchBlink)
                    {
                        blinkNum++;
                        catchBlink = true;
                        label3.Text = blinkNum.ToString();
                    }
                    else
                    {
                        catchBlink = true;
                    }

                }
                else
                {
                    catchBlink = false;
                }

                //label2.Text = ((double)prevThresholdValue / (double)catchThreshold).ToString();
                return;
            }
            else
            {
                if (catchThreshold < 120)
                {
                    catchThreshold += 1;
                    prevThresholdValue = catchThreshold;
                    thresholdEffect(catchThreshold);
                }
            }
        }

        // Worker Thread가 실제 하는 일
        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            Image<Gray, Byte> grayFrame = (Image<Gray, Byte>)e.Argument;
            // 머신러닝을 이용한 얼굴 인식 Haaracascade 돌리기
            MCvAvgComp[][] facesDetected = grayFrame.DetectHaarCascade(_faces, 1.1, 0, Emgu.CV.CvEnum.HAAR_DETECTION_TYPE.FIND_BIGGEST_OBJECT, new Size(20, 20));
            if (facesDetected[0].Length != 0)
            {
                MCvAvgComp face = facesDetected[0][0];

                #region 얼굴 인식한것을 토대로 눈 찾기
                Int32 yCoordStartSearchEyes = face.rect.Top + (face.rect.Height * 3 / 11);
                System.Drawing.Point startingPointSearchEyes = new System.Drawing.Point(face.rect.X, yCoordStartSearchEyes);
                System.Drawing.Point endingPointSearchEyes = new System.Drawing.Point((face.rect.X + face.rect.Width), yCoordStartSearchEyes);

                Size searchEyesAreaSize = new Size(face.rect.Width, (face.rect.Height * 2 / 9));
                Size eyeAreaSize = new Size(face.rect.Width / 2, (face.rect.Height * 2 / 9));
                System.Drawing.Point lowerEyesPointOptimized = new System.Drawing.Point(face.rect.X, yCoordStartSearchEyes + searchEyesAreaSize.Height);
                System.Drawing.Point startingLeftEyePointOptimized = new System.Drawing.Point(face.rect.X + face.rect.Width / 2, yCoordStartSearchEyes);

                Rectangle leftEyeArea = new Rectangle(new System.Drawing.Point(startingPointSearchEyes.X + 25, startingPointSearchEyes.Y + 10),
                                                     new Size(eyeAreaSize.Width - 25, eyeAreaSize.Height - 20));
                Rectangle rightEyeArea = new Rectangle(new System.Drawing.Point(startingLeftEyePointOptimized.X + 5, startingLeftEyePointOptimized.Y + 10),
                                                     new Size(eyeAreaSize.Width - 33, eyeAreaSize.Height - 20));
                #endregion

                //#region 눈 영역 그리기
                //a = new LineSegment2D(startingPointSearchEyes, endingPointSearchEyes);
                //b = new LineSegment2D(new System.Drawing.Point(lowerEyesPointOptimized.X, lowerEyesPointOptimized.Y),
                //                      new System.Drawing.Point((lowerEyesPointOptimized.X + face.rect.Width), (yCoordStartSearchEyes + searchEyesAreaSize.Height)));
                //d = new Bgr(Color.Chocolate);



                ////그리기
                //frame.Draw(a, d, 3);
                //frame.Draw(b, d, 3);
                //#endregion

                #region 눈 영역 검출한 Rectangle의 크기가 양수일 경우에만 눈 영역 적출하기
                if (leftEyeArea.Width > 0 && leftEyeArea.Height > 0 && rightEyeArea.Width > 0 && rightEyeArea.Height > 0)
                {
                    possibleROI_leftEye = leftEyeArea;
                    possibleROI_rightEye = rightEyeArea;
                }
                #endregion 
            }// if(faceDetect[0])
        }

        // Progress 리포트 - UI Thread
        void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
        }

        // 작업 완료 - UI Thread
        void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {

        }


        private void trackBar2_Scroll(object sender, EventArgs e)
        {
        }

        private static Bitmap ResizeImage(Bitmap image, Size newSize)
        {
            Bitmap newImage = new Bitmap(newSize.Width, newSize.Height);
            using (Graphics g = Graphics.FromImage((System.Drawing.Image)newImage))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(image, 0, 0, newSize.Width, newSize.Height);
            }
            return newImage;
        }
    }

}