#region

using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;

#endregion

namespace Kinect
{
    public partial class MainWindow
    {
        public static byte[] InfraPixels;
        private Utils _utils;
        private WriteableBitmap _infraBitmap;
        private ushort[] _infraData;
        private InfraredFrameReader _infraReader;
        private KinectSensor _kinectSensor;
        private MultiSourceFrameReader _reader;
        private readonly int _bytePerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7)/8;

        public MainWindow()
        {
            InitializeComponent();
            Closing += Close;
        }

        private void Close(object sender, CancelEventArgs e)
        {
            if (_kinectSensor != null)
            {
                _kinectSensor.Close();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _kinectSensor = KinectSensor.GetDefault();
            InitializeInfrared();
            _kinectSensor?.Open();

            if (_kinectSensor != null)
            {
                _utils = new Utils(_kinectSensor.CoordinateMapper);
                _reader =
                    _kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Depth |
                                                             FrameSourceTypes.BodyIndex);
            }
            _reader.MultiSourceFrameArrived += Reader_MultiSourceFrameArrived;
        }

        private void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            var reference = e.FrameReference.AcquireFrame();

            using (var colorFrame = reference.ColorFrameReference.AcquireFrame())
            using (var depthFrame = reference.DepthFrameReference.AcquireFrame())
            using (var bodyIndexFrame = reference.BodyIndexFrameReference.AcquireFrame())
            {
                if (colorFrame != null && depthFrame != null && bodyIndexFrame != null)
                {
                    _utils.GreenScreen(colorFrame, depthFrame, bodyIndexFrame);
                }
            }
        }

        private void InitializeInfrared()
        {
            if (_kinectSensor == null) return;

            // Get frame description for the color output
            var desc = _kinectSensor.InfraredFrameSource.FrameDescription;

            // Get the framereader for Color
            _infraReader = _kinectSensor.InfraredFrameSource.OpenReader();

            // Allocate pixel array
            _infraData = new ushort[desc.Width*desc.Height];
            InfraPixels = new byte[desc.Width*desc.Height*_bytePerPixel];

            // Create new WriteableBitmap
            _infraBitmap = new WriteableBitmap(desc.Width, desc.Height, 96, 96, PixelFormats.Bgr32, null);

            // Hook-up event
            _infraReader.FrameArrived += OnInfraredFrameArrived;
        }

        private void OnInfraredFrameArrived(object sender, InfraredFrameArrivedEventArgs e)
        {
            // Reference to infrared frame
            var refer = e.FrameReference;

            // Get infrared frame
            var frame = refer.AcquireFrame();

            if (frame == null) return;

            // Process it
            using (frame)
            {
                // Get the description
                var frameDesc = frame.FrameDescription;

                if (((frameDesc.Width*frameDesc.Height) != _infraData.Length) ||
                    (frameDesc.Width != _infraBitmap.PixelWidth) || (frameDesc.Height != _infraBitmap.PixelHeight))
                    return;

                // Copy data
                frame.CopyFrameDataToArray(_infraData);

                var colorPixelIndex = 0;

                foreach (var intensity in _infraData.Select(ir => (byte)(ir)))
                {
                    // Assign infrared intensity
                    InfraPixels[colorPixelIndex++] = intensity;
                    InfraPixels[colorPixelIndex++] = intensity;
                    InfraPixels[colorPixelIndex++] = intensity;

                    colorPixelIndex++;
                }

                Utils.OldBytes = Utils.BufBytes;

                decimal heartBeat = Utils.ProcessInfraredData();
                if (heartBeat == 0) return;

                // Copy output to bitmap
                _infraBitmap.WritePixels(
                    new Int32Rect(0, 0, frameDesc.Width, frameDesc.Height),
                    Utils.BufBytes,
                    frameDesc.Width * _bytePerPixel,
                    0);

                image1.Source = _infraBitmap;
                label.Content = heartBeat;
            }
        }
    }
}