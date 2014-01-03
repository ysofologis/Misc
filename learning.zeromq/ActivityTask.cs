using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace learning.zeromq
{
    public class ActivityTask : IPersistedTask
    {
        private WorkflowActivity _activity;
        private string _activityType;

        public ActivityTask()
        {
            this.TaskId = Guid.NewGuid().ToString();
        }

        public string TaskId
        {
            get;
            set;
        }

        public string ActivityType
        {
            get { return _activityType; }

            set 
            { 
                _activityType = value;

                if (_activity == null)
                {
                    _activity = (WorkflowActivity)Activator.CreateInstance(Type.GetType(_activityType, false));
                }
            }
        }

        public WorkflowActivity Activity
        {
            get { return _activity; }
            set
            {
                _activity = value;

                if (_activity != null)
                {
                    _activityType = _activity.GetType().AssemblyQualifiedName;
                }
            }
        }

        public void Execute()
        {
            this.Activity.Execute();   
        }
    }
}
