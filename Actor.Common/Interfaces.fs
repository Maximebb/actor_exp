namespace Actor.Common

open System.Threading
open System.Threading.Tasks

type IActor<'message> =
    abstract member SendAsync   : 'message -> CancellationToken -> Task
    abstract member Send        : 'message -> bool
    abstract member Ping        : unit -> Task<bool>
    abstract member Stop        : unit -> Task
    abstract member Dispose     : unit -> unit