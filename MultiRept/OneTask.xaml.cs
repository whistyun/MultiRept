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
using System.Text.RegularExpressions;


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

		public bool CheckError()
		{
			replaceFromTextBox.HasError = false;
			replaceToTextBox.HasError = false;

			// チェック：キーワード未入力チェック
			if (ReplaceFrom == "")
			{
				replaceFromTextBox.ErrorMessage = "未入力です";
			}
			// チェック：正規表現の場合は、正当な正規表現かチェック
			else if (IsRegexMode)
			{
				try
				{
					var regex = new Regex(ReplaceFrom);
				}
				catch (Exception)
				{
					replaceFromTextBox.ErrorMessage = "不正な正規表現です";
				}
			}

			// チェック：正規表現の場合は、置換後テキストが成形可能な文字か確認
			if (IsRegexMode)
			{
				try
				{
					var res = ExtendReplaceTo.Parse(ReplaceTo);
				}
				catch (Exception)
				{
					replaceToTextBox.ErrorMessage = "置換後テキストが不正です。\n※正規表現が有効の場合、成型文字が使用できます。";
				}
			}

			return replaceFromTextBox.HasError | replaceToTextBox.HasError;
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
