using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;
namespace Rovunet.Setup;
internal static class InstallService
{
    public static bool Available=>Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains("ReviewBackend");
    internal static bool Testing;
    internal static bool Elevated=>new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    internal static string ProgramFilesTarget=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Rovunet");
    internal static IEnumerable<string> RegisteredPaths()
    {
        foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})foreach(var hive in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser})foreach(var name in new[]{"Rovunet","RovunetPrivateReview9","RovunetPrivateReview8","RovunetPrivateReview7"})
        {
            using var root=RegistryKey.OpenBaseKey(hive,view);using var key=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\"+name);
            string? path=key?.GetValue("InstallLocation") as string;
            if(string.IsNullOrEmpty(path)){var icon=key?.GetValue("DisplayIcon") as string;if(!string.IsNullOrEmpty(icon))path=Path.GetDirectoryName(icon.Trim('"'));}
            if(!string.IsNullOrEmpty(path)&&Directory.Exists(path))yield return path;
        }
    }
    internal static bool MachinePath(string path)=>new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}.Where(p=>!string.IsNullOrEmpty(p)).Any(p=>path.StartsWith(p.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase));
    public static async Task Install(string destination,bool desktop)
    {
        destination=InstallTransaction.ValidateTarget(destination);_=InstallTransaction.IsUpgrade(destination);
        if(MachinePath(destination)&&!Elevated)
        {
            try{using var process=Process.Start(new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=true,Verb="runas",ArgumentList={"--install-elevated",destination,desktop?"1":"0",Testing?"1":"0"}})??throw new InstallProblem("failed");await process.WaitForExitAsync();if(process.ExitCode!=0)throw new InstallProblem(process.ExitCode switch{41=>"appRunning",43=>"unknownFolder",45=>"invalidPath",46=>"rollbackFailed",_=>"failed"});}
            catch(System.ComponentModel.Win32Exception e) when(e.NativeErrorCode==1223){throw new InstallProblem("uacCancelled");}return;
        }
        await InstallLocal(destination,desktop);
    }
    internal static async Task InstallLocal(string destination,bool desktop)
    {
        destination=InstallTransaction.ValidateTarget(destination);await ClosePrevious.Run(destination);
        destination=InstallTransaction.ValidateTarget(destination);string stagingRoot=Elevated?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Temp"):Path.GetTempPath();string staging=Path.Combine(stagingRoot,"Rovunet-setup-"+Guid.NewGuid().ToString("N"));
        if(Elevated){if((File.GetAttributes(stagingRoot)&FileAttributes.ReparsePoint)!=0)throw new InstallProblem("invalidPath");var security=new DirectorySecurity();security.SetAccessRuleProtection(true,false);security.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null));foreach(var sid in new[]{WellKnownSidType.BuiltinAdministratorsSid,WellKnownSidType.LocalSystemSid})security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid,null),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));new DirectoryInfo(staging).Create(security);}else Directory.CreateDirectory(staging);
        string backend=Path.Combine(staging,"backend.exe"),payload=Path.Combine(staging,"payload");
        try
        {
            using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("ReviewBackend")??throw new InstallProblem("failed"))using(var output=new FileStream(backend,FileMode.CreateNew))await input.CopyToAsync(output);
            async Task RunBackend(string arguments){using var process=Process.Start(new ProcessStartInfo(backend){UseShellExecute=false,CreateNoWindow=true,Arguments=arguments})??throw new InstallProblem("failed");await process.WaitForExitAsync();if(process.ExitCode!=0)throw new InstallProblem("failed");}
            await RunBackend("/S /D="+payload);
            if(File.ReadAllText(Path.Combine(payload,"review-install.marker"))!=InstallTransaction.Marker)throw new InstallProblem("failed");
            File.WriteAllText(Path.Combine(payload,".install-scope"),MachinePath(destination)?"1":"0");File.WriteAllText(Path.Combine(payload,".install-id"),Testing?"1":"0");
            await Task.Run(()=>InstallTransaction.Apply(payload,destination,()=>{
                using var registration=new InstallRegistration(MachinePath(destination),Testing);
                try{RunBackend("/S /REGISTER=1 /ALLUSERS="+(MachinePath(destination)?"1":"0")+" /TEST="+(Testing?"1":"0")+" /DESKTOP="+(desktop?"1":"0")+" /D="+destination).GetAwaiter().GetResult();}
                catch{try{registration.Restore();}catch{throw new InstallProblem("rollbackFailed");}throw;}
            }));
            CleanupLegacyEntries(destination);
        }
        finally
        {
            string tempRoot=Path.GetFullPath(stagingRoot).TrimEnd('\\')+"\\";
            try{if(staging.StartsWith(tempRoot,StringComparison.OrdinalIgnoreCase)){if(Directory.Exists(payload)){foreach(var file in Directory.EnumerateFiles(payload,"*",SearchOption.AllDirectories))File.Delete(file);foreach(var dir in Directory.EnumerateDirectories(payload,"*",SearchOption.AllDirectories).OrderByDescending(p=>p.Length))Directory.Delete(dir);Directory.Delete(payload);}if(File.Exists(backend))File.Delete(backend);Directory.Delete(staging);}}catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
    static void CleanupLegacyEntries(string destination)
    {
        string stable=Testing?"RovunetInstallerTest":"Rovunet";
        foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64})foreach(var registryHive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})foreach(var id in Testing?new[]{stable}:new[]{stable,"RovunetPrivateReview7","RovunetPrivateReview8","RovunetPrivateReview9"})
        {
            // HKCU uninstall keys can be shared between 32/64-bit registry views.
            if(id==stable&&registryHive==(MachinePath(destination)?RegistryHive.LocalMachine:RegistryHive.CurrentUser)&&(registryHive==RegistryHive.CurrentUser||view==RegistryView.Registry64))continue;
            try{
                using var hive=RegistryKey.OpenBaseKey(registryHive,view);string keyPath=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\"+id;
                using(var key=hive.OpenSubKey(keyPath)){string? path=key?.GetValue("InstallLocation") as string;if(string.IsNullOrEmpty(path)&&key?.GetValue("DisplayIcon") is string icon)path=Path.GetDirectoryName(icon.Trim('"'));if(string.IsNullOrEmpty(path)||!Path.GetFullPath(path).TrimEnd('\\').Equals(destination,StringComparison.OrdinalIgnoreCase))continue;}
                hive.DeleteSubKey(keyPath,false);
                if(id!=stable){
                    string title=id.Replace("RovunetPrivateReview","Rovunet Private Review ");bool machine=registryHive==RegistryHive.LocalMachine;
                    string start=Path.Combine(Environment.GetFolderPath(machine?Environment.SpecialFolder.CommonPrograms:Environment.SpecialFolder.Programs),title);
                    foreach(var file in new[]{Path.Combine(start,"Rovunet.lnk"),Path.Combine(start,"Uninstall.lnk"),Path.Combine(Environment.GetFolderPath(machine?Environment.SpecialFolder.CommonDesktopDirectory:Environment.SpecialFolder.DesktopDirectory),title+".lnk")})if(File.Exists(file))File.Delete(file);
                    if(Directory.Exists(start)&&!Directory.EnumerateFileSystemEntries(start).Any())Directory.Delete(start);
                }
            }catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}catch(IOException){}catch(ArgumentException){}catch(InvalidOperationException){}
        }
    }
}
