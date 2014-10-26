#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Microsoft.Kinect;

#endregion

namespace Kinect
{
    public class Utils
    {
        public static byte[] BufBytes = new byte[868352];
        private readonly int _bytesPerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7)/8;
        private ushort[] _depthData;
        private byte[] _bodyData;
        private byte[] _colorData;
        private byte[] _displayPixels;
        private ColorSpacePoint[] _colorPoints;
        private readonly CoordinateMapper _coordinateMapper;
        public static byte[] OldBytes;

        public Utils(CoordinateMapper mapper)
        {
            _coordinateMapper = mapper;
        }
        public void GreenScreen(ColorFrame colorFrame, DepthFrame depthFrame, BodyIndexFrame bodyIndexFrame) //from https://github.com/Vangos/kinect-2-background-removal
        {
            var colorWidth = colorFrame.FrameDescription.Width;
            var colorHeight = colorFrame.FrameDescription.Height;

            var depthWidth = depthFrame.FrameDescription.Width;
            var depthHeight = depthFrame.FrameDescription.Height;

            var bodyIndexWidth = bodyIndexFrame.FrameDescription.Width;
            var bodyIndexHeight = bodyIndexFrame.FrameDescription.Height;

            if (_displayPixels == null)
            {
                _depthData = new ushort[depthWidth*depthHeight];
                _bodyData = new byte[depthWidth*depthHeight];
                _colorData = new byte[colorWidth*colorHeight*_bytesPerPixel];
                _displayPixels = new byte[depthWidth*depthHeight*_bytesPerPixel];
                _colorPoints = new ColorSpacePoint[depthWidth*depthHeight];
            }

            if (((depthWidth*depthHeight) != _depthData.Length) ||
                ((colorWidth*colorHeight*_bytesPerPixel) != _colorData.Length) ||
                ((bodyIndexWidth*bodyIndexHeight) != _bodyData.Length)) return;

            depthFrame.CopyFrameDataToArray(_depthData);

            if (colorFrame.RawColorImageFormat == ColorImageFormat.Bgra)
            {
                colorFrame.CopyRawFrameDataToArray(_colorData);
            }
            else
            {
                colorFrame.CopyConvertedFrameDataToArray(_colorData, ColorImageFormat.Bgra);
            }

            bodyIndexFrame.CopyFrameDataToArray(_bodyData);

            _coordinateMapper.MapDepthFrameToColorSpace(_depthData, _colorPoints);

            Array.Clear(_displayPixels, 0, _displayPixels.Length);

            for (var y = 0; y < depthHeight; ++y)
            {
                for (var x = 0; x < depthWidth; ++x)
                {
                    var depthIndex = (y*depthWidth) + x;

                    var player = _bodyData[depthIndex];

                    if (player == 0xff) continue;
                    var colorPoint = _colorPoints[depthIndex];

                    var colorX = (int) Math.Floor(colorPoint.X + 0.5);
                    var colorY = (int) Math.Floor(colorPoint.Y + 0.5);

                    if ((colorX < 0) || (colorX >= colorWidth) || (colorY < 0) || (colorY >= colorHeight)) continue;
                    var colorIndex = ((colorY*colorWidth) + colorX)*_bytesPerPixel;
                    var displayIndex = depthIndex*_bytesPerPixel;

                    _displayPixels[displayIndex + 0] = _colorData[colorIndex];
                    _displayPixels[displayIndex + 1] = _colorData[colorIndex + 1];
                    _displayPixels[displayIndex + 2] = _colorData[colorIndex + 2];
                    _displayPixels[displayIndex + 3] = 0xff;
                }
            }

            Array.Clear(BufBytes, 0, BufBytes.Length); //Zerofill array
            for (var d = 0; d < _displayPixels.Count(); d++)
            {
                if (_displayPixels[d] != 0)
                {
                    BufBytes[d] = MainWindow.InfraPixels[d];
                }
            }
        }

        public static decimal ProcessInfraredData()
        {
            try
            {
                var temp1 = BufBytes.Where(i => i != 0).ToList();
                var temp2 = OldBytes.Where(i => i != 0).ToList();

                var average1 = (decimal)temp1.Average(i => i);
                var average2 = (decimal)temp2.Average(i => i);
                
                return decimal.Divide(average1 + average2, 2);
            }
            catch (Exception)
            {
                //todo:implement a non try/catch fix later
                return 0;
            }
        }
    }
}