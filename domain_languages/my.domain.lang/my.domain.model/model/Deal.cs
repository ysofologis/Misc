using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace my.domain.model
{
    public class Deal
    {
        public Deal()
        {
        }

        public IEnumerable<Loan> Loans { get; set; }
    }
}
