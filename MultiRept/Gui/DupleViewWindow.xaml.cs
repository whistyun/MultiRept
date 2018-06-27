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
using System.Windows.Shapes;
using System.IO;
using System.Windows.Markup;

namespace MultiRept.Gui
{
	/// <summary>
	/// DupleViewWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class DupleViewWindow : Window
	{
		public DupleViewWindow()
		{
			InitializeComponent();
		}

		public void Load(string filepath, string originalFilePath, string changedFilePath)
		{
			Title = filepath;
			LoadXamlPackage(leftOriginal, originalFilePath);
			LoadXamlPackage(rightChanged, changedFilePath);
		}

		void LoadXamlPackage(ScrollViewer scrollViewer, string fileName)
		{
			if (File.Exists(fileName))
			{
				using (FileStream fStream = new FileStream(fileName, FileMode.OpenOrCreate))
				{
					scrollViewer.Content = XamlReader.Load(fStream);
				}
			}
		}

		private void SyncScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			var point = e.VerticalOffset;

			if (rightChanged.VerticalOffset != point)
			{
				rightChanged.ScrollToVerticalOffset(point);
			}

			if (leftOriginal.VerticalOffset != point)
			{
				leftOriginal.ScrollToVerticalOffset(point);
			}
		}
	}
}
