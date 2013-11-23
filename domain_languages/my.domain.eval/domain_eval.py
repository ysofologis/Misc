import sys
import pyparsing as pp

class ExpressionEvaluator :
    def __init__(self,tmpl,context = None):
        self.parsing_template = tmpl.copy().setParseAction(self._on_evaluate)
        self.context = context
        self.result = None

    def evaluate(self,expr):
        self.parsing_template.parseString(expr)
        return self.result

    def _on_evaluate(self,str,loc,tokens):
        pass

def get_property_by_path(context,property_path,part_index = 0):
    if part_index == len(property_path) :
        return context
    else:
        part_name = property_path[part_index]
        if hasattr(context,part_name):
            child_prop = getattr(context,part_name)
            return get_property_by_path(child_prop,property_path,part_index+1)
        else:
            raise AttributeError( "property %s not found in proeprty path %s" % (part_name, ".".join(property_path) ) )

class VariableEvaluator (ExpressionEvaluator) :

    def _on_evaluate(self, str, loc, tokens):
        if not self.context is None:
            property_path = tokens[0].split('.')
            self.result = get_property_by_path(self.context, property_path)


class MyContext ( object ):
    def __init__(self, entityType, entityName = None):
        self.entity_type = entityType
        self.entity_name = entityName

    def __str__(self):
        return self.entity_type

class MyCollectionContext ( MyContext ):
    def __init__(self, entityType):
        super(MyCollectionContext, self).__init__(entityType)
        self.Items = []

    def add(self,item):
        self.Items.append(item)

    def get_item(self,name):
        for i in self.Items:
            if i.entity_name == name:
                return i
        return None

    def __getitem__(self,index):
        return self.Items[index]

    def __getattr__(self, name):
        r = self.get_item(name)
        if r is None:
            return super(MyCollectionContext, self).__getattribute__(name)
        else:
            return r

def _validate_var_name(str,loc,tokens):
    curLoc = loc
    print curLoc
    for t in tokens[0].split('.'):
        curLoc += len(t) + 1
        if not t[0].isalpha() and t[0] != '.':
            raise pp.ParseException("Each path part must start with letters", curLoc)
    return

def _validate_func_name(str,loc,tokens):
    pass

# =========== variable names rules ====================
var_indexer = pp.nestedExpr("[","]", pp.quotedString | pp.nums )
var_name = pp.Word(pp.alphas, pp.alphanums + "." )
var_name.setParseAction(_validate_var_name).setName("var_name")

# =========== function rules ====================
func_args = pp.Forward()
func_args << ( var_name | pp.Word(pp.alphanums) | pp.quotedString ) + pp.Optional( pp.Literal(",") + func_args )
func_name = var_name + pp.Literal("(") +  pp.Optional(func_args) + pp.Literal(")")
func_name.setName("func_name")

# =========== elementary query rules ====================
bin_oper = pp.oneOf("== != <> <= < >= >")
logic_oper = pp.CaselessLiteral("AND").setParseAction( pp.replaceWith("and") ) | \
                            pp.CaselessLiteral("OR").setParseAction( pp.replaceWith("or") ) | \
                            pp.Literal("&&").setParseAction(pp.replaceWith("and")) | \
                            pp.Literal("||").setParseAction(pp.replaceWith("or"))
query_var = var_name
query_val = pp.Word(pp.alphanums) | pp.quotedString

# =========== query rules ====================
query_expr_elem = query_var + bin_oper + query_val
query_expr_bin = query_expr_elem | pp.nestedExpr(content = query_expr_elem)
query_expr_logic = pp.Forward()
query_expr_logic << query_expr_bin + pp.Optional( logic_oper + query_expr_bin ) + pp.Optional(logic_oper + query_expr_logic)
query_expr_full = query_expr_logic | pp.nestedExpr( content = query_expr_logic )

