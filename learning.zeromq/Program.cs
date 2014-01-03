using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace learning.zeromq
{
    class Program
    {
        static void Main(string[] args)
        {
            // zguide.asycnsrv.Program.Main(args);

            TaskQueue q = new TaskQueue();

            int exec_count = 0;

            q.ActivityExecuted += (s, a) =>
                {
                    var count = Interlocked.Increment(ref exec_count);

                    var threadId = Thread.CurrentThread.ManagedThreadId;

                    var line = string.Format( "{0:D4} >>> thread: {1},  Executed --> {2}: '{3}' ", count, threadId, a.TaskId, a.GetType().Name );

                    // Debug.WriteLine(line);

                    Console.WriteLine(line);

                };

            q.Start();

            for (int ix = 0; ix < 5000; ix++)
            {
                q.ExecuteActivity(new DoAdd(10, 20));
                q.ExecuteActivity(new DoSubtract(10, 20));
                q.ExecuteActivity(new DoMultiplie(10, 20));
                q.ExecuteActivity(new DoDivide(10, 20));

                q.ExecuteActivity(new ActivityTask() { Activity = new ActivityAdd(10, 30) });
                q.ExecuteActivity(new ActivityTask() { Activity = new ActivityDivide(10, 20) });

            }

            while (q.ActiveTasks > 0)
            {
                Thread.Sleep(100);
            }

            q.Shutdown();

        }
    }
}
