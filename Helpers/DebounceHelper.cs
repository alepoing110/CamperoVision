namespace CamperoDesktop.Helpers;

public class DebounceHelper
{
    private CancellationTokenSource? _cts;
    private readonly Action<Exception>? _onError;

    public DebounceHelper(Action<Exception>? onError = null)
    {
        _onError = onError;
    }

    public void Cancel()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public static DebounceHelper Debounce(Action action, TimeSpan delay, Action<Exception>? onError = null)
    {
        var helper = new DebounceHelper(onError);
        helper.ExecuteInternal(action, delay);
        return helper;
    }

    private async void ExecuteInternal(Action action, TimeSpan delay)
    {
        _cts = new CancellationTokenSource();
        try
        {
            await Task.Delay(delay, _cts.Token);
            if (!_cts.Token.IsCancellationRequested)
            {
                action();
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            _onError?.Invoke(ex);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }
}
