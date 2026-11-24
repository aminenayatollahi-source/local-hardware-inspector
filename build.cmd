@echo off
setlocal
pushd "%~dp0" || exit /b 1
set "CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
"%CSC%" /nologo /optimize+ /platform:anycpu /target:winexe /out:HardwareInspector.exe /reference:System.Management.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll HardwareInspector.cs
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%
