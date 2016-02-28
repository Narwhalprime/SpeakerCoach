using LightBuzz.Vitruvius;
using Microsoft.Kinect;
using Microsoft.Kinect.Face;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LightBuzz.Vituvius.Samples.WPF
{
    public partial class FacePage : Page
    {
        private KinectSensor _sensor = null;
        private InfraredFrameSource _infraredSource = null;
        private InfraredFrameReader _infraredReader = null;
        private BodyFrameSource _bodySource = null;
        private BodyFrameReader _bodyReader = null;
        private HighDefinitionFaceFrameSource _faceSource = null;
        private HighDefinitionFaceFrameReader _faceReader = null;
        
        private List<Ellipse> _ellipses = new List<Ellipse>();

        private readonly int FRAME_INTERVAL = 10; // for testing
        private readonly int RECOMMEND_TIME = 3; // how long should user position head
        private int frameCount = 0;
        private long currentTime = 0;

        /* ADDED BY DOUG */
        // Settings about the user's neutral/looking away positions
        private double[] yTilts = new double[3]; // neutral, left, right; forehead/chin points
        private double[] xTilts = new double[3]; // neutral, up, down; cheek points
        private enum PracticeState {None, KinectWait, FaceWait, NeutralWait,
        UpTiltWait, DownTiltWait, LeftTiltWait, RightTiltWait, Presentation, Evaluation};
        private PracticeState currentState = PracticeState.None;

        // temporary tracker variables while in setup phase
        private readonly int NUM_SAMPLES = 50;
        private int sampleInd = 0;
        private double[] faceSamples;

        public FacePage()
        {
            InitializeComponent();

            currentState = PracticeState.KinectWait;
            faceSamples = new double[NUM_SAMPLES];

            _sensor = KinectSensor.GetDefault();
            currentTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

            if (_sensor != null)
            {
                currentState = PracticeState.FaceWait;
                _infraredSource = _sensor.InfraredFrameSource;
                _infraredReader = _infraredSource.OpenReader();
                _infraredReader.FrameArrived += InfraredReader_FrameArrived;

                _bodySource = _sensor.BodyFrameSource;
                _bodyReader = _bodySource.OpenReader();
                _bodyReader.FrameArrived += BodyReader_FrameArrived;

                _faceSource = new HighDefinitionFaceFrameSource(_sensor);
                _faceReader = _faceSource.OpenReader();
                _faceReader.FrameArrived += FaceReader_FrameArrived;

                _sensor.Open();
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            currentState = PracticeState.None;
            if (_faceReader != null)
            {
                _faceReader.Dispose();
            }

            if (_bodyReader != null)
            {
                _bodyReader.Dispose();
            }

            if (_infraredReader != null)
            {
                _infraredReader.Dispose();
            }

            if (_sensor != null)
            {
                _sensor.Close();
            }

            GC.SuppressFinalize(this);
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }

        private void Okay_Click(object sender, RoutedEventArgs e)
        {
            // Record the setup parameters based on the samples
            double average = 0;
            foreach(double d in faceSamples)
                average += d;
            average /= NUM_SAMPLES;

            switch(currentState)
            {
                case PracticeState.NeutralWait:
                    yTilts[0] = average;
                    xTilts[0] = average;
                    currentState = PracticeState.UpTiltWait;
                    break;
                case PracticeState.UpTiltWait:
                    yTilts[1] = average;
                    currentState = PracticeState.DownTiltWait;
                    break;
                case PracticeState.DownTiltWait:
                    yTilts[2] = average;
                    currentState = PracticeState.LeftTiltWait;
                    break;
                case PracticeState.LeftTiltWait:
                    xTilts[1] = average;
                    currentState = PracticeState.RightTiltWait;
                    break;
                case PracticeState.RightTiltWait:
                    xTilts[2] = average;
                    currentState = PracticeState.Presentation;
                    break;
                case PracticeState.Presentation:
                    currentState = PracticeState.Evaluation;
                    break;
            }
        }

        private void InfraredReader_FrameArrived(object sender, InfraredFrameArrivedEventArgs args)
        {
            using (var frame = args.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    camera.Source = frame.ToBitmap();
                }
            }
        }

        private void BodyReader_FrameArrived(object sender, BodyFrameArrivedEventArgs args)
        {
            using (var frame = args.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    Body body = frame.Bodies().Closest();

                    if (!_faceSource.IsTrackingIdValid)
                    {
                        if (body != null)
                        {
                            _faceSource.TrackingId = body.TrackingId;
                        }
                    }
                }
            }
        }

        private void FaceReader_FrameArrived(object sender, HighDefinitionFaceFrameArrivedEventArgs args)
        {
            using (var frame = args.FrameReference.AcquireFrame())
            {
                string output = "";
                if (frame != null && frame.IsFaceTracked)
                {
                    // Prepare to enter the setup chain in the state diagram
                    if (currentState == PracticeState.FaceWait)
                        currentState = PracticeState.NeutralWait;

                    // Display basic points only.
                    Face face = frame.Face();

                    Point pointEyeLeft = face.EyeLeft.ToPoint(Visualization.Infrared);
                    Point pointEyeRight = face.EyeRight.ToPoint(Visualization.Infrared);
                    Point pointCheekLeft = face.CheekLeft.ToPoint(Visualization.Infrared);
                    Point pointCheekRight = face.CheekRight.ToPoint(Visualization.Infrared);
                    Point pointNose = face.Nose.ToPoint(Visualization.Infrared);
                    Point pointMouth = face.Mouth.ToPoint(Visualization.Infrared);
                    Point pointChin = face.Chin.ToPoint(Visualization.Infrared);
                    Point pointForehead = face.Forehead.ToPoint(Visualization.Infrared);

                    Canvas.SetLeft(eyeLeft, pointEyeLeft.X - eyeLeft.Width / 2.0);
                    Canvas.SetTop(eyeLeft, pointEyeLeft.Y - eyeLeft.Height / 2.0);

                    Canvas.SetLeft(eyeRight, pointEyeRight.X - eyeRight.Width / 2.0);
                    Canvas.SetTop(eyeRight, pointEyeRight.Y - eyeRight.Height / 2.0);

                    Canvas.SetLeft(cheekLeft, pointCheekLeft.X - cheekLeft.Width / 2.0);
                    Canvas.SetTop(cheekLeft, pointCheekLeft.Y - cheekLeft.Height / 2.0);

                    Canvas.SetLeft(cheekRight, pointCheekRight.X - cheekRight.Width / 2.0);
                    Canvas.SetTop(cheekRight, pointCheekRight.Y - cheekRight.Height / 2.0);

                    Canvas.SetLeft(nose, pointNose.X - nose.Width / 2.0);
                    Canvas.SetTop(nose, pointNose.Y - nose.Height / 2.0);

                    Canvas.SetLeft(mouth, pointMouth.X - mouth.Width / 2.0);
                    Canvas.SetTop(mouth, pointMouth.Y - mouth.Height / 2.0);

                    Canvas.SetLeft(chin, pointChin.X - chin.Width / 2.0);
                    Canvas.SetTop(chin, pointChin.Y - chin.Height / 2.0);

                    Canvas.SetLeft(forehead, pointForehead.X - forehead.Width / 2.0);
                    Canvas.SetTop(forehead, pointForehead.Y - forehead.Height / 2.0);

                    /* ADDED BY DOUG */
                    // State handling to determine what to print to message area
                    switch(currentState)
                    {
                        case PracticeState.NeutralWait:
                            output += "Step 2/6: Position your head in neutral forward-facing position.\n"
                            + "Hold for at least + " + RECOMMEND_TIME + " seconds, then press OK.\n";
                            faceSamples[sampleInd] = Math.Round(face.Chin.Z - face.Forehead.Z, 4);
                            break;
                        case PracticeState.UpTiltWait:
                            output += "Step 3/6: Now tilt your head about 45 degress upwards.\n"
                            + "Hold for at least + " + RECOMMEND_TIME + " seconds, then press OK.\n";
                            faceSamples[sampleInd] = Math.Round(face.Chin.Z - face.Forehead.Z, 4);
                            break;
                        case PracticeState.DownTiltWait:
                            output += "Step 4/6: Now tilt your head about 45 degress downwards.\n"
                            + "Hold for at least + " + RECOMMEND_TIME + " seconds, then press OK.\n";
                            faceSamples[sampleInd] = Math.Round(face.Chin.Z - face.Forehead.Z, 4);
                            break;
                        case PracticeState.LeftTiltWait:
                            output += "Step 5/6: Turn your head 45 degress diagonally 45 degrees to the left.\n"
                            + "Hold for at least + " + RECOMMEND_TIME + " seconds, then press OK.\n";
                            faceSamples[sampleInd] = Math.Round(face.CheekRight.Z - face.CheekLeft.Z, 4);
                            break;
                        case PracticeState.RightTiltWait:
                            output += "Step 6/6: Now turn your head 45 degress diagonally 45 degrees to the right.\n"
                            + "Hold for at least + " + RECOMMEND_TIME + " seconds, then press OK.\n";
                            faceSamples[sampleInd] = Math.Round(face.CheekRight.Z - face.CheekLeft.Z, 4);
                            break;
                        case PracticeState.Presentation:
                            output += "yTilts = { " + yTilts[0] + ", " + yTilts[1] + ", " + yTilts[2] + " }\n"
                                + "xTilts = { " + xTilts[0] + ", " + xTilts[1] + ", " + xTilts[2] + " }\n";
                            break;
                    }
                    // Record samples for the setup parameters
                    sampleInd = (sampleInd + 1) % NUM_SAMPLES;

                    // Track positions of certain interesting face points
                    frameCount++;
                    if (frameCount >= FRAME_INTERVAL)
                    {
                        output += "Forehead-chin diffs: ("
                            + Math.Round(face.Chin.X - face.Forehead.X, 4) + ", "
                            + Math.Round(face.Chin.Y - face.Forehead.Y, 4) + ", "
                            + Math.Round(face.Chin.Z - face.Forehead.Z, 4)
                            + ")";
                        frameCount = 0;
                    }

                    // TODO: for test
                    output += sampleInd;
                }
                else
                {
                    output = "Step 1/6: Stand in front of the Kinect camera.";
                }
                // Show message in window
                tblFaceStatus.Text = output;
            }
        }
    }
}
