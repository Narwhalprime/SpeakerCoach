using LightBuzz.Vitruvius;
using Microsoft.Kinect;
using System.Windows;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace LightBuzz.Vituvius.Samples.WPF
{
    public partial class AnglePage : Page
    {
        public readonly String RESULT_FILE_PATH = @"C:\Users\Douglass\Desktop\Kinect stuff\Kinect_LightBuzzStrippedTest\Assets\results.txt";

        public AnglePage()
        {
            InitializeComponent();

            // Get the file I/O going
            String output = "";
            try {
                string theResultsText = System.IO.File.ReadAllText(RESULT_FILE_PATH);
                string[] tokens = theResultsText.Split('\n');
                long startTime = Convert.ToInt64(tokens[0]);
                long endTime = Convert.ToInt64(tokens[1]);
                output += "Presentation duration: " + (endTime - startTime) + " seconds\n";
                int ind = 2;
                while(ind < tokens.Length)
                {
                    long flagTime = Convert.ToInt64(tokens[ind]);
                    ind++;
                    String flagType = tokens[ind];
                    ind++;
                    long numSecondsIn = flagTime - startTime;
                    switch(flagType)
                    {
                        case "HeadUp":
                        output += "At " + numSecondsIn + " seconds in, you looked upwards for a bit too long.\n";
                        break;
                        case "HeadDown":
                        output += "At " + numSecondsIn + " seconds in, you looked downwards for a bit too long.\n";
                        break;
                        case "HeadStatic":
                        output += "Starting at around " + numSecondsIn + " seconds in, your eyes should sweep across the audience more often.\n";
                        break;
                        case "Shoulders":
                        output += "At " + numSecondsIn + " seconds in, you either slouched or tilted your shoulders too far - posture is key!\n";
                        break;
                    }
                }
            }
            catch(FileNotFoundException e)
            {
                output = "No results file found! Perform a practice presentation to see results here.";
            }

            tblResultsText.Text = output;
        }
           
        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }
    }
}
