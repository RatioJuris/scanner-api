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

    private readonly dynamic? _deviceManager;

    public WiaEngine()
    {
        try
        {
            Type? wiaType = Type.GetTypeFromProgID("WIA.DeviceManager");
            if (wiaType == null) throw new PlatformNotSupportedException("[-] WIA component mapping missing.");
            _deviceManager = Activator.CreateInstance(wiaType);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Critical WIA Initialization Error: {ex.Message}");
            _deviceManager = null;
        }
    }

    public Dictionary<string, string> EnumerateScanners()
    {
        var scannerMap = new Dictionary<string, string>();
        if (_deviceManager == null) return scannerMap;
        try
        {
            foreach (dynamic info in _deviceManager.DeviceInfos)
            {
                try
                {
                    if (info == null) continue;
                    if (info.Type.ToString() == WiaDeviceTypeScanner)
                    {
                        dynamic? props = info.Properties;
                        if (props == null) continue;
                        string name = props["Name"]?.Value?.ToString() ?? "Unknown Scanner Node";
                        string deviceId = info.DeviceID?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(deviceId)) scannerMap[name] = deviceId;
                    }
                }
                catch { }
            }
        }
        catch (Exception ex) { Console.WriteLine($"[!] Collection enumeration faulted: {ex.Message}"); }
        return scannerMap;
    }

    public string QueryRealtimeStatus(string deviceId)
    {
        if (_deviceManager == null) return "OFFLINE: Subsystem unavailable";
        try
        {
            dynamic? targetInfo = FindDeviceInfo(deviceId);
            if (targetInfo == null) return "OFFLINE: Target node reference not found";
            dynamic? connectedDevice = targetInfo.Connect();
            if (connectedDevice == null) return "OFFLINE: Established reference is null";

            int statusFlags = 0;
            try 
            { 
                dynamic? props = connectedDevice.Properties;
                if (props != null)
                {
                    statusFlags = (int)props[WIA_DPS_DOCUMENT_HANDLING_STATUS].Value; 
                }
            }
            catch { return "ONLINE: Connected | Sensors locked/busy"; }

            if ((statusFlags & 1) == 1) return "ERROR: PHYSICAL PAPER JAM INSIDE UNIT";
            if ((statusFlags & 2) == 2) return "ATTENTION: AUTOMATIC DOCUMENT FEEDER EMPTY";
            if ((statusFlags & 4) == 4) return "ERROR: COVER OR FLATBED GLASS COVER OPEN";
            if ((statusFlags & 32) == 32) return "OFFLINE: DEVICE WARMING UP";
            return "READY: HARDWARE IDLE & ONLINE";
        }
        catch (Exception ex) { return $"OFFLINE: Subsystem fault -> {ex.Message}"; }
    }

    public bool ExecuteScanJob(string deviceId, int dpi, int colorMode, int paperSource, string targetPath)
    {
        if (_deviceManager == null) return false;
        try
        {
            dynamic? targetInfo = FindDeviceInfo(deviceId);
            if (targetInfo == null) return false;
            dynamic? connectedDevice = targetInfo.Connect();
            if (connectedDevice == null) return false;

            dynamic? items = connectedDevice.Items;
            if (items == null || items.Count == 0) return false;
            
            dynamic? item = items[1];
            if (item == null) return false;

            dynamic? itemProperties = item.Properties;
            if (itemProperties != null)
            {
                try
                {
                    itemProperties[WIA_IPS_XRES].Value = dpi;
                    itemProperties[WIA_IPS_YRES].Value = dpi;
                    itemProperties[WIA_IPA_DATATYPE].Value = colorMode; 
                }
                catch (Exception ex) { Console.WriteLine($"[-] Warning properties rejected: {ex.Message}"); }
            }

            try 
            { 
                dynamic? devProps = connectedDevice.Properties;
                if (devProps != null)
                {
                    devProps[WIA_DPS_DOCUMENT_HANDLING_SELECT].Value = paperSource; 
                }
            } 
            catch { }

            dynamic? imageFile = null;
            try
            {
                Type? dlgType = Type.GetTypeFromProgID("WIA.CommonDialog");
                if (dlgType == null) return false;
                dynamic? commonDialog = Activator.CreateInstance(dlgType);
                if (commonDialog == null) return false;
                imageFile = commonDialog.ShowTransfer(item, wiaFormatJpeg, false);
            }
            catch (Exception ex) { Console.WriteLine($"[!] Memory transfer failed: {ex.Message}"); return false; }

            if (imageFile != null)
            {
                if (File.Exists(targetPath)) File.Delete(targetPath);
                imageFile.SaveFile(targetPath);
                return true;
            }
        }
        catch (Exception ex) { Console.WriteLine($"[!] Fatal WIA Engine error: {ex.Message}"); }
        return false;
    }

    private dynamic? FindDeviceInfo(string identifier)
    {
        if (_deviceManager == null || string.IsNullOrEmpty(identifier)) return null;
        try
        {
            foreach (dynamic info in _deviceManager.DeviceInfos)
            {
                try
                {
                    if (info == null) continue;
                    string idStr = info.DeviceID?.ToString() ?? string.Empty;
                    dynamic? props = info.Properties;
                    if (props == null) continue;
                    dynamic? nameProp = props["Name"];
                    string nameStr = nameProp?.Value?.ToString() ?? string.Empty;
                    if (idStr == identifier || nameStr.Contains(identifier, StringComparison.OrdinalIgnoreCase)) return info;
                }
                catch { }
            }
        }
        catch { }
        return null;
    }
}
