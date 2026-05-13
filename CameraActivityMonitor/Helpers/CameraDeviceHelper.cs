using Windows.Devices.Enumeration;

namespace CameraActivityMonitor.Helpers;

public static class CameraDeviceHelper
{
    public static async Task<IReadOnlyList<DeviceInformation>> GetAllCamerasAsync()
    {
        var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
        return devices;
    }

    public static async Task<string?> GetFirstCameraIdAsync()
    {
        var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
        return devices.Count > 0 ? devices[0].Id : null;
    }

    public static async Task<int?> TryGetCameraIndexByIdAsync(string cameraId)
    {
        var devices = await GetAllCamerasAsync();
        int index = -1;
        for (var i = 0; i < devices.Count; i++)
        {
            if (devices[i].Id == cameraId)
            {
                index = i;
                break;
            }
        }
        return index >= 0 ? index : null;
    }
}