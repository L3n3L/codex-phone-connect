using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace CodexPhoneConnect;

public sealed record ConnectionEntryViewModel(NetworkConnection Connection, BitmapSource QrCode)
{
    public static ConnectionEntryViewModel Create(NetworkConnection connection)
    {
        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(connection.Url, QRCodeGenerator.ECCLevel.M);
        var pngBytes = new PngByteQRCode(qrData).GetGraphic(8);

        using var stream = new MemoryStream(pngBytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();

        return new ConnectionEntryViewModel(connection, image);
    }
}
