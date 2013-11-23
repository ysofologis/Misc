using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace my.domain.model
{
    public class NamedValue : WrappedValue<string>, INamedInstance
    {
        public static implicit operator NamedValue(string t)
        {
            return new NamedValue() { Value = t };
        }

        string INamedInstance.Name
        {
            get 
            {
                return this.Value;
            }
        }
    }
}
