using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace my.domain.model
{
    public class LoanProduct
    {
        public NamedValue ProductCategory { get; set; }
        public NamedValue ProductType { get; set; }
    }
}
