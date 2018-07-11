namespace StatelessActor.Infrastructure

open Actor.Common
open System.Threading
open System.Threading.Tasks

module ConnectedStateProvider =
    let boilerplate = 1

    //type ProviderImpl<'identity, 'state when 'identity : comparison> () =
    //    interface IStateProvider<'identity,'state> with 
    //        member x.SaveStateAsync cancelToken id s = //Read from cosmos db
    //        member x.GetStateAsync cancelToken id = //Save to cosmos db