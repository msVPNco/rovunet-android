using SkiaSharp;

string output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);
using var surface=SKSurface.Create(new SKImageInfo(640,420));var c=surface.Canvas;c.Clear(SKColor.Parse("#F4F5FC"));
using var paint=new SKPaint{IsAntialias=true};using var font=new SKFont(SKTypeface.FromFamilyName("Segoe UI",SKFontStyle.Bold),34);
paint.Color=SKColor.Parse("#20283B");c.DrawText("Rovunet",320,60,SKTextAlign.Center,font,paint);
using var subtitle=new SKFont(SKTypeface.FromFamilyName("Segoe UI"),15);paint.Color=SKColor.Parse("#68728A");c.DrawText("Ready for your Mac",320,90,SKTextAlign.Center,subtitle,paint);
paint.Color=SKColor.Parse("#E9E7F7");c.DrawRoundRect(new SKRect(72,124,268,300),26,26,paint);paint.Color=SKColor.Parse("#E1F3EB");c.DrawRoundRect(new SKRect(372,124,568,300),26,26,paint);
paint.Color=SKColor.Parse("#359475");paint.Style=SKPaintStyle.Stroke;paint.StrokeWidth=4;paint.StrokeCap=SKStrokeCap.Round;c.DrawLine(288,210,350,210,paint);c.DrawLine(338,198,350,210,paint);c.DrawLine(338,222,350,210,paint);paint.Style=SKPaintStyle.Fill;
using var instruction=new SKFont(SKTypeface.FromFamilyName("Segoe UI",SKFontStyle.Bold),18);paint.Color=SKColor.Parse("#20283B");c.DrawText("Drag Rovunet to Applications",320,351,SKTextAlign.Center,instruction,paint);paint.Color=SKColor.Parse("#68728A");c.DrawText("Then open it from Applications",320,381,SKTextAlign.Center,subtitle,paint);
using var snapshot=surface.Snapshot();using var png=snapshot.Encode(SKEncodedImageFormat.Png,100);using var file=File.Create(Path.Combine(output,"background.png"));png.SaveTo(file);
