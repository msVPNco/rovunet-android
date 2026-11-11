using Avalonia.Controls;
using SkiaSharp;

namespace Rovunet.Desktop;

public static class MenuBarMark
{
    const string Logo = "M4,0 H329 C416,0 466,57 466,148 C466,238 416,296 334,296 L476,482 H317 L178,296 H129 V482 H2 Z M127,101 H333 V201 H127 Z M0,516 H476 V534 H0 Z";

    enum Status { Disconnected, Connecting, Connected, Error }

    static readonly Lazy<WindowIcon> disconnected = new(() => Load("disconnected"));
    static readonly Lazy<WindowIcon> connecting = new(() => Load("connecting"));
    static readonly Lazy<WindowIcon> connected = new(() => Load("connected"));
    static readonly Lazy<WindowIcon> error = new(() => Load("error"));

    static Status FromState(string state) => state switch
    {
        "connected" => Status.Connected,
        "connecting" or "engine-started" or "trust" => Status.Connecting,
        "error" => Status.Error,
        _ => Status.Disconnected
    };

    public static byte[] Png(string state)
    {
        var status = FromState(state);
        var color = status switch
        {
            Status.Connected => SKColor.Parse("#30D158"),
            Status.Connecting => SKColor.Parse("#FFD60A"),
            Status.Error => SKColor.Parse("#FF453A"),
            _ => SKColors.White
        };

        using var surface = SKSurface.Create(new SKImageInfo(64, 64, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Save();
        canvas.Translate(8, 6);
        canvas.Scale(.085f);
        using var path = SKPath.ParseSvgPathData(Logo);
        path.FillType = SKPathFillType.EvenOdd;
        // Keep the white mark visible on a light menu bar.
        using var outline = new SKPaint
        {
            Color = SKColor.Parse("#182231"),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 28,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true
        };
        using var fill = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawPath(path, outline);
        canvas.DrawPath(path, fill);
        canvas.Restore();
        if (status == Status.Connected)
        {
            outline.StrokeWidth = 1.4f;
            canvas.DrawCircle(55, 53, 6, outline);
            canvas.DrawCircle(55, 53, 6, fill);
        }
        using var snapshot = surface.Snapshot();
        using var data = snapshot.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    static WindowIcon Load(string state)
    {
        using var input = new MemoryStream(Png(state));
        return new WindowIcon(input);
    }

    public static WindowIcon Icon(string state) => FromState(state) switch
    {
        Status.Connected => connected.Value,
        Status.Connecting => connecting.Value,
        Status.Error => error.Value,
        _ => disconnected.Value
    };
}
