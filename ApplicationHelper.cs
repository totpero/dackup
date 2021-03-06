using System;
using System.IO;
using System.Linq;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Threading.Tasks;

using Serilog;

using dackup.Configuration;

namespace dackup
{
    public static class ApplicationHelper
    {
        public static PerformConfig PrepaireConfig(string configfile, string logPath, string tmpPath)
        {
            if (string.IsNullOrEmpty(logPath))
            {
                logPath = "log";
            }
            if (string.IsNullOrEmpty(tmpPath))
            {
                tmpPath = "tmp";
            }

            var tmpWorkDirPath = Path.Combine(tmpPath, $"dackup-tmp-{DateTime.UtcNow:s}");
            DackupContext.Create(Path.Join(logPath, "dackup.log"), tmpWorkDirPath);

            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(DackupContext.Current.LogFile, rollingInterval: RollingInterval.Month, rollOnFileSizeLimit: true)
            .CreateLogger();

            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Error("*** Crash! ***", "UnhandledException");
            TaskScheduler.UnobservedTaskException += (s, e) => Log.Error("*** Crash! ***", "UnobservedTaskException");

            return PerformConfigHelper.LoadFrom(configfile);

        }
        public static Task<BackupTaskResult[]> RunBackup(PerformConfig cfg)
        {
            Log.Information("Dackup start backup task ");

            var backupTaskList = PerformConfigHelper.ParseBackupTaskFromConfig(cfg);
            var backupTaskResult = new List<Task<BackupTaskResult>>();
            backupTaskList.ForEach(task =>
            {
                backupTaskResult.Add(task.BackupAsync());
            });
            return Task.WhenAll(backupTaskResult.ToArray());
        }
        public static (Task<UploadResult[]>, Task<PurgeResult[]>) RunStorage(PerformConfig cfg)
        {
            Log.Information("Dackup start storage task ");

            var storageList = PerformConfigHelper.ParseStorageFromConfig(cfg);
            var storageUploadResultList = new List<Task<UploadResult>>();
            var storagePurgeResultList = new List<Task<PurgeResult>>();

            storageList.ForEach(storage =>
            {
                DackupContext.Current.GenerateFilesList.ForEach(file =>
                {
                    storageUploadResultList.Add(storage.UploadAsync(file));
                });
                storagePurgeResultList.Add(storage.PurgeAsync());
            });

            var storageUploadTasks = Task.WhenAll(storageUploadResultList.ToArray());
            var storagePurgeTasks = Task.WhenAll(storagePurgeResultList.ToArray());

            return (storageUploadTasks, storagePurgeTasks);
        }
        public static Task<NotifyResult[]> RunNotify(PerformConfig cfg, Statistics statistics)
        {
            Log.Information("Dackup start notify task ");

            var notifyList = PerformConfigHelper.ParseNotifyFromConfig(cfg);

            var notifyResultList = new List<Task<NotifyResult>>();

            notifyList.ForEach(notify =>
            {
                notifyResultList.Add(notify.NotifyAsync(statistics));
            });

            return Task.WhenAll(notifyResultList.ToArray());
        }
        public static void Clean()
        {
            Log.Information("Dackup clean tmp folder ");

            var di = new DirectoryInfo(DackupContext.Current.TmpPath);
            foreach (var file in di.GetFiles())
            {
                file.Delete();
            }
            foreach (var dir in di.GetDirectories())
            {
                dir.Delete(true);
            }

            Directory.Delete(DackupContext.Current.TmpPath);

        }
    }
}