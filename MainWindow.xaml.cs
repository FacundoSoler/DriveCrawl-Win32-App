using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using DriveCrawl_Win32_App.Data;
using DriveCrawl_Win32_App.Models;
using DriveCrawl_Win32_App.Services;

namespace DriveCrawl_Win32_App
{
    public class FileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long b)
                return b switch
                {
                    >= 1_073_741_824 => $"{b / 1_073_741_824.0:F1} GB",
                    >= 1_048_576     => $"{b / 1_048_576.0:F1} MB",
                    >= 1_024         => $"{b / 1_024.0:F0} KB",
                    _                => $"{b} B"
                };
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<FileModel> _results = new();
        private readonly DatabaseService _db     = new();
        private readonly SearchEngine    _search = new();
        private CancellationTokenSource? _cts;

        public MainWindow()
        {
            InitializeComponent();
            ResultsGrid.ItemsSource = _results;
            ColSize.Binding = new Binding("FileSize") { Converter = new FileSizeConverter() };
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) StartSearch();
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
            => StartSearch();

        private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsGrid.SelectedItem is FileModel file)
                OpenContainingFolder(file.FullPath);
        }

        private async void StartSearch()
        {
            string query = TxtSearch.Text.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _results.Clear();
            SetBusy(true);
            EmptyState.Visibility = Visibility.Collapsed;
            StatusLabel.Text = "Buscando...";
            ResultCount.Text = "0 resultados";

            try
            {
                List<WatchedFolders> folders = await Task.Run(() => _db.GetFolders(), token);

                if (folders.Count == 0)
                {
                    EmptyLabel.Text = "No hay carpetas configuradas en la base de datos.";
                    EmptyState.Visibility = Visibility.Visible;
                    StatusLabel.Text = "Sin carpetas configuradas.";
                    return;
                }

                var sw = Stopwatch.StartNew();
                var found = await Task.Run(() => _search.Search(folders, query), token);
                sw.Stop();

                long totalResultsSize = 0;

                foreach (var f in found)
                {
                    token.ThrowIfCancellationRequested();
                    _results.Add(f);
                    totalResultsSize = totalResultsSize + f.FileSize;
                }

                int count = _results.Count;

                var converter = new FileSizeConverter();
                string legibleSize = converter.Convert(totalResultsSize, typeof(string), null, null)?.ToString() ?? "0 B";

                ResultCount.Text = count + (count != 1 ? " resultados" : " resultado");
                StatusLabel.Text = "Listo en " + sw.Elapsed.TotalSeconds.ToString("F2") + "s" + " Total Size: " + legibleSize;

                if (count == 0)
                {
                    EmptyLabel.Text = "Sin resultados para: " + query;
                    EmptyState.Visibility = Visibility.Visible;
                }
            }
            catch (OperationCanceledException)
            {
                StatusLabel.Text = "Busqueda cancelada.";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "Error: " + ex.Message;
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            BtnSearch.IsEnabled       = !busy;
            SearchProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void OpenContainingFolder(string fullPath)
        {
            try
            {
                string? folder = Path.GetDirectoryName(fullPath);
                if (folder == null || !Directory.Exists(folder))
                {
                    MessageBox.Show("No se encontro la carpeta.", "Aviso",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName        = "explorer.exe",
                    Arguments       = "/select,\"" + fullPath + "\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al abrir Explorer: " + ex.Message,
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
