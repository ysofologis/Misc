using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroMQ;
using ZeroMQ.Devices;


namespace learning.zeromq
{
    /// <summary>
    /// 
    /// </summary>
    public interface IPersistedTask
    {
        string TaskId { get; set; }
        void Execute();
    }
    /// <summary>
    /// 
    /// </summary>
    public class TaskQueue
    {
        protected class AtomicCounter
        {
            private long _value;

            public long Value
            {
                get
                {
                    lock (this)
                    {
                        return _value;
                    }
                }
            }

            public long Increase()
            {
                lock (this)
                {
                    return _value ++;
                }
            }

            public long Decrease()
            {
                lock (this)
                {
                    return _value--;
                }
            }

            /*
            public static AtomicCounter operator ++(AtomicCounter c)
            {
                c.Increase();

                return c;
            }

            
            public static AtomicCounter operator --(AtomicCounter c)
            {
                c.Decrease();

                return c;
            }
            */

            public static implicit operator long(AtomicCounter c)
            {
                return c.Value;
            }

            public override string ToString()
            {
                return this.Value.ToString();
            }
        }

        protected class TaskDistributor : StreamerDevice
        {
            public TaskDistributor(ZmqContext context, string frontend, string backend)
                : base(context, frontend, backend, DeviceMode.Threaded)
            {
            }

            public SendStatus Broadcast(byte [] data)
            {
                return this.BackendSocket.Send(data);
            }

            protected override void FrontendHandler(SocketEventArgs args)
            {
                base.FrontendHandler(args);
            }
        }

        private const string WORKER_SOCKET = "inproc://activity_q";
        private const string QUEUE_SOCKET = "tcp://*:5555";

        private const int ACTIVITY_THREADS = 50;

        public static readonly Encoding MessageEncoding = Encoding.UTF8;

        protected ZmqContext        _queueContext;
        protected TaskDistributor   _queueDevice;

        protected object _lock;
        protected List<TaskWorker> _runningWorkers;
        protected int _threadCount;
        protected ManualResetEvent _keepRunning;
        protected Random _randomizer;
        protected int _next_topic;
        protected AtomicCounter _active_tasks;

        public event Action<TaskQueue, IPersistedTask> ActivityExecuted;
        public event Action<TaskQueue, string> WorkerStarted;

        public TaskQueue(int queueThreads = ACTIVITY_THREADS)
        {
            _lock = new object();
            _threadCount = queueThreads;
            _randomizer = new Random(DateTime.Now.Millisecond);
            _runningWorkers = new List<TaskWorker>();
            _keepRunning = new ManualResetEvent(false);
            _next_topic = 1;
            _active_tasks = new AtomicCounter();

            // this.Storage = new TaskStorageOnFileSystem_PurgeCompleted();
            // this.Storage = new InMemoryStorage();
            this.Storage = new OnTheFlyStorage();
        }

        public long ActiveTasks
        {
            get
            {
                return _active_tasks;
            }
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_queueContext == null)
                {
                    foreach (var p in this.Storage.GetPending())
                    {
                        this.Storage.SetCompleted(p, CompletionTag.orphaned);
                    }

                    _queueContext = ZmqContext.Create();

                    _queueDevice = new TaskDistributor(_queueContext, QUEUE_SOCKET, WORKER_SOCKET);
                    _queueDevice.Start();

                    for (int ix = 0; ix < _threadCount; ix++)
                    {
                        _runningWorkers.Add(new TaskWorker(_queueContext, WorkerThread));
                    }

                    _keepRunning.Reset();

                    foreach (var t in _runningWorkers)
                    {
                        t.Start();
                        t.WaitStart();
                    }
                }
            }
        }

        public void Shutdown()
        {
            lock (_lock)
            {
                if (_queueContext != null)
                {
                    _queueDevice.Dispose();
                    _queueDevice.Stop();

                    _keepRunning.Set();

                    foreach (var t in _runningWorkers)
                    {
                        t.Join();
                    }

                    _runningWorkers.Clear();

                    _queueContext.Dispose();

                    _queueContext = null;
                }
            }
        }

        public ITaskStorage Storage { get; protected set; }

        public void ExecuteTask(IPersistedTask task)
        {
            var message = "";
            var message_topic = 0;

            lock (_lock)
            {
                _next_topic++;

                if (_next_topic > _runningWorkers.Count || _next_topic < 1)
                {
                    _next_topic = 1;
                }

                message_topic = _next_topic;
            }


            var taskContent = this.Storage.HydrateTask(task);
            
            message = string.Format("{0:D4} {1}", message_topic, taskContent);

            _active_tasks.Increase();

            var status = _queueDevice.Broadcast( MessageEncoding.GetBytes(message) );
        }

        protected void ForwarderThread()
        {

        }

        protected void WorkerThread(TaskWorker.WorkerContext workerContext)
        {
            int threadIndex = _runningWorkers.IndexOf(_runningWorkers.Where(x => x.IsCurrentThread() ).Single());

            string subscriberTopic = string.Format("{0:D4}", threadIndex + 1);

            using (ZmqSocket worker = workerContext.QueueContext.CreateSocket(SocketType.PULL))
            {
                worker.Connect(WORKER_SOCKET);
                // worker.Subscribe(MessageEncoding.GetBytes(subscriberTopic));

                workerContext.NotifyStart.Set();

                if (this.WorkerStarted != null)
                {
                    this.WorkerStarted(this, subscriberTopic);
                }

                worker.ReceiveReady += (s, e) =>
                    {
                        ProcessRequest(e.Socket);
                    };

                var poller = new Poller(new List<ZmqSocket> { worker });

                while (!_keepRunning.WaitOne(1))
                {
                    var messageSize = poller.Poll(TimeSpan.FromMilliseconds(5));
                }
            }

            workerContext.NotifyStart.Dispose();
        }

        private void ProcessRequest(ZmqSocket socket)
        {
            var taskContent = string.Empty;

            try
            {
                taskContent = socket.Receive(MessageEncoding);

                taskContent = taskContent.Substring(taskContent.IndexOf(" ") + 1);

                var activity = this.Storage.DehydrateTask(taskContent);

                activity.Execute();

                this.Storage.SetCompleted(taskContent, CompletionTag.completed);

                if (this.ActivityExecuted != null)
                {
                    this.ActivityExecuted(this, activity);
                }

            }
            catch (Exception x)
            {
                this.Storage.SetCompleted(taskContent, CompletionTag.faulted);

                Debug.WriteLine(x);
            }
            finally
            {
                _active_tasks.Decrease();
            }
        }
    }
}
