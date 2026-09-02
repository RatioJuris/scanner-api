using System;
using System.CommandLine;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 1 && (args[0] == "--help" || args[0] == "-h"))
        {
            RenderDeveloperDashboardWelcomeScreen();
            Console.WriteLine("\n[Press any key to close the application console terminal window...]");
            Console.ReadKey(true);
            return 0;
        }

        if (args.Length == 1 && (args[0] == "--version" || args[0] == "-v"))
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
                var copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "Copyright © 2026";
                
                Console.WriteLine($"{product} [Version {version}]");
                Console.WriteLine($"{copyright}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Error fetching assembly metadata matrix parameters: {ex.Message}");
            }
            
            Console.WriteLine("\n[Press any key to close the application console terminal window...]");
            Console.ReadKey(true);
            return 0;
        }

        if (args.Length == 0)
        {
            RenderDeveloperDashboardWelcomeScreen();
            Console.WriteLine("\n[Press any key to close the application console terminal window...]");
            Console.ReadKey(true);
            return 0;
        }

        var rootCommand = new RootCommand("Proprietary Universal Scanner CLI Engine (scanner-api.exe)");

        var listOption = new Option<bool>("--list", "List all discovered scanning nodes across active TWAIN and WIA driver layers.");
        var statusOption = new Option<string>("--status", "Query real-time hardware status metrics using device name, GUID string, or network IP.");
        var selectOption = new Option<string>("--select", "Select specific device identifier string, GUID, or IP path targeted for execution.");
        var dpiOption = new Option<int>("--dpi", () => 300, "Resolution quality parameter setting (e.g., 150, 300, 600 DPI).");
        var colorOption = new Option<string>("--color", () => "color", "Color payload spectrum transformation constraints: [color, gray, bw]");
        var sourceOption = new Option<string>("--source", () => "flatbed", "Paper input feed ingestion selector settings: [flatbed, feeder]");
        var outputOption = new Option<string>("--output", () => "scan_output.jpg", "Destination system storage file directory path location.");

        rootCommand.AddOption(listOption);
        rootCommand.AddOption(statusOption);
        rootCommand.AddOption(selectOption);
        rootCommand.AddOption(dpiOption);
        rootCommand.AddOption(colorOption);
        rootCommand.AddOption(sourceOption);
        rootCommand.AddOption(outputOption);

        rootCommand.SetHandler(async (bool list, string status, string select, int dpi, string color, string source, string output) =>
        {
            if (list)
            {
                Console.WriteLine("[*] Enumerating active local WIA COM device registration tables...");
                try
                {
                    var wia = new WiaEngine();
                    foreach (var entry in wia.EnumerateScanners())
                    {
                        Console.WriteLine($"  -> [Protocol: WIA] Name: {entry.Key} | Registry ID: {entry.Value}");
                    }
                }
                catch (Exception ex) { Console.WriteLine($"  [-] WIA discovery fault: {ex.Message}"); }

                Console.WriteLine("[*] Pinging local Win32 TWAIN DSM registry data sources...");
                try
                {
                    using var twain = new TwainEngine();
                    foreach (var name in twain.EnumerateScanners())
                    {
                        Console.WriteLine($"  -> [Protocol: TWAIN] Driver Mapping Identity Name: {name}");
                    }
                }
                catch (Exception ex) { Console.WriteLine($"  [-] TWAIN discovery fault: {ex.Message}"); }
                return;
            }

            if (!string.IsNullOrEmpty(status))
            {
                ProtocolType protocol = ScannerFactory.DetectProtocol(status);
                Console.WriteLine($"[*] Routing status verification request to Factory engine: {protocol}");

                try
                {
                    using IScannerDevice device = ScannerFactory.CreateDevice(protocol);
                    string statusResult = await device.GetStatusAsync(status);
                    Console.WriteLine($"[+] Status Loop Response -> {statusResult}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[-] Hardware Interface Query Fault: {ex.Message}");
                }
                return;
            }

            if (!string.IsNullOrEmpty(select))
            {
                ProtocolType protocol = ScannerFactory.DetectProtocol(select);
                Console.WriteLine($"[*] Factory identified routing protocol driver layer context as: {protocol}");

                try
                {
                    using IScannerDevice device = ScannerFactory.CreateDevice(protocol);
                    bool jobSuccess = await device.ExecuteScanAsync(select, dpi, color, source, output);
                    
                    if (jobSuccess)
                    {
                        Console.WriteLine($"[+] Scan Operation Complete. Binary stream written to: {Path.GetFullPath(output)}");
                    }
                    else
                    {
                        Console.WriteLine("[-] Critical Execution Failure: Underlying protocol subsystem driver rejected the payload script command.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Critical Error processing native middleware device streams: {ex.Message}");
                }
                return;
            }

        }, listOption, statusOption, selectOption, dpiOption, colorOption, sourceOption, outputOption);

        int exitCode = await rootCommand.InvokeAsync(args);
        
        Console.WriteLine("\n[Process transaction pipeline finished. Press any key to close window...]");
        Console.ReadKey(true);
        return exitCode;
    }

    private static void RenderDeveloperDashboardWelcomeScreen()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        var description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;
        var copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "Copyright © 2026";

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==================================================================================");
        Console.WriteLine($" {product?.ToUpper()} (ENGINE ROUTER API)");
        Console.WriteLine($"    Core Module Version Architecture Build Target Pipeline: [ v{version} ]");
        Console.WriteLine($"    {copyright}");
        Console.WriteLine("==================================================================================");
        Console.ResetColor();
        Console.WriteLine($"\nDescription:\n  {description}");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n----------------------------------------------------------------------------------");
        Console.WriteLine(" SUPPORTED PARAMETERS AND PAYLOAD OPTIONS DESCRIPTION MAP");
        Console.WriteLine("----------------------------------------------------------------------------------");
        Console.ResetColor();
        Console.WriteLine("  --list             : Triggers dynamic scanning device routing tables updates for local drivers.");
        Console.WriteLine("  --status <target>  : Pulls real-time physical telemetry loop details from targeted nodes.");
        Console.WriteLine("  --select <target>  : Chooses a target node by local hardware matching name identity, GUID, or IP.");
        Console.WriteLine("  --dpi <int>        : Resolution constraints configurations pipeline data rules (Default: 300).");
        Console.WriteLine("  --color <string>   : Custom color space layout options mapping rules: [color, gray, bw].");
        Console.WriteLine("  --source <string>  : Target loading component selection criteria configuration: [flatbed, feeder].");
        Console.WriteLine("  --output <path>    : File conversion destination output file path mapping location (Default: scan_output.jpg).");
        Console.WriteLine("  --version / -v     : Outputs compiled engine metadata properties metrics layout tracking matrix scripts.");
        Console.WriteLine("  --help / -h        : Renders active dashboard documentation text interface matrices arrays.");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n----------------------------------------------------------------------------------");
        Console.WriteLine(" SYSTEM COMMAND STRINGS EXECUTION EXAMPLES SYNTAX");
        Console.WriteLine("----------------------------------------------------------------------------------");
        Console.ResetColor();

        Console.WriteLine(@"  1. Discover hardware devices across active local pipelines:
     > scanner-api.exe --list");

        Console.WriteLine(@"  2. Poll real-time status matrices of local scanners or driverless IP endpoints:
     > scanner-api.exe --status ""Canon DR-C225""
     > scanner-api.exe --status ""192.168.1.95""");

        Console.WriteLine(@"  3. Execute high-speed grayscale scanner feeder scan automation payload via raw TWAIN driver handles:
     > scanner-api.exe --select ""Fujitsu fi-7160"" --dpi 200 --color gray --source feeder --output scan_output.jpg");

        Console.WriteLine(@"  4. Execute standard driverless network scan deployment task package payload mapping over eSCL layer sockets:
     > scanner-api.exe --select ""192.168.1.120"" --dpi 300 --color color --source flatbed --output scan_output.jpg");
        
        Console.WriteLine("==================================================================================");
    }
}
