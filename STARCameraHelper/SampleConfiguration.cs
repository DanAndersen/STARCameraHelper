using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace STARCameraHelper
{
    public partial class MainPage : Page
    {
        public const string FEATURE_NAME = "STAR Camera Helper";

        List<Scenario> scenarios = new List<Scenario>
        {
            new Scenario() { Title="Setup", ClassType=typeof(Scenario0_Setup)},
            new Scenario() { Title="Camera Calibration", ClassType=typeof(Scenario1_ExampleOperations)},
            new Scenario() { Title="WebRTC Streaming", ClassType=typeof(Scenario2_WebRTC)}
        };
    }

    public class Scenario
    {
        public string Title { get; set; }
        public Type ClassType { get; set; }
    }
}
