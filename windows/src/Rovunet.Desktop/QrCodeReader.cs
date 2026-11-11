using Rovunet.Core;
using ZXing;
using ZXing.Common;
namespace Rovunet.Desktop;

public static class QrCodeReader
{
    public static string Read(byte[] pixels, int width, int height)
    {
        if (width < 1 || height < 1 || (long)width * height > 16000000 || pixels.Length != (long)width * height * 4) throw new SafeError("QR_TOO_LARGE");
        var result = new BarcodeReaderGeneric { AutoRotate = true, Options = new DecodingOptions { TryHarder = true, PossibleFormats = [BarcodeFormat.QR_CODE] } }.Decode(pixels, width, height, RGBLuminanceSource.BitmapFormat.BGRA32);
        return !string.IsNullOrEmpty(result?.Text) ? result.Text : throw new SafeError("QR_NOT_FOUND");
    }
}
