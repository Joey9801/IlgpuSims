using ILGPU;
using ILGPU.Runtime.Cuda;

namespace IlgpuSims
{
    class Program
    {
        static void Main(string[] args)
        {
            const int height = 1080;
            const int width = 1920;

            using var ilgpuContext = Context.Create()
                .Debug()
                .Cuda()
                .Optimize(OptimizationLevel.Debug)
                .ToContext();
            using var accelerator = ilgpuContext.CreateCudaAccelerator(0);
            
            using var window = new Window(width, height, accelerator);
            window.Run();
        }
    }
}