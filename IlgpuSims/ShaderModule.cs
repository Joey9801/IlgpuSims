using System;
using System.IO;
using System.Text;
using OpenTK.Graphics.OpenGL4;

namespace IlgpuSims
{
    public sealed class ShaderModule : IDisposable
    {
        public readonly int Handle;

        private bool _disposed;

        private ShaderModule(int vert, int frag)
        {
            Handle = GL.CreateProgram();
            GL.AttachShader(Handle, vert);
            GL.AttachShader(Handle, frag);

            GL.LinkProgram(Handle);

            // No longer need the individual vert/frag shaders after linking
            GL.DetachShader(Handle, vert);
            GL.DetachShader(Handle, frag);
            GL.DeleteShader(vert);
            GL.DeleteShader(frag);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public static ShaderModule FromPaths(string vertPath, string fragPath)
        {
            static string ReadFile(string path)
            {
                using var reader = new StreamReader(path, Encoding.UTF8);
                return reader.ReadToEnd();
            }

            return FromSourceStrings(ReadFile(vertPath), ReadFile(fragPath));
        }

        public static ShaderModule FromSourceStrings(string vertSource, string fragSource)
        {
            var vert = GetShader(vertSource, ShaderType.VertexShader);
            var frag = GetShader(fragSource, ShaderType.FragmentShader);
            return new ShaderModule(vert, frag);
        }

        public void Use()
        {
            GL.UseProgram(Handle);
        }

        public int GetAttribLocation(string attribName)
        {
            return GL.GetAttribLocation(Handle, attribName);
        }

        private static int GetShader(string source, ShaderType type)
        {
            var shader = GL.CreateShader(type);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);

            var infoLog = GL.GetShaderInfoLog(shader);
            if (infoLog != "")
            {
                Console.Error.Write(infoLog);
                throw new Exception("Failed to compile shader");
            }

            return shader;
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                GL.DeleteProgram(Handle);
                _disposed = true;
            }
        }

        ~ShaderModule()
        {
            GL.DeleteProgram(Handle);
        }
    }
}