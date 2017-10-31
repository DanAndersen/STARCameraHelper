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
    }
}
