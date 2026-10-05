namespace HR28.Infrastructure.Helpers;

/// <summary>
/// Checks that an uploaded photo really is a JPEG or PNG (by its content, not its name)
/// and rebuilds it without metadata: EXIF (which can hold the GPS position where the
/// photo was taken), XMP, IPTC, comments and text chunks, and without anything appended
/// after the image. HR28 must not collect precise locations.
/// </summary>
public static class PhotoSanitizer
{
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";

    /// <summary>The cleaned image and its type, or null with a plain-language reason.</summary>
    public static (byte[]? Content, string? ContentType, string? Error) Clean(byte[] data)
    {
        try
        {
            if (data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
                return CleanJpeg(data) is { } jpeg ? (jpeg, Jpeg, null) : (null, null, Unreadable);

            if (data.Length > 8 && data.AsSpan(0, 8).SequenceEqual(PngSignature))
                return CleanPng(data) is { } png ? (png, Png, null) : (null, null, Unreadable);
        }
        catch (IndexOutOfRangeException)
        {
            return (null, null, Unreadable);
        }
        catch (ArgumentOutOfRangeException)
        {
            return (null, null, Unreadable);
        }

        return (null, null, "Please choose a photo in JPG or PNG format.");
    }

    private const string Unreadable = "That photo could not be read. Please choose another JPG or PNG photo.";

    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    // ---------- JPEG ----------

    private static byte[]? CleanJpeg(byte[] data)
    {
        using var output = new MemoryStream(data.Length);
        output.Write(data, 0, 2); // SOI

        var pos = 2;
        var sawImage = false;

        while (pos < data.Length)
        {
            if (data[pos] != 0xFF)
                return null;

            // Skip fill bytes.
            while (pos < data.Length && data[pos] == 0xFF)
                pos++;

            var marker = data[pos++];

            if (marker == 0xD9) // EOI: stop here, dropping anything appended after the image
            {
                output.Write(new byte[] { 0xFF, 0xD9 });
                return sawImage ? output.ToArray() : null;
            }

            if (marker == 0x01 || marker is >= 0xD0 and <= 0xD7) // markers without a length
            {
                output.Write(new byte[] { 0xFF, marker });
                continue;
            }

            var length = (data[pos] << 8) | data[pos + 1];

            if (length < 2 || pos + length > data.Length)
                return null;

            // APP1 = EXIF / XMP, APP13 = IPTC / Photoshop, COM = comment: dropped.
            var drop = marker is 0xE1 or 0xED or 0xFE;

            if (!drop)
            {
                output.Write(new byte[] { 0xFF, marker });
                output.Write(data, pos, length);
            }

            pos += length;

            if (marker != 0xDA) // not the start of image data
                continue;

            // Image data runs until the next real marker (0xFF followed by something
            // other than 0x00 or a restart marker). Progressive JPEGs have several scans.
            sawImage = true;
            var end = pos;

            while (end < data.Length - 1)
            {
                if (data[end] == 0xFF)
                {
                    var next = data[end + 1];

                    if (next == 0x00 || next is >= 0xD0 and <= 0xD7)
                    {
                        end += 2;
                        continue;
                    }

                    break;
                }

                end++;
            }

            output.Write(data, pos, end - pos);
            pos = end;
        }

        return null; // no end-of-image marker
    }

    // ---------- PNG ----------

    // Image data and colour information only; text, EXIF and time chunks are dropped.
    private static readonly HashSet<string> KeptPngChunks = new(StringComparer.Ordinal)
    {
        "IHDR", "PLTE", "IDAT", "IEND", "tRNS", "gAMA", "cHRM", "sRGB", "iCCP", "sBIT", "pHYs", "bKGD"
    };

    private static byte[]? CleanPng(byte[] data)
    {
        using var output = new MemoryStream(data.Length);
        output.Write(PngSignature);

        var pos = 8;
        var first = true;

        while (pos + 12 <= data.Length)
        {
            var length = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];

            if (length < 0 || pos + 12 + length > data.Length)
                return null;

            var type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);

            if (first && type != "IHDR")
                return null;

            first = false;

            if (KeptPngChunks.Contains(type))
                output.Write(data, pos, 12 + length);

            pos += 12 + length;

            if (type == "IEND") // stop here, dropping anything appended after the image
                return output.ToArray();
        }

        return null; // no end chunk
    }
}
