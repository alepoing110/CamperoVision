namespace CamperoDesktop.Helpers;

public static class AsyncHelper
{
    public static void FireAndForget(Func<Task> asyncAction, Action<Exception>? onError = null)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await asyncAction();
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
            }
        });
    }

    public static void FireAndForgetOnUi(Func<Task> asyncAction, Action<Exception>? onError = null)
    {
        _ = InternalFireAndForget(asyncAction, onError);
    }

    private static async Task InternalFireAndForget(Func<Task> asyncAction, Action<Exception>? onError)
    {
        try
        {
            await asyncAction();
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex);
        }
    }
}
