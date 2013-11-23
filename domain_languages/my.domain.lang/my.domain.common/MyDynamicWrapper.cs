using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace my.domain
{
    public class DynamicState : Dictionary<string, object>
    {

    }

    public class MyDynamicContextWrapper : DynamicObject
    //,IDictionary<string,object> --> disables python stepinto ???
    {
        protected object _ref;
        protected DynamicState _cached_values;
        protected PropertyInfo[] _prop_info;
        protected MethodInfo[] _method_info;

        protected MyDynamicContextWrapper()
        {
        }

        public MyDynamicContextWrapper(object obj)
        {
            InitWrapper(obj, obj != null ? obj.GetType() : null, true);
        }

        protected void InitWrapper(object obj, Type objType, bool init_props)
        {
            _ref = obj;
            _cached_values = new DynamicState();

            if (objType != null)
            {
                _method_info = objType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

                _prop_info = objType.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

                if (_ref != null && init_props)
                {
                    foreach (var p in _prop_info)
                    {
                        _cached_values[p.Name] = p.GetValue(_ref);
                    }
                }
            }
            else
            {
                _prop_info = new PropertyInfo[] { };
                _method_info = new MethodInfo[] { };
            }
        }

        public bool Exists()
        {
            return (_ref != null);
        }

        public IDictionary<string, MyDynamicContextWrapper> DynamicProperties
        {
            get
            {
                var dynState = new Dictionary<string, MyDynamicContextWrapper>();

                foreach (var k in _cached_values.Keys)
                {
                    var host = _cached_values[k];

                    if (host is IEnumerable)
                    {
                        dynState[k] = TrasformReference(host, host.GetType()) as MyDynamicContextWrapper;
                    }
                    else
                    {
                        dynState[k] = new MyDynamicContextWrapper(host);
                    }
                }

                return dynState;
            }
        }

        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return _cached_values.Keys;
        }

        public static Type GetElementType(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType();
            }
            else
            {
                bool isEnumerable = type.GetInterfaces().Contains(typeof(IEnumerable));

                if (isEnumerable)
                {
                    var r = type.GetGenericArguments().First();

                    return r;
                }
                else
                {
                    return type;
                }
            }
        }

        public virtual object GetProperty(string name)
        {
            if (!_cached_values.ContainsKey(name))
            {
                var prop = _prop_info.Where(x => x.Name == name).FirstOrDefault();

                if (prop != null)
                {
                    _cached_values[name] = prop.GetValue(_ref);

                    var r = _cached_values[name];

                    return TrasformReference(r, prop.PropertyType);

                }
                else
                {
                    throw new ArgumentException(string.Format("object doesn't support property '{0}'", name));
                }
            }
            else
            {
                var prop = _prop_info.Where(x => x.Name == name).FirstOrDefault();

                var r = _cached_values[name];

                return TrasformReference(r, prop.PropertyType);

            }

        }

        public static object TrasformReference(object obj, Type objectType)
        {
            if (obj is MyDynamicContextWrapper)
            {
                return obj as MyDynamicContextWrapper;
            }
            else if (obj is IEnumerable && obj.GetType() != typeof(string))
            {
                var collGenType = typeof(MyDynamicCollectionWrapper<>);
                var collType = collGenType.MakeGenericType(GetElementType(objectType));
                var wrapper = Activator.CreateInstance(collType, obj);

                return wrapper;
            }
            else
            {
                if (obj != null)
                {
                    if (objectType.IsValueType || objectType.IsPrimitive || objectType == typeof(string))
                    {
                        return obj;
                    }
                    else
                    {
                        return new MyDynamicContextWrapper(obj);
                    }
                }
                else
                {
                    return new MyDynamicContextWrapper(obj);
                }
            }

        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            result = null;
            var method = _method_info.Where(x => x.Name == binder.Name).FirstOrDefault();

            if (method != null)
            {
                result = method.Invoke(_ref, args);

                return true;
            }

            return false;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            result = null;

            try
            {
                result = GetProperty(binder.Name);
            }
            catch (ArgumentException x)
            {
                Trace.WriteLine(x);

                throw;
            }

            return (result != null);
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            var prop = _prop_info.Where(x => x.Name == binder.Name).FirstOrDefault();

            if (prop != null)
            {
                prop.SetValue(_ref, value);

                _cached_values[prop.Name] = value;

                return true;
            }
            else
            {
                return false;
            }
        }

        /*
        #region "dictionary interface"
        void IDictionary<string, object>.Add(string key, object value)
        {
            _cached_values.Add(key, value);
        }

        bool IDictionary<string, object>.ContainsKey(string key)
        {
            return _cached_values.ContainsKey(key);
        }

        ICollection<string> IDictionary<string, object>.Keys
        {
            get
            {
                return _cached_values.Keys;
            }
        }

        bool IDictionary<string, object>.Remove(string key)
        {
            return _cached_values.Remove(key);
        }

        bool IDictionary<string, object>.TryGetValue(string key, out object value)
        {
            return _cached_values.TryGetValue(key, out  value);
        }

        ICollection<object> IDictionary<string, object>.Values
        {
            get
            {
                return _cached_values.Values;
            }
        }

        object IDictionary<string, object>.this[string key]
        {
            get
            {
                return _cached_values[key];
            }
            set
            {
                _cached_values[key] = value;
            }
        }

        void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item)
        {
            (_cached_values as ICollection<KeyValuePair<string, object>>).Add(item);
        }

        void ICollection<KeyValuePair<string, object>>.Clear()
        {
            (_cached_values as ICollection<KeyValuePair<string, object>>).Clear();
        }

        bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> item)
        {
            return (_cached_values as ICollection<KeyValuePair<string, object>>).Contains(item);
        }

        void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            (_cached_values as ICollection<KeyValuePair<string, object>>).CopyTo(array, arrayIndex);
        }

        int ICollection<KeyValuePair<string, object>>.Count
        {
            get
            {
                return (_cached_values as ICollection<KeyValuePair<string, object>>).Count;
            }
        }

        bool ICollection<KeyValuePair<string, object>>.IsReadOnly
        {
            get
            {
                return (_cached_values as ICollection<KeyValuePair<string, object>>).IsReadOnly;
            }
        }

        bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> item)
        {
            return (_cached_values as ICollection<KeyValuePair<string, object>>).Remove(item);
        }

        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
        {
            return (_cached_values as IEnumerable<KeyValuePair<string, object>>).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (_cached_values as IEnumerable).GetEnumerator();
        }
        #endregion
         * */

    }

    public class MyDynamicCollectionWrapper<T> : MyDynamicContextWrapper
    {
        public MyDynamicCollectionWrapper(IEnumerable<T> e)
        {
            InitWrapper(e, typeof(T), false);
        }

        public object this[int index]
        {
            get
            {
                object result = null;

                var coll = (_ref as IEnumerable);

                var ix = 0;
                foreach (var i in coll)
                {
                    if (ix == index)
                    {
                        result = TrasformReference(i, typeof(T));

                        return result;
                    }

                    ix++;
                }

                throw new IndexOutOfRangeException();
            }
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            result = null;

            var index = Convert.ToInt32(indexes[0]);
            var coll = (_ref as IEnumerable);

            var ix = 0;
            foreach (var i in coll)
            {
                if (ix == index)
                {
                    result = TrasformReference(i, i.GetType());

                    return true;
                }

                ix++;
            }

            return false;
        }

        protected IEnumerable<T> Values
        {
            get
            {
                return _ref as IEnumerable<T>;
            }
        }

        public int Count()
        {
            return this.Values.Count();
        }

        public T Max()
        {
            return this.Values.Max();
        }

        public T Min()
        {
            return this.Values.Min();
        }

        public decimal Sum()
        {
            var r = this.Values.Sum(x => ToDecimal(x));

            return r;
        }

        public T First()
        {
            return this.Values.First();
        }

        public T FirstOrDefault()
        {
            return this.Values.FirstOrDefault();
        }

        public T Last()
        {
            return this.Values.Last();
        }

        public T LastOrDefault()
        {
            return this.Values.LastOrDefault();
        }

        public object Where(Func<T,bool> predicate)
        {
            var items = this.Values.Where(predicate);

            var r = TrasformReference(items,items.GetType());

            return r;
        }

        protected static decimal ToDecimal(object v)
        {
            if (v is IConvertible)
            {
                return Convert.ToDecimal(v);
            }
            else if (v is IWrappedValue)
            {
                return Convert.ToDecimal((v as IWrappedValue).Value);
            }
            else
            {
                throw new ArgumentException("Property cannot be converted to decimal");
            }
        }

        public override object GetProperty(string name)
        {
            var p = _prop_info.Where(x => x.Name == name).FirstOrDefault();

            if (p != null)
            {
                var listType = typeof(List<>).MakeGenericType(p.PropertyType);

                IList selectedItems = Activator.CreateInstance(listType) as IList;

                foreach (var i in (_ref as IEnumerable<T>))
                {
                    selectedItems.Add(p.GetValue(i));
                }

                return TrasformReference(selectedItems, p.PropertyType);
            }
            else
            {
                throw new ArgumentException(string.Format("Collection type doesn't support property '{0}'", name));
            }
        }
    }
}
