using System.IO;
using System.Reflection;

namespace Rovunet.Setup;

internal sealed class InstallProblem(string code) : Exception(code) { public string Code {get;} = code; }
internal static class InstallTransaction
{
    internal const string Marker="Rovunet-Windows";
    internal static string ValidateTarget(string target)
    {
        target=Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
        var forbidden=new[]{Path.GetPathRoot(target),Environment.GetFolderPath(Environment.SpecialFolder.Windows),Environment.GetFolderPath(Environment.SpecialFolder.System),Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)};
        if(forbidden.Where(p=>!string.IsNullOrEmpty(p)).Any(p=>target.Equals(p!.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)) || target.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows)+"\\",StringComparison.OrdinalIgnoreCase))throw new InstallProblem("invalidPath");
        for(var folder=new DirectoryInfo(target);folder!=null;folder=folder.Parent)if(folder.Exists&&(folder.Attributes&FileAttributes.ReparsePoint)!=0)throw new InstallProblem("invalidPath");
        return target;
    }
    internal static bool IsUpgrade(string target)
    {
        if(!Directory.Exists(target)||!Directory.EnumerateFileSystemEntries(target).Any())return false;
        string marker=Path.Combine(target,"review-install.marker");
        bool known=File.Exists(marker)&&new[]{Marker,"Rovunet-private-review7","Rovunet-private-review8","Rovunet-private-review9"}.Contains(File.ReadAllText(marker).Trim());
        if(!known)known=InstallService.RegisteredPaths().Any(p=>Path.GetFullPath(p).TrimEnd('\\').Equals(target,StringComparison.OrdinalIgnoreCase));
        try{if(!known||AssemblyName.GetAssemblyName(Path.Combine(target,"Rovunet.dll")).Name!="Rovunet")throw new InstallProblem("unknownFolder");}
        catch(InstallProblem){throw;}catch{throw new InstallProblem("unknownFolder");}return true;
    }
    internal static void Apply(string source,string destination,Action register,Action<int>? copied=null)
    {
        destination=ValidateTarget(destination);bool upgrade=IsUpgrade(destination);
        var files=Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories).Select(f=>(Source:f,Relative:Path.GetRelativePath(source,f))).ToArray();
        string backup=Path.Combine(Path.GetDirectoryName(destination)!,".Rovunet-backup-"+Guid.NewGuid().ToString("N"));
        var changed=new List<(string Target,string? Backup,FileAttributes Attributes)>();var locks=new List<FileStream>();bool retainBackup=false;
        try
        {
            // Test every existing destination before changing any file. An active/locked app must close first.
            foreach(var file in files)
            {
                var target=Child(destination,file.Relative);
                for(var d=new DirectoryInfo(Path.GetDirectoryName(target)!);d!=null&&d.FullName.StartsWith(destination,StringComparison.OrdinalIgnoreCase);d=d.Parent)if(d.Exists&&(d.Attributes&FileAttributes.ReparsePoint)!=0)throw new InstallProblem("invalidPath");
                if(File.Exists(target)) {var attributes=File.GetAttributes(target);if((attributes&FileAttributes.ReparsePoint)!=0)throw new InstallProblem("invalidPath");try{locks.Add(File.Open(target,FileMode.Open,(attributes&FileAttributes.ReadOnly)!=0?FileAccess.Read:FileAccess.ReadWrite,FileShare.None));}catch(IOException){throw new InstallProblem("appRunning");}}
            }
            foreach(var handle in locks)handle.Dispose();locks.Clear();
            Directory.CreateDirectory(destination);Directory.CreateDirectory(backup);int count=0;
            foreach(var file in files.OrderBy(f=>f.Relative=="review-install.marker"?1:0))
            {
                string target=Child(destination,file.Relative);string? saved=null;FileAttributes attributes=FileAttributes.Normal;Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if(File.Exists(target)){attributes=File.GetAttributes(target);saved=Child(backup,file.Relative);Directory.CreateDirectory(Path.GetDirectoryName(saved)!);File.Copy(target,saved,false);}
                changed.Add((target,saved,attributes));string temporary=target+".rovunet-"+Guid.NewGuid().ToString("N");
                try{File.Copy(file.Source,temporary,false);if(saved!=null)ClearReadOnly(target);File.Move(temporary,target,true);}finally{if(File.Exists(temporary)){ClearReadOnly(temporary);File.Delete(temporary);}}
                copied?.Invoke(++count);
            }
            register();
        }
        catch(Exception failure)
        {
            if(failure is InstallProblem{Code:"rollbackFailed"})retainBackup=true;
            foreach(var handle in locks)handle.Dispose();locks.Clear();bool rollback=true;
            foreach(var file in changed.AsEnumerable().Reverse())try{
                if(File.Exists(file.Target))ClearReadOnly(file.Target);
                if(file.Backup!=null){string restore=file.Target+".restore-"+Guid.NewGuid().ToString("N");try{File.Copy(file.Backup,restore,false);File.Move(restore,file.Target,true);File.SetAttributes(file.Target,file.Attributes);}finally{if(File.Exists(restore)){ClearReadOnly(restore);File.Delete(restore);}}}
                else if(File.Exists(file.Target))File.Delete(file.Target);
            }catch{rollback=false;}
            if(!rollback){retainBackup=true;throw new InstallProblem("rollbackFailed");}
            if(failure is IOException)throw new InstallProblem("failed");throw;
        }
        finally
        {
            foreach(var handle in locks)handle.Dispose();
            // Never recursively delete a user path. Remove only our known backup files/empty directories.
            if(Directory.Exists(backup))
            {
                if(!retainBackup){foreach(var file in changed)try{if(file.Backup!=null&&File.Exists(file.Backup)){ClearReadOnly(file.Backup);File.Delete(file.Backup);}}catch{}foreach(var dir in Directory.EnumerateDirectories(backup,"*",SearchOption.AllDirectories).OrderByDescending(p=>p.Length))try{Directory.Delete(dir);}catch{}try{Directory.Delete(backup);}catch{}}
            }
        }
    }
    static void ClearReadOnly(string path){var attributes=File.GetAttributes(path);if((attributes&FileAttributes.ReadOnly)!=0)File.SetAttributes(path,attributes&~FileAttributes.ReadOnly);}
    static string Child(string root,string relative)
    {
        string path=Path.GetFullPath(Path.Combine(root,relative));if(!path.StartsWith(root.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase))throw new InstallProblem("invalidPath");return path;
    }
}
