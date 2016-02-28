﻿// By Douglass Chen
// WPF initial sample from Vitrivius examples

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
        private enum FaceState
        {
            None, KinectWait, FaceWait,
            NeutralWait, UpTiltWait, DownTiltWait, LeftTiltWait, RightTiltWait,
            Presentation, Evaluation
        };
        private FaceState currFaceState = FaceState.None;
        private enum BodyState
        {
            None, KinectWait, BodyWait,
            FrontWait, Ready
        };
        private BodyState currBodyState = BodyState.None;

        // temporary tracker variables while in setup phase
        private readonly int NUM_SAMPLES = 50;
        private int sampleInd = 0;
        private double[] faceSamples;

        // posture-related
        JointType sholL = JointType.ShoulderLeft;
        JointType spMid = JointType.SpineMid;
        JointType sholR = JointType.ShoulderRight;
        private double currRotation = 0.0;
        private readonly double FRONT_FACING_THRESHOLD = 0.15; // TODO: modify based on distance from camera?

        public FacePage()
        {
            InitializeComponent();

            currFaceState = FaceState.KinectWait;
            currBodyState = BodyState.KinectWait;
            faceSamples = new double[NUM_SAMPLES];

            _sensor = KinectSensor.GetDefault();
            currentTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

            if (_sensor != null)
            {
                currFaceState = FaceState.FaceWait;
                currBodyState = BodyState.BodyWait;
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

        // Free up the resources upon leaving this screen
        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            currFaceState = FaceState.None;
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

            switch(currFaceState)
            {
                case FaceState.NeutralWait:
                    yTilts[0] = average;
                    xTilts[0] = average;
                    currFaceState = FaceState.UpTiltWait;
                    break;
                case FaceState.UpTiltWait:
                    yTilts[1] = average;
                    currFaceState = FaceState.DownTiltWait;
                    break;
                case FaceState.DownTiltWait:
                    yTilts[2] = average;
                    currFaceState = FaceState.LeftTiltWait;
                    break;
                case FaceState.LeftTiltWait:
                    xTilts[1] = average;
                    currFaceState = FaceState.RightTiltWait;
                    break;
                case FaceState.RightTiltWait:
                    xTilts[2] = average;
                    currFaceState = FaceState.Presentation;
                    break;
                case FaceState.Presentation:
                    currFaceState = FaceState.Evaluation;
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

                    /* ADDED BY DOUG */
                    // If we have a body, check the joints and their positions/angles
                    if (body != null)
                    {
                        currBodyState = BodyState.FrontWait;
                        IReadOnlyDictionary<JointType, Joint> joints = body.Joints;
                       
                        frameCount++;
                        if (frameCount >= FRAME_INTERVAL)
                        {
                            currRotation = Math.Round(Math.Abs(joints[sholR].Position.Z - joints[sholL].Position.Z), 4);
                            tblFeedback.Text = "Shoulder Z-coordinate difference: \n"
                                + currRotation;
                            frameCount = 0;
                        }
                        
                        if (!_faceSource.IsTrackingIdValid)
                        {
                            _faceSource.TrackingId = body.TrackingId;
                        }

                        if (currRotation <= FRONT_FACING_THRESHOLD)
                        {
                            currBodyState = BodyState.Ready;
                        }
                        else
                        {
                            currBodyState = BodyState.FrontWait;
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
                if (frame != null && frame.IsFaceTracked && currBodyState == BodyState.Ready)
                {
                    // Prepare to enter the setup chain in the state diagram
                    if (currFaceState == FaceState.FaceWait)
                        currFaceState = FaceState.NeutralWait;

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
                    switch(currFaceState)
                    {
                        case FaceState.NeutralWait:
                            output += "Step 2/6: Position your head in neutral forward-facing position.\n"
                            + "Hold for at least " + RECOMMEND_TIME + " seconds, then press OK.\n";
                            faceSamples[sampleInd] = Math.Round(face.Chin.Z - face.Forehead.Z, 4);
                            break;
                        case FaceState.UpTiltWait:
                            output += "Step 3/6: Now tilt your head about 45 degress upwards.\n"
                            + "Hold for at least " + RECOMMEND_TIME + " seconds, then press OK.\n";
                            faceSamples[sampleInd] = Math.Round(face.Chin.Z - face.Forehead.Z, 4);
                            break;
                        case FaceState.DownTiltWait:
                            output += "Step 4/6: Now tilt your head about 45 degress downwards.\n"
                            + "Hold for at least " + RECOMMEND_TIME + " seconds, then press OK.\n";
                            faceSamples[sampleInd] = Math.Round(face.Chin.Z - face.Forehead.Z, 4);
                            break;
                        case FaceState.LeftTiltWait:
                            output += "Step 5/6: Turn your head 45 degress diagonally 45 degrees to the left.\n"
                            + "Hold for at least " + RECOMMEND_TIME + " seconds, then press OK.\n";
                            faceSamples[sampleInd] = Math.Round(face.CheekRight.Z - face.CheekLeft.Z, 4);
                            break;
                        case FaceState.RightTiltWait:
                            output += "Step 6/6: Now turn your head 45 degress diagonally 45 degrees to the right.\n"
                            + "Hold for at least " + RECOMMEND_TIME + " seconds, then press OK.\n";
                            faceSamples[sampleInd] = Math.Round(face.CheekRight.Z - face.CheekLeft.Z, 4);
                            break;
                        case FaceState.Presentation:
                            output += "yTilts = { " + yTilts[0] + ", " + yTilts[1] + ", " + yTilts[2] + " }\n"
                                + "xTilts = { " + xTilts[0] + ", " + xTilts[1] + ", " + xTilts[2] + " }\n";
                            break;
                    }
                    // Record samples for the setup parameters
                    sampleInd = (sampleInd + 1) % NUM_SAMPLES;
                    output += sampleInd;
                }
                else
                {
                    output = "Step 1/6: Stand up, face as squarely with the Kinect as you can, then wait for camera to find your face.";
                }

                tblFaceStatus.Text = output; // Show message in window
            }
        }
    }
}
