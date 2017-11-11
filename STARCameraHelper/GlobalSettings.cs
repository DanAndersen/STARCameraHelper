using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STARCameraHelper
{
    public class GlobalSettings
    {
        Windows.Storage.ApplicationDataContainer _localSettings;

        public GlobalSettings()
        {
            _localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

            Windows.Storage.StorageFolder localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;

            Debug.WriteLine("local folder for settings: " + localFolder.Path);
        }
        

        private object getValue(string key, object defaultValue)
        {
            object retval;
            if (!_localSettings.Values.TryGetValue(key, out retval))
            {
                retval = defaultValue;
                _localSettings.Values[key] = defaultValue;
            }
            return retval;
        }

        private void setValue(string key, object newValue)
        {
            _localSettings.Values[key] = newValue;
        }

        public int OpenCvCameraIndex
        {
            get { return (int)getValue("OpenCvCameraIndex", 0); }
            set { setValue("OpenCvCameraIndex", value); }
        }

        public int WebRtcCameraIndex
        {
            get { return (int)getValue("WebRtcCameraIndex", 0); }
            set { setValue("WebRtcCameraIndex", value); }
        }

        public int WebRtcDesiredResolutionWidth
        {
            get { return (int)getValue("WebRtcDesiredResolutionWidth", 1920); }
            set { setValue("WebRtcDesiredResolutionWidth", value); }
        }

        public int WebRtcDesiredResolutionHeight
        {
            get { return (int)getValue("WebRtcDesiredResolutionHeight", 1080); }
            set { setValue("WebRtcDesiredResolutionHeight", value); }
        }

        public int OpenCvDesiredResolutionWidth
        {
            get { return (int)getValue("OpenCvDesiredResolutionWidth", 1920); }
            set { setValue("OpenCvDesiredResolutionWidth", value); }
        }

        public int OpenCvDesiredResolutionHeight
        {
            get { return (int)getValue("OpenCvDesiredResolutionHeight", 1080); }
            set { setValue("OpenCvDesiredResolutionHeight", value); }
        }

        public int ChessX
        {
            get { return (int)getValue("ChessX", 5); }
            set { setValue("ChessX", value); }
        }

        public int ChessY
        {
            get { return (int)getValue("ChessY", 7); }
            set { setValue("ChessY", value); }
        }

        public float ChessSquareSize
        {
            get { return (float)getValue("ChessSquareSize", 0.03f); }
            set { setValue("ChessSquareSize", value); }
        }

        public int MaxInputFramesCalibration
        {
            get { return (int)getValue("MaxInputFramesCalibration", 30); }
            set { setValue("MaxInputFramesCalibration", value); }
        }

        public string HoloLensAddress
        {
            get { return (string)getValue("HoloLensAddress", "127.0.0.1"); }
            set { setValue("HoloLensAddress", value); }
        }

        public int HoloLensPort
        {
            get { return (int)getValue("HoloLensPort", 4434); }
            set { setValue("HoloLensPort", value); }
        }

    }
}
