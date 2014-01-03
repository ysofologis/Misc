using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace learning.zeromq
{
    public abstract class WorkflowActivity
    {
        public WorkflowActivity()
        {
            this.Input = new JObject();
            this.Output = new JObject();
        }

        public dynamic Input { get; set; }
        public dynamic Output { get; set; }

        public abstract void Execute();
    }

    public abstract class MathOperationActivity : WorkflowActivity
    {
        public MathOperationActivity()
        {
        }

        public MathOperationActivity(decimal param1, decimal param2)
        {
            this.Input.Param1 = param1;
            this.Input.Param2 = param2;
        }

        protected decimal GetParam1() { return this.Input.Param1; }
        protected decimal GetParam2() { return this.Input.Param2; }

        public override void Execute()
        {
            this.Output.Result = this.doCalculate();
        }

        protected abstract decimal doCalculate();
    }

    public class ActivityAdd : MathOperationActivity
    {
        public ActivityAdd() { }
        public ActivityAdd(decimal param1, decimal param2) : base(param1,param2) { }

        protected override decimal doCalculate()
        {
            return this.GetParam1() + this.GetParam2();   
        }
    }

    public class ActivityDivide : MathOperationActivity
    {
        public ActivityDivide() { }
        public ActivityDivide(decimal param1, decimal param2) : base(param1, param2) { }

        protected override decimal doCalculate()
        {
            return this.GetParam1() / this.GetParam2();
        }
    }
}
