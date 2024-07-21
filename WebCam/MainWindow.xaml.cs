using AForge.Video;
using AForge.Video.DirectShow;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Media.Imaging;

namespace WebCam
{
    public partial class MainWindow : Window
    {
        private FilterInfoCollection filterInfoCollection;
        private VideoCaptureDevice videoCaptureDevice;
        private VideoWriter _videoWriter;
        private bool _isRecording = false;
        private string _videoFileName;
        private Bitmap _currentFrame;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            filterInfoCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo device in filterInfoCollection)
            {
                comboBoxCameras.Items.Add(device.Name);
            }

            if (comboBoxCameras.Items.Count > 0)
            {
                comboBoxCameras.SelectedIndex = 0; // Select the first camera by default
            }
            else
            {
                MessageBox.Show("No video devices found.");
            }
        }

        private void buttonStartCamera_Click(object sender, RoutedEventArgs e)
        {
            if (comboBoxCameras.SelectedIndex >= 0)
            {
                videoCaptureDevice = new VideoCaptureDevice(filterInfoCollection[comboBoxCameras.SelectedIndex].MonikerString);
                videoCaptureDevice.NewFrame += VideoCaptureDevice_NewFrame;
                videoCaptureDevice.Start();
            }
            else
            {
                MessageBox.Show("Please select a video source from the list.");
            }
        }

        private void VideoCaptureDevice_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                _currentFrame = (Bitmap)eventArgs.Frame.Clone();
                Dispatcher.Invoke(() =>
                {
                    imageDisplay.Source = BitmapToImageSource(_currentFrame);
                });

                if (_isRecording)
                {
                    using (Mat mat = BitmapToMat(_currentFrame))
                    {
                        _videoWriter.Write(mat);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error capturing frame: " + ex.Message);
            }
        }

        private BitmapSource BitmapToImageSource(Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                System.Windows.Media.PixelFormats.Bgr24, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);
            return bitmapSource;
        }

        private unsafe Mat BitmapToMat(Bitmap bitmap)
        {
            Mat mat = new Mat(bitmap.Height, bitmap.Width, DepthType.Cv8U, 3);
            System.Drawing.Imaging.BitmapData bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            byte* src = (byte*)bitmapData.Scan0;
            for (int y = 0; y < bitmap.Height; y++)
            {
                byte* dest = (byte*)mat.DataPointer + y * mat.Step;
                for (int x = 0; x < bitmap.Width; x++)
                {
                    dest[x * 3] = src[x * 3 + 2]; // B
                    dest[x * 3 + 1] = src[x * 3 + 1]; // G
                    dest[x * 3 + 2] = src[x * 3]; // R
                }
            }

            bitmap.UnlockBits(bitmapData);
            return mat;
        }

        private void buttonCaptureImage_Click(object sender, RoutedEventArgs e)
        {
            if (videoCaptureDevice != null && videoCaptureDevice.IsRunning)
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "JPEG Image|*.jpg",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    _currentFrame.Save(saveFileDialog.FileName);
                }
            }
            else
            {
                MessageBox.Show("Please start the camera first.");
            }
        }

        private void buttonStartRecording_Click(object sender, RoutedEventArgs e)
        {
            if (videoCaptureDevice != null && videoCaptureDevice.IsRunning)
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "AVI Files (*.avi)|*.avi|MP4 Files (*.mp4)|*.mp4",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    _videoFileName = saveFileDialog.FileName;
                    string fileExtension = System.IO.Path.GetExtension(_videoFileName).ToLower();
                    int codec = fileExtension == ".mp4" ? FourCC.H264 : FourCC.MJPG;

                    _videoWriter = new VideoWriter(_videoFileName, codec, 25, new System.Drawing.Size(_currentFrame.Width, _currentFrame.Height), true);
                    _isRecording = true;
                }
            }
            else
            {
                MessageBox.Show("Please start the camera first.");
            }
        }

        private void buttonStopRecording_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecording)
            {
                _isRecording = false;
                _videoWriter.Dispose();
                MessageBox.Show("Recording stopped and saved to " + _videoFileName);
            }
        }

        private void buttonExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            StopVideoCaptureDevice();
        }

        private void StopVideoCaptureDevice()
        {
            if (videoCaptureDevice != null && videoCaptureDevice.IsRunning)
            {
                videoCaptureDevice.SignalToStop();
                videoCaptureDevice.WaitForStop();
            }

            if (_isRecording)
            {
                StopRecording();
            }
        }

        private void StopRecording()
        {
            if (_isRecording)
            {
                _isRecording = false;
                _videoWriter.Dispose();
            }
        }
    }

    public static class FourCC
    {
        public const int MJPG = 1196444237; // "MJPG" FourCC
        public const int XVID = 1145656920; // "XVID" FourCC
        public const int H264 = 875967075;  // "H264" FourCC (for MP4)
        // Add other FourCC codes as needed
    }
}
