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
using System.Globalization;

namespace MultiRept.Gui
{

	/// <summary>
	/// DupleViewWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class DupleViewWindow : Window
	{
		private Rectangle rectangle = new Rectangle();

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


					var backgroundRect = new Rectangle()
					{
						Width = canvas.Width,
						Height = TotalLineCount,
						Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("LightGray"))
					};
					Canvas.SetLeft(backgroundRect, 0);
					Canvas.SetTop(backgroundRect, 0);
					canvas.Children.Add(backgroundRect);


					var pageRect = new Rectangle()
					{
						Width = canvas.Width - 8,
						Height = TotalLineCount,
						Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White")),
						Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("Black")),
						StrokeThickness = 1,
					};
					Canvas.SetLeft(pageRect, 4);
					Canvas.SetTop(pageRect, 0);
					canvas.Children.Add(pageRect);


					// あり/なしの一覧から、変更箇所の位置と厚さを確認する
					var brush = new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0x00));
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
								Width = canvas.Width - 10,
								Height = thickness,
								Fill = brush
							};
							Canvas.SetLeft(rect, 5);
							Canvas.SetTop(rect, startIdx);
							canvas.Children.Add(rect);
						}
					}


					rectangle = new Rectangle()
					{
						Width = canvas.Width,
						Stroke = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0xFF)) { Opacity = 0.9 },
						StrokeThickness = 1,
						Fill = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0xFF)) { Opacity = 0.4 }
					};
					canvas.Children.Add(rectangle);

					// キャンバス内のインジケータのバインディング設定
					var mbinding = new MultiBinding();
					mbinding.Converter = new MyDupleViewWindowConverter();
					mbinding.Bindings.Add(new Binding("ViewportHeight") { Source = leftOriginal });
					mbinding.Bindings.Add(new Binding("TotalLineCount") { Source = this });
					mbinding.Bindings.Add(new Binding("ExtentHeight") { Source = leftOriginal });
					BindingOperations.SetBinding(rectangle, Rectangle.HeightProperty, mbinding);
				}
			}
		}

		void LoadXamlPackage(ScrollViewer scrollViewer, string fileName)
		{
			if (File.Exists(fileName))
			{
				using (FileStream fStream = new FileStream(fileName, FileMode.OpenOrCreate))
				{
					var clientElement = XamlReader.Load(fStream) as UIElement;
					DockPanel.SetDock(clientElement, Dock.Left);

					/*
					 * コンテンツを直接ScrollViewerに張り付けた場合、
					 * ScrollViewerの横サイズに合わせて、コンテンツもサイズ変更される。
					 * 
					 * コンテンツのサイズ変更処理に多大なコストがかかる場合、
					 * ウィンドウサイズ変更がもたつくため、DockPanelを使用し、
					 * コンテンツより処理の軽いもの(Canvas)にサイズ変更を肩代わりさせる。
					 */
					var dockPanel = new DockPanel();
					dockPanel.LastChildFill = true;
					dockPanel.Children.Add(clientElement);
					dockPanel.Children.Add(new Canvas());

					scrollViewer.Content = dockPanel;
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

		private void scrollByCanvas(double mousePointY)
		{
			double percent = mousePointY / TotalLineCount;
			leftOriginal.ScrollToVerticalOffset(leftOriginal.ScrollableHeight * percent);
		}

		private void SyncScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			if (e.VerticalChange != 0)
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

				if (sender == leftOriginal)
				{
					var scrlRate = leftOriginal.VerticalOffset / (leftOriginal.ExtentHeight - leftOriginal.ViewportHeight);

					var viewRate = leftOriginal.ViewportHeight / leftOriginal.ExtentHeight;
					var viewLine = TotalLineCount * viewRate;

					Canvas.SetTop(rectangle, scrlRate * (TotalLineCount - viewLine));
				}
			}
		}
	}

	class MyDupleViewWindowConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			var v0 = (double)values[0];
			var v1 = (int)values[1];
			var v2 = (double)values[2];

			return v0 * v1 / v2;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
