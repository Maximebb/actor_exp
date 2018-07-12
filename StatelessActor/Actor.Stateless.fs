namespace StatelessActor

open System.Threading
open System.Threading.Tasks
open System
open Actor.Common

type ExecutionResult<'state> =
| Disposed of 'state
| Stopped of 'state
| Crashed of Exception

module OptimisticConcurrency =
    open System.Threading.Channels

    type ControlMessages =
    | Stop of TaskCompletionSource<bool>
    | Ping of TaskCompletionSource<bool>

    type MessageType =
    | Control
    | Incoming

    type MessageProcImpl<'identity, 'message, 'state> internal (handler : 'state -> 'message -> 'state, stateProvider : IStateProvider<'identity, 'state>) =
        
        interface IMessageProc<'identity, 'message, 'state> with
            member x.SendAsync (id : 'identity) (message : 'message) cancel = (async {
                    let! state0 = stateProvider.GetStateAsync cancel id |> Async.AwaitTask
                    let state = handler state0 message
                    do! stateProvider.SaveStateAsync cancel id state |> Async.AwaitTask
                }
                |> Async.StartAsTask :> Task)

    let Create<'identity, 'message, 'state> (handler, stateProvider) =
        new MessageProcImpl<'identity, 'message, 'state> (handler, stateProvider)
