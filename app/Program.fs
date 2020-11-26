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

        let spriteBuilder = sdlfs.SpriteBuilder(32)

        // /home/hidekiai/remote/projects/sdl2-fs/sdllogo.bmp
        let sprites =
            spriteBuilder.Load window renderer @"data/Sonic-Idle.json"

        spriteBuilder.Draw renderer window sprites
        |> ignore

        let handleWindowEvents (wEvent: SDL.SDL_WindowEvent) =
            match wEvent.windowEvent with
            | event when SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED = event ->
                printf "Window resized: %Ax%A" wEvent.data1 wEvent.data2
            | _ -> failwith "Unhandled SDL Event"

        let mutable sdlEvent: SDL.SDL_Event = SDL.SDL_Event()

        while (SDL.SDL_PollEvent(ref sdlEvent) = 1) do
            match sdlEvent.typeFSharp with
            | event when SDL.SDL_EventType.SDL_WINDOWEVENT = event -> handleWindowEvents sdlEvent.window
            | _ -> // do nothing
                0 |> ignore
            System.Threading.Thread.Sleep 1

        //libSDLWrapper.shutdown window renderer texture
        0 // return 0 as success

    | errVal -> errVal
