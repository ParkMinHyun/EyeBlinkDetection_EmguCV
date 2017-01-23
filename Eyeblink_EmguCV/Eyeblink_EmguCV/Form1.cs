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

        private Bitmap newBitmap;
        private int blurAmount = 1;

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

                #region 눈 영역 그리기
                a = new LineSegment2D(startingPointSearchEyes, endingPointSearchEyes);
                b = new LineSegment2D(new System.Drawing.Point(lowerEyesPointOptimized.X, lowerEyesPointOptimized.Y),
                                      new System.Drawing.Point((lowerEyesPointOptimized.X + face.rect.Width), (yCoordStartSearchEyes + searchEyesAreaSize.Height)));
                d = new Bgr(Color.Chocolate);

                //그리기
                frame.Draw(a, d, 3);
                frame.Draw(b, d, 3);
                #endregion

                #region 눈 영역 검출한 Rectangle의 크기가 양수일 경우에만 눈 영역 적출하기
                if (leftEyeArea.Width > 0 && leftEyeArea.Height > 0 && rightEyeArea.Width > 0 && rightEyeArea.Height > 0)
                {
                    possibleROI_leftEye = leftEyeArea;
                    possibleROI_rightEye = rightEyeArea;
                }
                #endregion 
            }// if(faceDetect[0])

            #region 눈 영역이 Null이 아닐 경우
            if (possibleROI_rightEye.IsEmpty.Equals(false) && possibleROI_leftEye.IsEmpty.Equals(false))
            {
                try
                {
                    imageBox1.Image = frame.Copy(possibleROI_rightEye).Convert<Bgr, byte>();

                    var Thimage = (Bitmap)imageBox1.Image.Bitmap;
                    IFilter threshold = new Threshold(70);
                    Thimage = Grayscale.CommonAlgorithms.RMY.Apply(Thimage);
                    Thimage = threshold.Apply(Thimage);
                    Image<Bgr, Byte> myImage = new Image<Bgr, Byte>(Thimage);

                    pictureBox1.Image = Thimage;
                    newBitmap = myImage.Bitmap;

                    Median filter = new Median();
                    filter.ApplyInPlace(newBitmap);

                    ConservativeSmoothing filter2 = new ConservativeSmoothing();
                    filter.ApplyInPlace(newBitmap);


                    pictureBox1.Image = newBitmap;
                    //blurEffect();
                }
                catch (ArgumentException expt) { }

                #region Thread 이용하여 RGB평균값 계산
                //Worker worker = new Worker(imageBox2, frame, possibleROI_rightEye, label4, label5, label6);
                //Thread workerThread = new Thread(worker.calculateRGB_Eye);

                //if (!workerThread.IsAlive)
                //{
                //    workerThread.Start();
                //}
                #endregion
            }
            #endregion

        }//FrameGrabber

    }
}
