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


namespace MultiRept
{
	/// <summary>
	/// OneTask.xaml の相互作用ロジック
	/// </summary>
	public partial class OneTask : UserControl
	{
		public OneTask()
		{
			InitializeComponent();
		}

		public String ReplaceFrom
		{
			get { return replaceFromTextBox.Text; }
		}

		public String ReplaceTo
		{
			get { return replaceToTextBox.Text; }
		}

		public bool IsPlainMode
		{
			get
			{
				var val = modePlainRadioButton.IsChecked;
				return val.HasValue ? val.Value : false;
			}
		}
		public bool IsWordMode
		{
			get
			{
				var val = modeWordRadioButton.IsChecked;
				return val.HasValue ? val.Value : false;
			}
		}
		public bool IsRegexMode
		{
			get
			{
				var val = modeRegexRadioButton.IsChecked;
				return val.HasValue ? val.Value : false;
			}
		}

		public ReplaceParameter GetParameter()
		{
			return new ReplaceParameter(
				ReplaceFrom,
				ReplaceTo,
				IsRegexMode ? FindMode.Regex :
				IsWordMode ? FindMode.Word :
				FindMode.Plain);

		}
	}
}
