namespace StateFullActor

open System.Threading
open System.Threading.Tasks
open System
open System.Net.NetworkInformation

type ExecutionResult<'state> =
| Stopped of string
| Crashed of Exception

type IActor<'message> =
    abstract member SendAsync   : 'message -> CancellationToken -> Task
    abstract member Send        : 'message -> bool
    abstract member Ping        : unit -> Task<bool>
    abstract member Stop        : unit -> Task
    abstract member Dispose     : unit -> unit

module Actor =
    open System.Threading.Channels

    type ControlMessages =
    | Stop
    | Ping of TaskCompletionSource<bool>

    type Actor<'message, 'state> (handler : 'state -> 'message -> 'state, supervisorChannel : ChannelWriter<ExecutionResult<'state>>) =
        let controlChannel = Channel.CreateUnbounded<ControlMessages> (  new UnboundedChannelOptions( SingleReader = true, AllowSynchronousContinuations = true ) )
        let incomingChannel = Channel.CreateUnbounded<'message> (  new UnboundedChannelOptions( SingleReader = true, AllowSynchronousContinuations = true ) )
        let lifetimeToken = new CancellationTokenSource ()
        let mutable disposed = false

        let rec Run (state : 'state) = async {
            if disposed
            then return ExecutionResult<'state>.Stopped "Stopped because it is disposed"
            else
                //fold state on a batch of message
                let rec foldOnMessages (currState : 'state) (messages : 'message list) =
                    match messages with
                    | [] -> currState
                    | [ mess ] -> handler state mess
                    | mess :: tail -> tail |> foldOnMessages (handler currState mess)

                //drain message from channel reader
                let rec drain (acc : 'message list) =
                    let succ, readResult = incomingChannel.Reader.TryRead()
                    match succ, readResult with
                    | true, mess -> drain (mess :: acc)
                    | false, _ -> acc

                //wait asynchronously for messages to be read from the channel
                do! incomingChannel.Reader.WaitToReadAsync( lifetimeToken.Token ).AsTask()
                    |> Async.AwaitTask
                    |> Async.Ignore
                    
                //drain channel in a batch
                let batch = drain []

                //next state
                let nextState = 
                    batch
                    |> foldOnMessages state

                //recursive
                return! Run nextState
            }

        member x.BootStrap<'message,'state> (initialState : 'state) = async {
                let! result = async {
                    try
                        return! Run initialState
                    with
                    | :? TaskCanceledException as e -> return ExecutionResult<'state>.Stopped "Actor was asked to stop"
                    | e -> return ExecutionResult<'state>.Crashed e
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

        interface IActor<'message> with
            member x.SendAsync (m : 'message) (cancellation : CancellationToken) =
                let unionToken = CancellationTokenSource.CreateLinkedTokenSource( lifetimeToken.Token, cancellation )
                incomingChannel.Writer.WriteAsync( m, unionToken.Token ).AsTask()
            member x.Send (m : 'message) = incomingChannel.Writer.TryWrite( m )
            member x.Stop () =
                controlChannel.Writer.WriteAsync( ControlMessages.Stop ).AsTask()
            member x.Ping () =
                let tcs = new TaskCompletionSource<bool>()
                controlChannel.Writer.WriteAsync( ControlMessages.Ping tcs ).AsTask()
                |> Async.AwaitTask
                |> Async.Start
                tcs.Task
            member x.Dispose () =
                x.DisposeInternal ()

                

    let StartNew<'message,'state> (handler : 'state -> 'message -> 'state) (supervisorChannel : ChannelWriter<ExecutionResult<'state>>) (initialState : 'state) =
        let actor = new Actor<'message,'state> (handler, supervisorChannel)
        actor.BootStrap initialState
        |> Async.Start
        actor :> IActor<'message>