module Tests

open System
open Xunit
open StateFullActor
open System.Threading.Channels
open System.Threading

type internal TestMessages =
    | A
    | B
    | C

type internal TestState =
    {
        Received : TestMessages list
    }
    static member Empty = { Received = [] } : TestState

let internal initActorWithHandler handler =
    let supervisor = Channel.CreateUnbounded<ExecutionResult<TestState>> (  new UnboundedChannelOptions( SingleReader = false, AllowSynchronousContinuations = true ) )    
    let actor = 
        TestState.Empty
        |> StateFullActor.Actor.StartNew handler supervisor.Writer
    (actor, supervisor)

[<Fact>]
let ``Actor receives message`` () =
    let mutable received = false
    let handler (state : TestState) (message : TestMessages) =
        received <- true
        { state with Received = message :: state.Received }
    
    let (actor, _) = initActorWithHandler handler

    actor.SendAsync TestMessages.A CancellationToken.None
    |> Async.AwaitTask
    |> Async.RunSynchronously

    Async.Sleep 1000
    |> Async.RunSynchronously

    Assert.True received


[<Fact>]
let ``Actor receives multiple messages`` () =
    let mutable receivedWrongMessage = false
    let mutable receivedA = false
    let mutable receivedB = false
    let mutable receivedC = false
    let handler (state : TestState) (message : TestMessages) =
        match message with
        | A -> receivedA <- true
        | B -> receivedB <- true
        | C -> receivedC <- true
        | _ -> receivedWrongMessage <- true
        { state with Received = message :: state.Received }
    
    let (actor, supervisor) = initActorWithHandler handler

    actor.SendAsync TestMessages.A CancellationToken.None
    |> Async.AwaitTask
    |> Async.RunSynchronously

    actor.SendAsync TestMessages.B CancellationToken.None
    |> Async.AwaitTask
    |> Async.RunSynchronously

    actor.SendAsync TestMessages.C CancellationToken.None
    |> Async.AwaitTask
    |> Async.RunSynchronously

    Async.Sleep 1000
    |> Async.RunSynchronously

    Assert.True receivedA
    Assert.True receivedB
    Assert.True receivedC
    Assert.False receivedWrongMessage

[<Fact>]
let ``Actor notifies supervisor of execution result after processing - Success`` () =
    let mutable received = false
    let handler (state : TestState) (message : TestMessages) =
        received <- true
        { state with Received = message :: state.Received }
    
    let (actor, supervisor) = initActorWithHandler handler

    actor.SendAsync TestMessages.A CancellationToken.None
    |> Async.AwaitTask
    |> Async.RunSynchronously

    Async.Sleep 1000
    |> Async.RunSynchronously

    actor.Dispose ()

    let executionResult = 
        supervisor.Reader.ReadAsync( CancellationToken.None ).AsTask()
        |> Async.AwaitTask
        |> Async.RunSynchronously

    Assert.True received
    Assert.True ( match executionResult with
                  | Stopped reason -> true
                  | _ -> false )