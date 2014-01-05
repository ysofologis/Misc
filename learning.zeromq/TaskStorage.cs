using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace learning.zeromq
{
    public enum CompletionTag
    {
        completed,
        faulted,
        orphaned,
    }
    /// <summary>
    /// 
    /// </summary>
    public interface ITaskStorage
    {
        void HydrateActivity(IPersistedTask activity);
        IPersistedTask DehydrateActivity(string activityId);
        void SetCompleted(string activityId, CompletionTag tag);
        string[] GetPending();
    }
    /// <summary>
    /// 
    /// </summary>
    public class TaskStorageOnFileSystem : ITaskStorage
    {
        protected class TaskBag
        {
            public string Type { get; set; }
            public JObject Instance { get; set; }
        }


        protected const string ACTIVITY_FILENAME_FORMAT = @"activity_q\{0}.json";
        protected const string ARCHIVED_ACTIVITY_FILENAME_FORMAT = @"activity_q\{0}\{1}.json";

        public string[] GetPending()
        {
            List<string> r = new List<string>();

            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "activity_q");

            foreach( var f in Directory.GetFiles(path, "*.json") )
            {
                r.Add( Path.GetFileNameWithoutExtension(f) );
            }

            return r.ToArray();
        }

        public void HydrateActivity(IPersistedTask activity)
        {
            using (var fstream = new FileStream(string.Format(ACTIVITY_FILENAME_FORMAT, activity.TaskId), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var activityState = JObject.FromObject(activity);

                var r = JsonConvert.SerializeObject(new TaskBag() { Instance = activityState, Type = activity.GetType().AssemblyQualifiedName }, Formatting.Indented);

                using (var writer = new StreamWriter(fstream))
                {
                    writer.Write(r);
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="activityId"></param>
        /// <returns></returns>
        public IPersistedTask DehydrateActivity(string activityId)
        {
            using (var fstream = new FileStream(string.Format(ACTIVITY_FILENAME_FORMAT, activityId), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var reader = new StreamReader(fstream))
                {
                    var r = reader.ReadToEnd();

                    var bag = JsonConvert.DeserializeObject<TaskBag>(r);

                    var activity = (IPersistedTask) JsonConvert.DeserializeObject(bag.Instance.ToString(), Type.GetType(bag.Type, true));

                    return activity;
                }
            }
        }

        public virtual void SetCompleted(string activityId, CompletionTag tag)
        {
            var activityFile = string.Format(ACTIVITY_FILENAME_FORMAT, activityId);

            if (File.Exists(activityFile))
            {
                File.Move(activityFile, string.Format(ARCHIVED_ACTIVITY_FILENAME_FORMAT, tag, activityId));
            }
        }

    }

    public class TaskStorageOnFileSystem_PurgeCompleted : TaskStorageOnFileSystem
    {
        public override void SetCompleted(string activityId, CompletionTag tag)
        {
            var activityFile = string.Format(ACTIVITY_FILENAME_FORMAT, activityId);

            if (File.Exists(activityFile))
            {
                File.Delete(activityFile);
            }
        }
    }
    /// <summary>
    /// 
    /// </summary>
    public class InMemoryStorage : ITaskStorage
    {
        private Dictionary<string, IPersistedTask> _taskMap;
        private object _lock;

        public InMemoryStorage()
        {
            _lock = new object();
            _taskMap = new Dictionary<string, IPersistedTask>();
        }

        public void HydrateActivity(IPersistedTask activity)
        {
            lock (_lock)
            {
                _taskMap[activity.TaskId] = activity;
            }
        }

        public IPersistedTask DehydrateActivity(string activityId)
        {
            lock (_lock)
            {
                return _taskMap[activityId];
            }
        }

        public void SetCompleted(string activityId, CompletionTag tag)
        {
            lock (_lock)
            {
                if (_taskMap.ContainsKey(activityId))
                {
                    _taskMap.Remove(activityId);
                }
            }
        }

        public string[] GetPending()
        {
            return new string[] { };
        }
    }
}
