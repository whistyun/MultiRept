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
using MultiRept.Gui.Converter;

namespace MultiRept.Gui
{

	/// <summary>
	/// DupleViewWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class DupleViewWindow : Window
	{
		public int TotalLineCount
		{
			get;
			set;
		}

		public DupleViewWindow()
		{
			InitializeComponent();
		}

		public void Load(string filepath, string originalFilePath, string changedFilePath)
		{
			Title = filepath;
			LoadXamlPackage(leftOriginal, originalFilePath);
			LoadXamlPackage(rightChanged, changedFilePath);
			LoadDiffView(diffViewer, originalFilePath);
		}

		void LoadDiffView(Canvas canvas, string fileName)
		{
			List<bool> lines = new List<bool>();

			if (File.Exists(fileName))
			{
				using (FileStream fStream = new FileStream(fileName, FileMode.OpenOrCreate))
				{
					// 各行について、置換あり(true)、なし(false)の一覧化する
					var isChangeList =
						((StackPanel)XamlReader.Load(fStream)).Children.OfType<TextBlock>()
						.Select(tbk => tbk.Inlines.OfType<Run>()
							.Where(run => run.Background != null).Count() != 0).ToList();

					TotalLineCount = Math.Max(1, isChangeList.Count);

					// キャンバスのバインディング設定
					var binding = new Binding("ActualHeight");
					binding.Source = diffViewer;
					binding.Converter = new DevideConverter();
					binding.ConverterParameter = TotalLineCount.ToString();
					BindingOperations.SetBinding(canvasScale, ScaleTransform.ScaleYProperty, binding);

					// あり/なしの一覧から、変更箇所の位置と厚さを確認する
					var backRect = new Rectangle
					{
						Width = 10,
						Height = TotalLineCount,
						Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("LightGray"))
					};
					Canvas.SetLeft(backRect, 0);
					Canvas.SetTop(backRect, 0);
					canvas.Children.Add(backRect);

					var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("Yellow"));
					for (int i = 0; i < isChangeList.Count; ++i)
					{
						if (isChangeList[i])
						{
							int startIdx = i;

							for (; i < isChangeList.Count; ++i)
								if (!isChangeList[i]) break;

							int thickness = i - startIdx;

							var rect = new Rectangle()
							{
								Width = 10,
								Height = thickness,
								Fill = brush
							};
							Canvas.SetLeft(rect, 0);
							Canvas.SetTop(rect, startIdx);
							canvas.Children.Add(rect);
						}
					}
				}
			}
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

		private void CanvasMouseMove(object sender, MouseEventArgs e)
		{
			if (Mouse.LeftButton == MouseButtonState.Pressed)
			{
				var posi = e.GetPosition(diffViewer);
				scrollByCanvas(posi.Y);
			}
		}
		private void CanvasClicked(object sender, MouseButtonEventArgs e)
		{
			var posi = e.GetPosition(diffViewer);
			scrollByCanvas(posi.Y);
		}

		private void scrollByCanvas(double mousePointY) {
			double percent = mousePointY / TotalLineCount;
			leftOriginal.ScrollToVerticalOffset(leftOriginal.ScrollableHeight * percent);
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
