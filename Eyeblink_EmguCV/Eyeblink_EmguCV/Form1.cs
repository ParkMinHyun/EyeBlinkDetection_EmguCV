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
        }
    }
}
