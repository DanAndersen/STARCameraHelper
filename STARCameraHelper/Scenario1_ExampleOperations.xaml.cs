using OpenCVBridge;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using System.Json;
using System.Net.Sockets;

namespace STARCameraHelper
{
    /// <summary>
    /// Scenario that illustrates using OpenCV along with camera frames. 
    /// </summary>
    public sealed partial class Scenario1_ExampleOperations : Page
    {
        private MainPage rootPage;

        private MediaCapture _mediaCapture = null;
        private MediaFrameReader _reader = null;
        private FrameRenderer _previewRenderer = null;
        private FrameRenderer _outputRenderer = null;
        
        private int _frameCount = 0;
        
        private OpenCVHelper _helper;

        private DispatcherTimer _FPSTimer = null;
        private DispatcherTimer _guiTimer = null;

        private bool _isCalibratingIntrinsics = false;

        struct ChessParameters
        {
            public bool isValid;
            public int chessX;
            public int chessY;
            public float squareSizeMeters;
            public int maxInputFrames;
        }
        
        private bool _validIntrinsicCalibrationLoaded = false;
        private IntrinsicCalibration _currentIntrinsicCalibration;
        private bool _validExtrinsicsLoaded = false;
        private PnPResult _currentPnPResult;

        private ChessParameters _currentChessParameters;

        private bool _savingDetectedCorners;
        private bool SavingDetectedCorners
        {
            get
            {
                return _savingDetectedCorners;
            }
            set
            {
                _savingDetectedCorners = value;
                OnChangeCollectingState();
            }
        }

        private void OnChangeCollectingState()
        {
            if (SavingDetectedCorners) {
                ResumeCollectingCornersButton.IsEnabled = false;
                PauseCollectingCornersButton.IsEnabled = true;
            } else
            {
                ResumeCollectingCornersButton.IsEnabled = true;
                PauseCollectingCornersButton.IsEnabled = false;
            }
        }

        enum OperationType
        {
            CollectCornersForCalibration = 0,
            FindCurrentExtrinsics
        }
        OperationType currentOperation;

        public Scenario1_ExampleOperations()
        {
            this.InitializeComponent();

            UpdateChessParameters();

            SavingDetectedCorners = true;

            _previewRenderer = new FrameRenderer(PreviewImage);
            _outputRenderer = new FrameRenderer(OutputImage);

            _helper = new OpenCVHelper();

            _FPSTimer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _FPSTimer.Tick += UpdateFPS;

            _guiTimer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromSeconds(0.1)
            };
            _guiTimer.Tick += UpdateGui;
        }

        private void UpdateGui(object sender, object e)
        {
            int numDetectedCorners = _helper.GetNumDetectedCorners();
            this.CornerStatusTextBlock.Text = "Num corners collected: " + numDetectedCorners;

            this.CalibrateIntrinsicsButton.IsEnabled = (!_isCalibratingIntrinsics && numDetectedCorners >= 10);

            this.SaveIntrinsicsToFileButton.IsEnabled = _validIntrinsicCalibrationLoaded;

            this.SendCalibrationToHoloLensButton.IsEnabled = _validIntrinsicCalibrationLoaded && _validExtrinsicsLoaded;
        }

        private void UpdateChessParameters()
        {
            _currentChessParameters.isValid = false;

            if (!Int32.TryParse(ChessXTextBlock.Text, out _currentChessParameters.chessX))
            {
                Debug.WriteLine("Invalid value for ChessXTextBlock: " + ChessXTextBlock.Text);
                return;
            }

            if (!Int32.TryParse(ChessYTextBlock.Text, out _currentChessParameters.chessY))
            {
                Debug.WriteLine("Invalid value for ChessYTextBlock: " + ChessYTextBlock.Text);
                return;
            }

            if (!float.TryParse(ChessSizeTextBlock.Text, out _currentChessParameters.squareSizeMeters))
            {
                Debug.WriteLine("Invalid value for ChessSizeTextBlock: " + ChessSizeTextBlock.Text);
                return;
            }

            if (!Int32.TryParse(MaxInputFramesTextBlock.Text, out _currentChessParameters.maxInputFrames))
            {
                Debug.WriteLine("Invalid value for MaxInputFramesTextBlock: " + MaxInputFramesTextBlock.Text);
                return;
            }
            
            _currentChessParameters.isValid = true;
        }

        /// <summary>
        /// Initializes the MediaCapture object with the given source group.
        /// </summary>
        /// <param name="sourceGroup">SourceGroup with which to initialize.</param>
        private async Task InitializeMediaCaptureAsync(MediaFrameSourceGroup sourceGroup)
        {
            if (_mediaCapture != null)
            {
                return;
            }

            _mediaCapture = new MediaCapture();
            var settings = new MediaCaptureInitializationSettings()
            {
                SourceGroup = sourceGroup,
                SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                StreamingCaptureMode = StreamingCaptureMode.Video,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu
            };
            await _mediaCapture.InitializeAsync(settings);
        }

