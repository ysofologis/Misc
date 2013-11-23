using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace my.domain.model
{

    [DebuggerDisplay("Value: {Value}, Type: {ValueType}")]
    public class WrappedValue<T> : IWrappedValue, IConvertible
    {
        public T Value { get; set; }

        public Type ValueType { get { return typeof(T); } }

        object IWrappedValue.Value
        {
            get
            {
                return this.Value;
            }
            set
            {
                this.Value = (T)value;
            }
        }

        public static implicit operator T(WrappedValue<T> v)
        {
            return v.Value;
        }

        public static bool operator ==(WrappedValue<T> l, T r)
        {
            return l.Value.Equals(r);
        }

        public static bool operator !=(WrappedValue<T> l, T r)
        {
            return ! (l == r);
        }

        public static bool operator >(WrappedValue<T> l, T r)
        {
            return Convert.ToDecimal(l.Value) > Convert.ToDecimal(r);
        }

        public static bool operator <(WrappedValue<T> l, T r)
        {
            return Convert.ToDecimal(l.Value) < Convert.ToDecimal(r);
        }

        public static bool operator >=(WrappedValue<T> l, T r)
        {
            return Convert.ToDecimal(l.Value) >= Convert.ToDecimal(r);
        }

        public static bool operator <=(WrappedValue<T> l, T r)
        {
            return Convert.ToDecimal(l.Value) <= Convert.ToDecimal(r);
        }

        public override bool Equals(object obj)
        {
            if (obj is T)
            {
                return this.Value.Equals(obj);
            }
            else
            {
                if (obj is WrappedValue<T>)
                {
                    return this.Value.Equals((obj as WrappedValue<T>).Value);
                }
                else
                {
                    return false;
                }
            }
        }

        public override string ToString()
        {
            return this.Value.ToString();
        }

        public override int GetHashCode()
        {
            return this.Value.GetHashCode();
        }

        TypeCode IConvertible.GetTypeCode()
        {
            return Type.GetTypeCode(typeof(T));
        }

        bool IConvertible.ToBoolean(IFormatProvider provider)
        {
            return Convert.ToBoolean(this.Value);
        }

        byte IConvertible.ToByte(IFormatProvider provider)
        {
            return Convert.ToByte(this.Value);
        }

        char IConvertible.ToChar(IFormatProvider provider)
        {
            return Convert.ToChar(this.Value);
        }

        DateTime IConvertible.ToDateTime(IFormatProvider provider)
        {
            return Convert.ToDateTime(this.Value);
        }

        decimal IConvertible.ToDecimal(IFormatProvider provider)
        {
            return Convert.ToDecimal(this.Value);
        }

        double IConvertible.ToDouble(IFormatProvider provider)
        {
            return Convert.ToDouble(this.Value);
        }

        short IConvertible.ToInt16(IFormatProvider provider)
        {
            return Convert.ToInt16(this.Value);
        }

        int IConvertible.ToInt32(IFormatProvider provider)
        {
            return Convert.ToInt32(this.Value);
        }

        long IConvertible.ToInt64(IFormatProvider provider)
        {
            return Convert.ToInt64(this.Value);
        }

        sbyte IConvertible.ToSByte(IFormatProvider provider)
        {
            return Convert.ToSByte(this.Value);
        }

        float IConvertible.ToSingle(IFormatProvider provider)
        {
            return Convert.ToSingle(this.Value);
        }

        string IConvertible.ToString(IFormatProvider provider)
        {
            return Convert.ToString(this.Value);
        }

        object IConvertible.ToType(Type conversionType, IFormatProvider provider)
        {
            return Convert.ChangeType(this.Value, conversionType);
        }

        ushort IConvertible.ToUInt16(IFormatProvider provider)
        {
            return Convert.ToUInt16(this.Value);
        }

        uint IConvertible.ToUInt32(IFormatProvider provider)
        {
            return Convert.ToUInt32(this.Value);
        }

        ulong IConvertible.ToUInt64(IFormatProvider provider)
        {
            return Convert.ToUInt64(this.Value);
        }
    }
}
