open BenchmarkDotNet.Running
open BenchmarkDotNet.Configs
open PerfTest

[<EntryPoint>]
let main args =
    // Allow filtering benchmarks via command-line
    let defaultConfig = DefaultConfig.Instance
    BenchmarkSwitcher.FromAssembly(typeof<ModelBinding>.Assembly).Run(args, defaultConfig)
    |> ignore
    0
