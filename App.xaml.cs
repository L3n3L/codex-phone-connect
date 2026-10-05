using System.Windows;

namespace CodexPhoneConnect;

public partial class App : Application
{
    public App()
    {
        BridgeLog.Initialize();
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        Exit += App_Exit;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        BridgeLog.Error("App", "未处理的 UI 异常", e.Exception);
    }

    private void App_Exit(object sender, ExitEventArgs e)
    {
        BridgeLog.Info("App", $"程序退出，ExitCode={e.ApplicationExitCode}");
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        BridgeLog.Error("App", "未观察到的后台任务异常", e.Exception);
        e.SetObserved();
    }
}
