module Tests

open Xunit
open StatelessActor
open System.Threading.Channels
open System.Threading
open System.Threading.Tasks
open StatelessActor.Infrastructure
open System.Collections.Generic
open System

type internal TestMessages =
    | A
    | B
    | C

type internal TestState =
    {
        Received : TestMessages list
    }
    static member Empty = { Received = [] } : TestState

//let side (f : 'a -> unit) (x : 'a) =
//    f x
//    x

let internal initStatelessActorWithHandler handler stateProvider =
    let actor = StatelessActor.OptimisticConcurrency.Create<Guid, TestMessages, TestState> (handler, stateProvider, None)
    actor

let constructStateProvider dict =
    InMemoryStateProvider.CreateNew dict


[<Fact>]
let ``Stateless actor receives message`` () =
    let mutable received = false

    let handleOne (state : TestState) (message : TestMessages) =
        received <- true
        Task.FromResult { state with Received = message :: state.Received }
    
    let stateStore = new Dictionary<Guid, TestState>()
    let messageProc = initStatelessActorWithHandler handleOne (constructStateProvider stateStore)

    let id0 = Guid.NewGuid ()
    stateStore.Add( id0, TestState.Empty)

    messageProc.SendAsync id0 [| TestMessages.A |] CancellationToken.None
    |> Async.AwaitTask
    |> Async.RunSynchronously

    Async.Sleep 1000
    |> Async.RunSynchronously

    Assert.True received
    Assert.True (stateStore.ContainsKey( id0 ) && stateStore.[id0].Received.Length = 1) 