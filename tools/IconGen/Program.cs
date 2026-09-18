// One-shot icon generator for ElevenLabsStudio.
//
// Produces a single .ico file (src/ElevenLabsStudio/Assets/app.ico)
// containing six PNG-encoded frames at 16, 32, 48, 64, 128, 256 px
// so the OS picks the right size for every shell surface (taskbar,
// Alt-Tab, title bar, shortcut, etc.). PNG-in-ICO has been supported
// since Vista so we can use System.Drawing.Common to render the
// vector shapes and skip the BITMAPINFOHEADER bookkeeping.
//
// Run with:    dotnet run --project tools/IconGen
// Produces:    src/ElevenLabsStudio/Assets/app.ico

using System.Buffers.Binary;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: IconGen <output.ico>");
    return 1;
}
var outputPath = args[0];
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);

int[] sizes = { 16, 32, 48, 64, 128, 256 };
var frames = new List<(int Size, byte[] Png)>(sizes.Length);

foreach (var size in sizes)
{
    using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(bmp))
    {
        g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        // Rounded-rect background, indigo (App.Accent #4F46E5).
        var radius = Math.Max(2f, size * 0.18f);
        using (var bgPath = RoundedRect(0, 0, size, size, radius))
        using (var bgBrush = new SolidBrush(Color.FromArgb(0xFF, 0x4F, 0x46, 0xE5)))
        {
            g.FillPath(bgBrush, bgPath);
        }

        // White "E" mark, sized to ~58% of the icon's shorter side.
        using (var fg = new SolidBrush(Color.White))
        {
            // Measure for the size-1 frame and let GDI+ scale.
            var emSize = size * 0.62f;
            // GDI+ uses 1pt = 1.333px, so em-size in points = emSize / 1.333.
            // (System.Drawing.Font takes points.)
            var fontSizePt = emSize * 72f / bmp.HorizontalResolution;
            using var font = new Font("Segoe UI", fontSizePt, FontStyle.Bold, GraphicsUnit.Point);
            using var fmt = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            var rect = new RectangleF(0, 0, size, size);
            g.DrawString("E", font, fg, rect, fmt);
        }
    }
    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    frames.Add((size, ms.ToArray()));
}

// Compose the multi-image .ico manually. Layout:
//   ICONDIR header (6 bytes)
//   ICONDIRENTRY × N (16 bytes each)
//   N × image payload (PNG bytes from above)
using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
Span<byte> header = stackalloc byte[6];
BinaryPrimitives.WriteUInt16LittleEndian(header[0..2], 0);          // reserved
BinaryPrimitives.WriteUInt16LittleEndian(header[2..4], 1);          // type = 1 (icon)
BinaryPrimitives.WriteUInt16LittleEndian(header[4..6], (ushort)frames.Count);
output.Write(header);

long imageDataOffset = 6 + 16L * frames.Count;
Span<byte> entry = stackalloc byte[16];
foreach (var (size, png) in frames)
{
    // 0 means 256 (per the legacy 1-byte width/height contract).
    byte w = size >= 256 ? (byte)0 : (byte)size;
    byte h = size >= 256 ? (byte)0 : (byte)size;
    entry[0] = w;
    entry[1] = h;
    entry[2] = 0;                    // colorCount
    entry[3] = 0;                    // reserved
    BinaryPrimitives.WriteUInt16LittleEndian(entry[4..6], 1);       // planes
    BinaryPrimitives.WriteUInt16LittleEndian(entry[6..8], 32);     // bitCount
    BinaryPrimitives.WriteInt32LittleEndian(entry[8..12], png.Length);
    BinaryPrimitives.WriteInt32LittleEndian(entry[12..16], (int)imageDataOffset);
    output.Write(entry);
    imageDataOffset += png.Length;
}
foreach (var (_, png) in frames)
{
    output.Write(png);
}

Console.WriteLine($"Wrote {outputPath} ({frames.Count} frames, {new FileInfo(outputPath).Length} bytes).");
return 0;

static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
{
    var path = new GraphicsPath();
    var d = 2 * r;
    path.AddArc(x, y, d, d, 180, 90);
    path.AddArc(x + w - d, y, d, d, 270, 90);
    path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
    path.AddArc(x, y + h - d, d, d, 90, 90);
    path.CloseFigure();
    return path;
}