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

        private const string ACTIVITY_Q_ADDRESS = "inproc://activity_q";
        private const int ACTIVITY_THREADS = 50;

        public static readonly Encoding MessageEncoding = Encoding.UTF8;

        protected ZmqContext    _queueContext;
        protected ZmqSocket     _backendChannel;

        protected object            _lock;
        protected List<Thread>      _runningThreads;
        protected ManualResetEvent  _keepRunning;
        protected Random            _randomizer;
        protected int               _next_topic;
        protected volatile int      _active_tasks;

        public event Action<TaskQueue, IPersistedTask> ActivityExecuted;

        public TaskQueue()
        {
            _lock = new object();
            _randomizer = new Random(DateTime.Now.Millisecond);
            _runningThreads = new List<Thread>();
            _keepRunning = new ManualResetEvent(false);
            _next_topic = 1;
            _active_tasks = 0;

            this.Storage = new TaskStorageOnFileSystem();
        }

        public int ActiveTasks
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

                    for (int ix = 0; ix < ACTIVITY_THREADS; ix++)
                    {
                        var newThread = new Thread(this.ExecutorThread);

                        _runningThreads.Add(newThread);
                    }

                    _keepRunning.Reset();

                    foreach (var t in _runningThreads)
                    {
                        t.Start(_queueContext);
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

        public void ExecuteActivity(IPersistedTask activity)
        {
            lock (_lock)
            {
                string activityId = activity.TaskId;

                this.Storage.HydrateActivity(activity);

                _next_topic++;

                if (_next_topic> _runningThreads.Count)
                {
                    _next_topic = 1;
                }

                string message = string.Format("{0:D4} {1}", _next_topic, activityId);

                _active_tasks++;

                var status = GetBackend().Send(message, MessageEncoding);
            }
        }

        protected void ExecutorThread(object context)
        {
            int threadIndex = _runningThreads.IndexOf( _runningThreads.Where(x => x.ManagedThreadId == Thread.CurrentThread.ManagedThreadId).Single() );
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

                while ( ! _keepRunning.WaitOne(1) )
                {
                    var messageSize = poller.Poll( TimeSpan.FromMilliseconds(10) );
                }
            }
        }

        private void ProcessRequest(ZmqSocket socket)
        {
            var activityId = socket.Receive(MessageEncoding);

            activityId = activityId.Substring(activityId.IndexOf(" ") + 1);

            try
            {
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

            _active_tasks--;
        }
    }
}
