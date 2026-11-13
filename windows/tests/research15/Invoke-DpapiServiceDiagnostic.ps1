param([Parameter(Mandatory=$true)][string]$Diagnostic,[Parameter(Mandatory=$true)][string]$Root,[Parameter(Mandatory=$true)][switch]$ConfirmIsolatedVm)
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
public static class R09PrivateDesktop {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Startup {public uint Size;public string Reserved,Desktop,Title;public uint X,Y,Width,Height,CharsX,CharsY,Fill,Flags;public ushort Show,ReservedSize;public IntPtr ReservedData,In,Out,Error;}
 [StructLayout(LayoutKind.Sequential)] struct Child {public IntPtr Process,Thread;public uint Pid,Tid;}
 [StructLayout(LayoutKind.Sequential)] struct Security {public int Size;public IntPtr Descriptor;public int Inherit;}
 [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool LogonUserW(string user,string domain,IntPtr password,uint type,uint provider,out IntPtr token);
 [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool CreateProcessWithTokenW(IntPtr token,uint logon,string application,StringBuilder command,uint flags,IntPtr environment,string directory,ref Startup startup,out Child child);
 [DllImport("userenv.dll",SetLastError=true)] static extern bool CreateEnvironmentBlock(out IntPtr environment,IntPtr token,bool inherit);
 [DllImport("userenv.dll")] static extern bool DestroyEnvironmentBlock(IntPtr environment);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
 [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetExitCodeProcess(IntPtr handle,out uint code);
 [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string text,uint revision,out IntPtr descriptor,out uint size);
 [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr memory);
 [DllImport("user32.dll")] static extern IntPtr GetProcessWindowStation();
 [DllImport("user32.dll",SetLastError=true)] static extern bool SetProcessWindowStation(IntPtr station);
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateWindowStationW(string name,uint flags,uint access,ref Security security);
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateDesktopW(string name,IntPtr device,IntPtr mode,uint flags,uint access,ref Security security);
 [DllImport("user32.dll")] static extern bool CloseWindowStation(IntPtr station);
 [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr desktop);
 static void Check(bool ok){if(!ok)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
 public static object Launch(IntPtr password,string user,string expected,string application,string arguments){
  IntPtr token;Check(LogonUserW(user,user=="LOCAL SERVICE"?"NT AUTHORITY":".",password,user=="LOCAL SERVICE"?5u:2u,0,out token));IntPtr env=IntPtr.Zero;
  try{using(var identity=new WindowsIdentity(token)){if(identity.User.Value!=expected||new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))throw new Exception("Standard token required");}
   Check(CreateEnvironmentBlock(out env,token,false));
   string stationName="RovunetFixture"+Guid.NewGuid().ToString("N");IntPtr descriptor;uint length;
   Check(ConvertStringSecurityDescriptorToSecurityDescriptorW("O:"+WindowsIdentity.GetCurrent().User.Value+"G:BAD:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GA;;;"+expected+")",1,out descriptor,out length));
   IntPtr station=IntPtr.Zero,desktop=IntPtr.Zero,previous=GetProcessWindowStation();
   try{
    var security=new Security{Size=Marshal.SizeOf(typeof(Security)),Descriptor=descriptor};
    station=CreateWindowStationW(stationName,0,0xf037f,ref security);Check(station!=IntPtr.Zero);
    Check(SetProcessWindowStation(station));
    try{desktop=CreateDesktopW("Default",IntPtr.Zero,IntPtr.Zero,0,0xf01ff,ref security);Check(desktop!=IntPtr.Zero);}finally{Check(SetProcessWindowStation(previous));}
    var start=new Startup{Size=(uint)Marshal.SizeOf(typeof(Startup)),Desktop=stationName+"\\Default"};Child child;
    Check(CreateProcessWithTokenW(token,1,application,new StringBuilder("\""+application+"\" "+arguments),0x08000400,env,System.IO.Path.GetDirectoryName(application),ref start,out child));
    try{bool exited=WaitForSingleObject(child.Process,45000)==0;uint code;Check(GetExitCodeProcess(child.Process,out code));return new {pid=child.Pid,exited=exited,exitCode=code,privateDesktop=true};}finally{CloseHandle(child.Thread);CloseHandle(child.Process);}
   }finally{if(desktop!=IntPtr.Zero)CloseDesktop(desktop);if(station!=IntPtr.Zero)CloseWindowStation(station);LocalFree(descriptor);}
  }finally{if(env!=IntPtr.Zero)DestroyEnvironmentBlock(env);CloseHandle(token);}
 }
}
'@
$ErrorActionPreference='Stop'
if(!$ConfirmIsolatedVm -or [Security.Principal.WindowsIdentity]::GetCurrent().User.Value -ne 'S-1-5-18'){throw 'Explicit isolated SYSTEM diagnostic context required; this script does not elevate or install a service'}
$m=Get-CimInstance Win32_ComputerSystem
if(($m.Manufacturer+' '+$m.Model) -notmatch '(?i)virtual machine|vmware|virtualbox|qemu|kvm|parallels'){throw 'Physical host refused'}
if(![IO.Path]::IsPathFullyQualified($Root) -or (Test-Path -LiteralPath $Root)){throw 'Fresh absolute synthetic root required'}
New-Item -ItemType Directory -Path $Root,(Join-Path $Root results)|Out-Null
$acl=Get-Acl $Root;$acl.SetAccessRuleProtection($true,$false);$acl.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-18'))
foreach($sid in 'S-1-5-18','S-1-5-32-544'){$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid),'FullControl','ContainerInherit,ObjectInherit','None','Allow'))}
$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new('S-1-5-19'),'ReadAndExecute','ContainerInherit,ObjectInherit','None','Allow'));Set-Acl $Root $acl
& icacls.exe (Join-Path $Root results) /grant '*S-1-5-19:(OI)(CI)(M)'|Out-Null
$results=Join-Path $Root results;$rows=@()
foreach($writer in 'native','dotnet'){foreach($reader in 'native','dotnet'){
 $id=$writer+'_'+$reader
 & $Diagnostic protect $writer $Root $id $results
 if($LASTEXITCODE -ne 0){throw 'Synthetic SYSTEM protect failed; retain reports'}
 $blob=Join-Path $Root "$id.blob"
 & icacls.exe $blob /grant '*S-1-5-19:(R)'|Out-Null
 $hash=(Get-FileHash -LiteralPath $blob).Hash
 $arguments='unprotect '+$reader+' "'+$Root+'" '+$id+' "'+$results+'"'
 $launch=[R09PrivateDesktop]::Launch([IntPtr]::Zero,'LOCAL SERVICE','S-1-5-19',$Diagnostic,$arguments)
 $path=Join-Path $results "$id.unprotect.$reader.result.json"
 if(!(Test-Path -LiteralPath $path)){throw 'LocalService inner report missing; no decrypt result may be inferred'}
 $rows+=@{case=$id;launch=$launch;protect=(Get-Content (Join-Path $results "$id.protect.$writer.result.json") -Raw|ConvertFrom-Json);unprotect=(Get-Content $path -Raw|ConvertFrom-Json);blobUnchanged=(Get-FileHash $blob).Hash -eq $hash}
}}
@{scope='SYSTEM to LocalService only, explicit read grant, no ordinary-user regression claim';rows=$rows}|ConvertTo-Json -Depth 16|Set-Content (Join-Path $Root service-diagnostic.json)

