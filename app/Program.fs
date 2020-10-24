// Learn more about F# at http://fsharp.org

open System
open libSDL2Wrapper

[<EntryPoint>]
let main argv =
    let imageName =
        @"sdllogo.bmp"    // TODO: Pass this via arg
    let absPath =
        System.IO.Directory.GetCurrentDirectory() + @"/" + imageName
    printfn "Hello World from F#! - %A" absPath
    
    match HelloWorld.initSDL with
    | 0 ->
        let window = HelloWorld.getWindow "Hello World!" 100 100 640 480
        let renderer = HelloWorld.getRenderer window
        let surfaceBitMap = HelloWorld.getSurfaceBitMap window renderer absPath
        let texture = HelloWorld.getTexture window renderer surfaceBitMap
        HelloWorld.doTestRender renderer texture 1000u   // 1000mSec per loop
        HelloWorld.shutdown window renderer texture
        0   // return 0 as success
    | errVal -> errVal
