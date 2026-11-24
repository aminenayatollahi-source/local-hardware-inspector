# Local Hardware Inspector

A portable, English-language Windows utility that reads the local PC's processor models, physical cores and logical processors per socket, Windows-visible physical memory and current available memory, and display adapter models and driver versions. Double-click **HardwareInspector.exe** and use **Refresh** for a new reading.

Requires Windows with .NET Framework 4.5 or later and Windows Management Instrumentation (WMI) for device enumeration. Intended for Windows 10/11. No installer, agent, server, internet connection, external packages or elevation is required by the application. The graphical view does not save a report. Hardware and filesystem access are subject to local Windows policy.

## Export from PowerShell

Use a new filename in a writable directory. The executable refuses to replace an existing report.

```powershell
$p = Start-Process -FilePath .\HardwareInspector.exe -ArgumentList '--json "hardware.json"' -Wait -PassThru
$p.ExitCode
Get-Content .\hardware.json -Raw | ConvertFrom-Json
```

Exit codes: 0 completed, 1 capture or output error, 2 invalid arguments. An individual unavailable reading is recorded in a successful snapshot. Memory values in JSON are bytes. In the window they are GiB. Total memory is the amount visible to Windows, which can differ from installed DIMM capacity. Available memory varies with workload. Device groups distinguish an unavailable WMI query (`Available: false`) from a successful query that returned no devices. Unavailable individual fields are null. Display enumeration can include integrated, virtual or remote display adapters. The program does not infer dedicated VRAM or physical GPU count from adapter names.

## Build from source

Run `build.cmd` in this directory on Windows with the .NET Framework C# compiler. The build produces HardwareInspector.exe using only Windows and .NET Framework libraries. The data sources are Win32_Processor, Win32_VideoController, and GlobalMemoryStatusEx. The report contains no computer or user names, serial numbers, IP or MAC addresses, or private paths.
