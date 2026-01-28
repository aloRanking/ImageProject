namespace Operations
open System.Threading.Tasks

module Task =
    
    
    let catch (t: Task<'a>) : Task<Result<'a, string>> =
        t.ContinueWith(fun (task: Task<'a>) ->
            if task.IsFaulted then 
                Error task.Exception.InnerException.Message
            else Ok task.Result)