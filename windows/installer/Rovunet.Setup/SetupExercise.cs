using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;
namespace Rovunet.Setup;
internal static class SetupExercise
{
    public static async Task<int> Run(string report)
    {
        if(!Path.IsPathFullyQualified(report)||File.Exists(report))return 12;
        string root=Path.GetDirectoryName(report)!,destination=Path.Combine(root,"service install with spaces");
        var checks=new Dictionary<string,bool>();string? failure=null;
        string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        string Registration(){using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Rovunet");return key==null?"absent":JsonSerializer.Serialize(key.GetValueNames().Order().ToDictionary(n=>n,n=>key.GetValue(n)));}
        string ownerRegistration=Registration();
        string JournalHash(){string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Rovunet","proxy-recovery.dpapi");return File.Exists(path)?Hash(path):"absent";}
        string RecoveryArm(){using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\RunOnce");return key?.GetValue("!RovunetRecovery")?.ToString()??"absent";}
        string journalBefore=JournalHash(),armBefore=RecoveryArm();
        try
        {
            checks["program_files_target_resolves_to_product_subfolder"]=InstallService.ProgramFilesTarget==Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Rovunet")&&InstallService.MachinePath(InstallService.ProgramFilesTarget);
            checks["custom_user_target_has_user_scope"]=!InstallService.MachinePath(destination);
            if(!InstallService.Elevated){using var helper=Process.Start(new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,ArgumentList={"--install-elevated",InstallService.ProgramFilesTarget,"0","1"}})!;await helper.WaitForExitAsync();checks["elevated_helper_refuses_without_admin_token"]=helper.ExitCode==44;}
            var window=new SetupWindow("fa",true,false,destination,closeAppWhenLastWindow:false);window.Show();window.UpdateLayout();window.InstallButton.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            bool success=await window.Installation.Task.WaitAsync(TimeSpan.FromMinutes(3));window.UpdateLayout();
            checks["wpf_install_button_completed"]=success;checks["dedicated_success_page_with_finish"]=window.Completed&&window.InstallButton.Content?.ToString()=="پایان";
            checks["default_desktop_shortcut_actually_created"]=File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Rovunet Installer Test.lnk"));
            App.SaveImage((System.Windows.FrameworkElement)window.Content,Path.Combine(root,"setup-completed-fa-dark.png"));window.InstallButton.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));checks["finish_closes_installer"]=!window.IsVisible;if(window.IsVisible)window.Close();
            checks["payload_installed_to_space_path"]=File.Exists(Path.Combine(destination,"Rovunet.exe"));checks["test_registration_scope"]=File.ReadAllText(Path.Combine(destination,".install-id"))=="1"&&File.ReadAllText(Path.Combine(destination,".install-scope"))=="0";
            using(var hive=RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,RegistryView.Registry64))using(var key=hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\RovunetInstallerTest"))checks["stable_registration_survives_legacy_cleanup"]=key?.GetValue("InstallLocation")?.ToString()==destination;
            string keep=Path.Combine(destination,"user-note.txt");File.WriteAllText(keep,"keep-fixture");string dll=Hash(Path.Combine(destination,"Rovunet.dll"));
            // Simulate the recognized Review9 marker, then upgrade through the actual embedded service.
            File.WriteAllText(Path.Combine(destination,"review-install.marker"),"Rovunet-private-review9");
            await InstallService.Install(destination,false);checks["recognized_review9_upgrades_in_place"]=Hash(Path.Combine(destination,"Rovunet.dll"))==dll&&File.ReadAllText(Path.Combine(destination,"review-install.marker"))==InstallTransaction.Marker;checks["upgrade_preserves_unknown_user_file"]=File.ReadAllText(keep)=="keep-fixture";
            TransactionChecks(root,destination,checks);
            // A locked secondary DLL must prevent ANY uninstaller deletion, including the main EXE.
            using(var locked=File.Open(Path.Combine(destination,"Rovunet.Core.dll"),FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                using var refusal=Process.Start(new ProcessStartInfo(Path.Combine(destination,"Uninstall.exe")){UseShellExecute=false,CreateNoWindow=true,Arguments="/S _?="+destination})!;await refusal.WaitForExitAsync();checks["uninstall_preflights_all_owned_files"]=refusal.ExitCode==33&&File.Exists(Path.Combine(destination,"Rovunet.exe"))&&Hash(Path.Combine(destination,"Rovunet.dll"))==dll;
            }
            using var uninstall=Process.Start(new ProcessStartInfo(Path.Combine(destination,"Uninstall.exe")){UseShellExecute=false,CreateNoWindow=true,Arguments="/S _?="+destination})!;await uninstall.WaitForExitAsync();checks["uninstall_completed"]=uninstall.ExitCode==0&&!File.Exists(Path.Combine(destination,"Rovunet.dll"));checks["uninstall_preserves_user_file"]=File.ReadAllText(keep)=="keep-fixture";
            using var testKey=RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,RegistryView.Registry64).OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\RovunetInstallerTest");checks["test_registration_removed"]=testKey==null;checks["owner_registration_unchanged"]=Registration()==ownerRegistration;checks["owner_recovery_journal_unchanged"]=JournalHash()==journalBefore;checks["owner_recovery_arm_unchanged"]=RecoveryArm()==armBefore;
        }
        catch(Exception e){failure=e is InstallProblem p?p.Code:e.GetType().Name;checks["exercise_completed"]=false;}
        File.WriteAllText(report,JsonSerializer.Serialize(new{scope="real WPF install, in-place upgrade, file/registration rollback, locked uninstall, Finish; separate test identity; no normal app launch",failure,checks,passed=checks.Count>0&&checks.Values.All(v=>v)},new JsonSerializerOptions{WriteIndented=true}));return checks.Values.All(v=>v)?0:1;
    }
    static void TransactionChecks(string root,string installed,Dictionary<string,bool> checks)
    {
        string source=Path.Combine(root,"transaction-source"),target=Path.Combine(root,"transaction-target");Directory.CreateDirectory(source);Directory.CreateDirectory(target);
        File.Copy(Path.Combine(installed,"Rovunet.dll"),Path.Combine(source,"Rovunet.dll"));File.Copy(Path.Combine(installed,"Rovunet.dll"),Path.Combine(target,"Rovunet.dll"));
        File.WriteAllText(Path.Combine(source,"review-install.marker"),InstallTransaction.Marker);File.WriteAllText(Path.Combine(target,"review-install.marker"),"Rovunet-private-review9");File.WriteAllText(Path.Combine(source,"a.txt"),"new");File.WriteAllText(Path.Combine(target,"a.txt"),"old");File.WriteAllText(Path.Combine(source,"new.txt"),"new-only");File.WriteAllText(Path.Combine(target,"user.txt"),"keep");
        File.SetAttributes(Path.Combine(target,"a.txt"),File.GetAttributes(Path.Combine(target,"a.txt"))|FileAttributes.ReadOnly);
        bool Same()=>File.ReadAllText(Path.Combine(target,"a.txt"))=="old"&&(File.GetAttributes(Path.Combine(target,"a.txt"))&FileAttributes.ReadOnly)!=0&&File.ReadAllText(Path.Combine(target,"review-install.marker"))=="Rovunet-private-review9"&&!File.Exists(Path.Combine(target,"new.txt"))&&File.ReadAllText(Path.Combine(target,"user.txt"))=="keep";
        // An executing image can permit an exclusive READ handle while still prohibiting writes.
        // Use a separately owned native fixture; never stop the user's normal Rovunet.
        string native=Path.Combine(target,"Rovunet.exe"),replacement=Path.Combine(source,"Rovunet.exe");
        File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"ping.exe"),native);File.WriteAllText(replacement,"fixture replacement");
        using(var image=Process.Start(new ProcessStartInfo(native){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,ArgumentList={"-n","15","127.0.0.1"}})!)
        {
            try{InstallTransaction.Apply(source,target,()=>{});checks["executing_image_refused_before_mutation"]=false;}catch(InstallProblem p){checks["executing_image_refused_before_mutation"]=p.Code=="appRunning"&&Same();}
            finally{if(!image.HasExited)image.Kill();image.WaitForExit();}
        }
        File.Delete(native);File.Delete(replacement);
        using(var locked=File.Open(Path.Combine(target,"a.txt"),FileMode.Open,FileAccess.Read,FileShare.Read)){try{InstallTransaction.Apply(source,target,()=>{});checks["locked_upgrade_refused_before_mutation"]=false;}catch(InstallProblem p){checks["locked_upgrade_refused_before_mutation"]=p.Code=="appRunning"&&Same();}}
        try{InstallTransaction.Apply(source,target,()=>{},n=>{if(n==3)throw new IOException("injected");});checks["copy_failure_rolls_back_exact_files"]=false;}catch(InstallProblem){checks["copy_failure_rolls_back_exact_files"]=Same();}
        try{InstallTransaction.Apply(source,target,()=>throw new IOException("injected registration"));checks["registration_failure_rolls_back_files"]=false;}catch(InstallProblem){checks["registration_failure_rolls_back_files"]=Same();}
        string unknown=Path.Combine(root,"unknown-folder");Directory.CreateDirectory(unknown);File.WriteAllText(Path.Combine(unknown,"note.txt"),"keep");try{InstallTransaction.Apply(source,unknown,()=>{});checks["unknown_folder_refused"]=false;}catch(InstallProblem p){checks["unknown_folder_refused"]=p.Code=="unknownFolder"&&File.ReadAllText(Path.Combine(unknown,"note.txt"))=="keep";}
        foreach(string path in new[]{Path.GetPathRoot(target)!,Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.Windows)})try{InstallTransaction.ValidateTarget(path);checks["critical_path_"+checks.Count]=false;}catch(InstallProblem){checks["critical_path_"+checks.Count]=true;}
        InstallTransaction.Apply(source,target,()=>{});checks["transaction_upgrade_commits"]=File.ReadAllText(Path.Combine(target,"a.txt"))=="new"&&File.ReadAllText(Path.Combine(target,"new.txt"))=="new-only"&&File.ReadAllText(Path.Combine(target,"user.txt"))=="keep";
        checks["read_only_owned_file_upgraded_and_backup_cleaned"]=(File.GetAttributes(Path.Combine(target,"a.txt"))&FileAttributes.ReadOnly)==0&&!Directory.EnumerateDirectories(root,".Rovunet-backup-*").Any();
        string junction=Path.Combine(root,"junction-fixture");if(Directory.Exists(junction)){try{InstallTransaction.ValidateTarget(junction);checks["junction_target_refused"]=false;}catch(InstallProblem p){checks["junction_target_refused"]=p.Code=="invalidPath";}}
        // Capture and restore the actual test registration and shortcut contents.
        string regPath=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\RovunetInstallerTest";using(var snapshot=new InstallRegistration(false,true)){using(var hive=RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,RegistryView.Registry64))using(var key=hive.CreateSubKey(regPath))key.SetValue("InjectedFailure","fixture");snapshot.Restore();}
        using(var hive=RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,RegistryView.Registry64))using(var key=hive.OpenSubKey(regPath))checks["registration_snapshot_restores_values"]=key?.GetValue("InjectedFailure")==null&&key?.GetValue("InstallLocation")?.ToString()==installed;
    }
}
