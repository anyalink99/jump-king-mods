using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WorldsmithExtension
{
    internal static class PackedAssets
    {
        static int Integer(BinaryReader reader)
        {
            uint value = 0;
            for (int shift = 0; shift < 35; shift += 7)
            {
                byte part = reader.ReadByte();
                if (shift == 28 && part > 7)
                    throw new InvalidDataException("Invalid XNB integer.");
                value |= (uint)(part & 127) << shift;
                if ((part & 128) == 0 && value <= Int32.MaxValue)
                    return (int)value;
            }

            throw new InvalidDataException("Invalid XNB integer.");
        }

        internal static string Extract(string source, string destination, bool collision)
        {
            using (var reader = new BinaryReader(File.OpenRead(source)))
            {
                if (reader.BaseStream.Length < 10 || reader.BaseStream.Length > 512 * 1024 * 1024)
                    throw new InvalidDataException("Unsupported XNB size.");
                if (Encoding.ASCII.GetString(reader.ReadBytes(3)) != "XNB")
                    throw new InvalidDataException("Invalid XNB signature.");
                byte platform = reader.ReadByte(), version = reader.ReadByte(), flags = reader.ReadByte();
                if (platform != (byte)'w' || version != 5)
                    throw new NotSupportedException("Only Windows XNB version 5 is currently recoverable.");
                if ((flags & 0xC0) != 0)
                    throw new NotSupportedException("Compressed XNB is preserved; its source assets cannot currently be recovered.");
                if (reader.ReadInt32() != reader.BaseStream.Length)
                    throw new InvalidDataException("XNB length mismatch.");
                if (Integer(reader) != 1)
                    throw new NotSupportedException("This XNB reader table is not supported for recovery.");
                string type = reader.ReadString();
                reader.ReadInt32();
                if (Integer(reader) != 0 || Integer(reader) != 1)
                    throw new NotSupportedException("Shared XNB resources are not supported for recovery.");
                if (type.Split(',')[0] == "Microsoft.Xna.Framework.Content.Texture2DReader")
                {
                    Texture(reader, Path.ChangeExtension(destination, ".png"), collision);
                    return "PNG recovered; original source layers and color-keyed pixels cannot be restored.";
                }

                if (type.Split(',')[0] == "Microsoft.Xna.Framework.Content.SoundEffectReader")
                {
                    Sound(reader, Path.ChangeExtension(destination, ".wav"));
                    return "WAV recovered; original encoding and loop metadata are not reconstructed.";
                }

                throw new NotSupportedException("Compiled " + type.Split(',')[0] + " is preserved without an editable source.");
            }
        }

        static void Texture(BinaryReader reader, string output, bool collision)
        {
            int format = reader.ReadInt32(), width = reader.ReadInt32(), height = reader.ReadInt32(), mipmaps = reader.ReadInt32();
            if (format != 0)
                throw new NotSupportedException("Only uncompressed RGBA textures are currently recoverable.");
            if (width < 1 || height < 1 || (long)width * height > 64 * 1024 * 1024 || mipmaps < 1)
                throw new InvalidDataException("Invalid texture dimensions.");
            int length = reader.ReadInt32();
            if (length != checked(width * height * 4) || reader.BaseStream.Length - reader.BaseStream.Position < length)
                throw new InvalidDataException("Truncated texture.");
            if (File.Exists(output))
                return;
            byte[] rgba = reader.ReadBytes(length);
            for (int i = 0; i < rgba.Length; i += 4)
            {
                byte red = rgba[i];
                rgba[i] = rgba[i + 2];
                rgba[i + 2] = red;
                int alpha = rgba[i + 3];
                if (!collision && alpha > 0 && alpha < 255)
                    for (int c = 0; c < 3; c++)
                        rgba[i + c] = (byte)Math.Min(255, (rgba[i + c] * 255 + alpha / 2) / alpha);
            }

            using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    for (int y = 0; y < height; y++)
                        Marshal.Copy(rgba, y * width * 4, IntPtr.Add(data.Scan0, y * data.Stride), width * 4);
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }

                Collision.Png(bitmap, output);
            }
        }

        static void Sound(BinaryReader reader, string output)
        {
            int formatLength = reader.ReadInt32();
            if (formatLength < 16 || formatLength > 4096)
                throw new InvalidDataException("Invalid sound format.");
            byte[] format = reader.ReadBytes(formatLength);
            int length = reader.ReadInt32();
            if (length < 0 || reader.BaseStream.Length - reader.BaseStream.Position < length)
                throw new InvalidDataException("Truncated audio.");
            if (File.Exists(output))
                return;
            byte[] data = reader.ReadBytes(length);
            Files.Atomic(output, stream =>
            {
                var writer = new BinaryWriter(stream);
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(20 + formatLength + (formatLength & 1) + length + (length & 1));
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(formatLength);
                writer.Write(format);
                if ((formatLength & 1) != 0)
                    writer.Write((byte)0);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(length);
                writer.Write(data);
                if ((length & 1) != 0)
                    writer.Write((byte)0);
                writer.Flush();
            });
        }
    }
}
