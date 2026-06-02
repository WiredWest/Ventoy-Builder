using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Ventoy_Builder.Models;
using Ventoy_Builder.Services;

namespace Ventoy_Builder
{
    public partial class MainWindow : Window
    {
        private readonly UsbDriveService _usbService;
        private readonly IsoLibraryService _isoLibraryService;

        private UsbDriveInfo? _selectedDrive;

        private List<IsoLibraryItem> _isoLibraryItems = new();
        private List<BootMenuItem> _bootOrderItems = new();

        private CancellationTokenSource? _scanCancellationTokenSource;

        private Point _bootOrderDragStartPoint;

        private double _currentProgressValue = 0;

        private string _liveProgressLogLine = "";
        private bool _hasLiveProgressLine = false;

        private readonly string[] _allowedImageExtensions =
        {
            ".iso",
            ".img",
            ".wim",
            ".vhd",
            ".vhdx"
        };

        private readonly string[] _categoryFolders =
        {
            "Windows",
            "Linux",
            "Recovery",
            "Utilities",
            "Drivers",
            "Images",
            "Tools"
        };

        public MainWindow()
        {
            InitializeComponent();

            _usbService = new UsbDriveService();
            _isoLibraryService = new IsoLibraryService();

            AddLog("Ventoy Builder started.");

            SetProgress(
                "Ready",
                0,
                "Select a USB drive to begin building your Ventoy boot menu.");

            LoadUsbDrives();
            LoadIsoLibrary();

            UsbDriveList.SelectionChanged += UsbDriveList_SelectionChanged;
        }

