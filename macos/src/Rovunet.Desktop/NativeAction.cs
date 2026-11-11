using System.Windows.Input;
using Avalonia.Threading;

namespace Rovunet.Desktop;

// An explicit command gives Avalonia's macOS exporter a stable CanExecute
// predicate. Rebuilding a localized menu never leaves a Click-only native item.
public sealed class NativeAction(Func<Task> run,Action<Exception> failed) : ICommand
{
    bool running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter)=>!running;
    public void Execute(object? parameter)=>Dispatcher.UIThread.Post(async()=>{
        if(running)return;running=true;CanExecuteChanged?.Invoke(this,EventArgs.Empty);
        try{await run();}catch(Exception e){failed(e);}
        finally{running=false;CanExecuteChanged?.Invoke(this,EventArgs.Empty);}
    });
}
