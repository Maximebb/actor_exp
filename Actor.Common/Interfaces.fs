namespace Actor.Common

open System
open System.Threading
open System.Threading.Tasks

type IStatefulActor<'message> =
    inherit IDisposable
    abstract member SendAsync   : 'message[] -> CancellationToken -> Task
    abstract member Send        : 'message[] -> bool
    abstract member Ping        : unit -> Task<bool>
    abstract member Stop        : unit -> Task

/// Represent the interaction layer with the actor. 
type IMessageProc<'identity, 'message, 'state> =
    inherit IDisposable
    abstract member SendAsync   : 'identity -> 'message[] -> CancellationToken -> Task

type IStateProvider<'identity,'state>  =
    abstract member GetStateAsync   : CancellationToken -> 'identity -> Task<'state>
    abstract member SaveStateAsync  : CancellationToken -> 'identity -> 'state -> Task