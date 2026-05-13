using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using OpenCvSharp;
using System;

namespace CameraActivityMonitor.Helpers;

public static class CameraHelper
{
    public static WriteableBitmap? CaptureFrameBitmap(int cameraIndex = 0)
    {
        using var capture = new VideoCapture(cameraIndex);

        if (!capture.IsOpened())
            throw new InvalidOperationException("无法打开摄像头");

        using var frame = new Mat();

        // 刚打开摄像头时，前几帧可能黑屏/曝光不稳定
        for (int i = 0; i < 5; i++)
        {
            if (!capture.Read(frame) || frame.Empty())
                throw new InvalidOperationException("无法读取摄像头画面");
        }

        using var bgra = new Mat();

        // OpenCV 默认是 BGR，Avalonia 常用的是 BGRA8888
        Cv2.CvtColor(frame, bgra, ColorConversionCodes.BGR2BGRA);

        var size = new PixelSize(bgra.Width, bgra.Height);
        var dpi = new Vector(96, 96);
        var stride = (int)bgra.Step();

        return new WriteableBitmap(
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque,
            bgra.Data,
            size,
            dpi,
            stride);
    }
}