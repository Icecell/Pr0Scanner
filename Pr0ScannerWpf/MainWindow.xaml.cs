using Pr0grammScanner;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using Tesseract;

namespace Pr0ScannerWpf
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Pr0grammScannerMain scanner;
        private DispatcherTimer dispatcherTimer = new DispatcherTimer();

        public MainWindow()
        {
            InitializeComponent();

            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            dispatcherTimer.Start();

            ClearBtn_Click(null, null);
        }

        // Called in a 500ms period. Used to check scanner, calc results and add pics to GUI
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            // Badly need the scanner
            if (scanner == null)
                return;

            // Check scanner
            string statusString = "";
            switch (scanner.ScannerStatus)
            {
                case ScannerStatus.Running:
                    statusString = "läuft";
                    break;
                case ScannerStatus.Stopped:
                    statusString = "angehalten";
                    break;
                case ScannerStatus.Startup:
                    statusString = "startet ...";
                    break;
                case ScannerStatus.Stopping:
                    statusString = "anhalten ...";
                    break;
            }
            this.statusTextBlock.Text = statusString;

            // Enables start button when stopped
            if (scanner.ScannerStatus == ScannerStatus.Stopped)
            {
                this.startBtn.IsEnabled = true;
                this.startBtn.Content = "Starten";
            }

            // Adds all pics from ResultQueue to GUI
            while (scanner.ResultQueue.Count > 0)
            {
                var jobResult = new JobResult();
                if(scanner.ResultQueue.TryDequeue(out jobResult))
                {
                    var panel = new StackPanel();
                    panel.Margin = new Thickness(2);

                    var image = new JobImage();
                    image.Width = scanner.Settings.PreviewPicSize;
                    image.Height = scanner.Settings.PreviewPicSize;
                    image.Url = jobResult.Url;
                    image.MouseLeftButtonUp += Image_MouseLeftButtonUp;
                    image.MouseRightButtonUp += Image_MouseRightButtonUp;
                    image.Source = Imaging.CreateBitmapSourceFromBitmap(jobResult.Bitmap);
                    image.Cursor = Cursors.Hand;
                    image.Stretch = Stretch.UniformToFill;
                    jobResult.Bitmap.Dispose();

                    var textBlock = new JobTextBlock();
                    textBlock.Value = jobResult.Value;
                    textBlock.Text = $"{jobResult.Value} €";
                    textBlock.HorizontalAlignment = HorizontalAlignment.Center;

                    panel.Children.Add(image);
                    panel.Children.Add(textBlock);
                    panel.Background = jobResult.Value >= scanner.Settings.MinValue && jobResult.Value <= scanner.Settings.MaxValue ? Brushes.DarkGreen : Brushes.Red;
                    this.outputWrapPanel.Children.Add(panel);
                }
            }

            // Calculates result
            float sumValues = 0;
            int picsError = 0;
            foreach (var wrapChild in this.outputWrapPanel.Children)
            {
                var stack = wrapChild as StackPanel;
                if(stack != null)
                {
                    foreach (var stackChild in stack.Children)
                    {
                        var textBlock = stackChild as JobTextBlock;
                        if(textBlock != null)
                        {
                            if (textBlock.Value >= scanner.Settings.MinValue && textBlock.Value <= scanner.Settings.MaxValue)
                                sumValues += textBlock.Value;
                            else
                                picsError++;
                            break;
                        }
                    }
                }
            }

            // Displays result
            this.sumTextBlock.Text = $"{sumValues}";
            this.picsTodoTextBlock.Text = $"{scanner.jobQueue.Count}";
            this.picsDoneTextBlock.Text = $"{this.outputWrapPanel.Children.Count}";
            this.picsErrorTextBlock.Text = $"{picsError}";
        }

        private bool checkTesseract()
        {
            try
            {
                new TesseractEngine(scanner.Settings.TesseractEngineDataFolder, "eng", EngineMode.Default).Dispose();
            }
            catch (System.Exception e)
            {
                MessageBox.Show("Fehler beim Starten von Tesseract. Ist der Ordner \"tessdata\" im gleichen Ordner wie die exe?", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            // Stop
            if (scanner != null && scanner.ScannerStatus != ScannerStatus.Stopped)
            {
                scanner.Stop = true;
                this.startBtn.IsEnabled = false;
                this.startBtn.Content = "Stoppe ...";
            }

            // Start
            else
            {
                scanner = new Pr0grammScannerMain();

                try
                {
                    scanner.Settings = Settings.Load();
                }
                catch (System.Exception ex) // Error reading json, but file found!
                {
                    MessageBoxResult result = MessageBox.Show($"{ex.Message}\nDie Datei settings.json mit Vorlage überschreiben?",
                                          "Fehler beim Lesen von settings.json",
                                          MessageBoxButton.YesNo,
                                          MessageBoxImage.Error);
                    if (result == MessageBoxResult.Yes)
                    {
                        scanner.Settings = new Settings();
                        scanner.Settings.Save();
                    }
                    else
                    {
                        scanner = null;
                        return;
                    }
                }
                
                scanner.Settings.SetScalingFactor((float)PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11);

                // Check if tesseract is starting
                if(checkTesseract())
                {
                    Thread thread = new Thread(new ThreadStart(scanner.Run));
                    thread.Start();
                    this.startBtn.Content = "Stoppen";
                }
            }
        }

        // Stops the scanner
        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            if(scanner != null)
                scanner.Stop = true;
            this.startBtn.IsEnabled = false;
        }

        // Shutdown
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopBtn_Click(sender, null);
            dispatcherTimer.Stop();
        }

        // Clear wrap panel (all pics and text)
        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            this.outputWrapPanel.Children.Clear();
            this.sumTextBlock.Text = "0";
            System.GC.Collect();
        }

        // Open clicked pic in browser
        private void Image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var jobImage = sender as JobImage;
            if(jobImage != null)
            {
                System.Diagnostics.Process.Start(jobImage.Url);
            }
        }

        // Remove senders parent from WrapPanel
        private void Image_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var image = sender as Image;
            if (image != null)
            {
                var stackPanel = image.Parent as StackPanel;
                if (stackPanel != null)
                {
                    var wrapPanel = stackPanel.Parent as WrapPanel;
                    if(wrapPanel != null)
                        wrapPanel.Children.Remove(stackPanel);
                }
            }
        }
    }
}