        /// <summary>
        /// Unregisters FrameArrived event handlers, stops and disposes frame readers
        /// and disposes the MediaCapture object.
        /// </summary>
        private async Task CleanupMediaCaptureAsync()
        {
            if (_mediaCapture != null)
            {
                await _reader.StopAsync();
                _reader.FrameArrived -= ColorFrameReader_FrameArrivedAsync;
                _reader.Dispose();
                _mediaCapture = null;
            }
        }

        private void UpdateFPS(object sender, object e)
        {
            var frameCount = Interlocked.Exchange(ref _frameCount, 0);
            this.FPSMonitor.Text = "FPS: " + frameCount;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            rootPage = MainPage.Current;

            // setting up the combobox, and default operation
            OperationComboBox.ItemsSource = Enum.GetValues(typeof(OperationType));
            OperationComboBox.SelectedIndex = 0;
            currentOperation = OperationType.CollectCornersForCalibration;

            // Find the sources 
            var allGroups = await MediaFrameSourceGroup.FindAllAsync();
            var sourceGroups = allGroups.Select(g => new
            {
                Group = g,
                SourceInfo = g.SourceInfos.FirstOrDefault(i => i.SourceKind == MediaFrameSourceKind.Color)
            }).Where(g => g.SourceInfo != null).ToList();

            if (sourceGroups.Count == 0)
            {
                // No camera sources found
                return;
            }

            int requestedOpenCvCameraIndex = rootPage.Settings.OpenCvCameraIndex;

            int usedOpenCvCameraIndex = requestedOpenCvCameraIndex;
            if (requestedOpenCvCameraIndex >= sourceGroups.Count)
            {
                Debug.WriteLine("NOTE: requested OpenCV camera index of " + requestedOpenCvCameraIndex + " is out of range of the number of available source groups (" + sourceGroups.Count + "). Resetting to 0.");
                usedOpenCvCameraIndex = 0;
            }
            Debug.WriteLine("Selecting source group with index " + usedOpenCvCameraIndex);
            var selectedSource = sourceGroups[usedOpenCvCameraIndex];


            // Initialize MediaCapture
            try
            {
                await InitializeMediaCaptureAsync(selectedSource.Group);
            }
            catch (Exception exception)
            {
                Debug.WriteLine("MediaCapture initialization error: " + exception.Message);
                await CleanupMediaCaptureAsync();
                return;
            }

            uint IMAGE_ROWS = (uint)rootPage.Settings.OpenCvDesiredResolutionHeight;
            uint IMAGE_COLS = (uint)rootPage.Settings.OpenCvDesiredResolutionWidth;

            // Create the frame reader
            MediaFrameSource frameSource = _mediaCapture.FrameSources[selectedSource.SourceInfo.Id];
            BitmapSize size = new BitmapSize() // Choose a lower resolution to make the image processing more performant
            {
                Height = IMAGE_ROWS,
                Width = IMAGE_COLS
            };
            _reader = await _mediaCapture.CreateFrameReaderAsync(frameSource, MediaEncodingSubtypes.Bgra8, size);
            _reader.FrameArrived += ColorFrameReader_FrameArrivedAsync;
            await _reader.StartAsync();

            _FPSTimer.Start();
            _guiTimer.Start();
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs args)
        {
            _FPSTimer.Stop();
            _guiTimer.Stop();
            await CleanupMediaCaptureAsync();
        }

