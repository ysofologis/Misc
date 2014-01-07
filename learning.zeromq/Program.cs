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
        private const int ITERATIONS = 1000;
        private const int QUEUE_THREADS = 50;

        static void Main(string[] args)
        {
            // zguide.asycnsrv.Program.Main(args);

            TaskQueue q = new TaskQueue(QUEUE_THREADS);

            int exec_count = 0;

            q.ActivityExecuted += (s, a) =>
                {
                    var count = Interlocked.Increment(ref exec_count);

                    var threadId = Thread.CurrentThread.ManagedThreadId;

                    var line = string.Format("Active: {0:D4}, Executed: {1:D4} >>> thread: {2},  Task --> {3}: '{4}' ", q.ActiveTasks,  count, threadId, a.TaskId, a.GetType().Name);

                    Debug.WriteLine(line);
                    Console.WriteLine(line);

                };

            q.WorkerStarted += (s, topic) =>
                {
                    var line = string.Format("queue worker ({0}) activated.", topic);

                    Debug.WriteLine(line);
                    Console.WriteLine(line);

                };

            q.Start();

            Stopwatch startTime = new Stopwatch();
            startTime.Start();

            for (int ix = 0; ix < ITERATIONS; ix++)
            {
                q.ExecuteTask(new DoAdd(10, 20));
                q.ExecuteTask(new DoSubtract(10, 20));
                q.ExecuteTask(new DoMultiplie(10, 20));
                q.ExecuteTask(new DoDivide(10, 20));

                q.ExecuteTask(new ActivityTask() { Activity = new ActivityAdd(10, 30) });
                q.ExecuteTask(new ActivityTask() { Activity = new ActivityDivide(10, 20) });

            }

            while (q.ActiveTasks > 0)
            {
                Thread.Sleep(100);
            }

            startTime.Stop();
            var millisecsElapsed = (int) startTime.ElapsedMilliseconds;

            Console.WriteLine(string.Format("{0} tasks completed in {1} millisecs using {2} threads", ITERATIONS * 6, millisecsElapsed, QUEUE_THREADS));

            q.Shutdown();

            Console.ReadKey();

        }
    }
}
