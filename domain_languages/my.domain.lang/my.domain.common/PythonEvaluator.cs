using Microsoft.Scripting.Hosting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace my.domain
{
    public class PythonEvaluator : IExpressionEvaluator
    {
        private static ScriptEngine _engine;

        public static ScriptEngine PythonEngine
        {
            get
            {
                if (_engine == null)
                {
                    _engine = IronPython.Hosting.Python.CreateEngine(); 
                }

                return _engine;
            }
        }

        public object EvaluateExpression(string expression, IDictionary context)
        {
            var scope = PythonEngine.CreateScope();

            foreach (var k in context.Keys)
            {
                scope.SetVariable(k.ToString(), context[k]);
            }

            var script = PythonEngine.CreateScriptSourceFromString(expression, Microsoft.Scripting.SourceCodeKind.AutoDetect);

            var r = script.Execute(scope);

            return r;
        }
    }
}
