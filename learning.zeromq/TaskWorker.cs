using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroMQ;

namespace learning.zeromq
{
    public class TaskWorker
    {
        public class WorkerContext
        {
            public WorkerContext()
            {
                this.NotifyStart = new ManualResetEvent(false);
            }

            public ZmqContext QueueContext { get; set; }
            public ManualResetEvent NotifyStart { get; protected set; }
        }

        protected WorkerContext _WorkerContext;
        protected Action<WorkerContext> _WorkerThreadEntryPoint;
        protected Thread _WorkerThread;

        public TaskWorker(ZmqContext queueContext, Action<WorkerContext> workerThreadEntryPoint)
        {
            _WorkerThreadEntryPoint = workerThreadEntryPoint;
            _WorkerContext = new WorkerContext() { QueueContext = queueContext };
            _WorkerThread = new Thread( () => _WorkerThreadEntryPoint(_WorkerContext) );
        }

        public void Start()
        {
            _WorkerThread.Start();
        }

        public void WaitStart()
        {
            _WorkerContext.NotifyStart.WaitOne();
        }

        public void Join()
        {
            _WorkerThread.Join();
        }

        public bool IsCurrentThread()
        {
            return _WorkerThread.ManagedThreadId == Thread.CurrentThread.ManagedThreadId;
        }
    }
}