        private void AddLog(string message)
        {
            LogTextBox.AppendText(
                $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");

            LogTextBox.ScrollToEnd();
        }

        private void SetProgress(string label, double value, string detail)
        {
            Dispatcher.Invoke(() =>
            {
                double clamped =
                    Math.Max(0, Math.Min(100, value));

                _currentProgressValue = clamped;

                ProgressLabel.Text = label;
                ProgressDetailText.Text = detail;

                CircularGaugePercentText.Text =
                    clamped >= 100
                        ? "100%"
                        : $"{(int)clamped}%";

                CircularGaugeNeedleRotate.Angle =
                    (clamped / 100d) * 180d;

                UpdateLiveProgressLog(label, clamped, detail);
            });
        }

        private void UpdateLiveProgressLog(
            string label,
            double value,
            string detail)
        {
            string percentText =
                value >= 100
                    ? "100%"
                    : $"{(int)value}%";

            string line =
                $"[{DateTime.Now:HH:mm:ss}] {label} — {percentText}";

            if (!string.IsNullOrWhiteSpace(detail))
            {
                line += $" — {detail}";
            }

            string existingText =
                LogTextBox.Text ?? "";

            if (!_hasLiveProgressLine)
            {
                _liveProgressLogLine = line;
                _hasLiveProgressLine = true;

                LogTextBox.AppendText(line + Environment.NewLine);
                LogTextBox.ScrollToEnd();
                return;
            }

            int index =
                existingText.LastIndexOf(
                    _liveProgressLogLine,
                    StringComparison.Ordinal);

            if (index >= 0)
            {
                string before =
                    existingText.Substring(0, index);

                string after =
                    existingText.Substring(index + _liveProgressLogLine.Length);

                LogTextBox.Text =
                    before + line + after;

                _liveProgressLogLine = line;

                LogTextBox.CaretIndex =
                    LogTextBox.Text.Length;

                LogTextBox.ScrollToEnd();
            }
            else
            {
                _liveProgressLogLine = line;

                LogTextBox.AppendText(line + Environment.NewLine);
                LogTextBox.ScrollToEnd();
            }

            if (value >= 100)
            {
                _hasLiveProgressLine = false;
                _liveProgressLogLine = "";
            }
        }

        private void SetScanningUi(bool isScanning)
        {
            ScanIsoFolderButton.IsEnabled = !isScanning;
            CancelScanButton.IsEnabled = isScanning;
        }

        private void LoadUsbDrives()
        {
            List<UsbDriveInfo> drives =
                _usbService.GetUsbDrives();

            UsbDriveList.ItemsSource = drives;

            StatusText.Text =
                $"Detected {drives.Count} removable drives";

            AddLog(
                $"Detected {drives.Count} removable drive(s).");
        }

        private bool ValidateSelectedDrive()
        {
            if (_selectedDrive == null)
            {
                MessageBox.Show(
                    "Please select a USB drive first.",
                    "No Drive Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            return true;
        }

        private void RefreshButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            LoadUsbDrives();

            DriveInfoText.Text = "";
            UsbContentsTreeView.Items.Clear();

            _selectedDrive = null;
            _bootOrderItems.Clear();

            RefreshBootOrderView();

            AddLog("Drive list refreshed.");

            SetProgress(
                "Drive list refreshed",
                0,
                "Select the USB drive you want to work with.");
        }

        private void UsbDriveList_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            _selectedDrive =
                UsbDriveList.SelectedItem as UsbDriveInfo;

            if (_selectedDrive == null)
            {
                DriveInfoText.Text = "";
                return;
            }

            DriveInfoText.Text =
                $"Drive: {_selectedDrive.DriveLetter}\n" +
                $"Label: {_selectedDrive.Label}\n" +
                $"File System: {_selectedDrive.FileSystem}\n" +
                $"Total Size: {_selectedDrive.SizeText}\n" +
                $"Free Space: {_selectedDrive.FreeSpaceText}\n" +
                $"Path: {_selectedDrive.FullPath}";

            AddLog(
                $"Selected USB drive: {_selectedDrive.DriveLetter} {_selectedDrive.Label}");

            SetProgress(
                "USB drive selected",
                0,
                "Now install or update Ventoy, then create folders and copy boot images.");

            RefreshUsbContents();
            RefreshBootOrderFromUsb();
        }

        private void LoadIsoLibrary()
        {
            _isoLibraryItems =
                _isoLibraryService.LoadLibrary();

            foreach (IsoLibraryItem item in _isoLibraryItems)
            {
                ApplyMetadataToIsoLibraryItem(item);
            }

            SaveIsoLibrary();
            RefreshIsoLibraryView();

            AddLog(
                $"Loaded {_isoLibraryItems.Count} ISO library item(s).");
        }

        private void SaveIsoLibrary()
        {
            _isoLibraryService.SaveLibrary(_isoLibraryItems);
        }

        private void RefreshIsoLibraryView()
        {
            string search =
                IsoSearchTextBox.Text?
                    .Trim()
                    .ToLowerInvariant() ?? "";

            IEnumerable<IsoLibraryItem> items =
                _isoLibraryItems;

            if (!string.IsNullOrWhiteSpace(search))
            {
                items = items.Where(item =>
                    item.FileName.ToLowerInvariant().Contains(search) ||
                    item.Category.ToLowerInvariant().Contains(search) ||
                    item.FullPath.ToLowerInvariant().Contains(search) ||
                    item.DisplayName.ToLowerInvariant().Contains(search) ||
                    item.DetectedType.ToLowerInvariant().Contains(search) ||
                    item.Architecture.ToLowerInvariant().Contains(search));
            }

            IsoLibraryList.ItemsSource =
                items
                    .OrderBy(item => item.DetectedType)
                    .ThenBy(item => item.DisplayName)
                    .ToList();
        }

        private void IsoSearchTextBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            RefreshIsoLibraryView();
        }

        private void ClearIsoLibraryButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageBoxResult confirm =
                MessageBox.Show(
                    "Clear the ISO library?\n\nThis does not delete actual files.",
                    "Clear Library",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            _isoLibraryItems.Clear();

            SaveIsoLibrary();
            RefreshIsoLibraryView();

            AddLog("ISO library cleared.");

            SetProgress(
                "Library cleared",
                0,
                "The saved list was cleared, but no ISO files were deleted.");
        }

