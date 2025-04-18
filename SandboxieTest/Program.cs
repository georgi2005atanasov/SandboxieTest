using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace MultiViberStandard
{
    class Program
    {
        // Paths to standard Sandboxie (not Plus)
        private static string SandboxiePath = Path.Combine(
            Environment.CurrentDirectory,
            "Sandboxie",
            "Resources");

        private static string SandboxiePath32 = Path.Combine(
            Environment.CurrentDirectory,
            "Sandboxie",
            "Resources");

        // Path to Start.exe (will be determined at runtime)
        private static string StartExePath;

        // Configuration storage
        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Sandboxie");

        private static readonly string AccountsFile = Path.Combine(ConfigDir, "accounts.txt");
        private static readonly string ViberPathFile = Path.Combine(ConfigDir, "viber_path.txt");

        // Path to deployment batch file
        private static readonly string DeploymentBatchFile = Path.Combine(
            Environment.CurrentDirectory,
            "DeploySandboxie.bat");

        private static readonly List<ViberAccount> ViberAccounts = new List<ViberAccount>();
        private static string _viberPath = null;

        static void Main(string[] args)
        {
            Console.Title = "Multi-Viber for Standard Sandboxie";

            if (!CheckEnvironment())
                return;

            LoadAccounts();

            bool exit = false;
            while (!exit)
            {
                DisplayMenu();

                var choice = Console.ReadLine()?.Trim();
                switch (choice)
                {
                    case "1":
                        AddAccount();
                        break;
                    case "2":
                        ListAccounts();
                        break;
                    case "3":
                        LaunchAccount();
                        break;
                    case "4":
                        DeleteAccount();
                        break;
                    case "0":
                        exit = true;
                        break;
                    default:
                        WriteColor("Invalid option. Please try again.", ConsoleColor.Red);
                        break;
                }
            }
        }

        private static void DisplayMenu()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===================================================");
            Console.WriteLine("=     MULTI-VIBER FOR STANDARD SANDBOXIE          =");
            Console.WriteLine("===================================================");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("1. Add new Viber account");
            Console.WriteLine("2. List Viber accounts");
            Console.WriteLine("3. Launch Viber account");
            Console.WriteLine("4. Delete Viber account");
            Console.WriteLine("0. Exit");
            Console.WriteLine();
            Console.Write("Enter your choice: ");
        }

        private static bool CheckEnvironment()
        {
            Console.Clear();
            Console.WriteLine("Checking environment...");

            // Create the deployment batch file if it doesn't exist
            if (!CreateDeploymentBatchFile())
            {
                WriteColor("Failed to create deployment batch file.", ConsoleColor.Red);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return false;
            }

            // Check Sandboxie installation and find paths
            if (!IsSandboxieInstalled())
            {
                WriteColor("Sandboxie components not properly installed. Installing now...", ConsoleColor.Yellow);

                if (!InstallSandboxieComponents())
                {
                    WriteColor("Failed to install Sandboxie components. Please run as administrator.", ConsoleColor.Red);
                    WriteColor("You can try running the DeploySandboxie.bat file manually as administrator.", ConsoleColor.Yellow);
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return false;
                }
            }

            // Check if Sandboxie driver and service are running
            if (!AreSandboxieServicesRunning())
            {
                WriteColor("Sandboxie services not running. Attempting to start...", ConsoleColor.Yellow);

                if (!StartSandboxieServices())
                {
                    WriteColor("Failed to start Sandboxie services. You may need to restart your computer.", ConsoleColor.Red);
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return false;
                }
            }

            // Check Viber installation
            _viberPath = FindViberPath();
            if (_viberPath == null)
            {
                WriteColor("Viber is not installed or could not be found.", ConsoleColor.Red);
                WriteColor("Please enter the full path to Viber.exe:", ConsoleColor.Yellow);
                _viberPath = Console.ReadLine();

                if (!File.Exists(_viberPath))
                {
                    WriteColor("Invalid path. Viber.exe not found.", ConsoleColor.Red);
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return false;
                }

                // Save the Viber path for future use
                SaveViberPath(_viberPath);
            }

            // Ensure config directory exists
            if (!Directory.Exists(ConfigDir))
            {
                Directory.CreateDirectory(ConfigDir);
            }

            WriteColor("Environment check complete. Sandboxie and Viber are installed and running.", ConsoleColor.Green);
            Thread.Sleep(1000);
            return true;
        }

        private static bool CreateDeploymentBatchFile()
        {
            try
            {
                if (!File.Exists(DeploymentBatchFile))
                {
                    // Read the batch file content from the embedded resource or create it directly
                    string batchFileContent = GetDeploymentBatchFileContent();
                    File.WriteAllText(DeploymentBatchFile, batchFileContent);
                    WriteColor("Created deployment batch file.", ConsoleColor.Green);
                }
                return true;
            }
            catch (Exception ex)
            {
                WriteColor($"Error creating deployment batch file: {ex.Message}", ConsoleColor.Red);
                return false;
            }
        }

        private static string GetDeploymentBatchFileContent()
        {
            // This is the content of the batch file
            return @"@echo off
setlocal enabledelayedexpansion

echo =========================================================
echo =  SANDBOXIE FULL DEPLOYMENT TOOL                       =
echo =========================================================
echo.

REM Check for administrative privileges
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: Administrative privileges required!
    echo Please right-click this batch file and select ""Run as administrator""
    pause
    exit /b 1
)

REM Set paths
set ""PROJECT_DIR=%~dp0""
set ""RESOURCES_DIR=%PROJECT_DIR%Resources""
set ""SANDBOXIE_RESOURCE_DIR=%RESOURCES_DIR%\Sandboxie""

REM Check if Resources directory exists
if not exist ""%SANDBOXIE_RESOURCE_DIR%"" (
    echo ERROR: Sandboxie resources not found at %SANDBOXIE_RESOURCE_DIR%
    echo Please run the application on a machine with Sandboxie installed first.
    pause
    exit /b 1
)

REM Check for critical files
if not exist ""%SANDBOXIE_RESOURCE_DIR%\Start.exe"" (
    echo ERROR: Start.exe not found in Sandboxie resources.
    pause
    exit /b 1
)

if not exist ""%SANDBOXIE_RESOURCE_DIR%\SbieDrv.sys"" (
    echo ERROR: SbieDrv.sys driver not found.
    pause
    exit /b 1
)

if not exist ""%SANDBOXIE_RESOURCE_DIR%\SbieSvc.exe"" (
    echo ERROR: SbieSvc.exe service not found.
    pause
    exit /b 1
)

echo.
echo === INSTALLING SANDBOXIE COMPONENTS ===
echo.

REM Step 1: Copy user-mode files to application directory
echo Copying essential Sandboxie files to application directory...

copy ""%SANDBOXIE_RESOURCE_DIR%\Start.exe"" ""%PROJECT_DIR%"" /Y
echo - Copied Start.exe

copy ""%SANDBOXIE_RESOURCE_DIR%\SbieDll.dll"" ""%PROJECT_DIR%"" /Y
echo - Copied SbieDll.dll

copy ""%SANDBOXIE_RESOURCE_DIR%\SbieMsg.dll"" ""%PROJECT_DIR%"" /Y
echo - Copied SbieMsg.dll

copy ""%SANDBOXIE_RESOURCE_DIR%\SboxHostDll.dll"" ""%PROJECT_DIR%"" /Y
echo - Copied SboxHostDll.dll

REM Step 2: Create program directory for service components
set ""PROGRAM_DIR=%ProgramFiles%\MultiViberSandboxie""

echo Creating service directory: %PROGRAM_DIR%
if not exist ""%PROGRAM_DIR%"" mkdir ""%PROGRAM_DIR%""

REM Step 3: Copy service components
echo Copying service components...
copy ""%SANDBOXIE_RESOURCE_DIR%\SbieSvc.exe"" ""%PROGRAM_DIR%\"" /Y
echo - Copied SbieSvc.exe

copy ""%SANDBOXIE_RESOURCE_DIR%\SbieCtrl.exe"" ""%PROGRAM_DIR%\"" /Y
echo - Copied SbieCtrl.exe

copy ""%SANDBOXIE_RESOURCE_DIR%\SbieIni.exe"" ""%PROGRAM_DIR%\"" /Y
echo - Copied SbieIni.exe

copy ""%SANDBOXIE_RESOURCE_DIR%\SbieDll.dll"" ""%PROGRAM_DIR%\"" /Y
echo - Copied SbieDll.dll

copy ""%SANDBOXIE_RESOURCE_DIR%\SbieMsg.dll"" ""%PROGRAM_DIR%\"" /Y
echo - Copied SbieMsg.dll

copy ""%SANDBOXIE_RESOURCE_DIR%\SbieDrv.sys"" ""%PROGRAM_DIR%\"" /Y
echo - Copied SbieDrv.sys

REM Step 4: Install the driver
echo Installing Sandboxie driver...

REM Check if driver service already exists
sc query SbieDrv >nul 2>&1
if %errorLevel% equ 0 (
    echo Sandboxie driver service already exists. Attempting to start...
    sc start SbieDrv
    if %errorLevel% equ 0 (
        echo Driver service started successfully.
    ) else (
        echo WARNING: Failed to start existing driver service.
        goto alternativeDriverStart
    )
) else (
    REM Create the driver service with demand start (not auto)
    sc create SbieDrv type= kernel binPath= ""%PROGRAM_DIR%\SbieDrv.sys"" DisplayName= ""Sandboxie Driver"" start= demand group= ""System Reserved""
    if %errorLevel% neq 0 (
        echo WARNING: Failed to create driver service, will try alternative method...
        goto alternativeDriverInstall
    )

    REM Start the driver
    sc start SbieDrv
    if %errorLevel% neq 0 (
        echo WARNING: Failed to start driver service, will try alternative method...
        goto alternativeDriverInstall
    )

    echo Driver installed successfully
)
goto installService

:alternativeDriverStart
REM Alternative method to start the driver if initial start fails
echo Trying alternative driver start method...
sc start SbieDrv
if %errorLevel% neq 0 (
    echo WARNING: Could not start driver automatically.
    echo You may need to restart your computer to complete the installation.
    echo After restart, run the application and check Sandboxie status.
)
goto installService

:alternativeDriverInstall
echo Trying alternative driver installation method...

REM Create the registry entries manually
reg add ""HKLM\SYSTEM\CurrentControlSet\Services\SbieDrv"" /v Type /t REG_DWORD /d 1 /f
reg add ""HKLM\SYSTEM\CurrentControlSet\Services\SbieDrv"" /v Start /t REG_DWORD /d 3 /f
reg add ""HKLM\SYSTEM\CurrentControlSet\Services\SbieDrv"" /v ErrorControl /t REG_DWORD /d 1 /f
reg add ""HKLM\SYSTEM\CurrentControlSet\Services\SbieDrv"" /v ImagePath /t REG_EXPAND_SZ /d ""system32\drivers\SbieDrv.sys"" /f
reg add ""HKLM\SYSTEM\CurrentControlSet\Services\SbieDrv"" /v DisplayName /t REG_SZ /d ""Sandboxie Driver"" /f
reg add ""HKLM\SYSTEM\CurrentControlSet\Services\SbieDrv"" /v Group /t REG_SZ /d ""System Reserved"" /f

REM Copy driver to system32\drivers
copy ""%PROGRAM_DIR%\SbieDrv.sys"" ""%SystemRoot%\system32\drivers\"" /Y
echo - Copied SbieDrv.sys to system drivers directory

REM Try to start the driver again
sc start SbieDrv
if %errorLevel% neq 0 (
    echo WARNING: Could not start driver automatically.
    echo You may need to restart your computer to complete the installation.
    echo After restart, run the application and check Sandboxie status.
)

:installService
REM Step 5: Install the service
echo Installing Sandboxie service...

REM Check if service already exists
sc query SbieSvc >nul 2>&1
if %errorLevel% equ 0 (
    echo Sandboxie service already exists. Attempting to start...
    sc start SbieSvc
    if %errorLevel% equ 0 (
        echo Service started successfully.
    ) else (
        echo WARNING: Failed to start existing service. 
        echo It will start automatically on next boot or can be started manually from Services console.
    )
) else (
    REM Create the service if it doesn't exist
    sc create SbieSvc type= own start= auto binPath= ""\""""%PROGRAM_DIR%\SbieSvc.exe""\"""" DisplayName= ""Sandboxie Service"" depend= SbieDrv
    if %errorLevel% neq 0 (
        echo ERROR: Failed to create service
        pause
        exit /b 1
    )

    REM Start the service
    sc start SbieSvc
    if %errorLevel% neq 0 (
        echo WARNING: Failed to start service. It will start automatically on next boot.
        echo Or try to start it manually from Services console.
    ) else (
        echo Service installed successfully
    )
)

REM Step 6: Create local Sandboxie.ini
echo Creating local Sandboxie.ini...
echo [GlobalSettings] > ""%PROJECT_DIR%Sandboxie.ini""
echo FileRootPath=%PROJECT_DIR%Sandbox\%%SANDBOX%% >> ""%PROJECT_DIR%Sandboxie.ini""
echo KeyRootPath=Registry::HKEY_USERS\Sandbox_%%USER%%_%%SANDBOX%% >> ""%PROJECT_DIR%Sandboxie.ini""
echo IpcRootPath=\Sandbox\%%USER%%\%%SANDBOX%% >> ""%PROJECT_DIR%Sandboxie.ini""
echo.>> ""%PROJECT_DIR%Sandboxie.ini""
echo [DefaultBox]>> ""%PROJECT_DIR%Sandboxie.ini""
echo Enabled=y>> ""%PROJECT_DIR%Sandboxie.ini""

REM Step 7: Create the sandbox directory
echo Creating sandbox directory...
if not exist ""%PROJECT_DIR%Sandbox"" mkdir ""%PROJECT_DIR%Sandbox""
if not exist ""%PROJECT_DIR%Sandbox\DefaultBox"" mkdir ""%PROJECT_DIR%Sandbox\DefaultBox""

echo.
echo ===== SANDBOXIE DEPLOYMENT COMPLETE! =====
echo.
echo Sandboxie components have been installed:
echo - Application files: %PROJECT_DIR%
echo - System components: %PROGRAM_DIR%
echo.
echo Note: If the services did not start, you may need to restart your computer.
echo.
echo.
pause
exit /b 0";
        }

        private static bool InstallSandboxieComponents()
        {
            try
            {
                WriteColor("Running Sandboxie deployment script...", ConsoleColor.Yellow);

                // Run the batch file as administrator
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = DeploymentBatchFile,
                    UseShellExecute = true,
                    Verb = "runas" // This requests admin privileges
                };

                Process process = Process.Start(startInfo);
                process.WaitForExit();

                // Check if the batch file was successful
                bool success = process.ExitCode == 0;

                if (success)
                {
                    WriteColor("Sandboxie components installed successfully.", ConsoleColor.Green);
                    // Give services a moment to initialize
                    Thread.Sleep(2000);
                    // Refresh paths
                    StartExePath = FindStartExePath();
                }
                else
                {
                    WriteColor("Sandboxie component installation failed.", ConsoleColor.Red);
                }

                return success && IsSandboxieInstalled();
            }
            catch (Exception ex)
            {
                WriteColor($"Error installing Sandboxie components: {ex.Message}", ConsoleColor.Red);
                return false;
            }
        }

        private static bool AreSandboxieServicesRunning()
        {
            try
            {
                bool driverRunning = false;
                bool serviceRunning = false;

                // Check if SbieDrv service is running
                try
                {
                    using (ServiceController sbieDrv = new ServiceController("SbieDrv"))
                    {
                        driverRunning = sbieDrv.Status == ServiceControllerStatus.Running;
                        WriteColor($"SbieDrv status: {sbieDrv.Status}", ConsoleColor.DarkGray);
                    }
                }
                catch (Exception ex)
                {
                    WriteColor($"Error checking SbieDrv service: {ex.Message}", ConsoleColor.DarkGray);
                }

                // Check if SbieSvc service is running
                try
                {
                    using (ServiceController sbieSvc = new ServiceController("SbieSvc"))
                    {
                        serviceRunning = sbieSvc.Status == ServiceControllerStatus.Running;
                        WriteColor($"SbieSvc status: {sbieSvc.Status}", ConsoleColor.DarkGray);
                    }
                }
                catch (Exception ex)
                {
                    WriteColor($"Error checking SbieSvc service: {ex.Message}", ConsoleColor.DarkGray);
                }

                return driverRunning && serviceRunning;
            }
            catch (Exception ex)
            {
                WriteColor($"Error checking Sandboxie services: {ex.Message}", ConsoleColor.Red);
                // Service not found or cannot be queried
                return false;
            }
        }

        private static bool EnsureSandboxieServicesRunning()
        {
            if (AreSandboxieServicesRunning())
            {
                return true;
            }

            return StartSandboxieServices();
        }

        private static bool StartSandboxieServices()
        {
            try
            {
                WriteColor("Attempting to start Sandboxie services...", ConsoleColor.Yellow);

                bool success = true;

                // Try to start SbieDrv service
                try
                {
                    using (ServiceController sbieDrv = new ServiceController("SbieDrv"))
                    {
                        if (sbieDrv.Status != ServiceControllerStatus.Running)
                        {
                            sbieDrv.Start();
                            sbieDrv.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                            WriteColor("Sandboxie driver started successfully.", ConsoleColor.Green);
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteColor($"Failed to start Sandboxie driver: {ex.Message}", ConsoleColor.Red);
                    success = false;
                }

                // Try to start SbieSvc service
                try
                {
                    using (ServiceController sbieSvc = new ServiceController("SbieSvc"))
                    {
                        if (sbieSvc.Status != ServiceControllerStatus.Running)
                        {
                            sbieSvc.Start();
                            sbieSvc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                            WriteColor("Sandboxie service started successfully.", ConsoleColor.Green);
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteColor($"Failed to start Sandboxie service: {ex.Message}", ConsoleColor.Red);
                    success = false;
                }

                if (!success)
                {
                    WriteColor("Some Sandboxie services could not be started. Attempting to run deployment script again...", ConsoleColor.Yellow);
                    return InstallSandboxieComponents();
                }

                return true;
            }
            catch (Exception ex)
            {
                WriteColor($"Error starting Sandboxie services: {ex.Message}", ConsoleColor.Red);
                return false;
            }
        }

        private static bool IsSandboxieInstalled()
        {
            // Find the Start.exe path
            StartExePath = FindStartExePath();
            return !string.IsNullOrEmpty(StartExePath);
        }

        private static string FindStartExePath()
        {
            // Check for 64-bit version
            string start64 = Path.Combine(SandboxiePath, "Start.exe");

            // Check for 32-bit version
            string start32 = Path.Combine(SandboxiePath32, "Start.exe");

            // Check in current directory (where the batch file may have copied it)
            string startCurrent = Path.Combine(Environment.CurrentDirectory, "Start.exe");

            // Check in Program Files
            string startProgFiles = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "MultiViberSandboxie", "Start.exe");

            // Check in standard Sandboxie location
            string startSandboxie = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Sandboxie", "Start.exe");

            if (File.Exists(startCurrent))
            {
                return startCurrent;
            }
            else if (File.Exists(start64))
            {
                return start64;
            }
            else if (File.Exists(start32))
            {
                return start32;
            }
            else if (File.Exists(startProgFiles))
            {
                return startProgFiles;
            }
            else if (File.Exists(startSandboxie))
            {
                return startSandboxie;
            }

            return null;
        }

        private static string FindViberPath()
        {
            // First check if we have a saved path
            if (File.Exists(ViberPathFile))
            {
                try
                {
                    string savedPath = File.ReadAllText(ViberPathFile).Trim();
                    if (File.Exists(savedPath))
                        return savedPath;
                }
                catch
                {
                    // Ignore errors, continue searching
                }
            }

            // Check common Viber installation paths
            string[] possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Viber", "Viber.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Viber", "Viber.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Viber", "Viber.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Viber", "Viber.exe")
            };

            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        private static void SaveViberPath(string path)
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                    Directory.CreateDirectory(ConfigDir);

                File.WriteAllText(ViberPathFile, path);
            }
            catch (Exception ex)
            {
                WriteColor($"Failed to save Viber path: {ex.Message}", ConsoleColor.Red);
            }
        }

        private static void AddAccount()
        {
            Console.Clear();
            Console.WriteLine("=== ADD NEW VIBER ACCOUNT ===");
            Console.WriteLine();

            Console.Write("Enter a name for this Viber account: ");
            string accountName = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(accountName))
            {
                WriteColor("Account name cannot be empty.", ConsoleColor.Red);
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            // Check if the account already exists
            if (ViberAccounts.Any(a => a.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase)))
            {
                WriteColor($"An account with the name '{accountName}' already exists.", ConsoleColor.Red);
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            // Create a sanitized sandbox name from the account name
            // Ensure the sandbox name contains only alphanumeric characters and has no spaces
            string sandboxName = "Viber_" + new string(accountName.Where(c => char.IsLetterOrDigit(c)).ToArray());

            // Make sure sandbox name is not empty and doesn't contain spaces
            if (string.IsNullOrWhiteSpace(sandboxName) || sandboxName.Length <= 6)
            {
                sandboxName = "Viber_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            }

            // Create sandbox configuration entry
            Console.WriteLine($"Creating sandbox configuration for {accountName}...");

            try
            {
                // Create the sandbox by directly editing Sandboxie.ini
                string sandboxieIniPath = Path.Combine(Path.GetDirectoryName(StartExePath), "Sandboxie.ini");

                if (!File.Exists(sandboxieIniPath))
                {
                    // If Sandboxie.ini doesn't exist in the Start.exe directory, check the current directory
                    sandboxieIniPath = Path.Combine(Environment.CurrentDirectory, "Sandboxie.ini");

                    if (!File.Exists(sandboxieIniPath))
                    {
                        // Look in the system location
                        sandboxieIniPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                            "Sandboxie", "Sandboxie.ini");

                        if (!File.Exists(sandboxieIniPath))
                        {
                            // Try in Windows directory
                            sandboxieIniPath = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                                "Sandboxie.ini");

                            if (!File.Exists(sandboxieIniPath))
                            {
                                WriteColor($"Cannot find Sandboxie.ini", ConsoleColor.Red);
                                Console.WriteLine("Press any key to continue...");
                                Console.ReadKey();
                                return;
                            }
                        }
                    }
                }

                // Read the current config
                string[] iniLines = File.ReadAllLines(sandboxieIniPath);
                List<string> newConfig = new List<string>(iniLines);

                // Check if this sandbox section already exists
                bool sectionExists = false;
                for (int i = 0; i < iniLines.Length; i++)
                {
                    if (iniLines[i].Trim().Equals($"[{sandboxName}]", StringComparison.OrdinalIgnoreCase))
                    {
                        sectionExists = true;
                        break;
                    }
                }

                // Add new sandbox section if it doesn't exist
                if (!sectionExists)
                {
                    newConfig.Add("");
                    newConfig.Add($"[{sandboxName}]");
                    newConfig.Add("Enabled=y");
                    newConfig.Add("AutoDelete=y");
                    newConfig.Add("ConfigLevel=9");
                    newConfig.Add("RecoverFolder=%Desktop%");
                    newConfig.Add("BorderColor=#00ffff");
                    // Audio and network settings
                    newConfig.Add("OpenClsid={2C941FCE-975B-59BE-A960-9A2A262853A5}");
                    newConfig.Add("OpenClsid={D3DCB472-7261-43CE-924B-0704BD730D5A}");
                    newConfig.Add("OpenClsid={A81BA6FE-A5AB-4B2B-82DE-BA558AFDF786}");
                    newConfig.Add("ClosedIpcPath=\\RPC Control\\AudioClientRpc");
                    newConfig.Add("OpenIpcPath=\\BaseNamedObjects\\");
                    newConfig.Add("OpenIpcPath=\\Sessions\\*\\BaseNamedObjects\\");

                    // Save the modified config
                    File.WriteAllLines(sandboxieIniPath, newConfig);

                    // Reload the Sandboxie configuration
                    try
                    {
                        Process reloadProcess = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = StartExePath,
                                Arguments = "/reload",
                                UseShellExecute = true,
                                CreateNoWindow = false
                            }
                        };

                        reloadProcess.Start();
                        reloadProcess.WaitForExit();

                        // Give a short delay to allow configuration to be processed
                        Thread.Sleep(1000);
                    }
                    catch (Exception ex)
                    {
                        WriteColor($"Warning: Failed to reload configuration: {ex.Message}", ConsoleColor.Yellow);
                        WriteColor("This may not affect functionality, continuing...", ConsoleColor.Yellow);
                    }
                }

                // Add the account to our list
                ViberAccounts.Add(new ViberAccount
                {
                    Name = accountName,
                    SandboxName = sandboxName
                });

                // Save the updated accounts list
                SaveAccounts();

                WriteColor($"Account '{accountName}' added successfully.", ConsoleColor.Green);
                WriteColor($"You can now launch Viber with this account using the sandbox: {sandboxName}", ConsoleColor.Cyan);
            }
            catch (Exception ex)
            {
                WriteColor($"Error creating sandbox: {ex.Message}", ConsoleColor.Red);
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private static void ListAccounts()
        {
            Console.Clear();
            Console.WriteLine("=== VIBER ACCOUNTS ===");
            Console.WriteLine();

            if (ViberAccounts.Count == 0)
            {
                WriteColor("No accounts have been added yet.", ConsoleColor.Yellow);
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            for (int i = 0; i < ViberAccounts.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {ViberAccounts[i].Name} (Sandbox: {ViberAccounts[i].SandboxName})");
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to return to the menu...");
            Console.ReadKey();
        }

        private static void LaunchAccount()
        {
            Console.Clear();
            Console.WriteLine("=== LAUNCH VIBER ACCOUNT ===");
            Console.WriteLine();

            if (ViberAccounts.Count == 0)
            {
                WriteColor("No accounts have been added yet.", ConsoleColor.Yellow);
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            // List available accounts
            for (int i = 0; i < ViberAccounts.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {ViberAccounts[i].Name}");
            }

            Console.WriteLine();
            Console.Write("Enter the number of the account to launch (or 0 to cancel): ");

            if (!int.TryParse(Console.ReadLine(), out int accountNumber) ||
                accountNumber < 1 ||
                accountNumber > ViberAccounts.Count)
            {
                if (accountNumber != 0)
                    WriteColor("Invalid selection.", ConsoleColor.Red);

                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            // Get the selected account
            var account = ViberAccounts[accountNumber - 1];

            // Launch Viber in the sandbox
            WriteColor($"Launching Viber for account: {account.Name}...", ConsoleColor.Cyan);

            try
            {
                string sandboxieIniPath = Path.Combine(Path.GetDirectoryName(StartExePath), "Sandboxie.ini");

                if (!File.Exists(sandboxieIniPath))
                {
                    // If Sandboxie.ini doesn't exist in the Start.exe directory, check the current directory
                    sandboxieIniPath = Path.Combine(Environment.CurrentDirectory, "Sandboxie.ini");
                }

                // Verify the sandbox exists in the config
                bool boxExists = false;
                if (File.Exists(sandboxieIniPath))
                {
                    string[] iniLines = File.ReadAllLines(sandboxieIniPath);
                    for (int i = 0; i < iniLines.Length; i++)
                    {
                        if (iniLines[i].Trim().Equals($"[{account.SandboxName}]", StringComparison.OrdinalIgnoreCase))
                        {
                            boxExists = true;
                            break;
                        }
                    }
                }

                if (!boxExists)
                {
                    WriteColor($"Warning: Sandbox '{account.SandboxName}' not found in configuration.", ConsoleColor.Yellow);
                    WriteColor("Attempting to recreate the sandbox configuration...", ConsoleColor.Yellow);

                    // Add the sandbox section to the config file
                    List<string> newConfig = new List<string>();
                    if (File.Exists(sandboxieIniPath))
                    {
                        newConfig.AddRange(File.ReadAllLines(sandboxieIniPath));
                    }

                    newConfig.Add("");
                    newConfig.Add($"[{account.SandboxName}]");
                    newConfig.Add("Enabled=y");
                    newConfig.Add("AutoDelete=y");
                    newConfig.Add("ConfigLevel=9");
                    newConfig.Add("RecoverFolder=%Desktop%");
                    newConfig.Add("BorderColor=#00ffff");
                    newConfig.Add("OpenClsid={2C941FCE-975B-59BE-A960-9A2A262853A5}");
                    newConfig.Add("OpenClsid={D3DCB472-7261-43CE-924B-0704BD730D5A}");
                    newConfig.Add("OpenClsid={A81BA6FE-A5AB-4B2B-82DE-BA558AFDF786}");
                    newConfig.Add("ClosedIpcPath=\\RPC Control\\AudioClientRpc");
                    newConfig.Add("OpenIpcPath=\\BaseNamedObjects\\");
                    newConfig.Add("OpenIpcPath=\\Sessions\\*\\BaseNamedObjects\\");

                    // Save the modified config
                    File.WriteAllLines(sandboxieIniPath, newConfig);

                    // Reload the configuration
                    Process reloadProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = StartExePath,
                            Arguments = "/reload",
                            UseShellExecute = true
                        }
                    };

                    reloadProcess.Start();
                    reloadProcess.WaitForExit();
                }

                // Launch Viber in the sandbox using Start.exe
                try
                {
                    // Make sure the box name doesn't have any spaces or special characters
                    string safeSandboxName = new string(account.SandboxName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

                    WriteColor($"Launching with sandbox name: {safeSandboxName}", ConsoleColor.Cyan);

                    // Debug info
                    WriteColor($"Command: {StartExePath} /box:{safeSandboxName} \"{_viberPath}\"", ConsoleColor.DarkGray);

                    Process sandboxProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = StartExePath,
                            Arguments = $"/box:{safeSandboxName} \"{_viberPath}\"",
                            UseShellExecute = true,
                            CreateNoWindow = false
                        }
                    };

                    sandboxProcess.Start();
                }
                catch (Exception ex)
                {
                    WriteColor($"Error launching sandbox process: {ex.Message}", ConsoleColor.Red);
                    WriteColor("Attempting alternative launch method...", ConsoleColor.Yellow);

                    try
                    {
                        // Try alternative method using cmd.exe
                        Process cmdProcess = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "cmd.exe",
                                Arguments = $"/c \"\"{StartExePath}\" /box:{account.SandboxName} \"{_viberPath}\"\"",
                                UseShellExecute = true,
                                CreateNoWindow = false
                            }
                        };

                        cmdProcess.Start();
                    }
                    catch (Exception cmdEx)
                    {
                        WriteColor($"Alternative launch also failed: {cmdEx.Message}", ConsoleColor.Red);
                    }
                }

                WriteColor($"Viber launched successfully in sandbox: {account.SandboxName}", ConsoleColor.Green);
                WriteColor("Note: First time setup will require phone verification.", ConsoleColor.Yellow);
            }
            catch (Exception ex)
            {
                WriteColor($"Error launching Viber: {ex.Message}", ConsoleColor.Red);
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private static void DeleteAccount()
        {
            Console.Clear();
            Console.WriteLine("=== DELETE VIBER ACCOUNT ===");
            Console.WriteLine();

            if (ViberAccounts.Count == 0)
            {
                WriteColor("No accounts have been added yet.", ConsoleColor.Yellow);
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            // List available accounts
            for (int i = 0; i < ViberAccounts.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {ViberAccounts[i].Name}");
            }

            Console.WriteLine();
            Console.Write("Enter the number of the account to delete (or 0 to cancel): ");

            if (!int.TryParse(Console.ReadLine(), out int accountNumber) ||
                accountNumber < 1 ||
                accountNumber > ViberAccounts.Count)
            {
                if (accountNumber != 0)
                    WriteColor("Invalid selection.", ConsoleColor.Red);

                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            // Get the selected account
            var account = ViberAccounts[accountNumber - 1];

            Console.Write($"Are you sure you want to delete account '{account.Name}'? (y/n): ");
            string confirmation = Console.ReadLine()?.Trim().ToLower();

            if (confirmation != "y")
            {
                WriteColor("Account deletion cancelled.", ConsoleColor.Yellow);
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            try
            {
                // Remove the sandbox configuration
                string sandboxieIniPath = Path.Combine(Path.GetDirectoryName(StartExePath), "Sandboxie.ini");

                if (!File.Exists(sandboxieIniPath))
                {
                    // If Sandboxie.ini doesn't exist in the Start.exe directory, check the current directory
                    sandboxieIniPath = Path.Combine(Environment.CurrentDirectory, "Sandboxie.ini");
                }

                if (File.Exists(sandboxieIniPath))
                {
                    string[] iniLines = File.ReadAllLines(sandboxieIniPath);
                    List<string> newConfig = new List<string>();

                    bool inTargetSection = false;

                    foreach (string line in iniLines)
                    {
                        // Check if we're entering a section header
                        if (line.Trim().StartsWith("[") && line.Trim().EndsWith("]"))
                        {
                            // If we were in the target section, we're now leaving it
                            inTargetSection = line.Trim().Equals($"[{account.SandboxName}]", StringComparison.OrdinalIgnoreCase);

                            // Only add the line if it's not our target section
                            if (!inTargetSection)
                            {
                                newConfig.Add(line);
                            }
                        }
                        else if (!inTargetSection)
                        {
                            // Only keep lines that aren't part of our target section
                            newConfig.Add(line);
                        }
                    }

                    // Save the modified config
                    File.WriteAllLines(sandboxieIniPath, newConfig);

                    // Reload the configuration
                    Process reloadProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = StartExePath,
                            Arguments = "/reload",
                            UseShellExecute = true
                        }
                    };

                    reloadProcess.Start();
                    reloadProcess.WaitForExit();
                }

                // Remove the account from our list
                ViberAccounts.RemoveAt(accountNumber - 1);

                // Save the updated accounts list
                SaveAccounts();

                WriteColor($"Account '{account.Name}' deleted successfully.", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                WriteColor($"Error deleting account: {ex.Message}", ConsoleColor.Red);
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private static void LoadAccounts()
        {
            try
            {
                if (File.Exists(AccountsFile))
                {
                    string[] lines = File.ReadAllLines(AccountsFile);
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split('|');
                        if (parts.Length >= 2)
                        {
                            ViberAccounts.Add(new ViberAccount
                            {
                                Name = parts[0],
                                SandboxName = parts[1]
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteColor($"Error loading accounts: {ex.Message}", ConsoleColor.Red);
            }
        }

        private static void SaveAccounts()
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                    Directory.CreateDirectory(ConfigDir);

                List<string> lines = new List<string>();
                foreach (var account in ViberAccounts)
                {
                    lines.Add($"{account.Name}|{account.SandboxName}");
                }

                File.WriteAllLines(AccountsFile, lines);
            }
            catch (Exception ex)
            {
                WriteColor($"Error saving accounts: {ex.Message}", ConsoleColor.Red);
            }
        }

        private static void WriteColor(string message, ConsoleColor color)
        {
            ConsoleColor oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = oldColor;
        }
    }

    /// <summary>
    /// Simple class to represent a Viber account
    /// </summary>
    class ViberAccount
    {
        public string Name { get; set; }
        public string SandboxName { get; set; }
    }
}