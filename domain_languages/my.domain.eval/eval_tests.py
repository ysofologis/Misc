from domain_eval import *

def create_deal():
    my_deal = MyContext("Deal")
    my_deal.loans = MyCollectionContext("Loans")
    my_loan1 = MyContext("Loan","loan1")
    my_loan1.amount = 100
    my_deal.loans.add(my_loan1)
    my_loan2 = MyContext("Loan", "loan2")
    my_loan2.amount = 200
    my_deal.loans.add(my_loan2)
    return my_deal


def test_expr(template, expr):
    try:
        r = template.parseString(expr)
        print r
    except pp.ParseException as e:
        print e

def test_eval(eval, context, expr):
    try:
        r = eval.evaluate(expr)
        print r
    except Exception as e:
        print e

# >>>>> testing variable
test_expr(var_name, "adfdfg")
test_expr(var_name, "a23")
test_expr(var_name, "1a23")
test_expr(var_name, "adfdfg.etetrer.ertert")
test_expr(var_name, "adf12dfg.etetrer34.er34te34rt")
test_expr(var_name, "adf12dfg.etetrer34.1er34te34rt")

# >>>>> testing variable evaluator
my_context = create_deal()

my_var_eval = VariableEvaluator(var_name, my_context)
test_eval(my_var_eval, my_context, "loans.loan1.amount");
test_eval(my_var_eval, my_context, "loans.loan2.amount");
test_eval(my_var_eval, my_context, "loans[0].amount");
test_eval(my_var_eval, my_context, "loans.loan3.amount");

# >>>>> testing function
test_expr(func_name,"f()")
test_expr(func_name,"f(1,2)")
test_expr(func_name,"grf23 (1, 2)")
test_expr(func_name,"grf23 (a, '2')")
test_expr(func_name,"grf23 (axg.rer2, '2')")
test_expr(func_name,"sdf.grf23(axg.rer2, '2')")


# >>>>> testing query
test_expr(query_expr_full, "a12 == 1")
test_expr(query_expr_full, "(a12.bs == 1)")
test_expr(query_expr_full, "ab==1 and cx.zy.ty6 <= 3")
test_expr(query_expr_full, "(ab==1) and c <= 3")
test_expr(query_expr_full, "(ab==1) and (c <= 3)")
test_expr(query_expr_full, "(ab==1 and c <= 3)")
test_expr(query_expr_full, "(ab==1) and c <= 3 or d > 4")
