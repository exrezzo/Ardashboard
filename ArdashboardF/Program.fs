// For more information see https://aka.ms/fsharp-console-apps
// printfn "Hello from F#"


open ArdashboardF.EmailServiceF
open EmailServiceModule

[<EntryPoint>]
let main argv =
    getBankMessages |> Seq.toList |> ignore
    printf "Wow fatto."
    0