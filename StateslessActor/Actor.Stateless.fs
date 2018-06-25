namespace StatelessActor

//open System.Threading
//open System.Threading.Tasks
//open System
//open System.Net.NetworkInformation

//exception ActorDisposedException of obj
//exception ActorStoppedException of obj
//exception ActorCrashedException of string

//type ExecutionResult<'state> =
//| Disposed of 'state
//| Stopped of 'state
//| Crashed of Exception

//type IActor<'message> =
//    abstract member SendAsync   : 'message -> CancellationToken -> Task
//    abstract member Send        : 'message -> bool
//    abstract member Ping        : unit -> Task<bool>
//    abstract member Stop        : unit -> Task
//    abstract member Dispose     : unit -> unit

//module Actor =
//    open System.Threading.Channels

//    type ControlMessages =
//    | Stop of TaskCompletionSource<bool>
//    | Ping of TaskCompletionSource<bool>

//    type MessageType =
//    | Control
//    | Incoming

//    type Actor<'message, 'state> (handler : 'state -> 'message -> 'state, supervisorChannel : ChannelWriter<ExecutionResult<'state>>) =
//        let controlChannel = Channel.CreateUnbounded<ControlMessages> (  new UnboundedChannelOptions( SingleReader = true, AllowSynchronousContinuations = true ) )
//        let incomingChannel = Channel.CreateUnbounded<'message> (  new UnboundedChannelOptions( SingleReader = true, AllowSynchronousContinuations = true ) )
//        let lifetimeToken = new CancellationTokenSource ()
//        let mutable disposed = false

//        //fold state on a batch of message
//        let rec foldOnMessages (currState : 'state) (messages : 'message list) =
//            match messages with
//            | [] -> currState
//            | [ mess ] -> handler currState mess
//            | mess :: tail -> tail |> foldOnMessages (handler currState mess)

//        //drain message from channel reader
//        let rec drain (channelToReadFrom : Channel<'m>) (acc : 'm list) =
//            let succ, readResult = channelToReadFrom.Reader.TryRead()
//            match succ, readResult with
//            | true, mess -> drain channelToReadFrom (mess :: acc)
//            | false, _ -> acc

//        let handleControl state message =
//            match message with
//            | Stop tcs -> 
//                tcs.SetResult( true )
//                Choice3Of3 (ExecutionResult<'state>.Stopped state)
//            | Ping tcs -> 
//                tcs.SetResult( true )
//                Choice1Of3 state

//        let rec foldControl state (messages : ControlMessages list) =
//            match messages with
//            | [] -> Choice1Of3 state
//            | mess :: tail ->
//                match mess with
//                | Stop tcs -> 
//                    tcs.SetResult( true )
//                    Choice3Of3 (ExecutionResult<'state>.Stopped state)
//                | Ping tcs -> 
//                    tcs.SetResult( true )
//                    foldControl state tail

                
        
//        let rec Run (state : 'state) = async {
//            if disposed
//            then return ExecutionResult<'state>.Disposed state
//            else
//                let! result = async {
//                    try
//                        //wait asynchronously for messages to be read from the channel
//                        let! messageTypeToRead =
//                            let controlTask = async {
//                                do! controlChannel.Reader.WaitToReadAsync( lifetimeToken.Token ).AsTask() 
//                                    |> Async.AwaitTask
//                                    |> Async.Ignore
//                                return Some ( MessageType.Control )
//                            }
//                            let messageTask = async {
//                                do! incomingChannel.Reader.WaitToReadAsync( lifetimeToken.Token ).AsTask() 
//                                    |> Async.AwaitTask
//                                    |> Async.Ignore
//                                return Some ( MessageType.Incoming )
//                            }
//                            seq { yield controlTask; yield messageTask }
//                            |> Async.Choice
                    
//                        //drain channel in a batch
//                        match (messageTypeToRead |> Option.get) with
//                        | MessageType.Incoming -> 
//                            let nextState = (drain incomingChannel []) |> foldOnMessages state
//                            return Choice1Of3 nextState
//                        | MessageType.Control -> 
//                            return (drain controlChannel []) |> foldControl state
//                    with
//                        | ActorStoppedException(_) as e -> return Choice2Of3 e
//                        | :? TaskCanceledException -> 
//                            if disposed 
//                            then return Choice2Of3 (ActorDisposedException state) 
//                            else return Choice2Of3 (ActorCrashedException "Task was cancelled with actor being disposed")
//                        | _ -> return Choice2Of3 (ActorCrashedException "Actor crashed while reading from it's channel")
//                }

//                match result with
//                | Choice1Of3 nextState -> return! Run nextState
//                | Choice2Of3 e -> return raise e
//                | Choice3Of3 result -> return result
//            }

//        member x.BootStrap<'message,'state> (initialState : 'state) = async {
//                let! result = async {
//                    try
//                        return! Run initialState
//                    with
//                    | ActorStoppedException (state) -> return ExecutionResult<'state>.Stopped (state :?> 'state)
//                    | ActorDisposedException (state) -> return ExecutionResult<'state>.Disposed (state :?> 'state) //Dynamic cast is evil, but who's going to put me in prison for that?
//                    | :? ActorCrashedException as e -> return ExecutionResult<'state>.Crashed e
//                    | e -> return ExecutionResult<'state>.Crashed e
//                }
//                let timeout = new CancellationTokenSource ()
//                timeout.CancelAfter( TimeSpan.FromSeconds( 30000. ) ) //writing to a channel should never take long, but make sure we are not stuck on this after execution
//                do! supervisorChannel.WriteAsync( result, timeout.Token ).AsTask()
//                    |> Async.AwaitTask
//            }

//        member private x.DisposeInternal () =
//            disposed <- true
//            lifetimeToken.Cancel()
//            lifetimeToken.Dispose()

//        interface IActor<'message> with
//            member x.SendAsync (m : 'message) (cancellation : CancellationToken) =
//                let unionToken = CancellationTokenSource.CreateLinkedTokenSource( lifetimeToken.Token, cancellation )
//                incomingChannel.Writer.WriteAsync( m, unionToken.Token ).AsTask()
//            member x.Send (m : 'message) = incomingChannel.Writer.TryWrite( m )
//            member x.Stop () = (async {
//                    let tcs = new TaskCompletionSource<bool>()
//                    do! controlChannel.Writer.WriteAsync( ControlMessages.Stop tcs ).AsTask()
//                        |> Async.AwaitTask
//                    x.DisposeInternal ()
//                }
//                |> Async.StartAsTask) :> Task
//            member x.Ping () =
//                let tcs = new TaskCompletionSource<bool>()
//                controlChannel.Writer.WriteAsync( ControlMessages.Ping tcs ).AsTask()
//                |> Async.AwaitTask
//                |> Async.Start
//                tcs.Task
//            member x.Dispose () =
//                x.DisposeInternal ()

                

//    let StartNew<'message,'state> (handler : 'state -> 'message -> 'state) (supervisorChannel : ChannelWriter<ExecutionResult<'state>>) (initialState : 'state) =
//        let actor = new Actor<'message,'state> (handler, supervisorChannel)
//        actor.BootStrap initialState
//        |> Async.Start
//        actor :> IActor<'message>