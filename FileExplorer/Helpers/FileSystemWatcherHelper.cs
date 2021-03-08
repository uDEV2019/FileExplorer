﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using DevExpress.Mvvm;
using FileExplorer.Core;
using FileExplorer.Messages;
using FileExplorer.Native;

namespace FileExplorer.Helpers
{
    public class FileSystemWatcherHelper
    {
        public void Start()
        {
            if (DeviceWatcher == null)
                DeviceWatcher = new DeviceWatcher();

            DeviceWatcher.DeviceArrived += DeviceWatcher_DeviceArrived;
            DeviceWatcher.DeviceQueryRemove += DeviceWatcher_DeviceQueryRemove;
            DeviceWatcher.DeviceRemoveComplete += DeviceWatcher_DeviceRemoveComplete;

            DirectoryWatchers = new Dictionary<string, DirectoryWatcher>(StringComparer.OrdinalIgnoreCase);
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    RegisterDirectoryWatcher(drive.Name);

                    if (drive.DriveType != DriveType.Fixed)
                        DeviceWatcher.RegisterDeviceNotification(drive.Name);
                }
            }
        }

        public void Stop()
        {
            if (DirectoryWatchers != null)
            {
                foreach (DirectoryWatcher directoryWatcher in DirectoryWatchers.Values)
                    directoryWatcher.Stop();
            }

            if (DeviceWatcher != null)
            {
                DeviceWatcher.DeviceArrived -= DeviceWatcher_DeviceArrived;
                DeviceWatcher.DeviceQueryRemove -= DeviceWatcher_DeviceQueryRemove;
                DeviceWatcher.DeviceRemoveComplete -= DeviceWatcher_DeviceRemoveComplete;
            }
        }

        private void RegisterDirectoryWatcher(string path)
        {
            DirectoryWatcher directoryWatcher = new DirectoryWatcher(path, OnFileEvent, OnError);
            directoryWatcher.Start();

            DirectoryWatchers.Add(path, directoryWatcher);
        }

        private void OnFileEvent(FileEvent fileEvent)
        {
            if (SuppressNotification(fileEvent.Path))
                return;

            switch (fileEvent.ChangeType)
            {
                case ChangeType.Created:
                    SendNotificationMessage(NotificationType.Add, fileEvent.Path);
                    break;

                case ChangeType.Deleted:
                    SendNotificationMessage(NotificationType.Remove, fileEvent.Path);
                    break;

                case ChangeType.Changed:
                    SendNotificationMessage(NotificationType.Update, fileEvent.Path);
                    break;
            }

            if (fileEvent.ChangeType == ChangeType.Renamed)
            {
                if (fileEvent.Path.OrdinalEquals(fileEvent.NewPath))
                {
                    SendNotificationMessage(NotificationType.Add, fileEvent.Path);
                    return;
                }

                string oldParent = FileSystemHelper.GetParentFolderPath(fileEvent.Path);
                string newParent = FileSystemHelper.GetParentFolderPath(fileEvent.NewPath);
                if (oldParent.OrdinalEquals(newParent))
                    SendNotificationMessage(NotificationType.Rename, fileEvent.Path, fileEvent.NewPath);
            }
        }

        private void OnError(ErrorEventArgs e)
        {
            Journal.WriteLog(e.GetException());
        }

        private bool IsPathExcluded(string path)
        {
            if (String.IsNullOrEmpty(path))
                return true;
            if (path.OrdinalContains(RecycleBin))
                return true;
            if (path.OrdinalStartsWith(Windows))
                return true;
            if (path.OrdinalStartsWith(ApplicationData))
                return true;
            if (path.OrdinalStartsWith(LocalApplicationData))
                return true;
            if (path.OrdinalStartsWith(CommonApplicationData))
                return true;

            return false;
        }

        private bool SuppressNotification(string path)
        {
            if (IsPathExcluded(path))
                return true;

            try
            {
                FileInfo fileInfo = new FileInfo(path);
                FileAttributes attributes = fileInfo.Attributes;

                return false;
            }
            catch { return true; }
        }

        private void DeviceWatcher_DeviceArrived(object sender, DeviceNotificationEventArgs e)
        {
            if (DirectoryWatchers.ContainsKey(e.Name))
                DirectoryWatchers[e.Name].Start();
            else
                RegisterDirectoryWatcher(e.Name);

            SendNotificationMessage(NotificationType.Add, e.Name);
        }

        private void DeviceWatcher_DeviceQueryRemove(object sender, DeviceNotificationEventArgs e)
        {
            if (DirectoryWatchers.ContainsKey(e.Name))
                DirectoryWatchers[e.Name].Stop();
        }

        private void DeviceWatcher_DeviceRemoveComplete(object sender, DeviceNotificationEventArgs e)
        {
            SendNotificationMessage(NotificationType.Remove, e.Name);
        }

        private void SendNotificationMessage(NotificationType notificationType, string path, string newPath = null)
        {
            NotificationMessage notificationMessage = new NotificationMessage { Path = path, NewPath = newPath, NotificationType = notificationType };
            Application.Current.Dispatcher.Invoke(() => Messenger.Default.Send(notificationMessage));
        }

        private DeviceWatcher DeviceWatcher;

        private Dictionary<string, DirectoryWatcher> DirectoryWatchers;

        private static readonly string RecycleBin = "$Recycle.Bin";

        private static readonly string Windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        private static readonly string ApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        private static readonly string LocalApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        private static readonly string CommonApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    }
}
