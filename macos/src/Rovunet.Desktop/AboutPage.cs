using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    const string OfficialCommunityUrl="https://t.me/RovunetVPN";
    const string SupportWalletAddress="TG3Mhou3pdq9KaFgJSHjDiDx2ahhJuHvbJ";
    readonly Func<Uri,Task<bool>>? externalLinkLauncher;
    CancellationTokenSource? updateStop;
    Control? updateView;

    UpdateRoute CurrentUpdateRoute()=>new(state.State is "connected" or "engine-started",state.HttpPort);

    void ShowUpdates()
    {
        if(updateStop!=null&&updateView!=null){if(!ReferenceEquals(currentBody,updateView))NavigateFixed("MAC_UPDATE_TITLE",updateView);return;}
        var body=Page();body.Children.Add(Label(L.Text("MAC_UPDATE_HINT"),13,"Muted"));
        var beta=new CheckBox{Content=L.Text("MAC_UPDATE_INCLUDE_BETA"),IsChecked=BuildVersion.SemVer.Contains('-'),Margin=new(0,8)};body.Children.Add(beta);
        var status=Label(L.Text("MAC_UPDATE_IDLE"),13,"Muted");status.Name="UpdateStatus";body.Children.Add(status);
        var progress=new ProgressBar{Minimum=0,Maximum=100,IsVisible=false,Height=8,Name="UpdateProgress"};body.Children.Add(progress);
        Button? start=null;var cancel=ActionButton("CANCEL",()=>updateStop?.Cancel());cancel.IsEnabled=false;
        start=ActionButton("MAC_UPDATE_CHECK",()=>_=Guard(async()=>{
            if(updateStop!=null)return;using var stop=new CancellationTokenSource();updateStop=stop;
            start!.IsEnabled=false;cancel.IsEnabled=true;beta.IsEnabled=false;progress.IsVisible=true;
            var reporter=new Progress<MacUpdateProgress>(p=>{
                progress.IsIndeterminate=p.Total==0;progress.Value=p.Total>0?(double)p.Received/p.Total*100:0;
                status.Foreground=Tone("Muted");status.Text=L.Text(p.Stage=="downloading"?"MAC_UPDATE_DOWNLOADING":p.Stage=="route-changed"?"MAC_UPDATE_ROUTE_CHANGED":p.Stage=="ready"?"MAC_UPDATE_READY":"MAC_UPDATE_CHECKING")+
                    " · "+L.Text(p.ViaTunnel?"MAC_UPDATE_VIA_TUNNEL":"MAC_UPDATE_VIA_SYSTEM")+(p.Total>0?$" · {Bytes(p.Received)} / {Bytes(p.Total)}":"");
            });
            try{
                var updater=new MacUpdateClient(CurrentUpdateRoute);
                string rid=RuntimeInformation.OSArchitecture==Architecture.Arm64?"osx-arm64":RuntimeInformation.OSArchitecture==Architecture.X64?"osx-x64":"unsupported";
                var update=await updater.Check(BuildVersion.SemVer,rid,OperatingSystem.IsMacOS()?Environment.OSVersion.Version:new Version(15,0),beta.IsChecked==true,reporter,stop.Token);
                if(update==null){status.Text=L.Text("MAC_UPDATE_CURRENT");status.Foreground=Tone("Green");return;}
                status.Text=update.Version+" · "+Bytes(update.Size);
                var file=await StorageProvider.SaveFilePickerAsync(new(){Title=L.Text("MAC_UPDATE_SAVE"),SuggestedFileName=update.FileName,DefaultExtension="pkg",FileTypeChoices=[new FilePickerFileType("macOS Installer"){Patterns=["*.pkg"]}]});
                if(file==null){status.Text=L.Text("CANCELLED");return;}
                string path=file.TryGetLocalPath()??throw new SafeError("MAC_UPDATE_DOWNLOAD_FAILED");
                await updater.Download(update,path,reporter,stop.Token);
                status.Text=L.Text("MAC_UPDATE_READY")+" · "+update.Version;status.Foreground=Tone("Green");message=status.Text;messageTone="Green";UpdateStatus();
            }catch(OperationCanceledException){status.Text=L.Text("CANCELLED");}
            catch(Exception e){status.Foreground=Tone("Red");status.Text=L.Text(e is SafeError error?error.Code:"MAC_UPDATE_NETWORK_FAILED");throw;}
            finally{updateStop=null;start!.IsEnabled=true;cancel.IsEnabled=false;beta.IsEnabled=true;progress.IsIndeterminate=false;}
        }),true);start.Name="CheckMacUpdate";body.Children.Add(start);body.Children.Add(cancel);
        Navigate("MAC_UPDATE_TITLE",body);
        updateView=currentBody;
    }

    async Task CopySupportAddress()
    {
        if(Clipboard is not {} clipboard)throw new SafeError("ABOUT_COPY_FAILED");
        await clipboard.SetTextAsync(SupportWalletAddress);
        message=L.Text("COPIED");messageTone="Green";UpdateStatus();
    }

    async Task OpenOfficialCommunity()
    {
        var uri=new Uri(OfficialCommunityUrl);
        bool opened=await (externalLinkLauncher?.Invoke(uri)??Launcher.LaunchUriAsync(uri));
        if(!opened)throw new SafeError("ABOUT_LINK_FAILED");
    }

    static WriteableBitmap CreateSupportQr()
    {
        // Raw recipient address only: no amount, transfer request, or URL.
        var pixels=new ZXing.BarcodeWriterPixelData{
            Format=ZXing.BarcodeFormat.QR_CODE,
            Options=new ZXing.QrCode.QrCodeEncodingOptions{
                Width=370,Height=370,Margin=4,CharacterSet="UTF-8",
                ErrorCorrection=ZXing.QrCode.Internal.ErrorCorrectionLevel.M
            }
        }.Write(SupportWalletAddress);
        var bitmap=new WriteableBitmap(new(pixels.Width,pixels.Height),new(96,96),PixelFormat.Bgra8888,AlphaFormat.Opaque);
        using(var locked=bitmap.Lock())Marshal.Copy(pixels.Pixels,0,locked.Address,pixels.Pixels.Length);
        return bitmap;
    }

    void ShowAbout()
    {
        var page=Page();page.Children.Add(Brand());
        var version=new StackPanel{Spacing=6};
        version.Children.Add(Label(BuildVersion.Text+" · "+L.Text("MACOS_DEV"),16));
        version.Children.Add(Label(L.Text("MAC_UPDATE_CHANNEL"),12,"Muted"));
        version.Children.Add(Label(L.Text("MAC_BETA_TEST_SCOPE"),12,"Muted"));
        page.Children.Add(version);

        var community=new StackPanel{Spacing=6};
        community.Children.Add(Label(L.Text("ABOUT_COMMUNITY"),16,"Accent"));
        var link=Label(OfficialCommunityUrl,13,"Ink",true);link.Name="AboutCommunityAddress";community.Children.Add(link);
        var open=ActionButton("ABOUT_OPEN_COMMUNITY",()=>_=Guard(OpenOfficialCommunity),true);open.Name="AboutOpenCommunity";community.Children.Add(open);
        page.Children.Add(Card(community));

        var support=new StackPanel{Spacing=8};
        support.Children.Add(Label(L.Text("ABOUT_OPTIONAL_SUPPORT"),16,"Green"));
        support.Children.Add(Label(L.Text("ABOUT_SUPPORT_HINT"),13,"Muted"));
        support.Children.Add(Label(L.Text("ABOUT_SUPPORT_NETWORK"),13,"Green"));
        var address=Label(SupportWalletAddress,13,"Ink",true);address.TextWrapping=Avalonia.Media.TextWrapping.NoWrap;address.Name="AboutSupportAddress";
        support.Children.Add(new OverflowMarquee(address,30));
        var copy=ActionButton("ABOUT_COPY_ADDRESS",()=>_=Guard(CopySupportAddress),true);copy.Name="AboutCopySupportAddress";support.Children.Add(copy);

        var bitmap=CreateSupportQr();
        var qrBody=new StackPanel{Spacing=10,HorizontalAlignment=HorizontalAlignment.Stretch};
        var image=new Image{Source=bitmap,Width=222,Height=222,HorizontalAlignment=HorizontalAlignment.Center,Name="AboutSupportQr"};
        RenderOptions.SetBitmapInterpolationMode(image,BitmapInterpolationMode.None);
        qrBody.Children.Add(image);
        qrBody.Children.Add(Label("USDT · Tron / TRC20",13,"Ink",true));
        var save=ActionButton("SAVE_PNG",()=>_=Guard(async()=>{
            var file=await StorageProvider.SaveFilePickerAsync(new(){Title=L.Text("SAVE_PNG"),SuggestedFileName="Rovunet-USDT-TRC20.png",DefaultExtension="png",FileTypeChoices=[FilePickerFileTypes.ImagePng]});
            if(file==null)return;
            await using var stream=await file.OpenWriteAsync();if(stream.CanSeek)stream.SetLength(0);
            bitmap.Save(stream,PngBitmapEncoderOptions.Default);
        }));save.Name="AboutSaveSupportQr";qrBody.Children.Add(save);
        var qr=new Expander{Header=L.Text("ABOUT_SHOW_QR"),Content=qrBody,Background=Brushes.Transparent,BorderBrush=Tone("Outline"),Foreground=Tone("Ink"),HorizontalAlignment=HorizontalAlignment.Stretch,Name="AboutSupportQrExpander"};support.Children.Add(qr);
        page.Children.Add(Card(support));
        var update=ActionButton("MAC_UPDATE_CHECK",ShowUpdates);update.Name="AboutCheckUpdate";page.Children.Add(update);
        var attribution=Label("AGPL-3.0 | Based on Rovunet/SlipNet 2.5.6 contracts and logic by Anonvector; not affiliated.\nXray-core 26.10.31 | MPL-2.0\nLicenses and corresponding sources are included with the installation and source bundle.",10,"Muted");
        attribution.FlowDirection=FlowDirection.LeftToRight;attribution.TextAlignment=TextAlignment.Center;attribution.Name="AboutAttribution";page.Children.Add(attribution);
        Navigate("ABOUT",page);
    }
}
