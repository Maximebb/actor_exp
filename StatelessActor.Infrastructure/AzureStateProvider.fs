namespace StatelessActor.Infrastructure

open Actor.Common
open System.Threading
open System.Threading.Tasks

//module ConnectedStateProvider =


module InMemoryStateProvider =

    type ProviderImpl<'identity, 'state when 'identity : comparison> () =
        let mutable state = Map.empty<'identity, 'state>
        interface IStateProvider<'identity,'state> with 
            member x.SaveStateAsync cancelToken id s =
                state <- state.Add( id, s )
                Task.CompletedTask
            member x.GetStateAsync cancelToken id =
                match state.TryFind id with
                | Some s -> Task.FromResult s
                | None -> failwith (sprintf "State not found for id: %O" id) 