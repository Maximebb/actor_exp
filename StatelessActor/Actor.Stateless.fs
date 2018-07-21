namespace StatelessActor

open System.Threading
open System.Threading.Tasks
open System
open Actor.Common

module OptimisticConcurrency =
    open System.Threading.Channels
    open StatefulActor

    type MessageProcImpl<'identity, 'message, 'state> internal (handler : 'state -> 'message -> 'state, stateProvider : IStateProvider<'identity, 'state>) =
        let supervisor = Channel.CreateUnbounded<ExecutionResult<int>> (  new UnboundedChannelOptions( SingleReader = false, AllowSynchronousContinuations = true ) ) 
        
        let internalHandler state0 messages =
            state0 + 1

        //do the internal handling here
                
        let internalActor = StatefulActor.Actor.StartNew<('identity * 'message), int> internalHandler supervisor.Writer 0

        interface IMessageProc<'identity, 'message, 'state> with
            member x.SendAsync (id : 'identity) (messages : 'message[]) cancel =
                let identifiedBatch = 
                    messages
                    |> Array.map (fun m -> (id, m))
                internalActor.SendAsync identifiedBatch cancel

    let Create<'identity, 'message, 'state> (handler, stateProvider) =
        new MessageProcImpl<'identity, 'message, 'state> (handler, stateProvider)
