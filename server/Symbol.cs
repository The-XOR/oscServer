using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace oscServer
{
	public class Symbol
	{
		public string Value;

		public Symbol()
		{
			Value = "";
		}

		public Symbol(string value)
		{
			this.Value = value;
		}

		public override string ToString()
		{
			return Value;
		}

		public override bool Equals(object obj)
		{
			if (obj.GetType() == typeof(Symbol))
			{
				return this.Value == ((Symbol)obj).Value;
			}
			else if (obj.GetType() == typeof(string))
			{
				return this.Value == (string)obj;
			}
			
			return false;
		}

		public static bool operator ==(Symbol a, Symbol b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(Symbol a, Symbol b)
		{
			return !a.Equals(b);
		}

		public override int GetHashCode()
		{
			return Value.GetHashCode();
		}
	}
}
