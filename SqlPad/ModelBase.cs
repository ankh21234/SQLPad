using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SqlPad
{
	public class ModelBase : INotifyPropertyChanged, INotifyPropertyChanging
	{
		public event PropertyChangingEventHandler PropertyChanging;

		public event PropertyChangedEventHandler PropertyChanged;

		protected bool UpdateValueAndRaisePropertyChanged<T>(ref T value, T newValue, [CallerMemberName] string propertyName = null)
		{
			if (Equals(value, newValue))
				return false;

			if (PropertyChanging != null)
			{
				var args = new PropertyChangingEventArgs(propertyName);
				PropertyChanging(this, args);
			}

			value = newValue;
			
			RaisePropertyChanged(propertyName);

			return true;
		}

		protected void RaisePropertyChanged(string propertyName)
		{
			if (PropertyChanged == null)
			{
				return;
			}

			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
