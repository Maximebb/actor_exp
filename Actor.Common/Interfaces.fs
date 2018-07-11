namespace Actor.Common

open System.Threading
open System.Threading.Tasks

type IStatefulActor<'message> =
    abstract member SendAsync   : 'message -> CancellationToken -> Task
    abstract member Send        : 'message -> bool
    abstract member Ping        : unit -> Task<bool>
    abstract member Stop        : unit -> Task
    abstract member Dispose     : unit -> unit

type IMessageProc<'identity, 'message, 'state> =
    /// This method should throw if the implementation deems the operation a failure.
    /// Ex. If it implements optimistic concurrency, this should throw if
    /// failure conditions are met. It can be number of dirty write, 
    abstract member SendAsync   : 'identity -> 'message -> CancellationToken -> Task

type IStateProvider<'identity,'state>  =
    abstract member GetStateAsync   : CancellationToken -> 'identity -> Task<'state>
    abstract member SaveStateAsync  : CancellationToken -> 'identity -> 'state -> Task