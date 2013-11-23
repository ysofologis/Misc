using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace my.domain.model
{
    public class Loan
    {
        public Amount Amount { get; set; }
        public LoanProduct LoanProduct { get; set; }
    }
}
