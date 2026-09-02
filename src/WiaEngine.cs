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
            if (wiaType == null)
            {
                throw new PlatformNotSupportedException("[-] Windows Image Acquisition (WIA) subsystem component mapping missing.");
            }
            _deviceManager = Activator.CreateInstance(wiaType);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Critical: Failed to initialize WIA COM automation interface wrapper manager layer: {ex.Message}");
            _deviceManager = null;
        }
    }

    // =========================================================================
    // FEATURE 1: DISCOVER SCANNERS WITH COMPLETE SAFETY CATCH ROUTINES
    // =========================================================================
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
                    if (info.Type.ToString() == WiaDeviceTypeScanner)
                    {
                        string name = info.Properties["Name"].Value?.ToString() ?? "Unknown Scanner Node";
                        string deviceId = info.DeviceID?.ToString() ?? string.Empty;
                        
                        if (!string.IsNullOrEmpty(deviceId))
                        {
                            scannerMap[name] = deviceId;
                        }
                    }
                }
                catch (Exception deviceEx)
                {
                    // Catch individual corrupted/busy descriptor access errors so loop continues scanning
                    Console.WriteLine($"[-] Warning: Failed to parse individual device property record index metrics: {deviceEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Error: Device manager collection iteration faulted completely: {ex.Message}");
        }

        return scannerMap;
    }

    // =========================================================================
    // FEATURE 2: POLL REAL-TIME HARDWARE ERROR LOOP REGISTERS
    // =========================================================================
    public string QueryRealtimeStatus(string deviceId)
    {
        if (_deviceManager == null) return "OFFLINE: Subsystem completely unavailable";

        try
        {
            dynamic? targetInfo = FindDeviceInfo(deviceId);
            if (targetInfo == null) return "OFFLINE: Target hardware node reference not found";

            dynamic? connectedDevice = null;
            try
            {
                connectedDevice = targetInfo.Connect();
            }
            catch (Exception connEx)
            {
                return $"OFFLINE: Device context rejected. Unit might be busy or unpowered. ({connEx.Message})";
            }

            if (connectedDevice == null) return "OFFLINE: Established connection reference is null";

            int statusFlags = 0;
            try
            {
                statusFlags = (int)connectedDevice.Properties[WIA_DPS_DOCUMENT_HANDLING_STATUS].Value;
            }
            catch
            {
                // Fallback state if scanner handles connections but does not expose paper tray sensors
                return "ONLINE: Connected | Hardware properties locked/busy";
            }

            // Bitwise status analysis checks
            if ((statusFlags & 1) == 1) return "ERROR: PHYSICAL PAPER JAM INSIDE UNIT";
            if ((statusFlags & 2) == 2) return "ATTENTION: AUTOMATIC DOCUMENT FEEDER EMPTY";
            if ((statusFlags & 4) == 4) return "ERROR: COVER OR FLATBED GLASS COVER OPEN";
            if ((statusFlags & 32) == 32) return "OFFLINE: DEVICE WARMING UP";

            return "READY: HARDWARE IDLE & ONLINE";
        }
        catch (Exception ex)
        {
            return $"OFFLINE: Communication transaction stream channel faulted -> {ex.Message}";
        }
    }

    // =========================================================================
    // FEATURE 3 & 4: PASS PAYLOAD CONSTRAINTS AND TRIGGER SCANNING ACTION
    // =========================================================================
    public bool ExecuteScanJob(string deviceId, int dpi, int colorMode, int paperSource, string targetPath)
    {
        if (_deviceManager == null) return false;

        try
        {
            dynamic? targetInfo = FindDeviceInfo(deviceId);
            if (targetInfo == null)
            {
                Console.WriteLine("[-] Execution Blocked: Target scanner matching identification rules could not be found.");
                return false;
            }

            dynamic? connectedDevice = null;
            try
            {
                connectedDevice = targetInfo.Connect();
            }
            catch (Exception lockEx)
            {
                Console.WriteLine($"[!] Error: Unable to establish exclusive device stream pipeline context lock: {lockEx.Message}");
                return false;
            }

            if (connectedDevice == null || connectedDevice.Items.Count == 0)
            {
                Console.WriteLine("[-] Error: Hardware device reported empty sub-items processing matrix tables.");
                return false;
            }
            
            // Fix: Clean target indexing resolution
            dynamic itemProperties = connectedDevice.Items[1].Properties;

            // Set parameter payload parameters mapping with try validation blocks
            try
            {
                itemProperties[WIA_IPS_XRES].Value = dpi;
                itemProperties[WIA_IPS_YRES].Value = dpi;
                itemProperties[WIA_IPA_DATATYPE].Value = colorMode; 
            }
            catch (Exception propEx)
            {
                Console.WriteLine($"[-] Warning: Scanner rejected custom variant properties configurations script: {propEx.Message}");
            }

            try
            {
                connectedDevice.Properties[WIA_DPS_DOCUMENT_HANDLING_SELECT].Value = paperSource; 
            }
            catch { /* Suppress hardware limitations for units without automated ingestion assemblies */ }

            dynamic? imageFile = null;
            try
            {
                dynamic commonDialog = Activator.CreateInstance(Type.GetTypeFromProgID("WIA.CommonDialog")!);
                // False parameter suppresses manufacturer interface dialog popup loops
                imageFile = commonDialog.ShowTransfer(connectedDevice.Items[1], wiaFormatJpeg, false);
            }
            catch (Exception transEx)
            {
                Console.WriteLine($"[!] Error: Core uncompressed binary memory transfer loop failed: {transEx.Message}");
                return false;
            }

            if (imageFile != null)
            {
                try
                {
                    if (File.Exists(targetPath)) File.Delete(targetPath);
                    imageFile.SaveFile(targetPath);
                    return true;
                }
                catch (IOException ioEx)
                {
                    Console.WriteLine($"[!] Disk Error: Unable to write binary stream output data to file space layout path: {ioEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Fatal: Custom WIA Layer Job Stream Execution Internal Fault: {ex.Message}");
        }

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
                    string idStr = info.DeviceID?.ToString() ?? string.Empty;
                    string nameStr = info.Properties["Name"].Value?.ToString() ?? string.Empty;

                    if (idStr == identifier || nameStr.Contains(identifier, StringComparison.OrdinalIgnoreCase))
                    {
                        return info;
                    }
                }
                catch { /* Ignore unreadable properties entries during traversal */ }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[-] Error searching WIA device table tree layout records structures: {ex.Message}");
        }
        return null;
    }
}
