using System.Threading.Tasks;

namespace Ryujinx.Common.Utilities
{

    public static class TaskUtils
    {

        public static T WaitAndReturn<T>(this Task<T> task)
        {
            task.Wait();
            return task.Result;
        }

    }

}
