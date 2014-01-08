using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace learning.zeromq
{
    public abstract class MyTask : IPersistedTask
    {
        public MyTask()
        {
            this.TaskId = Guid.NewGuid().ToString();
            this.Input = new JObject();
            this.Output = new JObject();
        }

        public string TaskId
        {
            get;
            set;
        }

        public dynamic Input
        {
            get;
            set;
        }

        public dynamic Output
        {
            get;
            set;
        }

        public void Execute()
        {
            doExecute();
        }

        protected abstract void doExecute();
    }

    public abstract class ArithmeticOperationTask : MyTask
    {
        protected static readonly Random _randomizer = new Random();

        public ArithmeticOperationTask(decimal param1, decimal param2)
        {
            this.Input.Param1 = param1;
            this.Input.Param2 = param2;
        }

        public ArithmeticOperationTask()
            : this(0, 0)
        {
        }

        public decimal GetParam1() { return this.Input.Param1; }
        public decimal GetParam2() { return this.Input.Param2; }
        public decimal GetResult() { return this.Output.Result; }

        protected void SetResult(decimal v)
        {
            this.Output.Result = v;

            Thread.Sleep( _randomizer.Next(10, 20) );
        }
    }

    public class DoAdd : ArithmeticOperationTask
    {
        public DoAdd() { }

        public DoAdd(decimal p1, decimal p2)
            : base(p1, p2)
        {
        }

        protected override void doExecute()
        {
            SetResult( this.GetParam1() + this.GetParam2() );
        }
    }

    public class DoSubtract : ArithmeticOperationTask
    {
        public DoSubtract() { }

        public DoSubtract(decimal p1, decimal p2)
            : base(p1, p2)
        {
        }

        protected override void doExecute()
        {
            SetResult(this.GetParam1() - this.GetParam2());
        }
    }

    public class DoMultiplie : ArithmeticOperationTask
    {
        public DoMultiplie() { }

        public DoMultiplie(decimal p1, decimal p2)
            : base(p1, p2)
        {
        }

        protected override void doExecute()
        {
            SetResult(this.GetParam1() * this.GetParam2());
        }
    }

    public class DoDivide : ArithmeticOperationTask
    {
        public DoDivide() { }

        public DoDivide(decimal p1, decimal p2)
            : base(p1, p2)
        {
        }

        protected override void doExecute()
        {
            SetResult(this.GetParam1() / this.GetParam2());
        }
    }
}
