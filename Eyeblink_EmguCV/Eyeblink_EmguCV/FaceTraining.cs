using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using System.IO;
using System.Diagnostics;


namespace Eyeblink_EmguCV
{
    public partial class FaceTraining : Form
    {
        //Declararation of all variables, vectors and haarcascades
        Image<Bgr, Byte> currentFrame;
        Capture grabber;
        HaarCascade face;
        HaarCascade eye;

        MCvFont font = new MCvFont(FONT.CV_FONT_HERSHEY_TRIPLEX, 0.5d, 0.5d);
        Image<Gray, byte> result, TrainedFace = null;
        Image<Gray, byte> gray = null;
        List<Image<Gray, byte>> trainingImages = new List<Image<Gray, byte>>();
        List<string> labels = new List<string>();
        List<string> NamePersons = new List<string>();
        int ContTrain, t = 0;
        string name, names = null;

        public FaceTraining()
        {
            InitializeComponent();

            //얼굴 검출을 위한 Haarcascade Load
            face = new HaarCascade("haarcascade_frontalface_default.xml");
            try
            {
                //파일에 있는 Training Image 및 label load.
                string Labelsinfo = File.ReadAllText(Application.StartupPath + "/TrainedFaces/TrainedLabels.txt");
                string[] Labels = Labelsinfo.Split('%');
                ContTrain = Convert.ToInt16(Labels[0]);

                string LoadFaces;

                // 파일에 있는 TrainingImage List에 저장
                for (int tf = 1; tf < ContTrain + 1; tf++)
                {
                    LoadFaces = "face" + tf + ".bmp";
                    trainingImages.Add(new Image<Gray, byte>(Application.StartupPath + "/TrainedFaces/" + LoadFaces));
                    labels.Add(Labels[tf]);
                }


            }

            catch (Exception e)
            {
                //MessageBox.Show(e.ToString());
                MessageBox.Show("Nothing in binary database, please add at least a face(Simply train the prototype with the Add Face Button).", "Triained faces load", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void button1_Click(object sender, EventArgs eㅅ)
        {
            //Capture Device 초기화
            grabber = new Capture();
            grabber.QueryFrame();

            //the FrameGraber event 초기화
            Application.Idle += new EventHandler(FrameGrabber);
            button1.Enabled = false;
        }

        private void button2_Click(object sender, System.EventArgs e)
        {
            try
            {
                //카운터 증가
                ContTrain = ContTrain + 1;

                //현재 CaptureFrame 320X240 사이즈로 저장
                gray = grabber.QueryGrayFrame().Resize(320, 240, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);

                //얼굴 검출 시작
                MCvAvgComp[][] facesDetected = gray.DetectHaarCascade(face, 1.1, 0, Emgu.CV.CvEnum.HAAR_DETECTION_TYPE.FIND_BIGGEST_OBJECT, new Size(20, 20));

                //검출한 이미지 100X100으로 사이즈 재조정 및 TrainingImage List에 저장
                TrainedFace = result.Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
                trainingImages.Add(TrainedFace);
                labels.Add(textBox1.Text);

                //등록한 얼굴을 gray형태로 imageBox1에 투영
                imageBox1.Image = TrainedFace;

                //등록한 얼굴 수 TrainedLabels.txt에 저장 --> WriteAllText를 통해 File 존재시 덮어씀
                File.WriteAllText(Application.StartupPath + "/TrainedFaces/TrainedLabels.txt", trainingImages.ToArray().Length.ToString() + "%");

                //등록한 얼굴 bmp 파일로 저장 및 이름 TrainedLabels.txt에 저장
                for (int i = 1; i < trainingImages.ToArray().Length + 1; i++)
                {
                    trainingImages.ToArray()[i - 1].Save(Application.StartupPath + "/TrainedFaces/face" + i + ".bmp");
                    File.AppendAllText(Application.StartupPath + "/TrainedFaces/TrainedLabels.txt", labels.ToArray()[i - 1] + "%");
                }

                MessageBox.Show(textBox1.Text + "´s face detected and added :)", "Training OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch
            {
                MessageBox.Show("Enable the face detection first", "Training Fail", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }


        void FrameGrabber(object sender, EventArgs e)
        {
            label3.Text = "0";
            NamePersons.Add("");

            //현재 Frame을 Capture Device를 통해 얻기       
            currentFrame = grabber.QueryFrame().Resize(320, 240, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);

            //Grayscale로 현재 Frame 바꾼것 gray 변수에 저장
            gray = currentFrame.Convert<Gray, Byte>();

            //Face Detect
            MCvAvgComp[][] facesDetected = gray.DetectHaarCascade(face, 1.2, 10, Emgu.CV.CvEnum.HAAR_DETECTION_TYPE.DO_CANNY_PRUNING, new Size(20, 20));

            //Detect한 얼굴들(elements) 작업
            foreach (MCvAvgComp f in facesDetected[0])
            {
                //검출할 때마다 t값 증가 ( t= 사람 수 )
                t = t + 1;
                //result 변수에 현재 잡힌 얼굴 저장.( 얼굴 training 등록할 때 쓰임 )
                result = currentFrame.Copy(f.rect).Convert<Gray, byte>().Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
                //잡힌 얼굴 그리기
                currentFrame.Draw(f.rect, new Bgr(Color.Red), 2);

                // 등록한 trainingImages가 0이 아닐 경우
                if (trainingImages.ToArray().Length != 0)
                {
                    //TermCriteria for face recognition with numbers of trained images like maxIteration
                    MCvTermCriteria termCrit = new MCvTermCriteria(ContTrain, 0.001);

                    //Eigen face recognizer
                    EigenObjectRecognizer recognizer = new EigenObjectRecognizer(trainingImages.ToArray(), labels.ToArray(), 3000, ref termCrit);

                    name = recognizer.Recognize(result);

                    //Draw the label for each face detected and recognized
                    currentFrame.Draw(name, ref font, new Point(f.rect.X - 2, f.rect.Y - 2), new Bgr(Color.LightGreen));

                }

                NamePersons[t - 1] = name;
                NamePersons.Add("");


                //Set the number of faces detected on the scene
                label3.Text = facesDetected[0].Length.ToString();
            }
            t = 0;

            //Names concatenation of persons recognized
            for (int nnn = 0; nnn < facesDetected[0].Length; nnn++)
            {
                names = names + NamePersons[nnn] + ", ";
            }
            //Show the faces procesed and recognized
            imageBoxFrameGrabber.Image = currentFrame;
            label4.Text = names;
            names = "";
            //Clear the list(vector) of names
            NamePersons.Clear();

        }

        private void button3_Click(object sender, EventArgs e)
        {
            Process.Start("Donate.html");
        }

        private void FrmPrincipal_Load(object sender, EventArgs e)
        {

        }


        //private void button1_Click(object sender, EventArgs e)
        //{

        //    Form1 frm = new Form1();
        //    frm.Show();

        //}
    }
}
