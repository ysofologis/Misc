using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace my.domain.model
{
    public class Amount : WrappedValue<Decimal>
    {
        public static implicit operator Amount(decimal d)
        {
            return new Amount() { Value = d };
        }
    }
}
