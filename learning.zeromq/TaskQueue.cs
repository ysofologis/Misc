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
        protected class AtomicCounter
        {
            private long _value;

            public long Value
            {
                get
                {
                    return Interlocked.Read(ref _value);
                }
            }

            public long Increase()
            {
                return Interlocked.Increment(ref _value);
            }

            public long Decrease()
            {
                return Interlocked.Decrement(ref _value);
            }

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

            public static implicit operator long(AtomicCounter c)
            {
                return c.Value;
            }
        }

        private const string ACTIVITY_Q_ADDRESS = "inproc://activity_q";
        private const int ACTIVITY_THREADS = 50;

        public static readonly Encoding MessageEncoding = Encoding.UTF8;

        protected ZmqContext _queueContext;
        protected ZmqSocket _backendChannel;

        protected object _lock;
        protected List<Task> _runningThreads;
        protected int _threadCount;
        protected ManualResetEvent _keepRunning;
        protected Random _randomizer;
        protected int _next_topic;
        protected AtomicCounter _active_tasks;

        public event Action<TaskQueue, IPersistedTask> ActivityExecuted;

        public TaskQueue(int queueThreads = ACTIVITY_THREADS)
        {
            _lock = new object();
            _threadCount = queueThreads;
            _randomizer = new Random(DateTime.Now.Millisecond);
            _runningThreads = new List<Task>();
            _keepRunning = new ManualResetEvent(false);
            _next_topic = 1;
            _active_tasks = new AtomicCounter();

            this.Storage = new TaskStorageOnFileSystem_PurgeCompleted();
            // this.Storage = new InMemoryStorage();
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

                    for (int ix = 0; ix < _threadCount; ix++)
                    {
                        var newThread = new Task(() =>
                        {
                            ExecutorThread(_queueContext);
                        });

                        _runningThreads.Add(newThread);
                    }

                    _keepRunning.Reset();

                    foreach (var t in _runningThreads)
                    {
                        t.Start();
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
                        t.Wait();
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

        public void ExecuteActivity(IPersistedTask activity)
        {
            var message = "";

            lock (_lock)
            {
                _next_topic++;

                if (_next_topic > _runningThreads.Count)
                {
                    _next_topic = 1;
                }

                message = string.Format("{0:D4} {1}", _next_topic, activity.TaskId);
            }


            this.Storage.HydrateActivity(activity);

            _active_tasks++;

            var status = GetBackend().Send(message, MessageEncoding);
        }

        protected void ExecutorThread(object context)
        {
            int threadIndex = _runningThreads.IndexOf(_runningThreads.Where(x => x.Id == Task.CurrentId).Single());
            string subscriberTopic = string.Format("{0:D4}", threadIndex + 1);

            using (ZmqSocket worker = ((ZmqContext)context).CreateSocket(SocketType.SUB))
            {
                worker.Connect(ACTIVITY_Q_ADDRESS);
                worker.Subscribe(MessageEncoding.GetBytes(subscriberTopic));

                worker.ReceiveReady += (s, e) =>
                    {
                        ProcessRequest(e.Socket);
                    };

                var poller = new Poller(new List<ZmqSocket> { worker });

                while (!_keepRunning.WaitOne(5))
                {
                    var messageSize = poller.Poll(TimeSpan.FromMilliseconds(1));
                }
            }
        }

        private void ProcessRequest(ZmqSocket socket)
        {
            var activityId = string.Empty;

            try
            {
                activityId = socket.Receive(MessageEncoding);

                activityId = activityId.Substring(activityId.IndexOf(" ") + 1);

                var activity = this.Storage.DehydrateActivity(activityId);

                activity.Execute();

                this.Storage.SetCompleted(activityId, CompletionTag.completed);

                if (this.ActivityExecuted != null)
                {
                    this.ActivityExecuted(this, activity);
                }

            }
            catch (Exception x)
            {
                this.Storage.SetCompleted(activityId, CompletionTag.faulted);

                Debug.WriteLine(x);
            }
            finally
            {
                _active_tasks--;
            }
        }
    }
}
