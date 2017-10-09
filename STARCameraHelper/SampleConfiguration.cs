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
            new Scenario() { Title="Example Operations", ClassType=typeof(Scenario1_ExampleOperations)},
        };
    }

    public class Scenario
    {
        public string Title { get; set; }
        public Type ClassType { get; set; }
    }
}