        /// <summary>
        /// Handles a frame arrived event and renders the frame to the screen.
        /// </summary>
        private void ColorFrameReader_FrameArrivedAsync(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            var frame = sender.TryAcquireLatestFrame();
            if (frame != null)
            {
                SoftwareBitmap originalBitmap = null;
                var inputBitmap = frame.VideoMediaFrame?.SoftwareBitmap;
                if (inputBitmap != null)
                {
                    // The XAML Image control can only display images in BRGA8 format with premultiplied or no alpha
                    // The frame reader as configured in this sample gives BGRA8 with straight alpha, so need to convert it
                    originalBitmap = SoftwareBitmap.Convert(inputBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

                    SoftwareBitmap outputBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, originalBitmap.PixelWidth, originalBitmap.PixelHeight, BitmapAlphaMode.Premultiplied);

                    // Operate on the image in the manner chosen by the user.
                    if (currentOperation == OperationType.CollectCornersForCalibration)
                    {
                        if (_currentChessParameters.isValid)
                        {
                            _helper.DrawChessboard(originalBitmap, outputBitmap, _currentChessParameters.chessX, _currentChessParameters.chessY, _currentChessParameters.squareSizeMeters, SavingDetectedCorners);
                        }
                    } else if (currentOperation == OperationType.FindCurrentExtrinsics)
                    {
                        if (_currentChessParameters.isValid && _validIntrinsicCalibrationLoaded)
                        {
                            _currentPnPResult = _helper.FindExtrinsics(originalBitmap, outputBitmap, _currentChessParameters.chessX, _currentChessParameters.chessY, _currentChessParameters.squareSizeMeters, _currentIntrinsicCalibration);
                            if (_currentPnPResult.success)
                            {
                                _validExtrinsicsLoaded = true;
                                Debug.WriteLine("got extrinsics:");
                                Debug.WriteLine("rvec: " + _currentPnPResult.rvec_0 + " " + _currentPnPResult.rvec_1 + " " + _currentPnPResult.rvec_2);
                                Debug.WriteLine("tvec: " + _currentPnPResult.tvec_0 + " " + _currentPnPResult.tvec_1 + " " + _currentPnPResult.tvec_2);
                            } else
                            {
                                _validExtrinsicsLoaded = false;
                            }
                        }
                    }

                    // Display both the original bitmap and the processed bitmap.
                    _previewRenderer.RenderFrame(originalBitmap);
                    _outputRenderer.RenderFrame(outputBitmap);
                }

                Interlocked.Increment(ref _frameCount);
            }
        }

