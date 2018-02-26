using System;

namespace oscServer
{
	public struct Timetag
	{
		public UInt64 Tag;

		public DateTime Timestamp
		{
			get
			{
				return timetagToDateTime(Tag);
			}
			set
			{
				Tag = dateTimeToTimetag(value);
			}
		}

		/// <summary>
		/// Gets or sets the fraction of a second in the timestamp. the double precision number is multiplied by 2^32
		/// giving an accuracy down to about 230 picoseconds ( 1/(2^32) of a second)
		/// </summary>
		public double Fraction
		{
			get
			{
				return timetagToFraction(Tag);
			}
			set
			{
				Tag = (Tag & 0xFFFFFFFF00000000) + (UInt32)(value * 0xFFFFFFFF);
			}
		}

		public Timetag(UInt64 value)
		{
			this.Tag = value;
		}

		public Timetag(DateTime value)
		{
			Tag = 0;
			this.Timestamp = value;
		}

		public override bool Equals(System.Object obj)
		{
			if(obj.GetType() == typeof(Timetag))
			{
				return this.Tag == ((Timetag)obj).Tag;
			} else if(obj.GetType() == typeof(UInt64))
			{
				return this.Tag == (UInt64)obj;
			}
			
			return false;
		}

		public static bool operator ==(Timetag a, Timetag b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(Timetag a, Timetag b)
		{
			return !a.Equals(b);
		}

		public override int GetHashCode()
		{
			return (int)(((uint)(Tag >> 32) + (uint)(Tag & 0x00000000FFFFFFFF)) / 2);
		}

		private double timetagToFraction(UInt64 val)
		{
			if (val == 1)
				return 0.0;

			UInt32 seconds = (UInt32)(val & 0x00000000FFFFFFFF);
			double fraction = (double)seconds / (UInt32)(0xFFFFFFFF);
			return fraction;
		}

		private UInt64 dateTimeToTimetag(DateTime value)
		{
			UInt64 seconds = (UInt32)(value - DateTime.Parse("1900-01-01 00:00:00.000")).TotalSeconds;
			UInt64 fraction = (UInt32)(0xFFFFFFFF * ((double)value.Millisecond / 1000));

			UInt64 output = (seconds << 32) + fraction;
			return output;
		}

		private DateTime timetagToDateTime(UInt64 val)
		{
			if (val == 1)
				return DateTime.Now;

			UInt32 seconds = (UInt32)(val >> 32);
			var time = DateTime.Parse("1900-01-01 00:00:00");
			time = time.AddSeconds(seconds);
			var fraction = timetagToFraction(val);
			time = time.AddSeconds(fraction);
			return time;
		}
	}
}
