using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MultiRept.Gui
{
	/// <summary>
	/// ErrorHolderTextBox.xaml の相互作用ロジック
	/// </summary>
	public partial class ErrorableTextBox : TextBox
	{

		public static readonly DependencyProperty HasErrorProperty =
			DependencyProperty.Register(
					"HasError",
					typeof(bool),
					typeof(ErrorableTextBox));

		public static readonly DependencyProperty ErrorMessageProperty =
			DependencyProperty.Register(
					"ErrorMessage",
					typeof(string),
					typeof(ErrorableTextBox));

		public ErrorableTextBox()
		{
			InitializeComponent();
		}

		public bool HasError
		{
			set
			{
				this.SetValue(HasErrorProperty, value);
				if (!value) this.SetValue(ErrorMessageProperty, "");
			}
			get { return (bool)this.GetValue(HasErrorProperty); }
		}

		public string ErrorMessage
		{
			set
			{
				this.SetValue(HasErrorProperty, !String.IsNullOrEmpty(value));
				this.SetValue(ErrorMessageProperty, value);
			}
			get { return (string)this.GetValue(ErrorMessageProperty); }
		}
	}
}
