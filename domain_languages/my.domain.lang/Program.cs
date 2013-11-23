using my.domain.model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace my.domain.lang
{
    class Program
    {
        static void Test1()
        {
            var fakeDeal = new Deal() { 
                Loans = new Loan [] {
                    new Loan() { Amount = 20, LoanProduct = new LoanProduct() { ProductCategory = "Mortgage", ProductType = "MASTERCARD"} },
                    new Loan() { Amount = 50, LoanProduct = new LoanProduct() { ProductCategory = "Mortgage", ProductType = "VISA"} },
                    new Loan() { Amount = 100, LoanProduct = new LoanProduct() { ProductCategory = "Business", ProductType = "MASTERCARD"} },
                    new Loan() { Amount = 200, LoanProduct = new LoanProduct() { ProductCategory = "Business", ProductType = "MASTERCARD"} },
                    new Loan() { Amount = 300, LoanProduct = new LoanProduct() { ProductCategory = "Consumer", ProductType = "VISA"} },
                }
            };

            dynamic deal = new MyDynamicContextWrapper(fakeDeal);

            var eval = new PythonEvaluator();

            // var context = new Dictionary<string, object>();
            // context.Add("Loans", deal.Loans);

            var r2 = eval.EvaluateExpression("Loans[1].Amount", deal.DynamicProperties);
            var r1 = eval.EvaluateExpression("Loans.Count() > 2", deal.DynamicProperties);
            var r3 = eval.EvaluateExpression("Loans.Amount.Sum()", deal.DynamicProperties);
            var r4 = eval.EvaluateExpression("Loans.Where( lambda x: x.Amount >= 200 ).Amount.Sum()", deal.DynamicProperties);
        }

        static void Main(string[] args)
        {
            Test1();

            Console.ReadKey();
        }
    }
}
