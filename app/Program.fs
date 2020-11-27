open System
open System.Numerics
open sdlfs
open SDL2

module MEvents =
    type TMouseEvent =
        | Button of SDL.SDL_MouseButtonEvent
        | Motion of SDL.SDL_MouseMotionEvent
        | Wheel of SDL.SDL_MouseWheelEvent

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
        let mutable sprites =
            spriteBuilder.Load window renderer @"data/Sonic-Idle.json"
        // move two sprites into different locations, one is idle, one is impatient
        let posIdle =
            let p = spriteBuilder.GetWorldPostionHotPoint sprites.[0]
            Vector4(p.X + 512f, p.Y + 127f, p.Z, p.W)
        let posImpatient =
            let p = spriteBuilder.GetWorldPostionHotPoint sprites.[1]
            Vector4(p.X + 512f, p.Y + 512f, p.Z, p.W)
        sprites.[0] <- spriteBuilder.SetWorldTranslate sprites.[0] posIdle
        sprites.[1] <- spriteBuilder.SetWorldTranslate sprites.[1] posImpatient

        let handleWindowEvents (wEvent: SDL.SDL_WindowEvent) =
            match wEvent.windowEvent with
            | event when SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED = event ->
                let msg =
                    sprintf "Window resized: %Ax%A" wEvent.data1 wEvent.data2
                SDL.SDL_LogDebug(int(SDL.SDL_LogCategory.SDL_LOG_CATEGORY_APPLICATION), msg)
            | event when SDL.SDL_WindowEventID.SDL_WINDOWEVENT_SIZE_CHANGED = event ->
                let msg = sprintf "Window size changed: %Ax%A" wEvent.data1 wEvent.data2  // possibly data1&2 not available and only on RESIZED
                SDL.SDL_LogDebug(int(SDL.SDL_LogCategory.SDL_LOG_CATEGORY_APPLICATION), msg)
            | event when SDL.SDL_WindowEventID.SDL_WINDOWEVENT_CLOSE = event ->
                let msg = sprintf "Window closed: %Ax%A" wEvent.data1 wEvent.data2
                SDL.SDL_LogDebug(int(SDL.SDL_LogCategory.SDL_LOG_CATEGORY_APPLICATION), msg)
                let mutable refEvent = SDL.SDL_Event()
                refEvent.typeFSharp <- SDL.SDL_EventType.SDL_QUIT
                SDL.SDL_PushEvent(ref refEvent) |> ignore
            | _ -> failwith "Unhandled Window Event"

        SDL.SDL_LogDebug(int(SDL.SDL_LogCategory.SDL_LOG_CATEGORY_APPLICATION), "Begin event polling...")
        //let handleMouseEvents (mEvent: MEvents.TMouseEvent) = ;
        let mutable sdlEvent: SDL.SDL_Event = SDL.SDL_Event()
        let mutable quit = false
        let mutable deltaTMSec = 0.0
#if DEBUG
        let mutable tryCount = 0
#endif
        while (not quit) do
            let startTime =
                System.Diagnostics.Stopwatch.StartNew()
            // first procecess any O/S events
            SDL.SDL_PumpEvents()
            while ((SDL.SDL_PollEvent(ref sdlEvent) = 1) && (not quit)) do
                if  (sdlEvent.typeFSharp <> SDL.SDL_EventType.SDL_FIRSTEVENT) then
                    printfn "%A (%A)" sdlEvent.typeFSharp sdlEvent.window.windowEvent
                    match sdlEvent.typeFSharp with
                    | event when SDL.SDL_EventType.SDL_WINDOWEVENT = event ->
                        handleWindowEvents sdlEvent.window
                    | event when SDL.SDL_EventType.SDL_KEYDOWN = event ->
                        let scanCode = sdlEvent.key.keysym.scancode
                        printfn "%A" scanCode
                        if scanCode = SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE then
                            let mutable refEvent = SDL.SDL_Event()
                            refEvent.typeFSharp <- SDL.SDL_EventType.SDL_QUIT
                            SDL.SDL_PushEvent(ref refEvent) |> ignore
                    | event when SDL.SDL_EventType.SDL_QUIT = event ->
                        quit <- true
                        SDL.SDL_LogInfo(int(SDL.SDL_LogCategory.SDL_LOG_CATEGORY_APPLICATION), "QUIT event detected; Shutting down application...")
                        SDL.SDL_Quit()
                    | _ -> // do nothing
                        System.Threading.Thread.Sleep 0 // inner loop just yields
                        |> ignore
            // Now do other gameloop stuffs
            if not quit then
                let newSprites =
                    spriteBuilder.Draw renderer window deltaTMSec sprites
                sprites <- newSprites
            // each game ticks are 1mSec
            SDL.SDL_Delay(1u)
            System.Threading.Thread.Sleep 1 // outer loop waits 1 mSec
            |> ignore
            // finally, present the renderer
            SDL.SDL_RenderPresent(renderer)
            deltaTMSec <- float(startTime.ElapsedMilliseconds) + 1.0    // although the Sleep above will make sure it's 1mSec or more, we'll add 1mSec extra (harmless for a system based on FPS)
#if DEBUG
            // for now, die after 30 seconds
            tryCount <- tryCount + 1
            if tryCount > 1000 * 30 then
                quit <- true
#endif

        SDL.SDL_LogDebug(int(SDL.SDL_LogCategory.SDL_LOG_CATEGORY_APPLICATION), "Done event polling...")
        //libSDLWrapper.shutdown window renderer texture
        0 // return 0 as success

    | errVal -> errVal
