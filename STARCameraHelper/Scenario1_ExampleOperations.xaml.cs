using OpenCVBridge;
using System;
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

        private const int IMAGE_ROWS = 480;
        private const int IMAGE_COLS = 640;

        private OpenCVHelper _helper;

        private DispatcherTimer _FPSTimer = null;
        private DispatcherTimer _guiTimer = null;
        
        struct ChessParameters
        {
            public bool isValid;
            public int chessX;
            public int chessY;
            public float squareSizeMeters;
            public int maxInputFrames;
        }

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
            FindChessboardCorners = 0
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

            this.CalibrateIntrinsicsButton.IsEnabled = (numDetectedCorners >= 10);
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
            currentOperation = OperationType.FindChessboardCorners;

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
            var selectedSource = sourceGroups.FirstOrDefault();

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
                    if (currentOperation == OperationType.FindChessboardCorners)
                    {
                        if (_currentChessParameters.isValid)
                        {
                            _helper.DrawChessboard(originalBitmap, outputBitmap, _currentChessParameters.chessX, _currentChessParameters.chessY, _currentChessParameters.squareSizeMeters, SavingDetectedCorners);
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
            if (OperationType.FindChessboardCorners == currentOperation)
            {
                this.CurrentOperationTextBlock.Text = "Current: Find chessboard corners";
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

        private void CalibrateIntrinsicsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentChessParameters.isValid)
            {
                double rms = _helper.CalibrateIntrinsics(_currentChessParameters.maxInputFrames);
                Debug.WriteLine("got rms: " + rms);
            }
        }
    }
}
