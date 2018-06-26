module Tests

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
let ``Actor notifies supervisor of execution result after processing - Disposed`` () =
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
                  | Disposed finalState -> if finalState.Received.Length = 1 then true else false
                  | _ -> false )


[<Fact>]
let ``Actor notifies supervisor of execution result after processing - Crashed`` () =
    let mutable received = false
    let handler (state : TestState) (message : TestMessages) =
        received <- true
        Async.Sleep 1000
        |> Async.RunSynchronously
        failwith "Test"
        { state with Received = message :: state.Received }
    
    let (actor, supervisor) = initActorWithHandler handler

    actor.SendAsync TestMessages.A CancellationToken.None
    |> Async.AwaitTask
    |> Async.RunSynchronously

    Async.Sleep 1100
    |> Async.RunSynchronously

    actor.Dispose ()

    let executionResult = 
        supervisor.Reader.ReadAsync( CancellationToken.None ).AsTask()
        |> Async.AwaitTask
        |> Async.RunSynchronously

    Assert.True received
    Assert.True ( match executionResult with
                  | Crashed _ -> true
                  | _ -> false )

[<Fact>]
let ``Actor notifies supervisor of execution result after processing - Stopped`` () =
    let mutable received = false
    let handler (state : TestState) (message : TestMessages) =
        received <- true
        { state with Received = message :: state.Received }
    
    let (actor, supervisor) = initActorWithHandler handler

    let t =
        actor.SendAsync TestMessages.A CancellationToken.None
        |> Async.AwaitTask
    Async.RunSynchronously( t, 1000 )

    Async.Sleep 1000
    |> Async.RunSynchronously

    let t = 
        actor.Stop ()
        |> Async.AwaitTask
    Async.RunSynchronously( t, 1000 )

    let executionResult = 
        supervisor.Reader.ReadAsync( CancellationToken.None ).AsTask()
        |> Async.AwaitTask
        |> Async.RunSynchronously

    Assert.True received
    Assert.True ( match executionResult with
                  | Stopped _ -> true
                  | _ -> false )

[<Fact>]
let ``Actor ping`` () =
    let mutable received = false
    let handler (state : TestState) (message : TestMessages) =
        
        received <- true
        { state with Received = message :: state.Received }
    
    let (actor, _) = initActorWithHandler handler

    actor.SendAsync TestMessages.A CancellationToken.None
    |> Async.AwaitTask
    |> Async.RunSynchronously
    
    actor.Ping ()
    |> Async.AwaitTask
    |> Async.Ignore
    |> Async.RunSynchronously

    Assert.True received

[<Fact>]
let ``Actor ping - multiple messages sent and ping responds in between messages`` () =
    let mutable received = false
    let mutable amountReceived = 0
    let handler (state : TestState) (message : TestMessages) =
        amountReceived <- amountReceived + 1
        if amountReceived = 7 then received <- true
        { state with Received = message :: state.Received }
    
    let (actor, _) = initActorWithHandler handler

    actor.SendAsync TestMessages.A CancellationToken.None
    |> Async.AwaitTask
    |> Async.RunSynchronously

    actor.SendAsync TestMessages.A CancellationToken.None
    |> Async.AwaitTask
    |> Async.RunSynchronously

    actor.SendAsync TestMessages.A CancellationToken.None
    |> Async.AwaitTask
    |> Async.RunSynchronously

    let pingTask1 =
        actor.Ping ()
        |> Async.AwaitTask
        |> Async.Ignore

    actor.SendAsync TestMessages.A CancellationToken.None
    |> Async.AwaitTask
    |> Async.RunSynchronously

    let pingTask2 =
        actor.Ping ()
        |> Async.AwaitTask
        |> Async.Ignore

    actor.SendAsync TestMessages.A CancellationToken.None
    |> Async.AwaitTask
    |> Async.RunSynchronously

    let pingTask3 =
        actor.Ping ()
        |> Async.AwaitTask
        |> Async.Ignore
    actor.SendAsync TestMessages.A CancellationToken.None
    |> Async.AwaitTask
    |> Async.RunSynchronously

    let pingTask4 =
        actor.Ping ()
        |> Async.AwaitTask
        |> Async.Ignore

    actor.SendAsync TestMessages.A CancellationToken.None
    |> Async.AwaitTask
    |> Async.RunSynchronously

    [pingTask1; pingTask2; pingTask3; pingTask4]
    |> List.toSeq
    |> Async.Parallel
    |> Async.Ignore
    |> Async.RunSynchronously

    Async.Sleep 1000
    |> Async.RunSynchronously

    Assert.True received