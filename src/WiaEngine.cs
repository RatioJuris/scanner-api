using System;
using System.Collections.Generic;
using System.IO;

public class WiaEngine
{
    private const string WiaDeviceTypeScanner = "1";
    private const string WIA_COMPRESSION = "4107";
    private const string WIA_IPA_DATATYPE = "4103"; 
    private const string WIA_IPS_XRES = "6147";     
    private const string WIA_IPS_YRES = "6148";     
    private const string WIA_DPS_DOCUMENT_HANDLING_STATUS = "3088"; 
    private const string WIA_DPS_DOCUMENT_HANDLING_SELECT = "3087"; 

    private const string wiaFormatJpeg = "{B96B3CAF-0728-11D3-9D7B-0000F81EF32E}";

    private readonly dynamic _deviceManager;

    public WiaEngine()
    {
        Type? wiaType = Type.GetTypeFromProgID("WIA.DeviceManager");
        if (wiaType == null)
        {
            throw new PlatformNotSupportedException("[-] Windows Image Acquisition (WIA) subsystem unavailable.");
        }
        _deviceManager = Activator.CreateInstance(wiaType)!;
    }

    public Dictionary<string, string> EnumerateScanners()
    {
        var scannerMap = new Dictionary<string, string>();

        foreach (dynamic info in _deviceManager.DeviceInfos)
        {
            if (info.Type.ToString() == WiaDeviceTypeScanner)
            {
                string name = info.Properties["Name"].Value.ToString();
                string deviceId = info.DeviceID.ToString();
                scannerMap[name] = deviceId;
            }
        }

        return scannerMap;
    }

    public string QueryRealtimeStatus(string deviceId)
    {
        try
        {
            dynamic? targetInfo = FindDeviceInfo(deviceId);
            if (targetInfo == null) return "OFFLINE: Device not found";

            dynamic connectedDevice = targetInfo.Connect();
            int statusFlags = 0;
            
            try
            {
                statusFlags = (int)connectedDevice.Properties[WIA_DPS_DOCUMENT_HANDLING_STATUS].Value;
            }
            catch
            {
                return "ONLINE: Device ready";
            }

            if ((statusFlags & 1) == 1) return "ERROR: PHYSICAL PAPER JAM INSIDE UNIT";
            if ((statusFlags & 2) == 2) return "ATTENTION: AUTOMATIC DOCUMENT FEEDER EMPTY";
            if ((statusFlags & 4) == 4) return "ERROR: COVER OR FLATBED GLASS COVER OPEN";
            if ((statusFlags & 32) == 32) return "OFFLINE: DEVICE WARMING UP";

            return "READY: HARDWARE IDLE & ONLINE";
        }
        catch (Exception ex)
        {
            return $"OFFLINE: Communication channel faulted ({ex.Message})";
        }
    }

    public bool ExecuteScanJob(string deviceId, int dpi, int colorMode, int paperSource, string targetPath)
    {
        try
        {
            dynamic? targetInfo = FindDeviceInfo(deviceId);
            if (targetInfo == null) return false;

            dynamic connectedDevice = targetInfo.Connect();
            if (connectedDevice.Items.Count == 0) return false;
            
            dynamic itemProperties = connectedDevice.Items[1].Properties;

            itemProperties[WIA_IPS_XRES].Value = dpi;
            itemProperties[WIA_IPS_YRES].Value = dpi;
            itemProperties[WIA_IPA_DATATYPE].Value = colorMode; 

            try
            {
                connectedDevice.Properties[WIA_DPS_DOCUMENT_HANDLING_SELECT].Value = paperSource; 
            }
            catch { }

            dynamic commonDialog = Activator.CreateInstance(Type.GetTypeFromProgID("WIA.CommonDialog")!);
            dynamic imageFile = commonDialog.ShowTransfer(connectedDevice.Items[1], wiaFormatJpeg, false);

            if (imageFile != null)
            {
                if (File.Exists(targetPath)) File.Delete(targetPath);
                imageFile.SaveFile(targetPath);
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Custom WIA Layer Job Fault: {ex.Message}");
        }

        return false;
    }

    private dynamic? FindDeviceInfo(string identifier)
    {
        foreach (dynamic info in _deviceManager.DeviceInfos)
        {
            if (info.DeviceID.ToString() == identifier || 
                info.Properties["Name"].Value.ToString().Contains(identifier, StringComparison.OrdinalIgnoreCase))
            {
                return info;
            }
        }
        return null;
    }
}
