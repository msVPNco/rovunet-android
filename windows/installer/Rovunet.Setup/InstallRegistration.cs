using System.IO;
using Microsoft.Win32;
namespace Rovunet.Setup;

// A failed shortcut/registry registration must roll back along with the payload.
internal sealed class InstallRegistration : IDisposable
{
    readonly RegistryKey hive;
    readonly string keyPath;
    readonly bool existed;
    readonly Dictionary<string,(object Value,RegistryValueKind Kind)> values=[];
    readonly Dictionary<string,byte[]?> links=[];
    internal InstallRegistration(bool machine,bool testing)
    {
        string id=testing?"RovunetInstallerTest":"Rovunet",title=testing?"Rovunet Installer Test":"Rovunet";
        hive=RegistryKey.OpenBaseKey(machine?RegistryHive.LocalMachine:RegistryHive.CurrentUser,RegistryView.Registry64);
        keyPath=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\"+id;
        using var key=hive.OpenSubKey(keyPath);existed=key!=null;
        if(key!=null)foreach(var name in key.GetValueNames())values[name]=(key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames)!,key.GetValueKind(name));
        string start=Path.Combine(Environment.GetFolderPath(machine?Environment.SpecialFolder.CommonPrograms:Environment.SpecialFolder.Programs),title);
        foreach(string path in new[]{Path.Combine(start,"Rovunet.lnk"),Path.Combine(start,"Uninstall.lnk"),Path.Combine(Environment.GetFolderPath(machine?Environment.SpecialFolder.CommonDesktopDirectory:Environment.SpecialFolder.DesktopDirectory),title+".lnk")})links[path]=File.Exists(path)?File.ReadAllBytes(path):null;
    }
    internal void Restore()
    {
        if(!existed)hive.DeleteSubKey(keyPath,false);
        else{using var key=hive.CreateSubKey(keyPath);foreach(var name in key.GetValueNames())if(!values.ContainsKey(name))key.DeleteValue(name,false);foreach(var(name,value)in values)key.SetValue(name,value.Value,value.Kind);}
        foreach(var(path,bytes)in links){if(bytes!=null)File.WriteAllBytes(path,bytes);else if(File.Exists(path))File.Delete(path);}
    }
    public void Dispose()=>hive.Dispose();
}
