using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using Windows.UI.Core;
using Windows.Media.Playback;
using Windows.Media.Core;
using WSAUnity;
using System.Diagnostics;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace STARCameraHelper
{

    public static class WebRtcContextHolder
    {
        private static StarWebrtcContext starWebRtcContext;

        public static StarWebrtcContext GetContext()
        {
            return starWebRtcContext;
        }

        public static void SetContext(StarWebrtcContext context)
        {
            starWebRtcContext = context;
        }
    }




    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Scenario2_WebRTC : Page
    {
        private MainPage rootPage;
        
        MediaPlayer _mediaPlayer;



        public Scenario2_WebRTC()
        {
            this.InitializeComponent();

            Debug.WriteLine("MainPage()");
            
            
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            rootPage = MainPage.Current;

            // comment these out if not needed
            //Messenger.AddListener<string>(SympleLog.LogTrace, OnLog);
            //Messenger.AddListener<string>(SympleLog.LogDebug, OnLog);
            Messenger.AddListener<string>(SympleLog.LogInfo, OnLog);
            Messenger.AddListener<string>(SympleLog.LogError, OnLog);

            Messenger.AddListener<IMediaSource>(SympleLog.CreatedMediaSource, OnCreatedMediaSource);
            Messenger.AddListener(SympleLog.DestroyedMediaSource, OnDestroyedMediaSource);

            initWebrtcButton.IsEnabled = true;
            teardownButton.IsEnabled = false;
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs args)
        {
            Debug.WriteLine("OnNavigatedFrom webrtc page");

            //Messenger.RemoveListener<string>(SympleLog.LogTrace, OnLog);
            //Messenger.RemoveListener<string>(SympleLog.LogDebug, OnLog);
            Messenger.RemoveListener<string>(SympleLog.LogInfo, OnLog);
            Messenger.RemoveListener<string>(SympleLog.LogError, OnLog);

            Messenger.RemoveListener<IMediaSource>(SympleLog.CreatedMediaSource, OnCreatedMediaSource);
            Messenger.RemoveListener(SympleLog.DestroyedMediaSource, OnDestroyedMediaSource);

            teardown();
        }

        private void teardown()
        {
            //mediaPlayerElement.Source = null;
            //mediaPlayerElement.SetMediaPlayer(null);

            mediaPlayerElementContainer.Children.Clear();

            if (_mediaPlayer != null)
            {
                _mediaPlayer.Pause();
                //_mediaPlayer.Source = null;
                //_mediaPlayer.Dispose();
                _mediaPlayer = null;
            }

            if (WebRtcContextHolder.GetContext() != null)
            {
                WebRtcContextHolder.GetContext().teardown();
                WebRtcContextHolder.SetContext(null);
            }
        }

        private void OnDestroyedMediaSource()
        {
            Messenger.Broadcast(SympleLog.LogDebug, "OnDestroyedMediaSource");
            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                teardownButton.IsEnabled = false;
                initWebrtcButton.IsEnabled = true;

                teardown();

                /*
                MediaSource currentSource = _mediaPlayer.Source as MediaSource;
                if (currentSource != null)
                {
                    currentSource.Dispose();
                }
                */
                /*
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Source = null;
                }
                */
                
            }
            );

        }

        private void OnCreatedMediaSource(IMediaSource source)
        {
            Messenger.Broadcast(SympleLog.LogDebug, "OnCreatedMediaSource");

            OnLog("about to create from imediasource");

            MediaSource createdSource = MediaSource.CreateFromIMediaSource(source);

            OnLog("createdSource: " + createdSource.ToString() + " " + createdSource.State + " " + createdSource.IsOpen);
            
            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                mediaPlayerElementContainer.Children.Clear();

                MediaPlayerElement mediaPlayerElement = new MediaPlayerElement();
                mediaPlayerElement.AreTransportControlsEnabled = false;

                mediaPlayerElementContainer.Children.Add(mediaPlayerElement);

                _mediaPlayer = new MediaPlayer();
                mediaPlayerElement.SetMediaPlayer(_mediaPlayer);

                _mediaPlayer.Source = createdSource;
                _mediaPlayer.Play();
            }
            );

        }

        private void OnLog(string msg)
        {
            Debug.WriteLine(msg);

            // http://stackoverflow.com/questions/19341591/the-application-called-an-interface-that-was-marshalled-for-a-different-thread

            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                webrtcLogTextBox.Text += msg + "\n";
            }
            );
        }
        
        private async void initWebrtcButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("initWebrtcButton_Click");

            teardownButton.IsEnabled = true;
            initWebrtcButton.IsEnabled = false;

            StarWebrtcContext context = StarWebrtcContext.CreateTraineeContext();

            context = StarWebrtcContext.CreateTraineeContext();
            context.RequestedCameraIndexToTransmit = rootPage.Settings.WebRtcCameraIndex;
            context.RequestedVideoWidth = rootPage.Settings.WebRtcDesiredResolutionWidth;
            context.RequestedVideoHeight = rootPage.Settings.WebRtcDesiredResolutionHeight;
            Debug.WriteLine("NOTE: setting RequestedCameraIndexToTransmit to " + context.RequestedCameraIndexToTransmit);
            Debug.WriteLine("NOTE: setting RequestedVideoWidth to " + context.RequestedVideoWidth);
            Debug.WriteLine("NOTE: setting RequestedVideoHeight to " + context.RequestedVideoHeight);
            // right after creating the context (before starting the connections), we could edit some parameters such as the signalling server

            WebRtcContextHolder.SetContext(context);
            
            try
            {
                Debug.WriteLine("just before initAndStartWebRTC");
                context.initAndStartWebRTC();
            } catch (Exception exception)
            {
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    errorMessageTextBlock.Text = "Caught exception. Please try tearing down and re-attempting. Message: " + exception.Message;
                }
            );
            }
            
        }

        private async void teardownButton_Click(object sender, RoutedEventArgs e)
        {
            teardownButton.IsEnabled = false;
            initWebrtcButton.IsEnabled = true;

            teardown();
        }
    }
}
