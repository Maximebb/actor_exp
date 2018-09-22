namespace StatelessActor.Infrastructure

open Actor.Common
open System.Threading
open System.Threading.Tasks

module InMemoryStateProvider =
    open System.Collections.Generic

    type ProviderImpl<'identity, 'state when 'identity : comparison> internal (dict : Dictionary<'identity, 'state>) =
        let state = dict
        interface IStateProvider<'identity,'state> with 
            member x.SaveStateAsync cancelToken id s =
                state.[id] <- s
                Task.FromResult(s)
            member x.GetStateAsync cancelToken id =
                match state.TryGetValue id with
                | true, s -> Task.FromResult s
                | _ -> failwith (sprintf "State not found for id: %O" id) 

    let CreateNew<'identity, 'state when 'identity : comparison> (dict : Dictionary<'identity, 'state>) =
        new ProviderImpl<'identity, 'state> (dict)