using System.Collections.Generic;
using System.IO;
using Ventoy_Builder.Models;

namespace Ventoy_Builder.Services
{
    public class UsbDriveService
    {
        public List<UsbDriveInfo> GetUsbDrives()
        {
            List<UsbDriveInfo> drives = new();

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.DriveType == DriveType.Removable &&
                        drive.IsReady)
                    {
                        drives.Add(new UsbDriveInfo
                        {
                            DriveLetter = drive.Name,
                            Label = drive.VolumeLabel,
                            FileSystem = drive.DriveFormat,
                            FullPath = drive.RootDirectory.FullName,

                            TotalSizeBytes = drive.TotalSize,
                            FreeSpaceBytes = drive.TotalFreeSpace,

                            SizeText = FormatSize(drive.TotalSize),
                            FreeSpaceText = FormatSize(drive.TotalFreeSpace),

                            IsRemovable = true,
                            IsReady = true
                        });
                    }
                }
                catch
                {
                }
            }

            return drives;
        }

        private string FormatSize(long bytes)
        {
            double gb = bytes / 1024d / 1024d / 1024d;

            return $"{gb:F1} GB";
        }
    }
}