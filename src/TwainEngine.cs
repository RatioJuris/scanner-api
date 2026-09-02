using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class TwainEngine : IDisposable
{
    private const uint DG_CONTROL = 0x0001;
    private const uint DG_IMAGE   = 0x0002;

    private const ushort DAT_IDENTITY      = 0x000B;
    private const ushort DAT_USERINTERFACE = 0x0009;
    private const ushort DAT_CAPABILITY    = 0x0001;

    private const ushort MSG_GETFIRST = 0x0006;
    private const ushort MSG_GETNEXT  = 0x0007;
    private const ushort MSG_OPENDSM  = 0x0301;
    private const ushort MSG_CLOSEDSM = 0x0302;
    private const ushort MSG_OPENDS   = 0x0401;
    private const ushort MSG_CLOSEDS  = 0x0402;
    private const ushort MSG_USERIFON = 0x0501;

    private const ushort TWRC_SUCCESS  = 0x0000;
    private const ushort TWON_ONEVALUE = 0x0005;

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public struct TW_VERSION
    {
        public ushort MajorNum;
        public ushort MinorNum;
        public ushort Language;
        public ushort Country;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 34)] public string Info;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public struct TW_IDENTITY
    {
        public uint Id;
        public TW_VERSION Version;
        public ushort ProtocolMajor;
        public ushort ProtocolMinor;
        public uint SupportedGroups;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 34)] public string Manufacturer;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 34)] public string ProductFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 34)] public string ProductName;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public struct TW_USERINTERFACE
    {
        public ushort ShowUI;
        public ushort ModalUI;
        public IntPtr ParentHand;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public struct TW_CAPABILITY
    {
        public ushort Cap;
        public ushort ConType;
        public IntPtr hContainer;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public struct TW_ONEVALUE
    {
        public ushort ItemType;
        public uint Item;
    }

    [DllImport("twain_32.dll", EntryPoint = "DSM_Entry", CharSet = CharSet.Ansi)]
    private static extern ushort DSM32(ref TW_IDENTITY origin, IntPtr dest, uint dg, ushort dat, ushort msg, ref TW_IDENTITY pd);
    
    [DllImport("twain_32.dll", EntryPoint = "DSM_Entry", CharSet = CharSet.Ansi)]
    private static extern ushort DSM32Cap(ref TW_IDENTITY origin, ref TW_IDENTITY dest, uint dg, ushort dat, ushort msg, ref TW_CAPABILITY cap);

    [DllImport("twain_32.dll", EntryPoint = "DSM_Entry", CharSet = CharSet.Ansi)]
    private static extern ushort DSM32Ui(ref TW_IDENTITY origin, ref TW_IDENTITY dest, uint dg, ushort dat, ushort msg, ref TW_USERINTERFACE ui);

    [DllImport("twain64.dll", EntryPoint = "DSM_Entry", CharSet = CharSet.Ansi)]
    private static extern ushort DSM64(ref TW_IDENTITY origin, IntPtr dest, uint dg, ushort dat, ushort msg, ref TW_IDENTITY pd);

    [DllImport("twain64.dll", EntryPoint = "DSM_Entry", CharSet = CharSet.Ansi)]
    private static extern ushort DSM64Cap(ref TW_IDENTITY origin, ref TW_IDENTITY dest, uint dg, ushort dat, ushort msg, ref TW_CAPABILITY cap);

    [DllImport("twain64.dll", EntryPoint = "DSM_Entry", CharSet = CharSet.Ansi)]
    private static extern ushort DSM64Ui(ref TW_IDENTITY origin, ref TW_IDENTITY dest, uint dg, ushort dat, ushort msg, ref TW_USERINTERFACE ui);

    [DllImport("kernel32.dll", EntryPoint = "GlobalAlloc")]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", EntryPoint = "GlobalLock")]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", EntryPoint = "GlobalUnlock")]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", EntryPoint = "GlobalFree")]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    private TW_IDENTITY _appIdentity;
    private TW_IDENTITY _activeSource;
    private bool _isDsmOpen;
    private bool _isDsOpen;
    private readonly bool _is64Bit;

    public TwainEngine()
    {
        _is64Bit = IntPtr.Size == 8;
        
        _appIdentity = new TW_IDENTITY
        {
            Id = 0,
            Version = new TW_VERSION { MajorNum = 1, MinorNum = 0, Language = 19, Country = 1, Info = "V1" },
            ProtocolMajor = 2,
            ProtocolMinor = 3,
            SupportedGroups = 0x00000003,  
            Manufacturer = "Proprietary Core",
            ProductFamily = "Scanner API Wrapper",
            ProductName = "scanner-api"
        };

        try
        {
            OpenDataManager();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Critical Error: Unable to bind to the system Data Source Manager: {ex.Message}");
            _isDsmOpen = false;
        }
    }

    private void OpenDataManager()
    {
        try
        {
            ushort rc = _is64Bit
                ? DSM64(ref _appIdentity, IntPtr.Zero, DG_CONTROL, 0x0301, MSG_OPENDSM, ref _appIdentity)  
                : DSM32(ref _appIdentity, IntPtr.Zero, DG_CONTROL, 0x0301, MSG_OPENDSM, ref _appIdentity);

            if (rc == TWRC_SUCCESS) _isDsmOpen = true;
        }
        catch (DllNotFoundException)
        {
            Console.WriteLine($"[-] System Alert: The required TWAIN library component file (twain_{( _is64Bit ? "64" : "32" )}.dll) is missing from this Windows installation.");
            _isDsmOpen = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[-] Exception encountered opening DSM: {ex.Message}");
            _isDsmOpen = false;
        }
    }

    public List<string> EnumerateScanners()
    {
        var scannerList = new List<string>();
        if (!_isDsmOpen) return scannerList;

        try
        {
            TW_IDENTITY sourceIdentity = new TW_IDENTITY();
            ushort rc = _is64Bit  
                ? DSM64(ref _appIdentity, IntPtr.Zero, DG_CONTROL, DAT_IDENTITY, MSG_GETFIRST, ref sourceIdentity)
                : DSM32(ref _appIdentity, IntPtr.Zero, DG_CONTROL, DAT_IDENTITY, MSG_GETFIRST, ref sourceIdentity);

            while (rc == TWRC_SUCCESS)
            {
                if (!string.IsNullOrEmpty(sourceIdentity.ProductName))
                {
                    scannerList.Add(sourceIdentity.ProductName);
                }
                
                try
                {
                    rc = _is64Bit 
                        ? DSM64(ref _appIdentity, IntPtr.Zero, DG_CONTROL, DAT_IDENTITY, MSG_GETNEXT, ref sourceIdentity)
                        : DSM32(ref _appIdentity, IntPtr.Zero, DG_CONTROL, DAT_IDENTITY, MSG_GETNEXT, ref sourceIdentity);
                }
                catch (Exception loopEx)
                {
                    Console.WriteLine($"[-] Intermittent warning encountered parsing driver registry node: {loopEx.Message}");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Critical Error enumerating native TWAIN hardware tree entries: {ex.Message}");
        }

        return scannerList;
    }

    public bool SelectAndOpenScanner(string scannerName)
    {
        if (!_isDsmOpen || string.IsNullOrEmpty(scannerName)) return false;
        
        try
        {
            CloseActiveScanner();

            TW_IDENTITY sourceIdentity = new TW_IDENTITY();
            ushort rc = _is64Bit 
                ? DSM64(ref _appIdentity, IntPtr.Zero, DG_CONTROL, DAT_IDENTITY, MSG_GETFIRST, ref sourceIdentity)
                : DSM32(ref _appIdentity, IntPtr.Zero, DG_CONTROL, DAT_IDENTITY, MSG_GETFIRST, ref sourceIdentity);

            while (rc == TWRC_SUCCESS)
            {
                if (sourceIdentity.ProductName.Equals(scannerName, StringComparison.OrdinalIgnoreCase))
                {
                    ushort openRc = _is64Bit
                        ? DSM64(ref _appIdentity, IntPtr.Zero, DG_CONTROL, DAT_IDENTITY, MSG_OPENDS, ref sourceIdentity)
                        : DSM32(ref _appIdentity, IntPtr.Zero, DG_CONTROL, DAT_IDENTITY, MSG_OPENDS, ref sourceIdentity);

                    if (openRc == TWRC_SUCCESS)
                    {
                        _activeSource = sourceIdentity;
                        _isDsOpen = true;
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"[-] Connection Refused: The vendor driver '{scannerName}' rejected the exclusive connection handle.");
                        return false;
                    }
                }

                rc = _is64Bit 
                    ? DSM64(ref _appIdentity, IntPtr.Zero, DG_CONTROL, DAT_IDENTITY, MSG_GETNEXT, ref sourceIdentity)
                    : DSM32(ref _appIdentity, IntPtr.Zero, DG_CONTROL, DAT_IDENTITY, MSG_GETNEXT, ref sourceIdentity);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Critical Error occurred establishing an operational context with '{scannerName}': {ex.Message}");
        }
        return false;
    }

    public bool ExecuteScanJob(int dpi, ushort pixelType, ushort paperSource)
    {
        if (!_isDsOpen) return false;
        try
        {
            try { SetCapability(0x1118, (uint)dpi); } catch (Exception ex) { Console.WriteLine($"[-] Warning: Driver rejected ICAP_XRESOLUTION payload constraint modification values: {ex.Message}"); }
            try { SetCapability(0x1119, (uint)dpi); } catch (Exception ex) { Console.WriteLine($"[-] Warning: Driver rejected ICAP_YRESOLUTION payload constraint modification values: {ex.Message}"); }
            try { SetCapability(0x0101, pixelType); } catch (Exception ex) { Console.WriteLine($"[-] Warning: Driver rejected ICAP_PIXELTYPE color payload constraint parameters: {ex.Message}"); }
            try { SetCapability(0x1002, paperSource); } catch (Exception ex) { Console.WriteLine($"[-] Warning: Driver rejected CAP_FEEDERENABLED mechanical sheet allocation payloads: {ex.Message}"); }
            
            TW_USERINTERFACE ui = new TW_USERINTERFACE
            {
                ShowUI = 0,
                ModalUI = 0,
                ParentHand = IntPtr.Zero
            };
            
            ushort rc = _is64Bit
                ? DSM64Ui(ref _appIdentity, ref _activeSource, DG_CONTROL, DAT_USERINTERFACE, MSG_USERIFON, ref ui)
                : DSM32Ui(ref _appIdentity, ref _activeSource, DG_CONTROL, DAT_USERINTERFACE, MSG_USERIFON, ref ui);
            
            return rc == TWRC_SUCCESS;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Fatal Error occurred during uncompressed driver image pipeline transmission execution: {ex.Message}");
            return false;
        }
    }

    private void SetCapability(ushort capId, uint value)
    {
        IntPtr hContainer = IntPtr.Zero;
        try
        {
            hContainer = GlobalAlloc(0x0040, (UIntPtr)Marshal.SizeOf<TW_ONEVALUE>());
            if (hContainer == IntPtr.Zero) throw new OutOfMemoryException("[-] Native Win32 Kernel Global Alloc allocation memory pool bounds exceeded.");
            
            IntPtr pData = GlobalLock(hContainer);
            if (pData == IntPtr.Zero) throw new InvalidOperationException("[-] Failed to safely establish a lock token descriptor on the global pointer target context.");
            
            TW_ONEVALUE oneVal = new TW_ONEVALUE
            {
                ItemType = 5,
                Item = value
            };
            Marshal.StructureToPtr(oneVal, pData, false);
            GlobalUnlock(hContainer);
            
            TW_CAPABILITY cap = new TW_CAPABILITY
            {
                Cap = capId,
                ConType = TWON_ONEVALUE,
                hContainer = hContainer
            };
            
            ushort rc = _is64Bit
                ? DSM64Cap(ref _appIdentity, ref _activeSource, DG_CONTROL, DAT_CAPABILITY, 0x0002, ref cap)
                : DSM32Cap(ref _appIdentity, ref _activeSource, DG_CONTROL, DAT_CAPABILITY, 0x0002, ref cap);
            
            if (rc != TWRC_SUCCESS)
            {
                throw new InvalidOperationException($"Driver returned operational constraint exception code: {rc}");
            }
        }
        finally
        {
            if (hContainer != IntPtr.Zero)
            {
                GlobalFree(hContainer);
            }
        }
    }

    public void CloseActiveScanner()
    {
        if (!_isDsOpen) return;
        try
        {
            ushort rc = _is64Bit
                ? DSM64(ref _appIdentity, IntPtr.Zero, DG_CONTROL, DAT_IDENTITY, MSG_CLOSEDS, ref _activeSource)
                : DSM32(ref _appIdentity, IntPtr.Zero, DG_CONTROL, DAT_IDENTITY, MSG_CLOSEDS, ref _activeSource);
            if (rc == TWRC_SUCCESS) _isDsOpen = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[-] Warning: Device connection channel closure transaction reported an alert: {ex.Message}");
            _isDsOpen = false;
        }
    }

    public void Dispose()
    {
        try
        {
            CloseActiveScanner();
            if (_isDsmOpen)
            {
                if (_is64Bit) DSM64(ref _appIdentity, IntPtr.Zero, DG_CONTROL, 0x0301, MSG_CLOSEDSM, ref _appIdentity);
                else DSM32(ref _appIdentity, IntPtr.Zero, DG_CONTROL, 0x0301, MSG_CLOSEDSM, ref _appIdentity);
                _isDsmOpen = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[-] Warning: Memory cleanup operations flagged an engine exception: {ex.Message}");
        }
        GC.SuppressFinalize(this);
    }
}
