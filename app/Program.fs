open System
open sdlfs

[<EntryPoint>]
let main argv =
    let imageName = @"sdllogo.bmp" // TODO: Pass this via arg

    let absPath =
        System.IO.Directory.GetCurrentDirectory()
        + @"/"
        + imageName

    printfn "Hello World from F#! - %A" absPath

    match libSDLWrapper.initSDLVideo with
    | 0 ->
        let window =
            libSDLWrapper.getWindow "Hello World!" 100 100 640 480

        let renderer = libSDLWrapper.getRenderer window

        let surfaceBitMap =
            libSDLWrapper.getSurfaceBitMap window renderer true absPath

        let texture =
            libSDLWrapper.getTexture window renderer surfaceBitMap true

        libSDLWrapper.doTestRender renderer texture 1000u // 1000mSec per loop
        libSDLWrapper.shutdown window renderer texture
        0 // return 0 as success
    | errVal -> errVal