        private void OperationComboBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            currentOperation = (OperationType)((sender as ComboBox).SelectedItem);
            if (OperationType.CollectCornersForCalibration == currentOperation)
            {
                this.CurrentOperationTextBlock.Text = "Current: Collecting chessboard corners for intrinsic calibration";
            } else if (OperationType.FindCurrentExtrinsics == currentOperation)
            {
                this.CurrentOperationTextBlock.Text = "Current: Finding current extrinsics given loaded intrinsics";
            }
            else
            {
                this.CurrentOperationTextBlock.Text = string.Empty;
            }
        }

        private void ChessParameters_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateChessParameters();
        }

        private void ClearCollectedCornersButton_Click(object sender, RoutedEventArgs e)
        {
            _helper.ClearDetectedCorners();
        }

        private void PauseCollectingCornersButton_Click(object sender, RoutedEventArgs e)
        {
            SavingDetectedCorners = false;
        }

        private void ResumeCollectingCornersButton_Click(object sender, RoutedEventArgs e)
        {
            SavingDetectedCorners = true;
        }

        private async void CalibrateIntrinsicsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentChessParameters.isValid)
            {
                var originalButtonLabel = CalibrateIntrinsicsButton.Content;

                CalibrateIntrinsicsButton.Content = "Please wait, calibrating...";
                
                _isCalibratingIntrinsics = true;
                OpenCVBridge.IntrinsicCalibration calibration = await Task.Run(() => _helper.CalibrateIntrinsics(_currentChessParameters.maxInputFrames));
                _isCalibratingIntrinsics = false;

                Debug.WriteLine("calibration:");
                Debug.WriteLine("width: " + calibration.width);
                Debug.WriteLine("height: " + calibration.height);

                Debug.WriteLine("fx: " + calibration.fx);
                Debug.WriteLine("fy: " + calibration.fy);
                Debug.WriteLine("cx: " + calibration.cx);
                Debug.WriteLine("cy: " + calibration.cy);

                Debug.WriteLine("k1: " + calibration.k1);
                Debug.WriteLine("k2: " + calibration.k2);
                Debug.WriteLine("p1: " + calibration.p1);
                Debug.WriteLine("p2: " + calibration.p2);
                Debug.WriteLine("k3: " + calibration.k3);

                Debug.WriteLine("rms: " + calibration.rms);

                _currentIntrinsicCalibration = calibration;
                _validIntrinsicCalibrationLoaded = true;

                _validExtrinsicsLoaded = false;

                CalibrateIntrinsicsButton.Content = originalButtonLabel;
            }
        }

        private JsonObject IntrinsicsAndExtrinsicsToJson(IntrinsicCalibration calib, PnPResult pnpresult, ChessParameters chessParameters)
        {
            JsonObject obj = IntrinsicsToJson(calib);

            JsonArray rvecArray = new JsonArray();
            rvecArray.Add(pnpresult.rvec_0);
            rvecArray.Add(pnpresult.rvec_1);
            rvecArray.Add(pnpresult.rvec_2);

            JsonArray tvecArray = new JsonArray();
            tvecArray.Add(pnpresult.tvec_0);
            tvecArray.Add(pnpresult.tvec_1);
            tvecArray.Add(pnpresult.tvec_2);

            obj["rvec"] = rvecArray;
            obj["tvec"] = tvecArray;

            obj["chess_x"] = chessParameters.chessX;
            obj["chess_y"] = chessParameters.chessY;
            obj["chess_square_size_meters"] = chessParameters.squareSizeMeters;

            return obj;
        }

        private JsonObject IntrinsicsToJson(IntrinsicCalibration calib)
        {
            JsonObject obj = new JsonObject();
            obj["width"] = calib.width;
            obj["height"] = calib.height;

            obj["fx"] = calib.fx;
            obj["fy"] = calib.fy;
            obj["cx"] = calib.cx;
            obj["cy"] = calib.cy;

            obj["k1"] = calib.k1;
            obj["k2"] = calib.k2;
            obj["p1"] = calib.p1;
            obj["p2"] = calib.p2;
            obj["k3"] = calib.k3;

            obj["rms"] = calib.rms;

            return obj;
        }

        private async void SaveIntrinsicsToFileButton_Click(object sender, RoutedEventArgs e)
        {
            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;

            savePicker.FileTypeChoices.Add("JSON Text", new List<string>() { ".json" });

            savePicker.SuggestedFileName = "SavedIntrinsicCalibration";

            Windows.Storage.StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                // Prevent updates to remote version of the file until we finish making changes and call CompleteUpdatesAsync.
                Windows.Storage.CachedFileManager.DeferUpdates(file);
                // write to file


                JsonObject calibJson = IntrinsicsToJson(_currentIntrinsicCalibration);
                
                await Windows.Storage.FileIO.WriteTextAsync(file, calibJson.ToString());
                // Let Windows know we're finished changing the file, so another app can update the remote version of the file.
                Windows.Storage.Provider.FileUpdateStatus status = await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(file);
                if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                {
                    Debug.WriteLine("File " + file.Path + " was saved.");
                } else
                {
                    Debug.WriteLine("File " + file.Path + " couldn't be saved.");
                }
            } else
            {
                Debug.WriteLine("Saving intrinsics canceled.");
            }
        }

        private async void LoadIntrinsicsFromFileButton_Click(object sender, RoutedEventArgs e)
        {
            var openPicker = new Windows.Storage.Pickers.FileOpenPicker();
            openPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;

            openPicker.FileTypeFilter.Add(".json");

            Windows.Storage.StorageFile file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                string inputString = await Windows.Storage.FileIO.ReadTextAsync(file);

                var obj = JsonValue.Parse(inputString);

                IntrinsicCalibration calib;

                calib.width = obj["width"];
                calib.height = obj["height"];

                calib.fx = obj["fx"];
                calib.fy = obj["fy"];
                calib.cx = obj["cx"];
                calib.cy = obj["cy"];

                calib.k1 = obj["k1"];
                calib.k2 = obj["k2"];
                calib.p1 = obj["p1"];
                calib.p2 = obj["p2"];
                calib.k3 = obj["k3"];

                calib.rms = obj["rms"];

                _currentIntrinsicCalibration = calib;
                _validIntrinsicCalibrationLoaded = true;

                _validExtrinsicsLoaded = false;
            } else
            {
                Debug.WriteLine("Loading intrinsics canceled.");
            }
        }

        private void SendCalibrationToHoloLensButton_Click(object sender, RoutedEventArgs e)
        {
            JsonObject objToSend = IntrinsicsAndExtrinsicsToJson(_currentIntrinsicCalibration, _currentPnPResult, _currentChessParameters);

            Debug.WriteLine("TODO: send the object: " + objToSend.ToString());

            int holoPort;

            string holoAddress = HoloLensAddressTextBlock.Text;

            if (int.TryParse(HoloLensPortTextBlock.Text, out holoPort))
            {
                SendStringToTcpServer(holoAddress, holoPort, objToSend.ToString());
            }
        }

        private async void SendStringToTcpServer(string address, int port, string msg)
        {
            TcpClient client = null;
            NetworkStream stream = null;

            try
            {
                client = new TcpClient();

                Debug.WriteLine(String.Format("Connecting to {0}:{1}...", address, port));
                await client.ConnectAsync(address, port);

                Byte[] data = System.Text.Encoding.UTF8.GetBytes(msg);

                stream = client.GetStream();

                stream.Write(data, 0, data.Length);

                Debug.WriteLine(String.Format("Sent: {0}", msg));
            }
            finally
            {
                if (stream != null)
                {
                    stream.Dispose();
                }
                if (client != null)
                {
                    client.Dispose();
                }
            }
        }
    }
}
