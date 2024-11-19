using System.Threading;

namespace Ryujinx.HLE.HOS.Kernel.Threading
{
    class KCriticalSection
    {
        private readonly KernelContext _context;
        private readonly Lock _lock;
        private int _recursionCount;

        public Lock Lock => _lock;

        public KCriticalSection(KernelContext context)
        {
            _context = context;
            _lock = new Lock();
        }

        public void Enter()
        {
            _lock.Enter();
            _recursionCount++;
        }

        public void Leave()
        {
            if (_recursionCount == 0)
            {
                return;
            }

            if (--_recursionCount == 0)
            {
                ulong scheduledCoresMask = KScheduler.SelectThreads(_context);

                _lock.Exit();

                KThread currentThread = KernelStatic.GetCurrentThread();
                bool isCurrentThreadSchedulable = currentThread != null && currentThread.IsSchedulable;
                if (isCurrentThreadSchedulable)
                {
                    KScheduler.EnableScheduling(_context, scheduledCoresMask);
                }
                else
                {
                    KScheduler.EnableSchedulingFromForeignThread(_context, scheduledCoresMask);

                    // If the thread exists but is not schedulable, we still want to suspend
                    // it if it's not runnable. That allows the kernel to still block HLE threads
                    // even if they are not scheduled on guest cores.
                    if (currentThread != null && !currentThread.IsSchedulable && currentThread.Context.Running)
                    {
                        currentThread.SchedulerWaitEvent.WaitOne();
                    }
                }
            }
            else
            {
                _lock.Exit();
            }
        }
    }
}
