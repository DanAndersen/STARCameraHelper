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
        public GlobalSettings()
        {

        }
        
        private int _OpenCvCameraIndex = 0;

        public int OpenCvCameraIndex
        {
            get
            {
                return _OpenCvCameraIndex;
            }

            set
            {
                _OpenCvCameraIndex = value;
                Debug.WriteLine("Set _OpenCvCameraIndex to " + _OpenCvCameraIndex);
            }
        }

        private int _WebRtcCameraIndex = 0;

        public int WebRtcCameraIndex
        {
            get
            {
                return _WebRtcCameraIndex;
            }

            set
            {
                _WebRtcCameraIndex = value;
                Debug.WriteLine("Set _WebRtcCameraIndex to " + _WebRtcCameraIndex);
            }
        }

        private int _WebRtcDesiredResolutionWidth = 640;

        public int WebRtcDesiredResolutionWidth
        {
            get
            {
                return _WebRtcDesiredResolutionWidth;
            }

            set
            {
                _WebRtcDesiredResolutionWidth = value;
                Debug.WriteLine("Set _WebRtcDesiredResolutionWidth to " + _WebRtcDesiredResolutionWidth);
            }
        }

        private int _WebRtcDesiredResolutionHeight = 480;

        public int WebRtcDesiredResolutionHeight
        {
            get
            {
                return _WebRtcDesiredResolutionHeight;
            }

            set
            {
                _WebRtcDesiredResolutionHeight = value;
                Debug.WriteLine("Set _WebRtcDesiredResolutionHeight to " + _WebRtcDesiredResolutionHeight);
            }
        }

        private int _OpenCvDesiredResolutionWidth = 640;

        public int OpenCvDesiredResolutionWidth
        {
            get
            {
                return _OpenCvDesiredResolutionWidth;
            }

            set
            {
                _OpenCvDesiredResolutionWidth = value;
                Debug.WriteLine("Set _OpenCvDesiredResolutionWidth to " + _OpenCvDesiredResolutionWidth);
            }
        }

        private int _OpenCvDesiredResolutionHeight = 480;

        public int OpenCvDesiredResolutionHeight
        {
            get
            {
                return _OpenCvDesiredResolutionHeight;
            }

            set
            {
                _OpenCvDesiredResolutionHeight = value;
                Debug.WriteLine("Set _OpenCvDesiredResolutionHeight to " + _OpenCvDesiredResolutionHeight);
            }
        }
    }
}
