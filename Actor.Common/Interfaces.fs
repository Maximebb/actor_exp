namespace Actor.Common

open System.Threading
open System.Threading.Tasks

type IStatefulActor<'message> =
    abstract member SendAsync   : 'message -> CancellationToken -> Task
    abstract member Send        : 'message -> bool
    abstract member Ping        : unit -> Task<bool>
    abstract member Stop        : unit -> Task
    abstract member Dispose     : unit -> unit

type IStatelessActor<'identity, 'message> =
    abstract member SendAsync   : 'identity -> 'message -> CancellationToken -> Task
    abstract member Send        : 'identity -> 'message -> bool
    abstract member Ping        : unit -> Task<bool>

type IStateProvider<'identity,'state>  =
    abstract member GetStateAsync   : CancellationToken -> 'identity -> Task<'state>
    abstract member SaveStateAsync  : CancellationToken -> 'identity -> 'state -> Task