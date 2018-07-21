namespace StatefulActor

open System.Threading
open System.Threading.Tasks
open System
open Actor.Common

type ExecutionResult<'state> =
| Disposed of 'state
| Stopped of 'state
| Crashed of Exception

module Actor =
    open System.Threading.Channels

    type ControlMessages =
    | Stop of TaskCompletionSource<bool>
    | Ping of TaskCompletionSource<bool>

    type MessageType =
    | Control
    | Incoming

    type ActorImpl<'message, 'state> internal (handler : 'state -> 'message[] -> 'state, supervisorChannel : ChannelWriter<ExecutionResult<'state>>) =
        let controlChannel = Channel.CreateUnbounded<ControlMessages> (  new UnboundedChannelOptions( SingleReader = true, AllowSynchronousContinuations = true ) )
        let incomingChannel = Channel.CreateUnbounded<'message> (  new UnboundedChannelOptions( SingleReader = true, AllowSynchronousContinuations = true ) )
        let lifetimeToken = new CancellationTokenSource ()
        let mutable disposed = false

        //drain message from channel reader
        let rec drain (channelToReadFrom : Channel<'m>) (acc : 'm list) =
            let succ, readResult = channelToReadFrom.Reader.TryRead()
            match succ, readResult with
            | true, mess -> drain channelToReadFrom (mess :: acc)
            | false, _ -> acc

        let rec foldControl state (messages : ControlMessages list) =
            match messages with
            | [] -> Choice1Of3 state
            | mess :: tail ->
                match mess with
                | Stop tcs -> 
                    tcs.SetResult( true )
                    Choice3Of3 (ExecutionResult<'state>.Stopped state) //Stop right away
                | Ping tcs -> 
                    tcs.SetResult( true )
                    foldControl state tail //Continue processing control batch

                
        
        let rec Run (state : 'state) = async {
            if disposed
            then return ExecutionResult<'state>.Disposed state
            else
                let! result = async {
                    try
                        //wait asynchronously for messages to be read from the channel
                        let! messageTypeToRead =
                            let controlTask = async {
                                do! controlChannel.Reader.WaitToReadAsync( lifetimeToken.Token ).AsTask() 
                                    |> Async.AwaitTask
                                    |> Async.Ignore
                                return Some ( MessageType.Control )
                            }
                            let messageTask = async {
                                do! incomingChannel.Reader.WaitToReadAsync( lifetimeToken.Token ).AsTask() 
                                    |> Async.AwaitTask
                                    |> Async.Ignore
                                return Some ( MessageType.Incoming )
                            }
                            seq { yield controlTask; yield messageTask }
                            |> Async.Choice
                    
                        // TODO: Maybe drain all channels instead of one type at a time. Not affecting performance unless high input rate of control messages
                        match (messageTypeToRead |> Option.get) with
                        | MessageType.Incoming -> 
                            let nextState = (drain incomingChannel []) |> List.toArray |> handler state
                            return Choice1Of3 nextState
                        | MessageType.Control -> 
                            return (drain controlChannel []) |> foldControl state
                    with
                        | :? TaskCanceledException -> return Choice3Of3 (ExecutionResult.Disposed state)
                        | e -> return Choice2Of3 e
                }

                match result with
                | Choice1Of3 nextState -> return! Run nextState
                | Choice2Of3 e -> return raise e
                | Choice3Of3 result -> return result
            }

        member x.BootStrap<'message,'state> (initialState : 'state) = async {
                let! result = async {
                    try
                        return! Run initialState
                    with
                    | e -> return ExecutionResult.Crashed e
                }
                let timeout = new CancellationTokenSource ()
                timeout.CancelAfter( TimeSpan.FromSeconds( 30000. ) ) //writing to a channel should never take long, but make sure we are not stuck on this after execution
                do! supervisorChannel.WriteAsync( result, timeout.Token ).AsTask()
                    |> Async.AwaitTask
            }

        member private x.DisposeInternal () =
            disposed <- true
            lifetimeToken.Cancel()
            lifetimeToken.Dispose()

        interface IStatefulActor<'message> with
            member x.SendAsync (m : 'message[]) (cancellation : CancellationToken) =
                let unionToken = CancellationTokenSource.CreateLinkedTokenSource( lifetimeToken.Token, cancellation )
                m 
                |> Array.map (fun item -> incomingChannel.Writer.WriteAsync( item, unionToken.Token ).AsTask() |> Async.AwaitTask )
                |> Async.Parallel
                |> Async.Ignore
                |> Async.StartAsTask
                :> Task
            member x.Send (m : 'message[]) = m |> Array.map (fun item -> incomingChannel.Writer.TryWrite( item ) ) |> Array.forall id
            member x.Stop () = (async {
                    let tcs = new TaskCompletionSource<bool>()
                    do! controlChannel.Writer.WriteAsync( ControlMessages.Stop tcs ).AsTask()
                        |> Async.AwaitTask
                    x.DisposeInternal ()
                }
                |> Async.StartAsTask) :> Task
            member x.Ping () =
                let tcs = new TaskCompletionSource<bool>()
                controlChannel.Writer.WriteAsync( ControlMessages.Ping tcs ).AsTask()
                |> Async.AwaitTask
                |> Async.Start
                tcs.Task
        
        interface IDisposable with
            member x.Dispose () =
                x.DisposeInternal ()

                
    let StartNew<'message,'state> (handler : 'state -> 'message[] -> 'state) (supervisorChannel : ChannelWriter<ExecutionResult<'state>>) (initialState : 'state) =
        let actor = new ActorImpl<'message,'state> (handler, supervisorChannel)
        actor.BootStrap initialState
        |> Async.Start
        actor :> IStatefulActor<'message>