        private async void IsoLibraryList_MouseDoubleClick(
            object sender,
            MouseButtonEventArgs e)
        {
            IsoLibraryItem? selected =
                IsoLibraryList.SelectedItem as IsoLibraryItem;

            if (selected == null)
                return;

            if (!File.Exists(selected.FullPath))
            {
                MessageBox.Show(
                    "This ISO library item no longer exists on disk.",
                    "Missing File",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (!ValidateSelectedDrive())
                return;

            await AddImageFilesAsync(
                new[] { selected.FullPath });
        }

        private async void ScanIsoFolderButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenFolderDialog dialog =
                new OpenFolderDialog();

            bool? result =
                dialog.ShowDialog();

            if (result != true)
                return;

            _scanCancellationTokenSource =
                new CancellationTokenSource();

            CancellationToken token =
                _scanCancellationTokenSource.Token;

            int addedCount = 0;

            try
            {
                SetScanningUi(true);

                SetProgress(
                    "Scanning for boot images",
                    0,
                    dialog.FolderName);

                AddLog($"Scanning folder for boot images: {dialog.FolderName}");

                addedCount =
                    await Task.Run(() =>
                        ScanFolderForImagesLive(
                            dialog.FolderName,
                            token));

                SaveIsoLibrary();
                RefreshIsoLibraryView();

                SetProgress(
                    "Finished scanning",
                    100,
                    $"Added {addedCount} new boot image(s) to the library.");

                AddLog(
                    $"Scan complete. Added {addedCount} new boot image(s).");
            }
            catch (Exception ex)
            {
                SetProgress(
                    "Scan failed",
                    _currentProgressValue,
                    ex.Message);

                MessageBox.Show(
                    ex.Message,
                    "Scan Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetScanningUi(false);
            }
        }

        private void CancelScanButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _scanCancellationTokenSource?.Cancel();

            AddLog("Cancel scan requested.");

            SetProgress(
                "Cancelling scan",
                _currentProgressValue,
                "Keeping any boot images that were already found.");
        }

        private int ScanFolderForImagesLive(
            string rootFolder,
            CancellationToken token)
        {
            int addedCount = 0;
            int foldersScanned = 0;

            Stack<string> folders =
                new Stack<string>();

            folders.Push(rootFolder);

            while (folders.Count > 0)
            {
                if (token.IsCancellationRequested)
                    return addedCount;

                string current =
                    folders.Pop();

                foldersScanned++;

                Dispatcher.Invoke(() =>
                {
                    SetProgress(
                        $"Scanning folders ({foldersScanned})",
                        0,
                        current);
                });

                try
                {
                    foreach (string extension in _allowedImageExtensions)
                    {
                        IEnumerable<string> files;

                        try
                        {
                            files =
                                Directory.EnumerateFiles(
                                    current,
                                    "*" + extension,
                                    SearchOption.TopDirectoryOnly);
                        }
                        catch
                        {
                            continue;
                        }

                        foreach (string file in files)
                        {
                            if (token.IsCancellationRequested)
                                return addedCount;

                            bool added =
                                TryAddIsoLibraryItem(file);

                            if (added)
                            {
                                addedCount++;

                                Dispatcher.Invoke(() =>
                                {
                                    RefreshIsoLibraryView();

                                    StatusText.Text =
                                        $"Found {addedCount} image(s)";
                                });
                            }
                        }
                    }

                    foreach (string dir in Directory.EnumerateDirectories(current))
                    {
                        string name =
                            Path.GetFileName(dir);

                        if (name.Equals(
                                "System Volume Information",
                                StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (name.Equals(
                                "$RECYCLE.BIN",
                                StringComparison.OrdinalIgnoreCase))
                            continue;

                        folders.Push(dir);
                    }
                }
                catch
                {
                }
            }

            return addedCount;
        }

        private bool TryAddIsoLibraryItem(string file)
        {
            if (_isoLibraryItems.Any(item =>
                    string.Equals(
                        item.FullPath,
                        file,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            try
            {
                FileInfo info =
                    new FileInfo(file);

                IsoLibraryItem item =
                    new IsoLibraryItem
                    {
                        FileName = Path.GetFileName(file),
                        FullPath = file,
                        Category = DetermineCategoryFolder(file),
                        SizeBytes = info.Length,
                        SizeText = FormatSize(info.Length)
                    };

                ApplyMetadataToIsoLibraryItem(item);

                _isoLibraryItems.Add(item);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async void AddIsoButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!ValidateSelectedDrive())
                return;

            OpenFileDialog dialog =
                new OpenFileDialog
                {
                    Multiselect = true,
                    Filter =
                        "Boot Images|*.iso;*.img;*.wim;*.vhd;*.vhdx"
                };

            bool? result =
                dialog.ShowDialog();

            if (result != true)
                return;

            await AddImageFilesAsync(dialog.FileNames);
        }

        private async void Window_Drop(
            object sender,
            DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            string[] droppedFiles =
                (string[])e.Data.GetData(DataFormats.FileDrop);

            string[] validFiles =
                droppedFiles
                    .Where(IsSupportedImageFile)
                    .ToArray();

            if (validFiles.Length == 0)
                return;

            if (!ValidateSelectedDrive())
                return;

            await AddImageFilesAsync(validFiles);
        }

        private void Window_DragOver(
            object sender,
            DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private async Task AddImageFilesAsync(string[] files)
        {
            try
            {
                int completed = 0;

                foreach (string sourcePath in files)
                {
                    string fileName =
                        Path.GetFileName(sourcePath);

                    string destinationFolder =
                        DetermineCategoryFolder(fileName);

                    string destinationPath =
                        Path.Combine(
                            _selectedDrive!.FullPath,
                            destinationFolder,
                            fileName);

                    Directory.CreateDirectory(
                        Path.GetDirectoryName(destinationPath)!);

                    AddLog(
                        $"Copying boot image to USB: {fileName} → {destinationFolder}");

                    await CopyFileWithProgressAsync(
                        sourcePath,
                        destinationPath);

                    completed++;
                }

                RefreshUsbContents();
                RefreshBootOrderFromUsb();

                SetProgress(
                    "Finished copying boot images",
                    100,
                    $"{completed} boot image(s) were copied to the USB.");

                MessageBox.Show(
                    "Images copied successfully.",
                    "Done",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetProgress(
                    "Copy failed",
                    _currentProgressValue,
                    ex.Message);

                MessageBox.Show(
                    ex.Message,
                    "Copy Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task CopyFileWithProgressAsync(
            string sourcePath,
            string destinationPath)
        {
            const int bufferSize =
                1024 * 1024;

            FileInfo sourceInfo =
                new FileInfo(sourcePath);

            long totalBytes =
                sourceInfo.Length;

            long copiedBytes = 0;

            using FileStream sourceStream =
                new FileStream(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize,
                    true);

            using FileStream destinationStream =
                new FileStream(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize,
                    true);

            byte[] buffer =
                new byte[bufferSize];

            int bytesRead;

            while ((bytesRead =
                       await sourceStream.ReadAsync(
                           buffer,
                           0,
                           buffer.Length)) > 0)
            {
                await destinationStream.WriteAsync(
                    buffer,
                    0,
                    bytesRead);

                copiedBytes += bytesRead;

                double percent =
                    totalBytes == 0
                        ? 0
                        : (copiedBytes / (double)totalBytes) * 100d;

                SetProgress(
                    $"Copying {Path.GetFileName(sourcePath)}",
                    percent,
                    $"{FormatSize(copiedBytes)} of {FormatSize(totalBytes)}");
            }
        }

        private void RefreshUsbContentsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            RefreshUsbContents();
            RefreshBootOrderFromUsb();
        }

        private void RefreshUsbContents()
        {
            UsbContentsTreeView.Items.Clear();

            if (_selectedDrive == null)
                return;

            try
            {
                string root =
                    _selectedDrive.FullPath;

                foreach (string folderName in _categoryFolders)
                {
                    string folderPath =
                        Path.Combine(root, folderName);

                    if (!Directory.Exists(folderPath))
                        continue;

                    TreeViewItem folderNode =
                        new TreeViewItem
                        {
                            Header = folderName,
                            IsExpanded = true
                        };

                    IEnumerable<string> files =
                        Directory
                            .EnumerateFiles(folderPath)
                            .Where(IsSupportedImageFile)
                            .OrderBy(Path.GetFileName);

                    foreach (string file in files)
                    {
                        FileInfo info =
                            new FileInfo(file);

                        TreeViewItem fileNode =
                            new TreeViewItem
                            {
                                Header =
                                    $"{CreateCleanDisplayName(Path.GetFileName(file))} ({FormatSize(info.Length)})",

                                Tag = file
                            };

                        folderNode.Items.Add(fileNode);
                    }

                    UsbContentsTreeView.Items.Add(folderNode);
                }

                AddLog("USB contents refreshed.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "USB Contents Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UsbTreeViewItem_PreviewMouseRightButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem item)
            {
                item.IsSelected = true;
                item.Focus();
            }
        }

        private void UsbContentsTreeView_ContextMenuOpening(
            object sender,
            ContextMenuEventArgs e)
        {
            if (UsbContentsTreeView.SelectedItem is not TreeViewItem item ||
                item.Tag is not string path ||
                !File.Exists(path))
            {
                e.Handled = true;
            }
        }

        private void OpenUsbItemLocation_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (UsbContentsTreeView.SelectedItem is not TreeViewItem item)
                return;

            if (item.Tag is not string path)
                return;

            if (!File.Exists(path))
                return;

            Process.Start(
                new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });

            AddLog($"Opened file location: {path}");
        }

        private void DeleteUsbItem_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (UsbContentsTreeView.SelectedItem is not TreeViewItem item)
                return;

            if (item.Tag is not string path)
                return;

            if (!File.Exists(path))
                return;

            string fileName =
                Path.GetFileName(path);

            MessageBoxResult result =
                MessageBox.Show(
                    $"Delete '{fileName}' from the USB drive?",
                    "Delete Boot Image",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                File.Delete(path);

                AddLog($"Deleted boot image from USB: {fileName}");

                RefreshUsbContents();
                RefreshBootOrderFromUsb();

                SetProgress(
                    "Deleted boot image",
                    100,
                    $"{fileName} was removed from the selected USB.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Delete Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void CreateFoldersButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!ValidateSelectedDrive())
                return;

            foreach (string folder in _categoryFolders)
            {
                string path =
                    Path.Combine(
                        _selectedDrive!.FullPath,
                        folder);

                Directory.CreateDirectory(path);

                AddLog($"Created folder: {path}");
            }

            Directory.CreateDirectory(
                Path.Combine(
                    _selectedDrive.FullPath,
                    "ventoy"));

            RefreshUsbContents();
            RefreshBootOrderFromUsb();

            SetProgress(
                "Created recommended folder layout",
                100,
                "Windows, Linux, Recovery, Utilities, Drivers, Images, Tools, and ventoy folders are ready.");

            MessageBox.Show(
                "Folder layout created successfully.",
                "Done",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async void InstallVentoyButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await RunVentoyCliAsync("/I");
        }

        private async void UpdateVentoyButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await RunVentoyCliAsync("/U");
        }

        private void LaunchVentoyGuiButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                string exePath =
                    GetVentoyExePath();

                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = exePath,
                        WorkingDirectory =
                            Path.GetDirectoryName(exePath),

                        UseShellExecute = true,
                        Verb = "runas"
                    });

                AddLog("Opened official Ventoy tool.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Ventoy Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task RunVentoyCliAsync(string command)
        {
            if (!ValidateSelectedDrive())
                return;

            try
            {
                string exePath =
                    GetVentoyExePath();

                string driveArg =
                    $"/Drive:{_selectedDrive!.DriveLetter.TrimEnd('\\')}";

                string partitionStyle =
                    UseGptCheckBox.IsChecked == true
                        ? "/GPT"
                        : "";

                string args =
                    $"VTOYCLI {command} {driveArg} {partitionStyle} /FS:EXFAT";

                string operation =
                    command.Equals("/I", StringComparison.OrdinalIgnoreCase)
                        ? "Installing Ventoy"
                        : "Updating Ventoy";

                SetProgress(
                    operation,
                    25,
                    "The official Ventoy tool may ask for administrator permission.");

                AddLog($"{operation}: {args}");

                await Task.Run(() =>
                {
                    ProcessStartInfo psi =
                        new ProcessStartInfo
                        {
                            FileName = exePath,
                            Arguments = args,
                            WorkingDirectory =
                                Path.GetDirectoryName(exePath),

                            UseShellExecute = true,
                            Verb = "runas"
                        };

                    using Process? process =
                        Process.Start(psi);

                    process?.WaitForExit();
                });

                RefreshUsbContents();
                RefreshBootOrderFromUsb();

                SetProgress(
                    $"{operation} finished",
                    100,
                    "The selected USB is ready for boot images.");

                MessageBox.Show(
                    "Ventoy operation completed.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetProgress(
                    "Ventoy operation failed",
                    _currentProgressValue,
                    ex.Message);

                MessageBox.Show(
                    ex.Message,
                    "Ventoy Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RefreshBootOrderButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            RefreshBootOrderFromUsb();

            SetProgress(
                "Reloaded USB boot menu list",
                100,
                "The boot order list now reflects boot images currently found on the USB.");
        }

        private void RefreshBootOrderFromUsb()
        {
            if (_selectedDrive == null)
            {
                _bootOrderItems.Clear();
                RefreshBootOrderView();
                return;
            }

            List<BootMenuItem> discovered =
                DiscoverUsbBootImages();

            List<BootMenuItem> merged =
                new();

            foreach (BootMenuItem existing in _bootOrderItems)
            {
                BootMenuItem? stillExists =
                    discovered.FirstOrDefault(item =>
                        string.Equals(
                            item.FullPath,
                            existing.FullPath,
                            StringComparison.OrdinalIgnoreCase));

                if (stillExists != null)
                {
                    stillExists.CustomAlias = existing.CustomAlias;
                    merged.Add(stillExists);
                }
            }

            foreach (BootMenuItem item in discovered)
            {
                bool alreadyAdded =
                    merged.Any(existing =>
                        string.Equals(
                            existing.FullPath,
                            item.FullPath,
                            StringComparison.OrdinalIgnoreCase));

                if (!alreadyAdded)
                    merged.Add(item);
            }

            _bootOrderItems =
                merged;

            RefreshBootOrderView();
        }

        private List<BootMenuItem> DiscoverUsbBootImages()
        {
            List<BootMenuItem> items =
                new();

            if (_selectedDrive == null)
                return items;

            string root =
                _selectedDrive.FullPath;

            foreach (string category in _categoryFolders)
            {
                string folderPath =
                    Path.Combine(root, category);

                if (!Directory.Exists(folderPath))
                    continue;

                IEnumerable<string> files =
                    Directory
                        .EnumerateFiles(folderPath)
                        .Where(IsSupportedImageFile)
                        .OrderBy(Path.GetFileName);

                foreach (string file in files)
                {
                    string fileName =
                        Path.GetFileName(file);

                    items.Add(
                        new BootMenuItem
                        {
                            FileName = fileName,
                            FullPath = file,
                            Category = category,
                            RelativePath =
                                "/" + category + "/" + fileName,

                            DisplayName =
                                CreateCleanDisplayName(fileName),

                            DetectedType =
                                DetectType(fileName),

                            Architecture =
                                DetectArchitecture(fileName),

                            CustomAlias =
                                CreateCleanDisplayName(fileName)
                        });
                }
            }

            return items;
        }

        private void RefreshBootOrderView()
        {
            BootOrderListBox.ItemsSource = null;
            BootOrderListBox.ItemsSource = _bootOrderItems;
        }

        private void MoveBootItemUpButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            int index =
                BootOrderListBox.SelectedIndex;

            if (index <= 0)
                return;

            BootMenuItem item =
                _bootOrderItems[index];

            _bootOrderItems.RemoveAt(index);
            _bootOrderItems.Insert(index - 1, item);

            RefreshBootOrderView();

            BootOrderListBox.SelectedIndex = index - 1;
        }

        private void MoveBootItemDownButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            int index =
                BootOrderListBox.SelectedIndex;

            if (index < 0 ||
                index >= _bootOrderItems.Count - 1)
                return;

            BootMenuItem item =
                _bootOrderItems[index];

            _bootOrderItems.RemoveAt(index);
            _bootOrderItems.Insert(index + 1, item);

            RefreshBootOrderView();

            BootOrderListBox.SelectedIndex = index + 1;
        }

        private void BootOrderListBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (BootOrderListBox.SelectedItem is not BootMenuItem item)
            {
                SelectedBootItemText.Text =
                    "No item selected";

                CustomAliasTextBox.Text = "";

                return;
            }

            SelectedBootItemText.Text =
                $"{item.FileName}\n{item.DetectedType} • {item.Architecture}";

            CustomAliasTextBox.Text =
                item.CustomAlias;
        }

        private void ApplyAliasButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (BootOrderListBox.SelectedItem is not BootMenuItem item)
                return;

            string alias =
                CustomAliasTextBox.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(alias))
                return;

            item.CustomAlias = alias;

            RefreshBootOrderView();

            BootOrderListBox.SelectedItem = item;

            AddLog($"Applied custom boot menu name: {alias}");

            SetProgress(
                "Updated boot menu name",
                100,
                $"This item will appear as: {alias}");
        }

        private void ResetAliasButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (BootOrderListBox.SelectedItem is not BootMenuItem item)
                return;

            item.CustomAlias =
                CreateCleanDisplayName(item.FileName);

            CustomAliasTextBox.Text =
                item.CustomAlias;

            RefreshBootOrderView();

            BootOrderListBox.SelectedItem = item;

            AddLog($"Reset custom boot name for: {item.FileName}");

            SetProgress(
                "Reset boot menu name",
                100,
                "The selected item name was reset to a cleaned-up version of the filename.");
        }

        private void BootOrderListBox_PreviewMouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            _bootOrderDragStartPoint =
                e.GetPosition(null);
        }

        private void BootOrderListBox_MouseMove(
            object sender,
            MouseEventArgs e)
        {
            Point currentPosition =
                e.GetPosition(null);

            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            if (Math.Abs(currentPosition.X - _bootOrderDragStartPoint.X) <
                SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(currentPosition.Y - _bootOrderDragStartPoint.Y) <
                SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            if (BootOrderListBox.SelectedItem is not BootMenuItem selectedItem)
                return;

            DragDrop.DoDragDrop(
                BootOrderListBox,
                selectedItem,
                DragDropEffects.Move);
        }

        private void BootOrderListBox_DragOver(
            object sender,
            DragEventArgs e)
        {
            e.Effects =
                e.Data.GetDataPresent(typeof(BootMenuItem))
                    ? DragDropEffects.Move
                    : DragDropEffects.None;

            e.Handled = true;
        }

        private void BootOrderListBox_Drop(
            object sender,
            DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(BootMenuItem)))
                return;

            BootMenuItem droppedItem =
                (BootMenuItem)e.Data.GetData(typeof(BootMenuItem));

            BootMenuItem? targetItem =
                GetBootMenuItemUnderMouse(
                    e.GetPosition(BootOrderListBox));

            if (targetItem == null ||
                ReferenceEquals(droppedItem, targetItem))
                return;

            int oldIndex =
                _bootOrderItems.IndexOf(droppedItem);

            int newIndex =
                _bootOrderItems.IndexOf(targetItem);

            if (oldIndex < 0 ||
                newIndex < 0)
                return;

            _bootOrderItems.RemoveAt(oldIndex);
            _bootOrderItems.Insert(newIndex, droppedItem);

            RefreshBootOrderView();

            BootOrderListBox.SelectedItem = droppedItem;

            SetProgress(
                "Updated boot menu order",
                100,
                "The selected item was moved in the custom boot menu list.");
        }

        private BootMenuItem? GetBootMenuItemUnderMouse(Point point)
        {
            DependencyObject? element =
                BootOrderListBox.InputHitTest(point)
                    as DependencyObject;

            while (element != null)
            {
                if (element is ListBoxItem listBoxItem)
                    return listBoxItem.DataContext as BootMenuItem;

                element =
                    VisualTreeHelper.GetParent(element);
            }

            return null;
        }

        private void GenerateVentoyMenuButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!ValidateSelectedDrive())
                return;

            try
            {
                RefreshBootOrderFromUsb();

                if (_bootOrderItems.Count == 0)
                {
                    MessageBox.Show(
                        "No boot images were found on the selected USB.",
                        "No Boot Images",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                string ventoyFolder =
                    Path.Combine(
                        _selectedDrive!.FullPath,
                        "ventoy");

                Directory.CreateDirectory(ventoyFolder);

                string ventoyJsonPath =
                    Path.Combine(
                        ventoyFolder,
                        "ventoy.json");

                if (File.Exists(ventoyJsonPath))
                {
                    string backupPath =
                        Path.Combine(
                            ventoyFolder,
                            $"ventoy_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");

                    File.Copy(
                        ventoyJsonPath,
                        backupPath,
                        true);

                    AddLog(
                        $"Backed up existing ventoy.json to {backupPath}");
                }

                string json =
                    BuildVentoyJsonFromBootOrder();

                File.WriteAllText(
                    ventoyJsonPath,
                    json);

                AddLog(
                    $"Generated Ventoy menu: {ventoyJsonPath}");

                SetProgress(
                    "Generated custom Ventoy menu",
                    100,
                    "The ordered menu and friendly names were written to /ventoy/ventoy.json.");

                MessageBox.Show(
                    "Ventoy menu generated successfully.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetProgress(
                    "Menu generation failed",
                    _currentProgressValue,
                    ex.Message);

                MessageBox.Show(
                    ex.Message,
                    "Ventoy Menu Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private string BuildVentoyJsonFromBootOrder()
        {
            List<string> aliasEntries =
                new();

            List<string> classEntries =
                new();

            foreach (BootMenuItem item in _bootOrderItems)
            {
                string escapedPath =
                    JsonSerializer.Serialize(item.RelativePath);

                string escapedAlias =
                    JsonSerializer.Serialize(item.CustomAlias);

                aliasEntries.Add(
$@"        {{
            ""image"": {escapedPath},
            ""alias"": {escapedAlias}
        }}");

                string escapedCategory =
                    JsonSerializer.Serialize(item.Category);

                classEntries.Add(
$@"        {{
            ""image"": {escapedPath},
            ""class"": {escapedCategory}
        }}");
            }

            return
$@"{{
    ""control"": [
        {{
            ""VTOY_DEFAULT_SEARCH_ROOT"": ""/""
        }}
    ],

    ""menu_alias"": [
{string.Join(",\n", aliasEntries)}
    ],

    ""menu_class"": [
{string.Join(",\n", classEntries)}
    ]
}}";
        }

        private bool IsSupportedImageFile(string path)
        {
            string extension =
                Path.GetExtension(path)
                    .ToLowerInvariant();

            return _allowedImageExtensions.Contains(extension);
        }

        private void ApplyMetadataToIsoLibraryItem(
            IsoLibraryItem item)
        {
            item.DisplayName =
                CreateCleanDisplayName(item.FileName);

            item.DetectedType =
                DetectType(item.FileName);

            item.Architecture =
                DetectArchitecture(item.FileName);
        }

        private string CreateCleanDisplayName(string fileName)
        {
            string name =
                Path.GetFileNameWithoutExtension(fileName);

            string cleaned =
                name
                    .Replace("_", " ")
                    .Replace("-", " ")
                    .Replace(".", " ");

            while (cleaned.Contains("  "))
                cleaned = cleaned.Replace("  ", " ");

            return cleaned.Trim();
        }

        private string DetectArchitecture(string fileName)
        {
            string lower =
                fileName.ToLowerInvariant();

            if (lower.Contains("arm64") ||
                lower.Contains("aarch64"))
                return "ARM64";

            if (lower.Contains("x64") ||
                lower.Contains("amd64") ||
                lower.Contains("64bit") ||
                lower.Contains("64-bit"))
                return "x64";

            if (lower.Contains("x86") ||
                lower.Contains("i386") ||
                lower.Contains("i686") ||
                lower.Contains("32bit") ||
                lower.Contains("32-bit"))
                return "x86";

            return "Unknown";
        }

        private string DetectType(string fileName)
        {
            string lower =
                fileName.ToLowerInvariant();

            if (lower.Contains("win11") ||
                lower.Contains("windows11") ||
                lower.Contains("windows 11"))
                return "Windows 11";

            if (lower.Contains("win10") ||
                lower.Contains("windows10") ||
                lower.Contains("windows 10"))
                return "Windows 10";

            if (lower.Contains("windows") ||
                lower.StartsWith("win"))
                return "Windows";

            if (lower.Contains("ubuntu"))
                return "Ubuntu";

            if (lower.Contains("debian"))
                return "Debian";

            if (lower.Contains("fedora"))
                return "Fedora";

            if (lower.Contains("mint"))
                return "Linux Mint";

            if (lower.Contains("kali"))
                return "Kali Linux";

            if (lower.Contains("hiren") ||
                lower.Contains("hirens"))
                return "Hiren's";

            if (lower.Contains("medicat"))
                return "MediCat";

            if (lower.Contains("clonezilla"))
                return "Clonezilla";

            if (lower.Contains("linux"))
                return "Linux";

            return "Image";
        }

        private string DetermineCategoryFolder(string fileName)
        {
            string lower =
                fileName.ToLower();

            if (lower.Contains("windows") ||
                lower.Contains("win10") ||
                lower.Contains("win11"))
                return "Windows";

            if (lower.Contains("ubuntu") ||
                lower.Contains("linux") ||
                lower.Contains("debian") ||
                lower.Contains("fedora") ||
                lower.Contains("mint"))
                return "Linux";

            if (lower.Contains("hirens") ||
                lower.Contains("medicat") ||
                lower.Contains("clonezilla") ||
                lower.Contains("recovery") ||
                lower.Contains("rescue"))
                return "Recovery";

            if (lower.Contains("driver"))
                return "Drivers";

            return "Utilities";
        }

        private string GetVentoyExePath()
        {
            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets",
                "Ventoy",
                "Ventoy2Disk.exe");
        }

        private string FormatSize(long bytes)
        {
            double gb =
                bytes / 1024d / 1024d / 1024d;

            if (gb >= 1)
                return $"{gb:F2} GB";

            double mb =
                bytes / 1024d / 1024d;

            return $"{mb:F1} MB";
        }

        private class BootMenuItem
        {
            public string FileName { get; set; } = "";
            public string FullPath { get; set; } = "";
            public string RelativePath { get; set; } = "";
            public string Category { get; set; } = "";

            public string DisplayName { get; set; } = "";
            public string DetectedType { get; set; } = "";
            public string Architecture { get; set; } = "";

            public string CustomAlias { get; set; } = "";
        }
    }
}