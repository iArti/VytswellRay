namespace ServiceLib.Manager;

public class TaskManager
{
    private static readonly Lazy<TaskManager> _instance = new(() => new());
    public static TaskManager Instance => _instance.Value;
    private Config _config;
    private Func<bool, string, Task>? _updateFunc;

    public void RegUpdateTask(Config config, Func<bool, string, Task> updateFunc)
    {
        _config = config;
        _updateFunc = updateFunc;

        Task.Run(ScheduledTasks);
    }

    private async Task ScheduledTasks()
    {
        Logging.SaveLog("Setup Scheduled Tasks");

        var numOfExecuted = 1;
        while (true)
        {
            await Task.Delay(1000 * 60);

            if (numOfExecuted % 20 == 0)
            {
                try
                {
                    await ConfigHandler.SaveConfig(_config);
                }
                catch (Exception ex)
                {
                    Logging.SaveLog("ScheduledTasks - SaveConfig", ex);
                }
            }

            if (numOfExecuted % 60 == 0)
            {
                FileUtils.DeleteExpiredFiles(Utils.GetBinConfigPath(), DateTime.Now.AddHours(-1));
                FileUtils.DeleteExpiredFiles(Utils.GetLogPath(), DateTime.Now.AddMonths(-1));
                FileUtils.DeleteExpiredFiles(Utils.GetTempPath(), DateTime.Now.AddMonths(-1));
            }

            numOfExecuted++;
        }
    }
}
