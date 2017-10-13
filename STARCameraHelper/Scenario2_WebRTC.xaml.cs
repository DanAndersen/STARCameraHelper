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
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Scenario2_WebRTC : Page
    {
        StarWebrtcContext starWebrtcContext;
        MediaPlayer _mediaPlayer;

        public Scenario2_WebRTC()
        {
            this.InitializeComponent();

            Debug.WriteLine("MainPage()");

            _mediaPlayer = new MediaPlayer();
            mediaPlayerElement.SetMediaPlayer(_mediaPlayer);

            starWebrtcContext = StarWebrtcContext.CreateTraineeContext();
            // right after creating the context (before starting the connections), we could edit some parameters such as the signalling server

            // comment these out if not needed
            //Messenger.AddListener<string>(SympleLog.LogTrace, OnLog);
            Messenger.AddListener<string>(SympleLog.LogDebug, OnLog);
            Messenger.AddListener<string>(SympleLog.LogInfo, OnLog);
            Messenger.AddListener<string>(SympleLog.LogError, OnLog);

            Messenger.AddListener<IMediaSource>(SympleLog.CreatedMediaSource, OnCreatedMediaSource);
            Messenger.AddListener(SympleLog.DestroyedMediaSource, OnDestroyedMediaSource);

            initWebrtcButton.IsEnabled = true;
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs args)
        {
            Debug.WriteLine("OnNavigatedFrom webrtc page");
        }

        private void OnDestroyedMediaSource()
        {
            Messenger.Broadcast(SympleLog.LogDebug, "OnDestroyedMediaSource");
            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
            /*
            MediaSource currentSource = _mediaPlayer.Source as MediaSource;
            if (currentSource != null)
            {
                currentSource.Dispose();
            }
            */

                _mediaPlayer.Source = null;
            }
            );

        }

        private void OnCreatedMediaSource(IMediaSource source)
        {
            Messenger.Broadcast(SympleLog.LogDebug, "OnCreatedMediaSource");
            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                OnLog("about to create from imediasource");

                MediaSource createdSource = MediaSource.CreateFromIMediaSource(source);

                OnLog("createdSource: " + createdSource.ToString() + " " + createdSource.State + " " + createdSource.IsOpen);

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
            initWebrtcButton.IsEnabled = false;

            starWebrtcContext.initAndStartWebRTC();
        }
    }
}
