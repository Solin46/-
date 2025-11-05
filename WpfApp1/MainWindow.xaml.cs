using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace VectorEditor
{
    public partial class MainWindow : Window
    {
        #region Переменные и константы

        private enum ToolMode { Select, Rectangle, Ellipse, Line, Polygon }
        private enum EditorMode { Drawing, Editing }

        private ToolMode currentTool = ToolMode.Rectangle;
        private EditorMode currentMode = EditorMode.Drawing;

        private Shape currentShape;
        private Point startPoint;
        private bool isDrawing = false;
        private bool isMoving = false;
        private Point moveStartPoint;
        private Shape selectedShape;

        private Color fillColor = Colors.LightBlue;
        private Color strokeColor = Colors.Black;
        private double strokeThickness = 2;

        private Stack<EditorAction> undoStack = new Stack<EditorAction>();
        private Stack<Shape> deletedShapes = new Stack<Shape>();
        private const int MAX_UNDO_STEPS = 5;

        private List<Point> polygonPoints = new List<Point>();
        private bool isDrawingPolygon = false;

        // Предустановленные цвета
        private readonly (string Name, Color Color)[] fillColors = new[]
        {
            ("Красный", Colors.Red),
            ("Синий", Colors.Blue),
            ("Зеленый", Colors.Green),
            ("Желтый", Colors.Yellow),
            ("Оранжевый", Colors.Orange),
            ("Фиолетовый", Colors.Purple),
            ("Розовый", Colors.Pink),
            ("Голубой", Colors.LightBlue),
            ("Салатовый", Colors.LightGreen),
            ("Белый", Colors.White),
            ("Черный", Colors.Black),
            ("Серый", Colors.Gray)
        };

        private readonly (string Name, Color Color)[] strokeColors = new[]
        {
            ("Черный", Colors.Black),
            ("Красный", Colors.Red),
            ("Синий", Colors.Blue),
            ("Зеленый", Colors.Green),
            ("Фиолетовый", Colors.Purple),
            ("Оранжевый", Colors.Orange),
            ("Белый", Colors.White),
            ("Серый", Colors.Gray),
            ("Коричневый", Colors.Brown)
        };

        #endregion

        #region Классы для системы отмены

        public class EditorAction
        {
            public string Type { get; set; }
            public Shape TargetShape { get; set; }
            public object OldValue { get; set; }
            public object NewValue { get; set; }
            public string PropertyName { get; set; }
        }

        #endregion

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        #region Инициализация

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Устанавливаем начальные значения
                shapeComboBox.SelectedIndex = 0;
                zoomSlider.Value = 1.0;
                if (zoomText != null)
                    zoomText.Text = "100%";

                UpdateColorButtons();

                // Подписываемся на события
                shapeComboBox.SelectionChanged += ShapeComboBox_SelectionChanged;
                zoomSlider.ValueChanged += ZoomSlider_ValueChanged;
                drawModeRadio.Checked += DrawModeRadio_Checked;
                editModeRadio.Checked += EditModeRadio_Checked;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}");
            }
        }

        private void UpdateColorButtons()
        {
            if (fillColorButton != null)
            {
                fillColorButton.Background = new SolidColorBrush(fillColor);
                fillColorButton.Foreground = GetContrastColor(fillColor);

                if (currentMode == EditorMode.Editing && selectedShape != null)
                    fillColorButton.ToolTip = "Изменить цвет заливки ВЫБРАННОЙ фигуры";
                else
                    fillColorButton.ToolTip = "Установить цвет заливки для НОВЫХ фигур";
            }

            if (strokeColorButton != null)
            {
                strokeColorButton.Background = new SolidColorBrush(strokeColor);
                strokeColorButton.Foreground = GetContrastColor(strokeColor);

                if (currentMode == EditorMode.Editing && selectedShape != null)
                    strokeColorButton.ToolTip = "Изменить цвет обводки ВЫБРАННОЙ фигуры";
                else
                    strokeColorButton.ToolTip = "Установить цвет обводки для НОВЫХ фигур";
            }
        }

        private Brush GetContrastColor(Color color)
        {
            double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
            return luminance > 0.5 ? Brushes.Black : Brushes.White;
        }

        #endregion

        #region Обработчики режимов

        private void DrawModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            currentMode = EditorMode.Drawing;
            DeselectShape();
            UpdateColorButtons();
            System.Diagnostics.Debug.WriteLine("🆕 Режим: Создание (создание новых фигур)");
        }

        private void EditModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            currentMode = EditorMode.Editing;
            UpdateColorButtons();
            System.Diagnostics.Debug.WriteLine("🔧 Режим: Редактирование (изменение существующих фигур)");
        }

        private void ShapeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (shapeComboBox.SelectedIndex == 0) currentTool = ToolMode.Rectangle;
            else if (shapeComboBox.SelectedIndex == 1) currentTool = ToolMode.Ellipse;
            else if (shapeComboBox.SelectedIndex == 2) currentTool = ToolMode.Line;
            else if (shapeComboBox.SelectedIndex == 3) currentTool = ToolMode.Polygon;
        }

        #endregion

        #region Масштабирование

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            if (zoomSlider == null) return;
            zoomSlider.Value = Math.Min(zoomSlider.Maximum, zoomSlider.Value + 0.1);
            if (zoomText != null)
                zoomText.Text = $"{zoomSlider.Value * 100:0}%";
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            if (zoomSlider == null) return;
            zoomSlider.Value = Math.Max(zoomSlider.Minimum, zoomSlider.Value - 0.1);
            if (zoomText != null)
                zoomText.Text = $"{zoomSlider.Value * 100:0}%";
        }

        private void ZoomResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (zoomSlider == null) return;
            zoomSlider.Value = 1.0;
            if (zoomText != null)
                zoomText.Text = "100%";
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (zoomSlider == null) return;

            double scale = zoomSlider.Value;
            if (zoomText != null)
                zoomText.Text = $"{scale * 100:0}%";

            if (mainCanvas != null)
            {
                var transform = new ScaleTransform(scale, scale);
                mainCanvas.LayoutTransform = transform;
            }
        }

        #endregion

        #region Обработка мыши

        private void MainCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var position = e.GetPosition(mainCanvas);

            if (currentMode == EditorMode.Editing)
            {
                // режим редактирования: изменение цвета и перемещение
                var element = mainCanvas.InputHitTest(position) as FrameworkElement;
                if (element is Shape shape)
                {
                    SelectShape(shape);
                    isMoving = true;
                    moveStartPoint = position;
                }
                else
                {
                    DeselectShape();
                }
            }
            else
            {
                // режим создания: создание новых фигур
                if (currentTool == ToolMode.Polygon)
                {
                    if (e.ClickCount == 2) // Двойной клик для завершения
                    {
                        CompletePolygon();
                        e.Handled = true;
                    }
                    else if (e.LeftButton == MouseButtonState.Pressed)
                    {
                        if (!isDrawingPolygon)
                        {
                            StartPolygon(position);
                        }
                        else
                        {
                            AddPolygonPoint(position);
                        }
                        e.Handled = true;
                    }
                }
                else
                {
                    DeselectShape();
                    StartDrawingShape(position);
                }
            }
        }

        private void MainCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(mainCanvas);

            if (isMoving && selectedShape != null)
            {
                // Перемещение фигуры
                double deltaX = position.X - moveStartPoint.X;
                double deltaY = position.Y - moveStartPoint.Y;

                if (selectedShape is Line line)
                {
                    // особая логика для линии
                    line.X1 += deltaX;
                    line.Y1 += deltaY;
                    line.X2 += deltaX;
                    line.Y2 += deltaY;
                }
                else if (selectedShape is Polygon polygon)
                {
                    // особая логика для многоугольника
                    var newPoints = new PointCollection();
                    foreach (Point point in polygon.Points)
                    {
                        newPoints.Add(new Point(point.X + deltaX, point.Y + deltaY));
                    }
                    polygon.Points = newPoints;
                }
                else
                {
                    // логика для других фигур
                    double currentLeft = Canvas.GetLeft(selectedShape);
                    double currentTop = Canvas.GetTop(selectedShape);

                    if (!double.IsNaN(currentLeft) && !double.IsNaN(currentTop))
                    {
                        Canvas.SetLeft(selectedShape, currentLeft + deltaX);
                        Canvas.SetTop(selectedShape, currentTop + deltaY);
                    }
                }

                moveStartPoint = position;
            }
            else if (isDrawing && currentShape != null && currentTool != ToolMode.Polygon)
            {
                UpdateShapeSize(position);
            }
        }

        private void MainCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isDrawing && currentTool != ToolMode.Polygon)
            {
                FinishDrawingShape();
            }
            else if (isMoving)
            {
                isMoving = false;
                if (selectedShape != null)
                {
                    SaveAction("Move", selectedShape);
                }
            }
        }

        #endregion

        #region Создание фигур

        private void StartDrawingShape(Point position)
        {
            isDrawing = true;
            startPoint = position;

            switch (currentTool)
            {
                case ToolMode.Rectangle:
                    currentShape = new Rectangle
                    {
                        Fill = new SolidColorBrush(fillColor),
                        Stroke = new SolidColorBrush(strokeColor),
                        StrokeThickness = strokeThickness
                    };
                    break;
                case ToolMode.Ellipse:
                    currentShape = new Ellipse
                    {
                        Fill = new SolidColorBrush(fillColor),
                        Stroke = new SolidColorBrush(strokeColor),
                        StrokeThickness = strokeThickness
                    };
                    break;
                case ToolMode.Line:
                    currentShape = new Line
                    {
                        Stroke = new SolidColorBrush(strokeColor),
                        StrokeThickness = strokeThickness,
                        X1 = position.X,
                        Y1 = position.Y,
                        X2 = position.X,
                        Y2 = position.Y
                    };
                    break;
            }

            if (currentShape != null && currentTool != ToolMode.Line)
            {
                Canvas.SetLeft(currentShape, position.X);
                Canvas.SetTop(currentShape, position.Y);
                currentShape.Width = 0;
                currentShape.Height = 0;
                mainCanvas.Children.Add(currentShape);
            }
            else if (currentTool == ToolMode.Line)
            {
                mainCanvas.Children.Add(currentShape);
            }
        }

        private void UpdateShapeSize(Point position)
        {
            if (currentTool == ToolMode.Line && currentShape is Line line)
            {
                line.X2 = position.X;
                line.Y2 = position.Y;
            }
            else if (currentShape != null)
            {
                double width = position.X - startPoint.X;
                double height = position.Y - startPoint.Y;

                if (width < 0)
                {
                    Canvas.SetLeft(currentShape, position.X);
                    width = Math.Abs(width);
                }
                if (height < 0)
                {
                    Canvas.SetTop(currentShape, position.Y);
                    height = Math.Abs(height);
                }

                currentShape.Width = width;
                currentShape.Height = height;
            }
        }

        private void FinishDrawingShape()
        {
            isDrawing = false;
            if (currentShape != null)
            {
                if ((currentShape.Width > 1 || currentShape.Height > 1) || currentTool == ToolMode.Line)
                {
                    SelectShape(currentShape);
                    SaveAction("Create", currentShape);
                }
                else
                {
                    mainCanvas.Children.Remove(currentShape);
                }
                currentShape = null;
            }
        }

        private void StartPolygon(Point position)
        {
            isDrawingPolygon = true;
            polygonPoints.Clear();
            polygonPoints.Add(position);

            currentShape = new Polyline
            {
                Stroke = new SolidColorBrush(strokeColor),
                StrokeThickness = strokeThickness,
                StrokeDashArray = new DoubleCollection() { 4, 2 } // Пунктирная линия для preview
            };
            currentShape.SetValue(Polyline.PointsProperty, new PointCollection(polygonPoints));
            mainCanvas.Children.Add(currentShape);
        }

        private void AddPolygonPoint(Point position)
        {
            // Проверяем, не пересекается ли новая линия с существующими
            if (polygonPoints.Count >= 2 && LinesIntersect(polygonPoints, position))
            {
                // Если линии пересекаются, завершаем многоугольник
                CompletePolygon();
            }
            else
            {
                polygonPoints.Add(position);
                if (currentShape is Polyline polyline)
                {
                    polyline.Points = new PointCollection(polygonPoints);
                }
            }
        }

        private void CompletePolygon()
        {
            if (mainCanvas == null || polygonPoints.Count < 3) return;

            // Автоматически замыкаем многоугольник, добавляя первую точку в конец
            if (polygonPoints.Count > 2 && polygonPoints[0] != polygonPoints[polygonPoints.Count - 1])
            {
                polygonPoints.Add(polygonPoints[0]);
            }

            var polygon = new Polygon
            {
                Fill = new SolidColorBrush(fillColor),
                Stroke = new SolidColorBrush(strokeColor),
                StrokeThickness = strokeThickness
            };
            polygon.Points = new PointCollection(polygonPoints);

            // Удаляем временную полилинию и добавляем готовый многоугольник
            mainCanvas.Children.Remove(currentShape);
            mainCanvas.Children.Add(polygon);

            // Добавляем обработчики для перемещения и удаления
            polygon.MouseDown += Shape_MouseDown;
            polygon.MouseMove += Shape_MouseMove;
            polygon.MouseUp += Shape_MouseUp;

            SelectShape(polygon);
            SaveAction("Create", polygon);

            isDrawingPolygon = false;
            polygonPoints.Clear();
            currentShape = null;
        }

        // Метод для проверки пересечения линий
        private bool LinesIntersect(List<Point> points, Point newPoint)
        {
            if (points.Count < 2) return false;

            // Создаем новую линию от последней точки к новой
            Line newLine = new Line
            {
                X1 = points[points.Count - 1].X,
                Y1 = points[points.Count - 1].Y,
                X2 = newPoint.X,
                Y2 = newPoint.Y
            };

            // Проверяем пересечение новой линии со всеми существующими линиями (кроме смежных)
            for (int i = 0; i < points.Count - 2; i++)
            {
                Line existingLine = new Line
                {
                    X1 = points[i].X,
                    Y1 = points[i].Y,
                    X2 = points[i + 1].X,
                    Y2 = points[i + 1].Y
                };

                if (DoLinesIntersect(existingLine, newLine))
                {
                    return true;
                }
            }

            return false;
        }

        // Метод для определения пересечения двух отрезков
        private bool DoLinesIntersect(Line line1, Line line2)
        {
            double x1 = line1.X1, y1 = line1.Y1;
            double x2 = line1.X2, y2 = line1.Y2;
            double x3 = line2.X1, y3 = line2.Y1;
            double x4 = line2.X2, y4 = line2.Y2;

            // Вычисляем ориентированные площади треугольников
            double det = (x2 - x1) * (y4 - y3) - (x4 - x3) * (y2 - y1);
            if (det == 0) return false; // Линии параллельны

            double t = ((x3 - x1) * (y4 - y3) - (x4 - x3) * (y3 - y1)) / det;
            double u = -((x3 - x1) * (y2 - y1) - (x2 - x1) * (y3 - y1)) / det;

            // Проверяем, что точка пересечения находится на обоих отрезках
            return (t >= 0 && t <= 1 && u >= 0 && u <= 1);
        }

        #endregion

        #region Выделение фигур

        private void SelectShape(Shape shape)
        {
            DeselectShape();
            selectedShape = shape;
            UpdateColorButtons();
        }

        private void DeselectShape()
        {
            if (selectedShape != null)
            {
                selectedShape = null;
                UpdateColorButtons();
            }
        }

        // Обработчики событий для фигур
        private void Shape_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (currentMode == EditorMode.Editing && sender is Shape shape)
            {
                SelectShape(shape);
                isMoving = true;
                moveStartPoint = e.GetPosition(mainCanvas);
                e.Handled = true;
            }
        }

        private void Shape_MouseMove(object sender, MouseEventArgs e)
        {
            // Логика перемещения обрабатывается в MainCanvas_MouseMove
        }

        private void Shape_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isMoving)
            {
                isMoving = false;
                if (selectedShape != null)
                {
                    SaveAction("Move", selectedShape);
                }
            }
        }

        #endregion

        #region Система отмены и действий

        private void SaveAction(string type, Shape shape, object oldValue = null, object newValue = null, string property = null)
        {
            var action = new EditorAction
            {
                Type = type,
                TargetShape = shape,
                OldValue = oldValue,
                NewValue = newValue,
                PropertyName = property
            };

            undoStack.Push(action);

            if (undoStack.Count > MAX_UNDO_STEPS)
            {
                var array = undoStack.ToArray();
                undoStack.Clear();
                for (int i = 0; i < MAX_UNDO_STEPS; i++)
                {
                    undoStack.Push(array[i]);
                }
            }
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (undoStack.Count == 0)
            {
                return;
            }

            var lastAction = undoStack.Pop();

            switch (lastAction.Type)
            {
                case "Create":
                    mainCanvas.Children.Remove(lastAction.TargetShape);
                    if (selectedShape == lastAction.TargetShape) DeselectShape();
                    break;

                case "ModifyColor":
                    if (lastAction.OldValue is Color oldColor)
                    {
                        lastAction.TargetShape.Fill = new SolidColorBrush(oldColor);
                        fillColor = oldColor;
                        UpdateColorButtons();
                    }
                    break;

                case "ModifyStroke":
                    if (lastAction.OldValue is Color oldStrokeColor)
                    {
                        lastAction.TargetShape.Stroke = new SolidColorBrush(oldStrokeColor);
                        strokeColor = oldStrokeColor;
                        UpdateColorButtons();
                    }
                    break;

                case "Delete":
                    mainCanvas.Children.Add(lastAction.TargetShape);
                    SelectShape(lastAction.TargetShape);
                    break;

                case "Move":
                    DeselectShape();
                    break;
            }
        }

        private void RestoreDeletedButton_Click(object sender, RoutedEventArgs e)
        {
            if (deletedShapes.Count > 0)
            {
                var restoredShape = deletedShapes.Pop();
                mainCanvas.Children.Add(restoredShape);

                // Восстанавливаем оригинальные свойства перед выделением
                restoredShape.StrokeThickness = strokeThickness;
                restoredShape.Stroke = new SolidColorBrush(strokeColor);

                SelectShape(restoredShape);
                SaveAction("Create", restoredShape);
            }
        }

        #endregion

        #region Обработчики кнопок

        private void FillColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (fillColorButton == null) return;

            var contextMenu = new ContextMenu();

            foreach (var colorInfo in fillColors)
            {
                // Создаем StackPanel с цветным прямоугольником и текстом
                var stackPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Background = Brushes.White
                };

                // Цветной прямоугольник
                var colorRect = new Border
                {
                    Width = 20,
                    Height = 15,
                    Background = new SolidColorBrush(colorInfo.Color),
                    Margin = new Thickness(0, 0, 8, 0),
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1)
                };

                // Название цвета
                var textBlock = new TextBlock
                {
                    Text = colorInfo.Name,
                    Foreground = Brushes.Black,
                    VerticalAlignment = VerticalAlignment.Center
                };

                stackPanel.Children.Add(colorRect);
                stackPanel.Children.Add(textBlock);

                var menuItem = new MenuItem
                {
                    Header = stackPanel, // Используем StackPanel как содержимое
                    Background = Brushes.White
                };

                menuItem.Click += (s, args) =>
                {
                    if (currentMode == EditorMode.Editing && selectedShape != null)
                    {
                        if (selectedShape is Line)
                        {
                            return;
                        }

                        var oldColor = ((SolidColorBrush)selectedShape.Fill).Color;
                        SaveAction("ModifyColor", selectedShape, oldColor, colorInfo.Color, "FillColor");
                        selectedShape.Fill = new SolidColorBrush(colorInfo.Color);
                    }
                    else if (currentMode == EditorMode.Drawing)
                    {
                        fillColor = colorInfo.Color;
                        UpdateColorButtons();
                    }
                };
                contextMenu.Items.Add(menuItem);
            }

            contextMenu.PlacementTarget = fillColorButton;
            contextMenu.IsOpen = true;
        }

        private void StrokeColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (strokeColorButton == null) return;

            var contextMenu = new ContextMenu();

            foreach (var colorInfo in strokeColors)
            {
                // Создаем StackPanel с цветным прямоугольником и текстом
                var stackPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Background = Brushes.White
                };

                var colorRect = new Border
                {
                    Width = 20,
                    Height = 15,
                    Background = new SolidColorBrush(colorInfo.Color),
                    Margin = new Thickness(0, 0, 8, 0),
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1)
                };

                var textBlock = new TextBlock
                {
                    Text = colorInfo.Name,
                    Foreground = Brushes.Black,
                    VerticalAlignment = VerticalAlignment.Center
                };

                stackPanel.Children.Add(colorRect);
                stackPanel.Children.Add(textBlock);

                var menuItem = new MenuItem
                {
                    Header = stackPanel,
                    Background = Brushes.White
                };

                menuItem.Click += (s, args) =>
                {
                    if (currentMode == EditorMode.Editing && selectedShape != null)
                    {
                        var oldColor = ((SolidColorBrush)selectedShape.Stroke).Color;
                        SaveAction("ModifyStroke", selectedShape, oldColor, colorInfo.Color, "StrokeColor");
                        selectedShape.Stroke = new SolidColorBrush(colorInfo.Color);
                        strokeColor = colorInfo.Color;
                        UpdateColorButtons();
                    }
                    else if (currentMode == EditorMode.Drawing)
                    {
                        strokeColor = colorInfo.Color;
                        UpdateColorButtons();
                    }
                };
                contextMenu.Items.Add(menuItem);
            }

            contextMenu.PlacementTarget = strokeColorButton;
            contextMenu.IsOpen = true;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedShape != null && mainCanvas != null)
            {
                SaveAction("Delete", selectedShape);
                deletedShapes.Push(selectedShape);
                if (deletedShapes.Count > 5) deletedShapes.Pop();

                mainCanvas.Children.Remove(selectedShape);
                DeselectShape();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveToSvg();
        }

        #endregion

        #region Сохранение в SVG

        private void SaveToSvg()
        {
            if (mainCanvas == null) return;

            var saveDialog = new SaveFileDialog
            {
                Filter = "SVG files (*.svg)|*.svg|All files (*.*)|*.*",
                DefaultExt = "svg"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    using (var writer = new StreamWriter(saveDialog.FileName))
                    {
                        writer.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                        writer.WriteLine($"<svg width=\"{mainCanvas.Width}\" height=\"{mainCanvas.Height}\" xmlns=\"http://www.w3.org/2000/svg\">");

                        // ✅ Белый фон для SVG
                        writer.WriteLine("<rect width=\"100%\" height=\"100%\" fill=\"white\"/>");

                        int savedCount = 0;
                        foreach (var element in mainCanvas.Children)
                        {
                            if (element is Shape shape)
                            {
                                string svg = ShapeToSvg(shape);
                                if (!string.IsNullOrEmpty(svg))
                                {
                                    writer.WriteLine(svg);
                                    savedCount++;
                                }
                            }
                        }

                        writer.WriteLine("</svg>");

                        System.Diagnostics.Debug.WriteLine($"💾 Сохранено {savedCount} фигур");
                    }

                    MessageBox.Show("Файл успешно сохранен!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка сохранения: {ex.Message}");
                }
            }
        }

        private string ShapeToSvg(Shape shape)
        {
            try
            {
                if (shape is Rectangle rect)
                {
                    double left = Canvas.GetLeft(rect);
                    double top = Canvas.GetTop(rect);

                    if (double.IsNaN(left)) left = 0;
                    if (double.IsNaN(top)) top = 0;
                    if (rect.Width <= 0 || rect.Height <= 0) return "";

                    Color fillColor = ((SolidColorBrush)rect.Fill).Color;
                    Color strokeColor = ((SolidColorBrush)rect.Stroke).Color;

                    return $"<rect x=\"{left:F0}\" y=\"{top:F0}\" width=\"{rect.Width:F0}\" height=\"{rect.Height:F0}\" " +
                           $"fill=\"{ColorToHex(fillColor)}\" " +
                           $"stroke=\"{ColorToHex(strokeColor)}\" " +
                           $"stroke-width=\"{rect.StrokeThickness:F0}\"/>";
                }
                else if (shape is Ellipse ellipse)
                {
                    double left = Canvas.GetLeft(ellipse);
                    double top = Canvas.GetTop(ellipse);

                    if (double.IsNaN(left)) left = 0;
                    if (double.IsNaN(top)) top = 0;
                    if (ellipse.Width <= 0 || ellipse.Height <= 0) return "";

                    double cx = left + ellipse.Width / 2;
                    double cy = top + ellipse.Height / 2;
                    double rx = ellipse.Width / 2;
                    double ry = ellipse.Height / 2;

                    Color fillColor = ((SolidColorBrush)ellipse.Fill).Color;
                    Color strokeColor = ((SolidColorBrush)ellipse.Stroke).Color;

                    return $"<ellipse cx=\"{cx:F0}\" cy=\"{cy:F0}\" rx=\"{rx:F0}\" ry=\"{ry:F0}\" " +
                           $"fill=\"{ColorToHex(fillColor)}\" " +
                           $"stroke=\"{ColorToHex(strokeColor)}\" " +
                           $"stroke-width=\"{ellipse.StrokeThickness:F0}\"/>";
                }
                else if (shape is Line line)
                {
                    Color strokeColor = ((SolidColorBrush)line.Stroke).Color;

                    return $"<line x1=\"{line.X1:F0}\" y1=\"{line.Y1:F0}\" x2=\"{line.X2:F0}\" y2=\"{line.Y2:F0}\" " +
                           $"stroke=\"{ColorToHex(strokeColor)}\" " +
                           $"stroke-width=\"{line.StrokeThickness:F0}\"/>";
                }
                else if (shape is Polygon polygon)
                {
                    if (polygon.Points.Count < 3) return "";

                    string points = "";
                    foreach (Point point in polygon.Points)
                    {
                        points += $"{point.X:F0},{point.Y:F0} ";
                    }

                    Color fillColor = ((SolidColorBrush)polygon.Fill).Color;
                    Color strokeColor = ((SolidColorBrush)polygon.Stroke).Color;

                    return $"<polygon points=\"{points.Trim()}\" " +
                           $"fill=\"{ColorToHex(fillColor)}\" " +
                           $"stroke=\"{ColorToHex(strokeColor)}\" " +
                           $"stroke-width=\"{polygon.StrokeThickness:F0}\"/>";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка конвертации фигуры: {ex.Message}");
            }

            return "";
        }

        private string ColorToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        #endregion
    }
}