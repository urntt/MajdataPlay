using System;
using System.Text;

#nullable enable
namespace MajdataPlay.Tests.Game.Misc.Support
{
    /// <summary>
    /// The first bytes of media files, enough for the chart importer to tell their format; they are not playable media.
    /// </summary>
    internal static class MediaFixture
    {
        /// <summary>Gets an MP3 that starts with an ID3 tag.</summary>
        public static byte[] Mp3 => Join(new byte[] { 0x49, 0x44, 0x33, 0x03, 0, 0, 0, 0, 0, 0 }, Repeat(new byte[] { 0xFF, 0xFB, 0x90, 0x00 }, 16));

        /// <summary>Gets an Ogg Vorbis stream.</summary>
        public static byte[] Ogg => Join(Encoding.ASCII.GetBytes("OggS"), new byte[] { 0, 2 }, new byte[22], Encoding.ASCII.GetBytes("\u0001vorbis"), new byte[40]);

        /// <summary>Gets a PNG image.</summary>
        public static byte[] Png => Join(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, new byte[56]);

        /// <summary>Gets a JPEG image.</summary>
        public static byte[] Jpeg => Join(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, new byte[60]);

        /// <summary>Gets a WebP image.</summary>
        public static byte[] WebP => Join(Encoding.ASCII.GetBytes("RIFF"), new byte[] { 0x24, 0, 0, 0 }, Encoding.ASCII.GetBytes("WEBPVP8 "), new byte[48]);

        /// <summary>Gets an M4A (AAC in MPEG-4) file, which the game does not play.</summary>
        public static byte[] M4a => Join(new byte[] { 0, 0, 0, 0x20 }, Encoding.ASCII.GetBytes("ftypM4A "), new byte[4], Encoding.ASCII.GetBytes("M4A mp42isom"), new byte[40]);

        /// <summary>Gets an MPEG-4 video.</summary>
        public static byte[] Mp4 => Join(new byte[] { 0, 0, 0, 0x20 }, Encoding.ASCII.GetBytes("ftypisom"), new byte[] { 0, 0, 2, 0 }, Encoding.ASCII.GetBytes("isomiso2mp41"), new byte[40]);

        /// <summary>Gets the start of a RAR archive, which the game cannot unpack.</summary>
        public static byte[] Rar => Join(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00 }, new byte[56]);

        /// <summary>
        /// Gets a small PhiEdit (PEC) chart: 150 BPM, one judge line and four notes, with the CRLF line ends PhiEdit writes.
        /// </summary>
        public static string PecChart => string.Join("\r\n", new[]
        {
            "0", "bp 0.000 150.000", "cp 0 0.000 1024.00 200.00", "cd 0 0.000 0.000", "ca 0 0.000 255", "cv 0 0.000 10.000",
            "n1 0 1.00 0.00 1 0", "# 1.000", "& 1.00", "n2 0 3.00 4.00 512.00 1 0", "# 1.000", "& 1.00",
            "n3 0 5.00 256.00 1 0", "# 1.000", "& 1.00", "n4 0 6.00 -256.00 1 0", "# 1.000", "& 1.00", "",
        });

        /// <summary>Joins byte arrays.</summary>
        /// <param name="parts">The parts.</param>
        /// <returns>The joined bytes.</returns>
        private static byte[] Join(params byte[][] parts)
        {
            var length = 0;
            foreach (var part in parts)
            {
                length += part.Length;
            }
            var result = new byte[length];
            var offset = 0;
            foreach (var part in parts)
            {
                Buffer.BlockCopy(part, 0, result, offset, part.Length);
                offset += part.Length;
            }
            return result;
        }

        /// <summary>Repeats bytes.</summary>
        /// <param name="bytes">The bytes.</param>
        /// <param name="count">How many times.</param>
        /// <returns>The repeated bytes.</returns>
        private static byte[] Repeat(byte[] bytes, int count)
        {
            var parts = new byte[count][];
            for (var i = 0; i < count; i++)
            {
                parts[i] = bytes;
            }
            return Join(parts);
        }
    }
}
