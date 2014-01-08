using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroMQ;


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
        protected class WorkerContext
        {
            public WorkerContext()
            {
                this.NotifyStart = new ManualResetEvent(false);
            }

            public ZmqContext QueueContext { get; set; }
            public ManualResetEvent NotifyStart { get; protected set; }
        }

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

        private const string ACTIVITY_Q_ADDRESS = "inproc://activity_q";
        private const int ACTIVITY_THREADS = 50;

        public static readonly Encoding MessageEncoding = Encoding.UTF8;

        protected ZmqContext _queueContext;
        protected ZmqSocket _backendChannel;

        protected object _lock;
        protected List<Thread> _runningThreads;
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
            _runningThreads = new List<Thread>();
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

                    GetBackend();

                    List<WorkerContext> workerContexts = new List<WorkerContext>();

                    for (int ix = 0; ix < _threadCount; ix++)
                    {
                        var w = new WorkerContext() { QueueContext = _queueContext };

                        /*
                        var newThread = new Task(() =>
                        {
                            WorkerThread(w);
                        });
                         * */

                        var newThread = new Thread(() =>
                        {
                            WorkerThread(w);
                        });

                        workerContexts.Add(w);
                        _runningThreads.Add(newThread);
                    }

                    _keepRunning.Reset();

                    foreach (var t in _runningThreads)
                    {
                        t.Start();
                    }

                    foreach (var w in workerContexts)
                    {
                        w.NotifyStart.WaitOne();
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
                    _backendChannel.Close();
                    _backendChannel = null;

                    _keepRunning.Set();

                    foreach (var t in _runningThreads)
                    {
                        t.Join();
                    }

                    _runningThreads.Clear();

                    _queueContext.Dispose();

                    _queueContext = null;
                }
            }
        }

        public ITaskStorage Storage { get; protected set; }

        protected ZmqSocket GetBackend()
        {
            if (_backendChannel == null)
            {
                _backendChannel = _queueContext.CreateSocket(SocketType.PUB);

                _backendChannel.Bind(ACTIVITY_Q_ADDRESS);
            }

            return _backendChannel;

        }

        public void ExecuteTask(IPersistedTask task)
        {
            var message = "";
            var message_topic = 0;

            lock (_lock)
            {
                _next_topic++;

                if (_next_topic > _runningThreads.Count || _next_topic < 1)
                {
                    _next_topic = 1;
                }

                message_topic = _next_topic;
            }


            var taskContent = this.Storage.HydrateTask(task);
            
            message = string.Format("{0:D4} {1}", message_topic, taskContent);

            _active_tasks.Increase();

            var status = GetBackend().Send(message, MessageEncoding);
        }

        protected void ForwarderThread()
        {

        }

        protected void WorkerThread(WorkerContext workerContext)
        {
            int threadIndex = _runningThreads.IndexOf(_runningThreads.Where(x => x.ManagedThreadId == Thread.CurrentThread.ManagedThreadId).Single());

            string subscriberTopic = string.Format("{0:D4}", threadIndex + 1);

            using (ZmqSocket worker = ((ZmqContext)workerContext.QueueContext).CreateSocket(SocketType.SUB))
            {
                worker.Connect(ACTIVITY_Q_ADDRESS);
                worker.Subscribe(MessageEncoding.GetBytes(subscriberTopic));

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
            var taskId = string.Empty;

            try
            {
                taskId = socket.Receive(MessageEncoding);

                taskId = taskId.Substring(taskId.IndexOf(" ") + 1);

                var activity = this.Storage.DehydrateTask(taskId);

                activity.Execute();

                this.Storage.SetCompleted(taskId, CompletionTag.completed);

                if (this.ActivityExecuted != null)
                {
                    this.ActivityExecuted(this, activity);
                }

            }
            catch (Exception x)
            {
                this.Storage.SetCompleted(taskId, CompletionTag.faulted);

                Debug.WriteLine(x);
            }
            finally
            {
                _active_tasks.Decrease();
            }
        }
    }
}
