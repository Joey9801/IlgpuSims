using System;
using System.Runtime.InteropServices;
using ILGPU.Runtime.Cuda;

namespace IlgpuSims
{
    public enum CudaGraphicsMapFlags
    {
        None = 0,
        ReadOnly = 1,
        WriteDiscard = 2
    }

    public static class CudaGlInterop
    {
        [DllImport("nvcuda", EntryPoint = "cuGraphicsGLRegisterImage")]
        public static extern CudaError RegisterImage(
            out IntPtr resource,
            int image,
            int target,
            uint flags);

        [DllImport("nvcuda", EntryPoint = "cuGraphicsGLRegisterBuffer")]
        public static extern CudaError RegisterBuffer(
            out IntPtr resource,
            int buffer,
            uint flags);

        [DllImport("nvcuda", EntryPoint = "cuGraphicsMapResources")]
        public static extern CudaError MapResources(
            int count,
            IntPtr resources,
            IntPtr stream);

        [DllImport("nvcuda", EntryPoint = "cuGraphicsUnmapResources")]
        public static extern CudaError UnmapResources(
            int count,
            IntPtr resources,
            IntPtr stream);
        
        [DllImport("nvcuda", EntryPoint = "cuGraphicsResourceGetMappedPointer")]
        public static extern CudaError GetMappedPointer(
            out IntPtr devicePtr,
            out int size,
            IntPtr resource);
    }

    public class CudaGlInteropBuffer
    {
        
    }
}