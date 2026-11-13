using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using Rovunet.Platform;
var mode=args[0];var directory=args[1];var report=args[2];
var identity=WindowsIdentity.GetCurrent();
var result=new Dictionary<string,object?>{
 ["mode"]=mode,["sid"]=identity.User?.Value,["impersonation"]=identity.ImpersonationLevel.ToString(),
 ["threadImpersonationSid"]=WindowsIdentity.GetCurrent(true)?.User?.Value,
 ["profilePath"]=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
 ["profileLoaded"]=Microsoft.Win32.Registry.Users.OpenSubKey(identity.User!.Value)!=null,
 ["api"]="CryptProtectData/CryptUnprotectData via PrivateStore",["nativeFlags"]=1,

};
var file=Path.Combine(directory,"fixture.dpapi");
string Hash()=>File.Exists(file)?Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))):"absent";
try{
 if(mode=="write"){PrivateStore.SaveIn(directory,"fixture.dpapi","synthetic-review09");}
 result["hashBefore"]=Hash();result["lengthBefore"]=new FileInfo(file).Length;
 result["sddl"]=new FileInfo(file).GetAccessControl().GetSecurityDescriptorSddlForm(System.Security.AccessControl.AccessControlSections.All);
 try { using var stream=File.OpenRead(file); result["fileReadable"]=true; }
 catch (UnauthorizedAccessException){result["fileReadable"]=false;throw;}
 result["decryptPassed"]=PrivateStore.LoadFrom<string>(directory,"fixture.dpapi")=="synthetic-review09";
}catch(Exception e){result["error"]=e.GetType().Name;result["decryptPassed"]=false;}
try{result["hashAfter"]=Hash();result["lengthAfter"]=new FileInfo(file).Length;}catch(UnauthorizedAccessException){result["hashAfter"]="read-denied";}
Directory.CreateDirectory(Path.GetDirectoryName(report)!);File.WriteAllText(report,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
return 0;
