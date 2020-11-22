open System
open sdlfs
open SDL2

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
            libSDLWrapper.getWindow "Hello World!" 100 100 1024 768

        let renderer = libSDLWrapper.getRenderer window

        let surfaceBitMap =
            libSDLWrapper.getSurfaceBitMap window renderer true absPath

        let texture =
            libSDLWrapper.getTexture window renderer surfaceBitMap true true

        libSDLWrapper.doTestRender renderer texture 1000u // 1000mSec per loop

        let spriteBuilder =
            sdlfs.SpriteBuilder( 32 )

        let sprites =
            spriteBuilder.Load window renderer @"data/Sonic-Idle.json"

        spriteBuilder.Draw renderer window sprites
        |> ignore

        let input = SDL.SDL_CreateEvent

        libSDLWrapper.shutdown window renderer texture
        0 // return 0 as success
    | errVal -> errVal
