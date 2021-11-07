using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTK.Graphics.OpenGL4;

namespace IlgpuSims
{
    // A helper class, much like Shader, meant to simplify loading textures.
    public class Texture
    {
        public readonly int TexHandle;
        public readonly int PboHandle;
        public readonly int Width;
        public readonly int Height;
        
        public int TotalLength => Width * Height * 3;
        public readonly int XStride = 0;
        public int YStride => Width;
        public const PixelFormat StorageFormat = PixelFormat.Rgb;

        public static Texture FromRawRgb(byte[] data, int width, int height)
        {
            Trace.Assert(data.Length == width * height * 3);

            int texHandle = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, texHandle);

            int pboHandle = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pboHandle);

            unsafe
            {
                fixed (byte* dataPointer = &data[0])
                {
                    GL.BufferData(
                        BufferTarget.PixelUnpackBuffer,
                        width * height * 3,
                        new IntPtr(dataPointer),
                        BufferUsageHint.DynamicDraw);
                }
            }

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int) TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int) TextureMagFilter.Linear);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,
                (int) TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
                (int) TextureWrapMode.ClampToBorder);

            return new Texture(pboHandle, texHandle, width, height);
        }

        public Texture(int pboHandle, int texHandle, int width, int height)
        {
            PboHandle = pboHandle;
            TexHandle = texHandle;
            Width = width;
            Height = height;
        }

        public void Use(TextureUnit unit)
        {
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, PboHandle);
            GL.TexImage2D(TextureTarget.Texture2D,
                0,
                PixelInternalFormat.Rgb,
                Width,
                Height,
                0,
                PixelFormat.Rgb,
                PixelType.UnsignedByte,
                IntPtr.Zero);
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2D, TexHandle);
        }
    }
}