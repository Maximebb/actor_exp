namespace StatelessActor

open System.Threading
open System.Threading.Tasks
open System
open Actor.Common
open Microsoft.Extensions.Logging
open System.Threading.Channels
open StatefulActor
open StatefulActor.Actor
open System.Diagnostics

module OptimisticConcurrency =
    open Microsoft.Extensions.Logging.Abstractions

    type MessageProcImpl<'identity, 'message, 'state when 'identity : comparison> internal (handler : 'state -> 'message -> Task<'state>, stateProvider : IStateProvider<'identity, 'state>, logger : ILogger option) =
        let supervisor = Channel.CreateUnbounded<ExecutionResult<int>> (  new UnboundedChannelOptions( SingleReader = false, AllowSynchronousContinuations = true ) ) 
        let m_logger = logger |> Option.defaultWith (fun () -> NullLoggerFactory.Instance.CreateLogger( "Null" ))
        let lifetimeToken = new CancellationTokenSource ()

        let rec foldOnAll (state0 : 'state) (messages : 'message list) = async {
            match messages with
            | [] -> return state0
            | head :: tail -> 
                let! state1 = handler state0 head |> Async.AwaitTask
                return! foldOnAll state1 tail
        }

        let transact (id : 'identity) (messages : 'message list) = async {
            let! stateI = stateProvider.GetStateAsync lifetimeToken.Token id |> Async.AwaitTask
            let! stateF = foldOnAll stateI messages
            do! stateProvider.SaveStateAsync lifetimeToken.Token id stateF |> Async.AwaitTask
        }

        let rec handle1 (id : 'identity) (messages : 'message list) = async {
            try
                do! transact id messages
            with
                | :? DirtyWriteExn -> return! handle1 id messages
                | e -> m_logger.LogError( "Failed to process message for {0}, Ex: {1}", id, e.Message )
        }
            
        let internalHandler _ (idMessages : ('identity * 'message[])[]) = async {
            let sw = new Stopwatch ()
            for (id, batch) in idMessages do
                sw.Start ()
                do! handle1 (id) (batch |> Array.toList)
                sw.Stop ()
                m_logger.LogInformation( "Took {0}ms to process {2} messages for {1}", sw.ElapsedMilliseconds, id)
                sw.Reset ()
            return 1
        }
                
        let m_actor = StatefulActor.Actor.StartNew<('identity * 'message[]), int> (fun state0 messages -> (internalHandler state0 messages) |> Async.StartAsTask) supervisor.Writer 0
        
        interface IMessageProc<'identity, 'message, 'state> with
            member x.SendAsync (id : 'identity) (messages : 'message[]) cancel =
                m_actor.SendAsync [| (id, messages) |] cancel

        interface IDisposable with 
            member x.Dispose () =
                m_actor.Dispose ()

    let Create<'identity, 'message, 'state when 'identity : comparison> (handler, stateProvider, logger) =
        new MessageProcImpl<'identity, 'message, 'state> (handler, stateProvider, logger) :> IMessageProc<'identity, 'message, 'state>
